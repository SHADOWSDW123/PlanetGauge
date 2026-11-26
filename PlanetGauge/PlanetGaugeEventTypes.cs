using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ADOFAI;
using ADOFAI.LevelEditor.Controls;
using HarmonyLib;
using UnityEngine;

namespace PlanetGauge
{
    /// <summary>
    /// 레벨 이벤트의 "속성 설정" 드롭다운에 저장되는 값이다.
    /// 실제 판정값 반영은 <see cref="GaugeRuntime"/>가 담당한다.
    /// </summary>
    internal enum PlanetGaugeAttributeMode
    {
        Normal = 0,
        BlockRecovery = 1,
        AmplifyDecrease = 2,
        AmplifyIncrease = 3,
        AmplifyBoth = 4,
        Blindfold = 5,
        ForceRecovery = 6,
        HideGaugeHud = 7,
        XPlanetGauge = 8,
        AdjustJudgementDelta = 9
    }

    internal readonly struct PlanetGaugeJudgementDeltas
    {
        internal static readonly PlanetGaugeJudgementDeltas Default = new PlanetGaugeJudgementDeltas(
            GaugeRuntime.PerfectDelta,
            GaugeRuntime.XPlanetGaugePerfectDelta,
            GaugeRuntime.EarlyPerfectDelta,
            GaugeRuntime.VeryEarlyDelta,
            GaugeRuntime.TooEarlyDelta,
            GaugeRuntime.FailOverloadDelta,
            GaugeRuntime.FailMissDelta);

        internal PlanetGaugeJudgementDeltas(
            float xPerfect,
            float perfect,
            float earlyLatePerfect,
            float veryEarlyLate,
            float tooEarly,
            float failOverload,
            float failMiss)
        {
            XPerfect = PlanetGaugeValueRules.SanitizeJudgementDelta(xPerfect, GaugeRuntime.PerfectDelta);
            Perfect = PlanetGaugeValueRules.SanitizeJudgementDelta(perfect, GaugeRuntime.XPlanetGaugePerfectDelta);
            EarlyLatePerfect = PlanetGaugeValueRules.SanitizeJudgementDelta(earlyLatePerfect, GaugeRuntime.EarlyPerfectDelta);
            VeryEarlyLate = PlanetGaugeValueRules.SanitizeJudgementDelta(veryEarlyLate, GaugeRuntime.VeryEarlyDelta);
            TooEarly = PlanetGaugeValueRules.SanitizeJudgementDelta(tooEarly, GaugeRuntime.TooEarlyDelta);
            FailOverload = PlanetGaugeValueRules.SanitizeJudgementDelta(failOverload, GaugeRuntime.FailOverloadDelta);
            FailMiss = PlanetGaugeValueRules.SanitizeJudgementDelta(failMiss, GaugeRuntime.FailMissDelta);
        }

        internal float XPerfect { get; }
        internal float Perfect { get; }
        internal float EarlyLatePerfect { get; }
        internal float VeryEarlyLate { get; }
        internal float TooEarly { get; }
        internal float FailOverload { get; }
        internal float FailMiss { get; }

        internal bool EqualsApproximate(PlanetGaugeJudgementDeltas other)
        {
            return Mathf.Approximately(XPerfect, other.XPerfect)
                && Mathf.Approximately(Perfect, other.Perfect)
                && Mathf.Approximately(EarlyLatePerfect, other.EarlyLatePerfect)
                && Mathf.Approximately(VeryEarlyLate, other.VeryEarlyLate)
                && Mathf.Approximately(TooEarly, other.TooEarly)
                && Mathf.Approximately(FailOverload, other.FailOverload)
                && Mathf.Approximately(FailMiss, other.FailMiss);
        }
    }

    internal enum PlanetGaugeRateSource
    {
        None,
        Increase,
        Decrease,
        Both
    }

    internal struct PlanetGaugeRateChannel
    {
        internal PlanetGaugeRateChannel(bool enabled, float percent, PlanetGaugeRateSource source)
        {
            Enabled = enabled;
            Percent = PlanetGaugeValueRules.SanitizeMultiplier(percent);
            Source = enabled ? source : PlanetGaugeRateSource.None;
        }

        internal bool Enabled { get; }
        internal float Percent { get; }
        internal PlanetGaugeRateSource Source { get; }

        internal static PlanetGaugeRateChannel Disabled
        {
            get { return new PlanetGaugeRateChannel(false, 100f, PlanetGaugeRateSource.None); }
        }
    }

    internal struct PlanetGaugeEventSettings
    {
        internal static readonly PlanetGaugeEventSettings Default = new PlanetGaugeEventSettings(
            false,
            PlanetGaugeRateChannel.Disabled,
            PlanetGaugeRateChannel.Disabled,
            100f,
            100f,
            100f,
            false,
            true,
            false,
            100f,
            false,
            false,
            PlanetGaugeJudgementDeltas.Default);

        internal PlanetGaugeEventSettings(
            bool recoveryBlocked,
            PlanetGaugeRateChannel recoveryRate,
            PlanetGaugeRateChannel damageRate,
            float configuredIncreasePercent,
            float configuredDecreasePercent,
            float configuredBothPercent,
            bool blindfoldEnabled,
            bool failureProtection,
            bool recoveryCapEnabled,
            float recoveryCapPercent,
            bool xPlanetGaugeEnabled)
            : this(
                recoveryBlocked,
                recoveryRate,
                damageRate,
                configuredIncreasePercent,
                configuredDecreasePercent,
                configuredBothPercent,
                blindfoldEnabled,
                failureProtection,
                recoveryCapEnabled,
                recoveryCapPercent,
                xPlanetGaugeEnabled,
                false,
                PlanetGaugeJudgementDeltas.Default)
        {
        }

        internal PlanetGaugeEventSettings(
            bool recoveryBlocked,
            PlanetGaugeRateChannel recoveryRate,
            PlanetGaugeRateChannel damageRate,
            float configuredIncreasePercent,
            float configuredDecreasePercent,
            float configuredBothPercent,
            bool blindfoldEnabled,
            bool failureProtection,
            bool recoveryCapEnabled,
            float recoveryCapPercent,
            bool xPlanetGaugeEnabled,
            bool customDeltasEnabled,
            PlanetGaugeJudgementDeltas customDeltas)
        {
            RecoveryBlocked = recoveryBlocked;
            RecoveryRate = recoveryRate;
            DamageRate = damageRate;
            ConfiguredIncreasePercent = PlanetGaugeValueRules.SanitizeMultiplier(configuredIncreasePercent);
            ConfiguredDecreasePercent = PlanetGaugeValueRules.SanitizeMultiplier(configuredDecreasePercent);
            ConfiguredBothPercent = PlanetGaugeValueRules.SanitizeMultiplier(configuredBothPercent);
            BlindfoldEnabled = blindfoldEnabled;
            FailureProtection = failureProtection;
            RecoveryCapEnabled = recoveryCapEnabled;
            RecoveryCapPercent = recoveryCapPercent;
            XPlanetGaugeEnabled = xPlanetGaugeEnabled;
            CustomDeltasEnabled = customDeltasEnabled;
            CustomDeltas = customDeltasEnabled ? customDeltas : PlanetGaugeJudgementDeltas.Default;
        }

        internal bool RecoveryBlocked { get; }
        internal PlanetGaugeRateChannel RecoveryRate { get; }
        internal PlanetGaugeRateChannel DamageRate { get; }
        internal float ConfiguredIncreasePercent { get; }
        internal float ConfiguredDecreasePercent { get; }
        internal float ConfiguredBothPercent { get; }

        internal bool BlindfoldEnabled { get; }

        internal bool FailureProtection { get; }

        internal bool RecoveryCapEnabled { get; }

        internal float RecoveryCapPercent { get; }
        internal bool XPlanetGaugeEnabled { get; }
        internal bool CustomDeltasEnabled { get; }
        internal PlanetGaugeJudgementDeltas CustomDeltas { get; }
    }

    /// <summary>
    /// 이벤트 하나가 현재 런타임 설정 중 어떤 값만 바꿀지 나타낸다.
    /// PropertyInfo의 O/X 상태는 LevelEvent.disabled에 저장되며 여기서 한 번만 해석한다.
    /// </summary>
    internal struct PlanetGaugeEventCommand
    {
        internal bool ApplyAttributeMode;
        internal PlanetGaugeAttributeMode AttributeMode;
        internal bool AttributeEnabled;
        internal bool DisableOtherAttributes;
        internal bool ApplyMultiplier;
        internal float MultiplierPercent;
        internal float RecoveryAmountPercent;
        internal bool ApplyFailureProtection;
        internal bool FailureProtection;
        internal bool ApplyRecoveryCap;
        internal bool RecoveryCapEnabled;
        internal float RecoveryCapPercent;
        internal bool ForceRecoveryCap;
        internal bool HideGaugeBar;
        internal bool HideGaugeValue;
        internal bool HideAttributeText;
        internal bool HideRateToken;
        internal bool HideForceRecoveryVisuals;
        internal bool DisableHudHideAnimation;
        internal PlanetGaugeJudgementDeltas JudgementDeltas;
    }

    internal static class PlanetGaugeValueRules
    {
        internal static float SanitizeJudgementDelta(float value, float fallback)
        {
            return SanitizeFinite(value, fallback);
        }
        internal static float SanitizeMultiplier(float value)
        {
            return SanitizeMinimum(value, 100f, 0f);
        }

        internal static float SanitizeRecoveryCap(float value)
        {
            return SanitizeMinimum(value, 100f, 0.1f);
        }

        internal static float SanitizeRecoveryAmount(float value)
        {
            return SanitizeFinite(value, 0f);
        }

        internal static float SanitizeWarningOffsetAngle(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            return Mathf.Min(0f, value);
        }

        internal static float SanitizeWarningPulseBeats(float value)
        {
            return SanitizeMinimum(value, 0.5f, 0.0125f);
        }

        internal static bool IsAmplificationMode(PlanetGaugeAttributeMode mode)
        {
            return mode == PlanetGaugeAttributeMode.AmplifyDecrease
                || mode == PlanetGaugeAttributeMode.AmplifyIncrease
                || mode == PlanetGaugeAttributeMode.AmplifyBoth;
        }

        private static float SanitizeMinimum(
            float value,
            float fallback,
            float minimum)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallback;
            }

            return Mathf.Max(value, minimum);
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? fallback
                : value;
        }
    }

    /// <summary>
    /// Assembly-CSharp의 LevelEventInfo/PropertyInfo 계약에 PlanetGauge 이벤트를 등록한다.
    /// 네이티브 enum을 수정할 수 없으므로 충돌 가능성이 낮은 고정 숫자 ID를 사용한다.
    /// </summary>
}
