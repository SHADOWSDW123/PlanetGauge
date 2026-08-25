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
                // 커스텀 레벨의 승리 시간, 축하 문구, 결과 저장이 시작되기 전에 해제한다.
                GaugeRuntime.DisableBlindfoldForLevelCompletion();
            }
        }
    }

    /// <summary>
    /// 타일 전환 직전 판정을 계산해 게이지에 반영한다.
    /// Prefix에서 원본 메서드가 바꿀 수 있는 값을 캡처하고 Postfix에서 최종 실패 여부를 결정한다.
    /// </summary>
    [HarmonyPatch(typeof(scrPlanet), nameof(scrPlanet.SwitchChosen))]
    internal static class SwitchChosenPatch
    {
        private static bool[] judgementAppliedByDieAtDepth = new bool[4];
        private static int observedSwitchDepth;

        private struct SwitchState
        {
            // Harmony의 __state는 같은 원본 호출의 Prefix/Postfix 사이에서만 전달된다.
            internal bool Track;
            internal bool NoFailAtStart;
            internal HitMargin Judgement;
            internal scrPlayer Player;
            internal int ObservationDepth;
            internal bool TrackAutomaticRecovery;
        }

        private static void Prefix(scrPlanet __instance, ref SwitchState __state)
        {
            __state = default(SwitchState);

            if (__instance == null
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
            bool actualAutoPlay = GaugeRuntime.IsAutoPlay(__instance.player);
            if (actualAutoPlay || (autoFloor && !RDC.useOldAuto))
            {
                __state.TrackAutomaticRecovery = actualAutoPlay
                    || GaugeRuntime.EventSettings.AutoTileRecovery;
                __state.Player = __instance.player;
                return;
            }

            if (__instance.player != null && __instance.player.midspinInfiniteMargin)
            {
                return;
            }

            double marginScale = nextFloor == null ? 1d : nextFloor.marginScale;
            float effectiveBpm = (float)((double)conductor.bpm * planetarySystem.speed);

            // 원본 SwitchChosen 실행 후에도 판정 기준이 변하지 않도록 필요한 입력을 미리 확정한다.
            __state.Track = true;
            __state.NoFailAtStart = controller.noFail;
            __state.Player = __instance.player;
            __state.Judgement = scrMisc.GetHitMargin(
                (float)__instance.cachedAngle,
                (float)__instance.targetExitAngle,
                planetarySystem.isCW,
                effectiveBpm,
                conductor.song.pitch,
                marginScale);
            __state.ObservationDepth = BeginObservation();
        }

        private static void Postfix(
            scrPlanet __instance,
            scrPlanet __result,
            ref SwitchState __state)
        {
            bool judgementAppliedByDie = EndObservation(ref __state);

            if (__state.TrackAutomaticRecovery
                && __state.Player != null
                && __result != null
                && __result != __instance
                && GaugeRuntime.ShouldHandle(__state.Player))
            {
                // 자동 플레이는 성공적으로 다음 타일로 진행한 경우에만 회복한다.
                // 판정/사망 가로채기와는 연결하지 않아 원본 자동 플레이 흐름을 보존한다.
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

            if (judgementAppliedByDie)
            {
                // SwitchChosen 원본 내부의 OnDamage가 연속 Multipress로 Die를 호출한 경우,
                // PlayerDiePatch가 이미 같은 실패를 처리했으므로 Postfix에서 다시 차감하지 않는다.
                GaugeRuntime.ClearPendingDieCharge();
                return;
            }

            HitMargin judgement = __state.Judgement;
            scrFailBar failBar = __state.Player.failBar;
            bool invalidHit = !scrMisc.IsValidHit(judgement);
            bool overload = failBar != null
                && failBar.DidFail(false)
                && (!__state.NoFailAtStart || invalidHit);

            if (overload)
            {
                judgement = HitMargin.FailOverload;
            }

            if (judgement == HitMargin.TooLate)
            {
                // TooLate는 같은 타일에 머무는 중간 판정이다. 뒤이어 확정되는 FailMiss가 직접 차감한다.
                GaugeRuntime.ClearPendingDieCharge();
                return;
            }

            if (judgement == HitMargin.FailMiss
                || judgement == HitMargin.FailOverload)
            {
                // 이 판정 직후 원본 Die가 이어질 때 같은 실패를 두 번 차감하지 않게 한다.
                GaugeRuntime.MarkNextDieAlreadyCharged();
            }

            if (GaugeRuntime.ApplyJudgement(judgement))
            {
                GaugeRuntime.ForceDie(__state.Player);
            }
        }

        private static Exception Finalizer(Exception __exception, ref SwitchState __state)
        {
            EndObservation(ref __state);
            return __exception;
        }

        internal static void MarkJudgementAppliedByDie()
        {
            if (observedSwitchDepth > 0)
            {
                judgementAppliedByDieAtDepth[observedSwitchDepth - 1] = true;
            }
        }

        internal static void ResetSessionState()
        {
            observedSwitchDepth = 0;
            Array.Clear(
                judgementAppliedByDieAtDepth,
                0,
                judgementAppliedByDieAtDepth.Length);
        }

        private static int BeginObservation()
        {
            if (observedSwitchDepth == judgementAppliedByDieAtDepth.Length)
            {
                Array.Resize(
                    ref judgementAppliedByDieAtDepth,
                    judgementAppliedByDieAtDepth.Length * 2);
            }

            judgementAppliedByDieAtDepth[observedSwitchDepth] = false;
            observedSwitchDepth++;
            return observedSwitchDepth;
        }

        private static bool EndObservation(ref SwitchState state)
        {
            if (state.ObservationDepth <= 0)
            {
                return false;
            }

            int index = state.ObservationDepth - 1;
            bool applied = judgementAppliedByDieAtDepth[index];
            judgementAppliedByDieAtDepth[index] = false;

            if (observedSwitchDepth == state.ObservationDepth)
            {
                observedSwitchDepth--;
            }

            state.ObservationDepth = 0;
            return applied;
        }
    }

    /// <summary>
    /// 원본 사망 요청을 게이지 차감으로 변환하고, 게이지가 남으면 게임의 noFail 복구 경로를 빌린다.
    /// 임시 플래그는 Postfix와 Finalizer 양쪽에서 복원해 원본 예외 발생 시에도 상태 누출을 막는다.
    /// </summary>
    [HarmonyPatch(typeof(scrPlayer), nameof(scrPlayer.Die))]
    internal static class PlayerDiePatch
    {
        private struct DieState
        {
            internal bool RestoreNoFail;
            internal bool RecoveryStarted;
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

            if (hitbox && GaugeRuntime.ShouldHandle(__instance))
            {
                // hitbox 사망은 게이지로 흡수하지 않으므로 실제 사망 요청 시 숫자만 공개한다.
                GaugeRuntime.RevealBlindfold();
            }

            if (GaugeRuntime.IsForcingDeath
                || hitbox
                || !GaugeRuntime.ShouldHandle(__instance))
            {
                return;
            }

            if (GaugeRuntime.IsAutoPlay(__instance))
            {
                GaugeRuntime.ClearPendingDieCharge();
                return;
            }

            scrController controller = scrController.instance;
            if (controller == null)
            {
                return;
            }

            bool chargedByJudgement = GaugeRuntime.ConsumeNextDieAlreadyCharged();
            bool shouldDie = chargedByJudgement
                ? !controller.noFail && GaugeRuntime.Current <= 0f
                : GaugeRuntime.ApplyJudgement(overload
                    ? HitMargin.FailOverload
                    : HitMargin.FailMiss);

            if (!chargedByJudgement)
            {
                SwitchChosenPatch.MarkJudgementAppliedByDie();
            }

            // 실패 방지가 가장 높은 우선순위이므로 원본 noFail 분기를 그대로 실행한다.
            if (controller.noFail || shouldDie)
            {
                return;
            }

            if (overload)
            {
                // 놓침은 아래 바닐라 복구의 Hit(false)가 FailMiss를 기록한다.
                // 과부하는 Die의 noFail 분기가 타일을 진행시키지 않으므로 여기서 한 번 기록한다.
                scrMarginTracker marginTracker = __instance.marginTracker;
                if (marginTracker != null)
                {
                    marginTracker.AddHit(HitMargin.FailOverload);
                }
            }

            // 게이지가 남아 있는 동안만 원본 실패 방지 동작을 빌린다.
            __state.Controller = controller;
            __state.RestoreNoFail = true;
            __state.RecoveryStarted = true;
            controller.noFail = true;
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
                state.Controller.noFail = false;
                state.RestoreNoFail = false;
            }

            if (state.RecoveryStarted)
            {
                GaugeRuntime.EndFailureRecovery();
                state.RecoveryStarted = false;
            }
        }
    }
}
