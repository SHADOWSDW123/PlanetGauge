using UnityEngine;

namespace PlanetGauge
{
    /// <summary>
    /// 게이지 HUD의 각 표시 범위를 독립적으로 페이드한다.
    /// 게임플레이 활성 상태와 분리되어 있으며 세션 초기화 시 즉시 전부 표시된다.
    /// </summary>
    internal static class GaugeHudVisibilityTransitions
    {
        private const float DefaultDuration = 4f;

        private static FadeChannel gaugeBar = FadeChannel.Visible;
        private static FadeChannel gaugeValue = FadeChannel.Visible;
        private static FadeChannel attributeText = FadeChannel.Visible;
        private static FadeChannel rateToken = FadeChannel.Visible;
        private static FadeChannel forceRecoveryVisuals = FadeChannel.Visible;

        internal static float GaugeBarAlpha { get { return gaugeBar.Alpha; } }
        internal static float GaugeValueAlpha { get { return gaugeValue.Alpha; } }
        internal static float AttributeTextAlpha { get { return attributeText.Alpha; } }
        internal static float RateTokenAlpha { get { return rateToken.Alpha; } }
        internal static float ForceRecoveryVisualsAlpha { get { return forceRecoveryVisuals.Alpha; } }
        internal static bool GaugeBarHidden { get { return gaugeBar.TargetHidden; } }
        internal static bool GaugeValueHidden { get { return gaugeValue.TargetHidden; } }
        internal static bool AttributeTextHidden { get { return attributeText.TargetHidden; } }
        internal static bool RateTokenHidden { get { return rateToken.TargetHidden; } }
        internal static bool ForceRecoveryVisualsHidden { get { return forceRecoveryVisuals.TargetHidden; } }

        internal static void Reset()
        {
            gaugeBar = FadeChannel.Visible;
            gaugeValue = FadeChannel.Visible;
            attributeText = FadeChannel.Visible;
            rateToken = FadeChannel.Visible;
            forceRecoveryVisuals = FadeChannel.Visible;
        }

        internal static void Tick(float unscaledDeltaTime)
        {
            float deltaTime = float.IsNaN(unscaledDeltaTime)
                || float.IsInfinity(unscaledDeltaTime)
                || unscaledDeltaTime < 0f
                ? 0f
                : unscaledDeltaTime;
            gaugeBar.Tick(deltaTime);
            gaugeValue.Tick(deltaTime);
            attributeText.Tick(deltaTime);
            rateToken.Tick(deltaTime);
            forceRecoveryVisuals.Tick(deltaTime);
        }

        internal static void RevealAll()
        {
            gaugeBar.SetHidden(false);
            gaugeValue.SetHidden(false);
            attributeText.SetHidden(false);
            rateToken.SetHidden(false);
            forceRecoveryVisuals.SetHidden(false);
        }

        internal static void Apply(PlanetGaugeEventCommand command)
        {
            if (!command.AttributeEnabled)
            {
                RevealAll();
                return;
            }

            // 각 Bool은 선택 여부가 아니라 해당 범위의 목표 숨김 상태다.
            // false도 명시적인 표시 명령으로 적용해야 이전 이벤트의 숨김이 남지 않는다.
            gaugeBar.SetHidden(command.HideGaugeBar);
            gaugeValue.SetHidden(command.HideGaugeValue);
            attributeText.SetHidden(command.HideAttributeText);
            rateToken.SetHidden(command.HideRateToken);
            forceRecoveryVisuals.SetHidden(command.HideForceRecoveryVisuals);
        }

        private struct FadeChannel
        {
            internal float Alpha;
            private float startAlpha;
            private float targetAlpha;
            private float duration;
            private float elapsed;
            private bool transitioning;

            internal bool TargetHidden { get { return targetAlpha < 0.5f; } }

            internal static FadeChannel Visible
            {
                get
                {
                    return new FadeChannel
                    {
                        Alpha = 1f,
                        startAlpha = 1f,
                        targetAlpha = 1f,
                        duration = 0f,
                        elapsed = 0f,
                        transitioning = false
                    };
                }
            }

            internal void SetHidden(bool hidden)
            {
                float nextTarget = hidden ? 0f : 1f;
                if (Mathf.Approximately(targetAlpha, nextTarget)
                    && (transitioning || Mathf.Approximately(Alpha, nextTarget)))
                {
                    return;
                }

                float nextDuration = transitioning
                    ? Mathf.Max(0f, duration - elapsed)
                    : DefaultDuration;
                startAlpha = Alpha;
                targetAlpha = nextTarget;
                duration = nextDuration;
                elapsed = 0f;
                transitioning = duration > 0.0001f
                    && !Mathf.Approximately(startAlpha, targetAlpha);
                if (!transitioning)
                {
                    Alpha = targetAlpha;
                }
            }

            internal void Tick(float deltaTime)
            {
                if (!transitioning)
                {
                    return;
                }

                elapsed = Mathf.Min(duration, elapsed + deltaTime);
                float progress = duration <= 0f ? 1f : elapsed / duration;
                float eased = EaseInOutCubic(progress);
                Alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);
                if (elapsed >= duration)
                {
                    Alpha = targetAlpha;
                    transitioning = false;
                }
            }

            private static float EaseInOutCubic(float value)
            {
                float t = Mathf.Clamp01(value);
                return t < 0.5f
                    ? 4f * t * t * t
                    : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
            }
        }
    }
}
