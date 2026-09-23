using System;
using HarmonyLib;

namespace PlanetGauge
{
    // 세션 경계 패치: 이전 플레이의 게이지 및 보류 상태가 다음 시도에 누출되지 않게 한다.
    [HarmonyPatch(typeof(scnEditor), nameof(scnEditor.Play))]
    internal static class EditorPlayPatch
    {
        private static void Prefix()
        {
            if (Main.IsEnabled && Main.EditorGaugeEnabled)
            {
                Main.ResetSessionState();
            }
        }
    }

    // 중간 타일 재생 준비 중 과거 ffxPlusBase 효과가 다시 실행되므로 실제 시작 타일을 보존한다.
    [HarmonyPatch(typeof(scnGame), nameof(scnGame.Play), typeof(int), typeof(bool))]
    internal static class GamePlayStartFloorPatch
    {
        private static void Prefix(int seqID, ref bool __state)
        {
            __state = false;
            if (Main.IsEnabled && Main.EditorGaugeEnabled)
            {
                GaugeRuntime.PrepareSessionStart(seqID);
                __state = true;
            }
        }

        private static Exception Finalizer(Exception __exception, ref bool __state)
        {
            if (__state && __exception != null)
            {
                GaugeRuntime.CancelSessionStartRestore();
                __state = false;
            }

            return __exception;
        }
    }

    // WaitForStartCo 코루틴이 실제 과거 효과를 실행하는 정확한 동기 구간이다.
    [HarmonyPatch(typeof(scrVfxPlus), nameof(scrVfxPlus.ScrubToTime), typeof(float))]
    internal static class InitialVfxScrubPatch
    {
        private static void Prefix(ref bool __state)
        {
            __state = Main.IsEnabled
                && Main.EditorGaugeEnabled
                && GaugeRuntime.TryBeginInitialVfxScrub();
        }

        private static void Postfix(ref bool __state)
        {
            FinishScrub(ref __state, true);
        }

        private static Exception Finalizer(Exception __exception, ref bool __state)
        {
            FinishScrub(ref __state, false);
            return __exception;
        }

        private static void FinishScrub(ref bool state, bool applyDeferredCap)
        {
            if (state)
            {
                GaugeRuntime.EndInitialVfxScrub(applyDeferredCap);
                state = false;
            }
        }
    }

    [HarmonyPatch(typeof(scnEditor), nameof(scnEditor.SwitchToEditMode))]
    internal static class EditorSwitchToEditModePatch
    {
        private static void Postfix()
        {
            if (Main.IsEnabled && Main.EditorGaugeEnabled)
            {
                Main.ResetSessionState();
                RuntimeHost.ResetDebugVisibility();
            }
        }
    }

    [HarmonyPatch(typeof(scrController), nameof(scrController.Restart))]
    internal static class ControllerRestartPatch
    {
        private static void Prefix()
        {
            if (GaugeRuntime.IsGameplayContext(true))
            {
                Main.ResetSessionState();
            }
        }
    }

    [HarmonyPatch(typeof(scrController), nameof(scrController.ResetCustomLevel))]
    internal static class ResetCustomLevelPatch
    {
        private static void Prefix()
        {
            if (GaugeRuntime.IsGameplayContext(true))
            {
                Main.ResetSessionState();
            }
        }
    }

    [HarmonyPatch(
        typeof(scrController),
        nameof(scrController.OnLandOnPortal),
        typeof(scrPlanet),
        typeof(Portal),
        typeof(string))]
    internal static class ControllerLandOnPortalPatch
    {
        private static void Prefix()
        {
            if (GaugeRuntime.ShouldHandle())
            {
                // 커스텀 레벨의 승리 시간, 축하 문구, 결과 저장이 시작되기 전에
                // 결과 HUD 상태를 확정하고 Blindfold를 해제한다.
                GaugeRuntime.MarkLevelCompleted();
                GaugeRuntime.DisableBlindfoldForLevelCompletion();
            }
        }
    }

    /// <summary>
    /// 바닐라가 확정해 기록한 판정을 관찰해 게이지에 반영한다.
    /// 실패 피해는 Die가 소유하며, 원본 종료 후 실제 noFail 상태에서 일반 판정만 적용한다.
    /// </summary>
    [HarmonyPatch(typeof(scrPlanet), nameof(scrPlanet.SwitchChosen))]
    internal static class SwitchChosenPatch
    {
        private static int temporaryNoFailDepth;

        private struct SwitchState
        {
            // Harmony의 __state는 같은 원본 호출의 Prefix/Postfix 사이에서만 전달된다.
            internal bool Track;
            internal scrPlayer Player;
            internal VanillaJudgementObservation.Token Observation;
            internal bool TrackAutomaticRecovery;
            internal scrController TemporaryNoFailController;
            internal bool RestoreTemporaryNoFail;
            internal bool OriginalNoFail;
            internal bool TemporaryNoFailStarted;
        }

        private static void Prefix(
            scrPlanet __instance,
            ref SwitchState __state)
        {
            __state = default(SwitchState);

            if (__instance == null
                || __instance.player == null
                || GaugeRuntime.IsRecoveringFailure
                || !GaugeRuntime.ShouldHandle(__instance.player))
            {
                return;
            }

            scrController controller = scrController.instance;
            scrConductor conductor = scrConductor.instance;
            PlanetarySystem planetarySystem = __instance.planetarySystem;
            scrFloor currentFloor = __instance.currfloor;
            if (controller == null
                || conductor == null
                || planetarySystem == null
                || currentFloor == null
                || controller.noFailInfiniteMargin)
            {
                return;
            }

            scrFloor nextFloor = currentFloor.nextfloor;
            bool autoFloor = nextFloor != null && nextFloor.auto;
            if (GaugeRuntime.IsAutoPlay(__instance.player) || autoFloor)
            {
                __state.TrackAutomaticRecovery = true;
                __state.Player = __instance.player;
                return;
            }

            if (__instance.player != null && __instance.player.midspinInfiniteMargin)
            {
                return;
            }

            __state.Track = true;
            __state.Player = __instance.player;
            __state.Observation = VanillaJudgementObservation.Begin(__instance.player.marginTracker);
            if (!controller.noFail
                && GaugeRuntime.EventSettings.FailureProtection
                && GaugeRuntime.Current > 0f)
            {
                // 원본 SwitchChosen 전체가 바닐라 무적모드와 같은 분기를 타게 한다.
                // PG 차감은 원본 호출이 끝난 뒤 실제 noFail을 복원하고 처리한다.
                __state.TemporaryNoFailController = controller;
                __state.RestoreTemporaryNoFail = true;
                __state.OriginalNoFail = controller.noFail;
                __state.TemporaryNoFailStarted = true;
                temporaryNoFailDepth++;
                controller.noFail = true;
            }
        }

        private static void Postfix(
            scrPlanet __instance,
            scrPlanet __result,
            ref SwitchState __state)
        {
            RestoreTemporaryNoFail(ref __state);
            HitMargin? judgement = VanillaJudgementObservation.End(ref __state.Observation);

            if (__state.TrackAutomaticRecovery
                && __state.Player != null
                && __result != null
                && __result != __instance
                && GaugeRuntime.ShouldHandle(__state.Player))
            {
                // 별도 이벤트 토글 없이 자동 플레이와 Auto 타일은 항상 회복한다.
                // BlockRecovery 및 회복 배율·상한은 일반 양수 변화량과 동일하게 적용된다.
                GaugeRuntime.ApplyAutomaticRecovery();
                return;
            }

            if (!__state.Track
                || __state.Player == null
                || GaugeRuntime.IsAutoPlay(__state.Player)
                || !GaugeRuntime.ShouldHandle(__state.Player))
            {
                return;
            }

            if (judgement.HasValue && GaugeRuntime.ApplyJudgement(judgement.Value))
            {
                GaugeRuntime.ForceDie(__state.Player);
            }
        }

        private static Exception Finalizer(Exception __exception, ref SwitchState __state)
        {
            RestoreTemporaryNoFail(ref __state);
            VanillaJudgementObservation.End(ref __state.Observation);
            return __exception;
        }

        internal static bool IsBorrowingNoFail
        {
            get { return temporaryNoFailDepth > 0; }
        }

        internal static void ResetSessionState()
        {
            temporaryNoFailDepth = 0;
            PlayerDiePatch.ResetSessionState();
            VanillaJudgementObservation.Reset();
        }

        private static void RestoreTemporaryNoFail(ref SwitchState state)
        {
            if (state.RestoreTemporaryNoFail
                && state.TemporaryNoFailController != null)
            {
                state.TemporaryNoFailController.noFail = state.OriginalNoFail;
                state.RestoreTemporaryNoFail = false;
            }

            if (state.TemporaryNoFailStarted)
            {
                if (temporaryNoFailDepth > 0)
                {
                    temporaryNoFailDepth--;
                }

                state.TemporaryNoFailStarted = false;
            }
        }

    }

    [HarmonyPatch(typeof(scrMarginTracker), nameof(scrMarginTracker.AddHit), typeof(HitMargin))]
    internal static class VanillaMarginRecordedPatch
    {
        private static void Postfix(scrMarginTracker __instance, HitMargin hit)
        {
            if (Main.IsEnabled && !GaugeRuntime.IsRecoveringFailure)
                VanillaJudgementObservation.Record(__instance, hit);
        }
    }

    /// <summary>
    /// 원본 사망 요청을 게이지 차감으로 변환하고, 게이지가 남으면 게임의 noFail 복구 경로를 빌린다.
    /// 임시 플래그는 Postfix와 Finalizer 양쪽에서 복원해 원본 예외 발생 시에도 상태 누출을 막는다.
    /// </summary>
    [HarmonyPatch(typeof(scrPlayer), nameof(scrPlayer.Die))]
    internal static class PlayerDiePatch
    {
        private static int temporaryNoFailDepth;

        internal static bool IsBorrowingNoFail { get { return temporaryNoFailDepth > 0; } }
        internal static void ResetSessionState() { temporaryNoFailDepth = 0; }

        private struct DieState
        {
            internal bool RestoreNoFail;
            internal bool RecoveryStarted;
            internal bool OriginalNoFail;
            internal bool BorrowStarted;
            internal scrController Controller;
        }

        private static void Prefix(
            scrPlayer __instance,
            bool overload,
            bool multipress,
            string failMessage,
            bool hitbox,
            ref DieState __state)
        {
            __state = default(DieState);

            if (!GaugeRuntime.ShouldHandle(__instance)
                || (GaugeRuntime.IsRecoveringFailure && !GaugeRuntime.IsForcingDeath && !hitbox))
                return;

            scrController controller = scrController.instance;
            if (controller == null) return;

            // 하나의 Die 패치가 외부 noFail을 보존/복원하여 bridge 실행 순서 의존을 없앤다.
            __state.Controller = controller;
            __state.OriginalNoFail = controller.noFail;
            __state.RestoreNoFail = true;
            if (Main.IsBorrowingNoFail) controller.noFail = false;

            if (hitbox) GaugeRuntime.RevealBlindfold();
            if (GaugeRuntime.IsForcingDeath || hitbox || GaugeRuntime.IsAutoPlay(__instance))
                return;

            // 실패 판정 표시와 fail-bar 잔여 상태는 차감하지 않는다. 실제 Die 요청만 소유한다.
            bool shouldDie = GaugeRuntime.ApplyJudgement(overload
                ? HitMargin.FailOverload
                : HitMargin.FailMiss);
            VanillaJudgementObservation.MarkFailureHandled();

            if (shouldDie) return;

            // 실제 무적과 PG 보호 모두 원본의 복구 Hit를 그대로 사용한다.
            if (!controller.noFail)
            {
                temporaryNoFailDepth++;
                __state.BorrowStarted = true;
            }
            controller.noFail = true;
            __state.RecoveryStarted = true;
            GaugeRuntime.BeginFailureRecovery();
        }

        private static void Postfix(ref DieState __state)
        {
            RestoreTemporaryNoFail(ref __state);
        }

        private static Exception Finalizer(Exception __exception, ref DieState __state)
        {
            RestoreTemporaryNoFail(ref __state);
            return __exception;
        }

        private static void RestoreTemporaryNoFail(ref DieState state)
        {
            // 이 메서드는 Postfix와 Finalizer에서 모두 호출될 수 있으므로 반드시 멱등이어야 한다.
            if (state.RestoreNoFail && state.Controller != null)
            {
                state.Controller.noFail = state.OriginalNoFail;
                state.RestoreNoFail = false;
            }

            if (state.RecoveryStarted)
            {
                GaugeRuntime.EndFailureRecovery();
                state.RecoveryStarted = false;
            }

            if (state.BorrowStarted)
            {
                if (temporaryNoFailDepth > 0) temporaryNoFailDepth--;
                state.BorrowStarted = false;
            }
        }
    }
}
