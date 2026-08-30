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
    internal sealed class PlanetGaugeLevelEventEffect : ffxPlusBase
    {
        private PlanetGaugeEventCommand command;

        public override void Decode(LevelEvent levelEvent)
        {
            PlanetGaugeAttributeMode mode = levelEvent.Get<PlanetGaugeAttributeMode>(
                PlanetGaugeLevelEventRegistry.AttributeModeKey,
                PlanetGaugeAttributeMode.Normal);
            if (!Enum.IsDefined(typeof(PlanetGaugeAttributeMode), mode))
            {
                mode = PlanetGaugeAttributeMode.Normal;
            }

            float multiplier = PlanetGaugeValueRules.SanitizeMultiplier(
                levelEvent.Get<float>(PlanetGaugeLevelEventRegistry.MultiplierPercentKey, 100f));
            float recoveryAmount = PlanetGaugeValueRules.SanitizeRecoveryAmount(
                levelEvent.Get<float>(PlanetGaugeLevelEventRegistry.RecoveryAmountPercentKey, 0f));
            bool failureProtection = levelEvent.Get<bool>(
                PlanetGaugeLevelEventRegistry.FailureProtectionKey,
                true);
            bool recoveryCapEnabled = levelEvent.Get<bool>(
                PlanetGaugeLevelEventRegistry.RecoveryCapEnabledKey,
                false);
            float recoveryCap = PlanetGaugeValueRules.SanitizeRecoveryCap(
                levelEvent.Get<float>(PlanetGaugeLevelEventRegistry.RecoveryCapPercentKey, 100f));
            bool forceRecoveryCap = levelEvent.Get<bool>(
                PlanetGaugeLevelEventRegistry.ForceRecoveryCapKey,
                true);
            bool attributeEnabled = levelEvent.Get<bool>(
                PlanetGaugeLevelEventRegistry.AttributeEnabledKey,
                true);
            bool disableOtherAttributes = levelEvent.Get<bool>(
                PlanetGaugeLevelEventRegistry.DisableOtherAttributesKey,
                false);
            bool autoTileRecovery = levelEvent.Get<bool>(
                PlanetGaugeLevelEventRegistry.AutoTileRecoveryKey,
                false);
            bool hideGaugeBar = levelEvent.Get<bool>(
                PlanetGaugeLevelEventRegistry.HideGaugeBarKey,
                true);
            bool hideGaugeValue = levelEvent.Get<bool>(
                PlanetGaugeLevelEventRegistry.HideGaugeValueKey,
                true);
            bool hideAttributeText = levelEvent.Get<bool>(
                PlanetGaugeLevelEventRegistry.HideAttributeTextKey,
                true);
            bool hideRateToken = levelEvent.Get<bool>(
                PlanetGaugeLevelEventRegistry.HideRateTokenKey,
                true);
            bool hideForceRecoveryVisuals = levelEvent.Get<bool>(
                PlanetGaugeLevelEventRegistry.HideForceRecoveryVisualsKey,
                true);

            command = new PlanetGaugeEventCommand
            {
                ApplyAttributeMode = IsPropertyEnabled(
                    levelEvent,
                    PlanetGaugeLevelEventRegistry.AttributeModeKey),
                AttributeMode = mode,
                AttributeEnabled = attributeEnabled,
                DisableOtherAttributes = disableOtherAttributes,
                ApplyMultiplier = IsPropertyEnabled(
                    levelEvent,
                    PlanetGaugeLevelEventRegistry.MultiplierPercentKey),
                MultiplierPercent = multiplier,
                RecoveryAmountPercent = recoveryAmount,
                ApplyFailureProtection = IsPropertyEnabled(
                    levelEvent,
                    PlanetGaugeLevelEventRegistry.FailureProtectionKey),
                FailureProtection = failureProtection,
                ApplyRecoveryCap = IsPropertyEnabled(
                    levelEvent,
                    PlanetGaugeLevelEventRegistry.RecoveryCapEnabledKey),
                RecoveryCapEnabled = recoveryCapEnabled,
                RecoveryCapPercent = recoveryCap,
                ForceRecoveryCap = IsPropertyEnabled(
                    levelEvent,
                    PlanetGaugeLevelEventRegistry.RecoveryCapEnabledKey)
                    && recoveryCapEnabled
                    && forceRecoveryCap,
                ApplyAutoTileRecovery = IsPropertyEnabled(
                    levelEvent,
                    PlanetGaugeLevelEventRegistry.AutoTileRecoveryKey),
                AutoTileRecovery = autoTileRecovery,
                HideGaugeBar = hideGaugeBar,
                HideGaugeValue = hideGaugeValue,
                HideAttributeText = hideAttributeText,
                HideRateToken = hideRateToken,
                HideForceRecoveryVisuals = hideForceRecoveryVisuals
            };
        }

        public override void StartEffect(scrPlanet planet)
        {
            try
            {
                GaugeVisualTransitions.CancelWarning(VisualToken);
            }
            catch (Exception exception)
            {
                Main.LogException(
                    "SetPlanetGauge 사전 경고 정리에 실패했지만 이벤트 실행은 계속합니다.",
                    exception);
            }
            if (GaugeRuntime.ShouldHandle())
            {
                GaugeRuntime.ApplyEventSettings(command);
            }
        }

        internal int VisualToken { get; set; }

        private static bool IsPropertyEnabled(LevelEvent levelEvent, string propertyName)
        {
            bool disabled;
            return levelEvent.disabled == null
                || !levelEvent.disabled.TryGetValue(propertyName, out disabled)
                || !disabled;
        }
    }

    internal sealed class PlanetGaugeWarningLevelEventEffect : ffxPlusBase
    {
        private float recoveryAmount;
        private float warningPulseBeats;

        internal int VisualToken { get; set; }

        public override void Decode(LevelEvent levelEvent)
        {
            recoveryAmount = PlanetGaugeValueRules.SanitizeRecoveryAmount(
                levelEvent.Get<float>(PlanetGaugeLevelEventRegistry.RecoveryAmountPercentKey, 0f));
            warningPulseBeats = PlanetGaugeValueRules.SanitizeWarningPulseBeats(
                levelEvent.Get<float>(PlanetGaugeLevelEventRegistry.WarningPulseBeatsKey, 0.5f));
        }

        public override void StartEffect(scrPlanet planet)
        {
            if (GaugeRuntime.ShouldHandle())
            {
                try
                {
                    GaugeVisualTransitions.BeginWarning(
                        VisualToken,
                        recoveryAmount,
                        warningPulseBeats,
                        crotchet,
                        startTime);
                }
                catch (Exception exception)
                {
                    Main.LogException(
                        "SetPlanetGauge 사전 경고 등록에 실패해 경고 없이 계속합니다.",
                        exception);
                }
            }
        }
    }

}
