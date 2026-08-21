using System;
using UnityEngine;

namespace PlanetGauge
{
    internal static class GaugeRuntime
    {
        internal const float InitialGauge = 100f;
        internal const float MaximumGauge = 100f;
        internal const float NoFailMinimumGauge = -5f;
        internal const float PerfectDelta = 0.1f;
        internal const float EarlyPerfectDelta = -0.8f;
        internal const float LatePerfectDelta = -0.8f;
        internal const float VeryEarlyDelta = -1.5f;
        internal const float VeryLateDelta = -1.5f;
        internal const float TooEarlyDelta = -3f;
        internal const float FailMissDelta = -6f;
        internal const float FailOverloadDelta = -8f;

        private static bool frozen;
        private static bool nextDieAlreadyCharged;
        private static int failureRecoveryDepth;
        private static bool forcingDeath;
        private static bool blindfoldRevealed;
        private static int styleRevision;
        private static readonly float[] judgementTotals = new float[8];
        private static float autoTotal;

        internal static float Current { get; private set; } = InitialGauge;
        internal static PlanetGaugeEventSettings EventSettings { get; private set; } = PlanetGaugeEventSettings.Default;
        internal static bool IsRecoveringFailure { get { return failureRecoveryDepth > 0; } }
        internal static bool IsForcingDeath { get { return forcingDeath; } }
        internal static bool IsFrozen { get { return frozen; } }
        internal static bool HasPendingDieCharge { get { return nextDieAlreadyCharged; } }
        internal static bool IsBlindfolded
        {
            get { return EventSettings.BlindfoldEnabled && !blindfoldRevealed; }
        }
        internal static bool IsBlindfoldRevealed { get { return blindfoldRevealed; } }
        internal static int FailureRecoveryDepth { get { return failureRecoveryDepth; } }
        internal static int StyleRevision { get { return styleRevision; } }
        internal static float AutoTotal { get { return autoTotal; } }
        internal static float RecoveryMaximum
        {
            get
            {
                return EventSettings.RecoveryCapEnabled
                    ? PlanetGaugeValueRules.SanitizeRecoveryCap(EventSettings.RecoveryCapPercent)
                    : MaximumGauge;
            }
        }

        internal static void Reset()
        {
            Current = InitialGauge;
            frozen = false;
            nextDieAlreadyCharged = false;
            failureRecoveryDepth = 0;
            forcingDeath = false;
            blindfoldRevealed = false;
            EventSettings = PlanetGaugeEventSettings.Default;
            Array.Clear(judgementTotals, 0, judgementTotals.Length);
            autoTotal = 0f;
            styleRevision++;
        }

        internal static void ApplyEventSettings(PlanetGaugeEventCommand command)
        {
            PlanetGaugeEventSettings current = EventSettings;
            bool recoveryBlocked = current.RecoveryBlocked;
            PlanetGaugeRateChannel recoveryRate = current.RecoveryRate;
            PlanetGaugeRateChannel damageRate = current.DamageRate;
            float configuredIncrease = current.ConfiguredIncreasePercent;
            float configuredDecrease = current.ConfiguredDecreasePercent;
            float configuredBoth = current.ConfiguredBothPercent;
            bool blindfoldEnabled = current.BlindfoldEnabled;

            if (command.ApplyMultiplier)
            {
                float value = PlanetGaugeValueRules.SanitizeMultiplier(command.MultiplierPercent);
                switch (command.AttributeMode)
                {
                    case PlanetGaugeAttributeMode.AmplifyIncrease: configuredIncrease = value; break;
                    case PlanetGaugeAttributeMode.AmplifyDecrease: configuredDecrease = value; break;
                    case PlanetGaugeAttributeMode.AmplifyBoth: configuredBoth = value; break;
                }
            }

            if (command.ApplyAttributeMode)
            {
                if (command.DisableOtherAttributes)
                {
                    recoveryBlocked = false;
                    recoveryRate = PlanetGaugeRateChannel.Disabled;
                    damageRate = PlanetGaugeRateChannel.Disabled;
                    blindfoldEnabled = false;
                }

                switch (command.AttributeMode)
                {
                    case PlanetGaugeAttributeMode.BlockRecovery:
                        recoveryBlocked = command.AttributeEnabled;
                        break;
                    case PlanetGaugeAttributeMode.AmplifyIncrease:
                        recoveryRate = command.AttributeEnabled
                            ? new PlanetGaugeRateChannel(true, configuredIncrease, PlanetGaugeRateSource.Increase)
                            : PlanetGaugeRateChannel.Disabled;
                        break;
                    case PlanetGaugeAttributeMode.AmplifyDecrease:
                        damageRate = command.AttributeEnabled
                            ? new PlanetGaugeRateChannel(true, configuredDecrease, PlanetGaugeRateSource.Decrease)
                            : PlanetGaugeRateChannel.Disabled;
                        break;
                    case PlanetGaugeAttributeMode.AmplifyBoth:
                        PlanetGaugeRateChannel both = command.AttributeEnabled
                            ? new PlanetGaugeRateChannel(true, configuredBoth, PlanetGaugeRateSource.Both)
                            : PlanetGaugeRateChannel.Disabled;
                        recoveryRate = both;
                        damageRate = both;
                        break;
                    case PlanetGaugeAttributeMode.Blindfold:
                        blindfoldEnabled = command.AttributeEnabled;
                        break;
                }
            }

            bool failureProtection = command.ApplyFailureProtection ? command.FailureProtection : current.FailureProtection;
            bool recoveryCapEnabled = command.ApplyRecoveryCap ? command.RecoveryCapEnabled : current.RecoveryCapEnabled;
            float recoveryCapPercent = command.ApplyRecoveryCap
                ? PlanetGaugeValueRules.SanitizeRecoveryCap(command.RecoveryCapPercent)
                : current.RecoveryCapPercent;
            bool autoTileRecovery = command.ApplyAutoTileRecovery ? command.AutoTileRecovery : current.AutoTileRecovery;

            EventSettings = new PlanetGaugeEventSettings(
                recoveryBlocked, recoveryRate, damageRate,
                configuredIncrease, configuredDecrease, configuredBoth,
                blindfoldEnabled,
                failureProtection, recoveryCapEnabled, recoveryCapPercent, autoTileRecovery);
            styleRevision++;

            if (command.ForceRecoveryCap && recoveryCapEnabled && Current > recoveryCapPercent)
            {
                Current = recoveryCapPercent;
            }

            if (command.ApplyAttributeMode
                && command.AttributeMode == PlanetGaugeAttributeMode.ForceRecovery)
            {
                bool shouldDie = ApplyForcedRecovery(command.RecoveryAmountPercent);
                if (shouldDie)
                {
                    scrController controller = scrController.instance;
                    ForceDie(controller == null ? null : controller.playerOne);
                }
            }
        }

        internal static bool ShouldHandle(scrPlayer player = null)
        {
            if (!Main.IsEnabled || !Main.EditorGaugeEnabled)
            {
                return false;
            }

            scnEditor editor = scnEditor.instance;
            scrController controller = scrController.instance;
            if (editor == null || controller == null || controller.paused
                || !controller.gameworld || scrPlayerManager.playerCount != 1)
            {
                return false;
            }

            return player == null || controller.playerOne == player;
        }

        internal static bool IsGameplayContext(bool allowPaused)
        {
            scrController controller = scrController.instance;
            return Main.IsEnabled && Main.EditorGaugeEnabled && scnEditor.instance != null
                && controller != null && controller.gameworld
                && (allowPaused || !controller.paused)
                && scrPlayerManager.playerCount == 1;
        }

        internal static bool IsAutoPlay(scrPlayer player = null)
        {
            if (RDC.auto)
            {
                return true;
            }

            scrController controller = scrController.instance;
            scrPlayer targetPlayer = player ?? (controller == null ? null : controller.playerOne);
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
                return scrController.instance != null && !scrController.instance.noFail && Current <= 0f;
            }

            scrController controller = scrController.instance;
            if (IsFailureJudgement(judgement)
                && !EventSettings.FailureProtection
                && controller != null
                && controller.noFail)
            {
                ApplyProtectedFailureGaugeDeath(judgement);
                return false;
            }

            if (IsFailureJudgement(judgement) && !EventSettings.FailureProtection
                && (controller == null || !controller.noFail))
            {
                RevealBlindfold();
                return true;
            }

            float delta;
            return TryGetDelta(judgement, out delta) && ApplyDelta(delta, judgement, false);
        }

        internal static void ApplyAutomaticRecovery()
        {
            if (ShouldHandle() && !frozen)
            {
                ApplyDelta(PerfectDelta, HitMargin.Auto, true);
            }
        }

        internal static float GetJudgementTotal(HitMargin judgement)
        {
            int index = GetTotalIndex(judgement);
            return index < 0 ? 0f : judgementTotals[index];
        }

        private static bool ApplyDelta(float rawDelta, HitMargin judgement, bool automatic)
        {
            float delta = TransformDelta(rawDelta);
            float previous = Current;
            float next = delta > 0f
                ? (Current >= RecoveryMaximum ? Current : Mathf.Min(RecoveryMaximum, Current + delta))
                : Current + delta;

            scrController controller = scrController.instance;
            bool shouldDie = false;
            if (next > 0f)
            {
                Current = next;
            }
            else if (controller != null && controller.noFail)
            {
                Current = Mathf.Max(NoFailMinimumGauge, next);
                frozen = true;
                RevealBlindfold();
            }
            else
            {
                Current = 0f;
                frozen = true;
                shouldDie = true;
                RevealBlindfold();
            }

            float actual = Current - previous;
            if (automatic)
            {
                autoTotal += actual;
            }
            else
            {
                int index = GetTotalIndex(judgement);
                if (index >= 0) judgementTotals[index] += actual;
            }

            return shouldDie;
        }

        private static bool ApplyForcedRecovery(float amount)
        {
            if (frozen)
            {
                return false;
            }

            float delta = PlanetGaugeValueRules.SanitizeRecoveryAmount(amount);
            if (Mathf.Approximately(delta, 0f))
            {
                return false;
            }

            float next = delta > 0f
                ? (Current >= RecoveryMaximum ? Current : Mathf.Min(RecoveryMaximum, Current + delta))
                : Current + delta;

            scrController controller = scrController.instance;
            if (next > 0f)
            {
                Current = next;
                return false;
            }

            if (controller != null && controller.noFail)
            {
                Current = Mathf.Max(NoFailMinimumGauge, next);
                frozen = true;
                RevealBlindfold();
                return false;
            }

            Current = 0f;
            frozen = true;
            RevealBlindfold();
            return true;
        }

        private static void ApplyProtectedFailureGaugeDeath(HitMargin judgement)
        {
            float previous = Current;
            Current = 0f;
            frozen = true;
            RevealBlindfold();

            int index = GetTotalIndex(judgement);
            if (index >= 0)
            {
                judgementTotals[index] += Current - previous;
            }
        }

        internal static void MarkNextDieAlreadyCharged() { nextDieAlreadyCharged = true; }
        internal static bool ConsumeNextDieAlreadyCharged()
        {
            bool charged = nextDieAlreadyCharged;
            nextDieAlreadyCharged = false;
            return charged;
        }
        internal static void ClearPendingDieCharge() { nextDieAlreadyCharged = false; }
        internal static void RevealBlindfold()
        {
            if (EventSettings.BlindfoldEnabled && !blindfoldRevealed)
            {
                blindfoldRevealed = true;
                styleRevision++;
            }
        }

        internal static void DisableBlindfoldForLevelCompletion()
        {
            if (!EventSettings.BlindfoldEnabled)
            {
                return;
            }

            ApplyEventSettings(new PlanetGaugeEventCommand
            {
                ApplyAttributeMode = true,
                AttributeMode = PlanetGaugeAttributeMode.Blindfold,
                AttributeEnabled = false
            });
        }
        internal static void BeginFailureRecovery() { failureRecoveryDepth++; }
        internal static void EndFailureRecovery() { if (failureRecoveryDepth > 0) failureRecoveryDepth--; }

        internal static void ForceDie(scrPlayer player)
        {
            if (player == null || forcingDeath) return;
            ClearPendingDieCharge();
            forcingDeath = true;
            try
            {
                player.Die();
            }
            catch (Exception exception)
            {
                Main.LogException("게이지 소진 후 scrPlayer.Die 호출에 실패했습니다.", exception);
                try
                {
                    scrController controller = scrController.instance;
                    if (controller != null) controller.FailAction();
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
                case HitMargin.Perfect: delta = PerfectDelta; return true;
                case HitMargin.EarlyPerfect: delta = EarlyPerfectDelta; return true;
                case HitMargin.LatePerfect: delta = LatePerfectDelta; return true;
                case HitMargin.VeryEarly: delta = VeryEarlyDelta; return true;
                case HitMargin.VeryLate: delta = VeryLateDelta; return true;
                case HitMargin.TooEarly: delta = TooEarlyDelta; return true;
                case HitMargin.FailMiss: delta = FailMissDelta; return true;
                case HitMargin.FailOverload: delta = FailOverloadDelta; return true;
                default: delta = 0f; return false;
            }
        }

        private static float TransformDelta(float delta)
        {
            PlanetGaugeEventSettings settings = EventSettings;
            if (delta > 0f)
            {
                if (settings.RecoveryBlocked) return 0f;
                return settings.RecoveryRate.Enabled ? delta * settings.RecoveryRate.Percent / 100f : delta;
            }
            return delta < 0f && settings.DamageRate.Enabled ? delta * settings.DamageRate.Percent / 100f : delta;
        }

        private static int GetTotalIndex(HitMargin judgement)
        {
            switch (judgement)
            {
                case HitMargin.TooEarly: return 0;
                case HitMargin.VeryEarly: return 1;
                case HitMargin.EarlyPerfect: return 2;
                case HitMargin.Perfect: return 3;
                case HitMargin.LatePerfect: return 4;
                case HitMargin.VeryLate: return 5;
                case HitMargin.FailMiss: return 6;
                case HitMargin.FailOverload: return 7;
                default: return -1;
            }
        }

        private static bool IsFailureJudgement(HitMargin judgement)
        {
            return judgement == HitMargin.FailMiss || judgement == HitMargin.FailOverload;
        }
    }
}
