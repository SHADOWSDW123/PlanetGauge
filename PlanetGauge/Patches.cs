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
                GaugeRuntime.Reset();
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
                GaugeRuntime.Reset();
            }
        }
    }

    [HarmonyPatch(typeof(scrController), nameof(scrController.Restart))]
    internal static class ControllerRestartPatch
    {
        private static void Prefix()
        {
            if (GaugeRuntime.ShouldHandle())
            {
                GaugeRuntime.Reset();
            }
        }
    }

    [HarmonyPatch(typeof(scrController), nameof(scrController.ResetCustomLevel))]
    internal static class ResetCustomLevelPatch
    {
        private static void Prefix()
        {
            if (GaugeRuntime.ShouldHandle())
            {
                GaugeRuntime.Reset();
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
        private struct SwitchState
        {
            // Harmony의 __state는 같은 원본 호출의 Prefix/Postfix 사이에서만 전달된다.
            internal bool Track;
            internal bool NoFailAtStart;
            internal HitMargin Judgement;
            internal scrPlayer Player;
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
            if (GaugeRuntime.IsAutoPlay(__instance.player)
                || (autoFloor && !RDC.useOldAuto)
                || (__instance.player != null && __instance.player.midspinInfiniteMargin))
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
        }

        private static void Postfix(SwitchState __state)
        {
            if (!__state.Track
                || __state.Player == null
                || GaugeRuntime.IsAutoPlay(__state.Player)
                || !GaugeRuntime.ShouldHandle(__state.Player))
            {
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
