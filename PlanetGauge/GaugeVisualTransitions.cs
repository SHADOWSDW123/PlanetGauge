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
        private static readonly List<ForceTransition> transitions = new List<ForceTransition>();

        internal static void Reset()
        {
            warnings.Clear();
            transitions.Clear();
        }

        internal static void Tick(float unscaledDeltaTime)
        {
            float elapsed = float.IsNaN(unscaledDeltaTime) || float.IsInfinity(unscaledDeltaTime)
                ? 0f
                : Mathf.Max(0f, unscaledDeltaTime);

            for (int index = transitions.Count - 1; index >= 0; index--)
            {
                ForceTransition transition = transitions[index];
                transition.Elapsed += elapsed;
                if (transition.Elapsed >= ForceTransitionDuration)
                {
                    transitions.RemoveAt(index);
                }
                else
                {
                    transitions[index] = transition;
                }
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

            transitions.Add(new ForceTransition
            {
                InitialOffset = -actualDelta,
                Elapsed = 0f
            });
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
            float offset = 0f;
            for (int index = 0; index < transitions.Count; index++)
            {
                ForceTransition transition = transitions[index];
                float progress = Mathf.Clamp01(
                    transition.Elapsed / ForceTransitionDuration);
                float eased = Mathf.Sqrt(
                    Mathf.Max(0f, 1f - (progress - 1f) * (progress - 1f)));
                offset += transition.InitialOffset * (1f - eased);
            }

            return offset;
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
