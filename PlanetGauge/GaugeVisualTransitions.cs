using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlanetGauge
{
    internal struct GaugeOverlaySegment
    {
        internal GaugeOverlaySegment(float start, float end, Color32 color)
        {
            Start = Mathf.Clamp01(Mathf.Min(start, end));
            End = Mathf.Clamp01(Mathf.Max(start, end));
            Color = color;
        }

        internal float Start;
        internal float End;
        internal Color32 Color;
    }

    /// <summary>
    /// ForceRecovery의 표시 전용 상태다. 논리 체력과 사망 판단은 계속 GaugeRuntime만 소유한다.
    /// </summary>
    internal static class GaugeVisualTransitions
    {
        internal const float ForceTransitionDuration = 0.75f;
        internal const float BlindfoldTransitionDuration = 0.5f;

        private static readonly Color32 WarningBaseColor = new Color32(0, 0, 0, 255);
        private static readonly Color32 DamageColor = new Color32(176, 32, 32, 255);
        private static readonly Color32 RecoveryColor = new Color32(69, 214, 107, 255);

        private struct PendingWarning
        {
            internal int Token;
            internal float Amount;
            internal float PulseBeats;
            internal float BeatDuration;
            internal double StartSongTime;
        }

        private struct ForceTransition
        {
            internal float InitialOffset;
            internal float Elapsed;
        }

        private static readonly List<PendingWarning> warnings = new List<PendingWarning>();
        private static ForceTransition forceTransition;
        private static bool forceTransitionActive;
        private static float blindfoldAlpha;
        private static float blindfoldStartAlpha;
        private static float blindfoldTargetAlpha;
        private static float blindfoldElapsed;

        internal static float BlindfoldAlpha { get { return blindfoldAlpha; } }

        internal static void Reset()
        {
            warnings.Clear();
            forceTransition = default(ForceTransition);
            forceTransitionActive = false;
            blindfoldAlpha = 0f;
            blindfoldStartAlpha = 0f;
            blindfoldTargetAlpha = 0f;
            blindfoldElapsed = 0f;
        }

        internal static void Tick(float unscaledDeltaTime)
        {
            float elapsed = float.IsNaN(unscaledDeltaTime) || float.IsInfinity(unscaledDeltaTime)
                ? 0f
                : Mathf.Max(0f, unscaledDeltaTime);

            UpdateBlindfoldTransition(elapsed);

            if (forceTransitionActive)
            {
                forceTransition.Elapsed += elapsed;
                if (forceTransition.Elapsed >= ForceTransitionDuration)
                {
                    forceTransition = default(ForceTransition);
                    forceTransitionActive = false;
                }
            }
        }

        private static void UpdateBlindfoldTransition(float deltaTime)
        {
            float nextTarget = GaugeRuntime.IsBlindfolded ? 1f : 0f;
            if (!Mathf.Approximately(blindfoldTargetAlpha, nextTarget))
            {
                blindfoldStartAlpha = blindfoldAlpha;
                blindfoldTargetAlpha = nextTarget;
                blindfoldElapsed = 0f;
            }

            if (Mathf.Approximately(blindfoldAlpha, blindfoldTargetAlpha))
            {
                blindfoldAlpha = blindfoldTargetAlpha;
                return;
            }

            blindfoldElapsed = Mathf.Min(
                BlindfoldTransitionDuration,
                blindfoldElapsed + deltaTime);
            float progress = BlindfoldTransitionDuration <= 0f
                ? 1f
                : blindfoldElapsed / BlindfoldTransitionDuration;
            float eased = 1f - (1f - progress) * (1f - progress);
            blindfoldAlpha = Mathf.Lerp(
                blindfoldStartAlpha,
                blindfoldTargetAlpha,
                eased);
            if (blindfoldElapsed >= BlindfoldTransitionDuration)
            {
                blindfoldAlpha = blindfoldTargetAlpha;
            }
        }

        internal static void BeginWarning(
            int token,
            float amount,
            float pulseBeats,
            float beatDuration,
            double startSongTime)
        {
            CancelWarning(token);

            float sanitizedAmount = PlanetGaugeValueRules.SanitizeRecoveryAmount(amount);
            if (Mathf.Approximately(sanitizedAmount, 0f))
            {
                return;
            }

            warnings.Add(new PendingWarning
            {
                Token = token,
                Amount = sanitizedAmount,
                PulseBeats = PlanetGaugeValueRules.SanitizeWarningPulseBeats(pulseBeats),
                BeatDuration = SanitizeBeatDuration(beatDuration),
                StartSongTime = double.IsNaN(startSongTime) || double.IsInfinity(startSongTime)
                    ? GetSongPosition()
                    : startSongTime
            });
        }

        internal static void CancelWarning(int token)
        {
            for (int index = warnings.Count - 1; index >= 0; index--)
            {
                if (warnings[index].Token == token)
                {
                    warnings.RemoveAt(index);
                }
            }
        }

        internal static void AddForceTransition(float actualDelta)
        {
            if (float.IsNaN(actualDelta)
                || float.IsInfinity(actualDelta)
                || Mathf.Approximately(actualDelta, 0f))
            {
                return;
            }

            // 겹친 강제 회복은 현재 표시 오프셋에서 새 실제값으로 자연스럽게 이어 붙인다.
            // List.Add의 최초 backing-array 할당과 이벤트 수만큼의 매 프레임 순회를 피한다.
            float remainingOffset = GetRemainingOffset();
            forceTransition = new ForceTransition
            {
                InitialOffset = remainingOffset - actualDelta,
                Elapsed = 0f
            };
            forceTransitionActive = true;
        }

        internal static float GetDisplayedCurrent()
        {
            return GaugeRuntime.Current + GetRemainingOffset();
        }

        internal static bool TryGetTransitionSegment(out GaugeOverlaySegment segment)
        {
            float remainingOffset = GetRemainingOffset();
            if (Mathf.Approximately(remainingOffset, 0f))
            {
                segment = default(GaugeOverlaySegment);
                return false;
            }

            float maximum = Mathf.Max(0.1f, GaugeRuntime.RecoveryMaximum);
            float displayed = GaugeRuntime.Current + remainingOffset;
            segment = new GaugeOverlaySegment(
                GaugeRuntime.Current / maximum,
                displayed / maximum,
                remainingOffset > 0f ? DamageColor : RecoveryColor);
            return segment.End - segment.Start > 0.00001f;
        }

        internal static void FillWarningSegments(List<GaugeOverlaySegment> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.Clear();
            if (GaugeRuntime.IsBlindfolded)
            {
                return;
            }

            float maximum = Mathf.Max(0.1f, GaugeRuntime.RecoveryMaximum);
            double songPosition = GetSongPosition();
            for (int index = 0; index < warnings.Count; index++)
            {
                PendingWarning warning = warnings[index];
                float predicted = GaugeRuntime.PreviewForcedRecovery(warning.Amount);
                if (Mathf.Approximately(predicted, GaugeRuntime.Current))
                {
                    continue;
                }

                double elapsedBeats = Math.Max(
                    0d,
                    (songPosition - warning.StartSongTime) / warning.BeatDuration);
                float cycle = Mathf.Repeat((float)(elapsedBeats / warning.PulseBeats), 1f);
                float intensity = 1f - Mathf.Abs(2f * cycle - 1f);
                Color32 target = warning.Amount < 0f ? DamageColor : RecoveryColor;
                Color32 color = Color32.Lerp(WarningBaseColor, target, intensity);
                GaugeOverlaySegment segment = new GaugeOverlaySegment(
                    GaugeRuntime.Current / maximum,
                    predicted / maximum,
                    color);
                if (segment.End - segment.Start > 0.00001f)
                {
                    destination.Add(segment);
                }
            }
        }

        private static float GetRemainingOffset()
        {
            if (!forceTransitionActive)
            {
                return 0f;
            }

            float progress = Mathf.Clamp01(
                forceTransition.Elapsed / ForceTransitionDuration);
            float eased = Mathf.Sqrt(
                Mathf.Max(0f, 1f - (progress - 1f) * (progress - 1f)));
            return forceTransition.InitialOffset * (1f - eased);
        }

        private static float SanitizeBeatDuration(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value <= 0f
                ? 1f
                : value;
        }

        private static double GetSongPosition()
        {
            scrConductor conductor = scrConductor.instance;
            return conductor == null ? 0d : conductor.songposition_minusi;
        }
    }
}
