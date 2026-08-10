using System;
using UnityEngine;

namespace PlanetGauge
{
    /// <summary>
    /// 판정에 따른 게이지 상태 전이와 실패 복구 중 재진입 방지 상태를 관리한다.
    /// Harmony 패치들은 게임 이벤트를 해석하고, 실제 수치 변경은 이 클래스에만 위임한다.
    /// </summary>
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
        internal const float VeryEarlyDelta = -1.5f;
        internal const float VeryLateDelta = -1.5f;
        internal const float TooEarlyDelta = -3f;
        internal const float FailMissDelta = -8f;
        internal const float FailOverloadDelta = -8f;

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
            // 새 플레이 세션은 보류 중인 Die 차감과 중첩 복구 상태까지 모두 초기화한다.
            Current = InitialGauge;
            frozen = false;
            nextDieAlreadyCharged = false;
            failureRecoveryDepth = 0;
            forcingDeath = false;
        }

        internal static bool ShouldHandle(scrPlayer player = null)
        {
            // 모드는 에디터의 1인 실제 플레이에서만 원본 실패 흐름을 변경한다.
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

        internal static bool IsAutoPlay(scrPlayer player = null)
        {
            if (RDC.auto)
            {
                return true;
            }

            scrController controller = scrController.instance;
            scrPlayer targetPlayer = player;
            if (targetPlayer == null && controller != null)
            {
                targetPlayer = controller.playerOne;
            }

            return targetPlayer != null && targetPlayer.auto;
        }

        internal static bool ApplyJudgement(HitMargin judgement)
        {
            if (!ShouldHandle() || IsAutoPlay())
            {
                return false;
            }

            if (frozen)
            {
                // 소진 이후에는 값을 다시 변경하지 않고, 실제 실패가 필요한지만 재보고한다.
                return scrController.instance != null
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
            // SwitchChosen 직후 같은 실패를 알리는 Die가 이어질 수 있어 1회성 토큰으로 중복 차감을 막는다.
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
            // 실패 복구 중 SwitchChosen이 재진입할 수 있으므로 bool 대신 중첩 가능한 깊이를 사용한다.
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
                case HitMargin.FailMiss:
                    delta = FailMissDelta;
                    return true;
                case HitMargin.FailOverload:
                    delta = FailOverloadDelta;
                    return true;
                default:
                    // TooLate, Auto, Multipress, OverPress 등은 게이지를 바꾸지 않는다.
                    delta = 0f;
                    return false;
            }
        }
    }
}
