using System;
using HarmonyLib;
using UnityEngine;

namespace PlanetGauge
{
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

    [HarmonyPatch(typeof(scrPlanet), nameof(scrPlanet.SwitchChosen))]
    internal static class SwitchChosenPatch
    {
        private struct SwitchState
        {
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
            if (RDC.auto
                || (__instance.player != null && __instance.player.auto)
                || (autoFloor && !RDC.useOldAuto)
                || (__instance.player != null && __instance.player.midspinInfiniteMargin))
            {
                return;
            }

            double marginScale = nextFloor == null ? 1d : nextFloor.marginScale;
            float effectiveBpm = (float)((double)conductor.bpm * planetarySystem.speed);

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
            if (!__state.Track || __state.Player == null || !GaugeRuntime.ShouldHandle(__state.Player))
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

            if (judgement == HitMargin.TooLate
                || judgement == HitMargin.FailMiss
                || judgement == HitMargin.FailOverload)
            {
                // 이 판정 직후 원본 Die가 이어질 때 -18이 두 번 적용되지 않게 한다.
                GaugeRuntime.MarkNextDieAlreadyCharged();
            }

            if (GaugeRuntime.ApplyJudgement(judgement))
            {
                GaugeRuntime.ForceDie(__state.Player);
            }
        }
    }

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
