using System;
using UnityEngine;

namespace PlanetGauge
{
    internal static class GaugeRuntime
    {
        /*
         * 디버그/밸런스 조정 전용 상수.
         * 판정별 수치를 바꾸려면 Harmony 패치가 아니라 아래 값만 수정하면 된다.
         */
        internal const float InitialGauge = 100f;
        internal const float MaximumGauge = 100f;
        internal const float NoFailMinimumGauge = -5f;

        internal const float PerfectDelta = 0.1f;
        internal const float EarlyPerfectDelta = -0.5f;
        internal const float LatePerfectDelta = -0.5f;
        internal const float VeryEarlyDelta = -4f;
        internal const float VeryLateDelta = -4f;
        internal const float TooEarlyDelta = -7f;
        internal const float TooLateDelta = -18f;
        internal const float FailMissDelta = -18f;
        internal const float FailOverloadDelta = -18f;

        private static bool frozen;
        private static bool nextDieAlreadyCharged;
        private static int failureRecoveryDepth;
        private static bool forcingDeath;

        internal static float Current { get; private set; } = InitialGauge;

        internal static bool IsRecoveringFailure
        {
            get { return failureRecoveryDepth > 0; }
        }

        internal static bool IsForcingDeath
        {
            get { return forcingDeath; }
        }

        internal static void Reset()
        {
            Current = InitialGauge;
            frozen = false;
            nextDieAlreadyCharged = false;
            failureRecoveryDepth = 0;
            forcingDeath = false;
        }

        internal static bool ShouldHandle(scrPlayer player = null)
        {
            if (!Main.IsEnabled || !Main.EditorGaugeEnabled)
            {
                return false;
            }

            scnEditor editor = scnEditor.instance;
            scrController controller = scrController.instance;
            if (editor == null || controller == null)
            {
                return false;
            }

            if (controller.paused || !controller.gameworld || scrPlayerManager.playerCount != 1)
            {
                return false;
            }

            if (player != null && controller.playerOne != player)
            {
                return false;
            }

            return true;
        }

        internal static bool ApplyJudgement(HitMargin judgement)
        {
            if (!ShouldHandle() || frozen)
            {
                return frozen
                    && scrController.instance != null
                    && !scrController.instance.noFail
                    && Current <= 0f;
            }

            float delta;
            if (!TryGetDelta(judgement, out delta) || Mathf.Approximately(delta, 0f))
            {
                return false;
            }

            float next = Mathf.Min(MaximumGauge, Current + delta);
            if (next > 0f)
            {
                Current = next;
                return false;
            }

            scrController controller = scrController.instance;
            if (controller != null && controller.noFail)
            {
                // 실패 방지가 우선이다. 최저 -5까지만 허용한 뒤 변동을 정지한다.
                Current = Mathf.Max(NoFailMinimumGauge, next);
                frozen = true;
                return false;
            }

            Current = 0f;
            frozen = true;
            return true;
        }

        internal static void MarkNextDieAlreadyCharged()
        {
            nextDieAlreadyCharged = true;
        }

        internal static bool ConsumeNextDieAlreadyCharged()
        {
            bool charged = nextDieAlreadyCharged;
            nextDieAlreadyCharged = false;
            return charged;
        }

        internal static void ClearPendingDieCharge()
        {
            nextDieAlreadyCharged = false;
        }

        internal static void BeginFailureRecovery()
        {
            failureRecoveryDepth++;
        }

        internal static void EndFailureRecovery()
        {
            if (failureRecoveryDepth > 0)
            {
                failureRecoveryDepth--;
            }
        }

        internal static void ForceDie(scrPlayer player)
        {
            if (player == null || forcingDeath)
            {
                return;
            }

            ClearPendingDieCharge();
            forcingDeath = true;
            try
            {
                // 사용자가 요청한 최종 실패 경로: 별도 사유를 만들지 않고 원본 Die를 호출한다.
                player.Die();
            }
            catch (Exception exception)
            {
                Main.LogException("게이지 소진 후 scrPlayer.Die 호출에 실패했습니다.", exception);

                // 부분적으로 손상된 상태에서도 게임 진행이 멈추지 않도록 원본 실패 상태 진입을 시도한다.
                try
                {
                    scrController controller = scrController.instance;
                    if (controller != null)
                    {
                        controller.FailAction();
                    }
                }
                catch (Exception fallbackException)
                {
                    Main.LogException("Die 예외 후 FailAction 대체 처리에도 실패했습니다.", fallbackException);
                }
            }
            finally
            {
                forcingDeath = false;
            }
        }

        private static bool TryGetDelta(HitMargin judgement, out float delta)
        {
            switch (judgement)
            {
                case HitMargin.Perfect:
                    delta = PerfectDelta;
                    return true;
                case HitMargin.EarlyPerfect:
                    delta = EarlyPerfectDelta;
                    return true;
                case HitMargin.LatePerfect:
                    delta = LatePerfectDelta;
                    return true;
                case HitMargin.VeryEarly:
                    delta = VeryEarlyDelta;
                    return true;
                case HitMargin.VeryLate:
                    delta = VeryLateDelta;
                    return true;
                case HitMargin.TooEarly:
                    delta = TooEarlyDelta;
                    return true;
                case HitMargin.TooLate:
                    delta = TooLateDelta;
                    return true;
                case HitMargin.FailMiss:
                    delta = FailMissDelta;
                    return true;
                case HitMargin.FailOverload:
                    delta = FailOverloadDelta;
                    return true;
                default:
                    // Auto, Multipress, OverPress 등은 게이지를 바꾸지 않는다.
                    delta = 0f;
                    return false;
            }
        }
    }
}
