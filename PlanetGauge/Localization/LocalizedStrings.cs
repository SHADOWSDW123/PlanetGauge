using System;
using System.Globalization;
using UnityEngine;

namespace PlanetGauge
{
    internal static class LocalizedStrings
    {
        private static int revision;

        internal static int Revision { get { return revision; } }

        private static bool IsKorean
        {
            get
            {
                return Main.Settings != null
                    && Main.Settings.LanguageInitialized
                    ? Main.Settings.Language == PlanetGaugeLanguage.Korean
                    : DetectGameLanguage() == PlanetGaugeLanguage.Korean;
            }
        }

        private static bool IsAttributeDisplayKorean
        {
            get
            {
                return IsKorean
                    && Main.Settings != null
                    && Main.Settings.TranslateAttributeDisplayToKorean;
            }
        }

        internal static PlanetGaugeLanguage DetectGameLanguage()
        {
            return RDString.language == SystemLanguage.Korean
                ? PlanetGaugeLanguage.Korean
                : PlanetGaugeLanguage.English;
        }

        internal static void NotifyLanguageChanged()
        {
            IncrementRevision();

            PlanetGaugeLevelEventRegistry.RefreshLocalization();
            PlanetGaugeSkinLevelEventRegistry.RefreshLocalization();
            GaugeSkinManager.OnLanguageChanged();
        }

        internal static void NotifyAttributeDisplayLanguageChanged()
        {
            IncrementRevision();
        }

        private static void IncrementRevision()
        {
            unchecked
            {
                revision++;
            }
        }

        internal static string Format(string format, params object[] arguments)
        {
            try
            {
                return string.Format(CultureInfo.InvariantCulture, format, arguments);
            }
            catch (FormatException)
            {
                return format;
            }
        }

        internal static string MainGaugeSize { get { return IsKorean ? KoreanStrings.MainGaugeSize : EnglishStrings.MainGaugeSize; } }
        internal static string Scale { get { return IsKorean ? KoreanStrings.Scale : EnglishStrings.Scale; } }
        internal static string Width { get { return IsKorean ? KoreanStrings.Width : EnglishStrings.Width; } }
        internal static string ResetSize { get { return IsKorean ? KoreanStrings.ResetSize : EnglishStrings.ResetSize; } }
        internal static string CustomGaugeSkin { get { return IsKorean ? KoreanStrings.CustomGaugeSkin : EnglishStrings.CustomGaugeSkin; } }
        internal static string HealthPng { get { return IsKorean ? KoreanStrings.HealthPng : EnglishStrings.HealthPng; } }
        internal static string SelectHealthPng { get { return IsKorean ? KoreanStrings.SelectHealthPng : EnglishStrings.SelectHealthPng; } }
        internal static string ClearHealthPng { get { return IsKorean ? KoreanStrings.ClearHealthPng : EnglishStrings.ClearHealthPng; } }
        internal static string SelectHealthPngDialog { get { return IsKorean ? KoreanStrings.SelectHealthPngDialog : EnglishStrings.SelectHealthPngDialog; } }
        internal static string FramePngOptional { get { return IsKorean ? KoreanStrings.FramePngOptional : EnglishStrings.FramePngOptional; } }
        internal static string SelectFramePng { get { return IsKorean ? KoreanStrings.SelectFramePng : EnglishStrings.SelectFramePng; } }
        internal static string ClearFramePng { get { return IsKorean ? KoreanStrings.ClearFramePng : EnglishStrings.ClearFramePng; } }
        internal static string SelectFramePngDialog { get { return IsKorean ? KoreanStrings.SelectFramePngDialog : EnglishStrings.SelectFramePngDialog; } }
        internal static string FillDirection { get { return IsKorean ? KoreanStrings.FillDirection : EnglishStrings.FillDirection; } }
        internal static string HorizontalFill { get { return IsKorean ? KoreanStrings.HorizontalFill : EnglishStrings.HorizontalFill; } }
        internal static string VerticalFill { get { return IsKorean ? KoreanStrings.VerticalFill : EnglishStrings.VerticalFill; } }
        internal static string FrameOffset { get { return IsKorean ? KoreanStrings.FrameOffset : EnglishStrings.FrameOffset; } }
        internal static string FrameX { get { return IsKorean ? KoreanStrings.FrameX : EnglishStrings.FrameX; } }
        internal static string FrameY { get { return IsKorean ? KoreanStrings.FrameY : EnglishStrings.FrameY; } }
        internal static string ResetFrameOffset { get { return IsKorean ? KoreanStrings.ResetFrameOffset : EnglishStrings.ResetFrameOffset; } }
        internal static string ApplyCustomSkin { get { return IsKorean ? KoreanStrings.ApplyCustomSkin : EnglishStrings.ApplyCustomSkin; } }
        internal static string UseDefaultSkin { get { return IsKorean ? KoreanStrings.UseDefaultSkin : EnglishStrings.UseDefaultSkin; } }
        internal static string Warning { get { return IsKorean ? KoreanStrings.Warning : EnglishStrings.Warning; } }
        internal static string MainGaugePosition { get { return IsKorean ? KoreanStrings.MainGaugePosition : EnglishStrings.MainGaugePosition; } }
        internal static string ResetPosition { get { return IsKorean ? KoreanStrings.ResetPosition : EnglishStrings.ResetPosition; } }
        internal static string GaugeValueText { get { return IsKorean ? KoreanStrings.GaugeValueText : EnglishStrings.GaugeValueText; } }
        internal static string TextSize { get { return IsKorean ? KoreanStrings.TextSize : EnglishStrings.TextSize; } }
        internal static string IndependentPosition { get { return IsKorean ? KoreanStrings.IndependentPosition : EnglishStrings.IndependentPosition; } }
        internal static string TextX { get { return IsKorean ? KoreanStrings.TextX : EnglishStrings.TextX; } }
        internal static string TextY { get { return IsKorean ? KoreanStrings.TextY : EnglishStrings.TextY; } }
        internal static string ResetTextPosition { get { return IsKorean ? KoreanStrings.ResetTextPosition : EnglishStrings.ResetTextPosition; } }
        internal static string AttributeText { get { return IsKorean ? KoreanStrings.AttributeText : EnglishStrings.AttributeText; } }
        internal static string AttributeSize { get { return IsKorean ? KoreanStrings.AttributeSize : EnglishStrings.AttributeSize; } }
        internal static string AttributeX { get { return IsKorean ? KoreanStrings.AttributeX : EnglishStrings.AttributeX; } }
        internal static string AttributeY { get { return IsKorean ? KoreanStrings.AttributeY : EnglishStrings.AttributeY; } }
        internal static string ResetAttributePosition { get { return IsKorean ? KoreanStrings.ResetAttributePosition : EnglishStrings.ResetAttributePosition; } }
        internal static string RateToken { get { return IsKorean ? KoreanStrings.RateToken : EnglishStrings.RateToken; } }
        internal static string RateSize { get { return IsKorean ? KoreanStrings.RateSize : EnglishStrings.RateSize; } }
        internal static string RateX { get { return IsKorean ? KoreanStrings.RateX : EnglishStrings.RateX; } }
        internal static string RateY { get { return IsKorean ? KoreanStrings.RateY : EnglishStrings.RateY; } }
        internal static string ResetRatePosition { get { return IsKorean ? KoreanStrings.ResetRatePosition : EnglishStrings.ResetRatePosition; } }
        internal static string Advanced { get { return IsKorean ? KoreanStrings.Advanced : EnglishStrings.Advanced; } }
        internal static string ShowDecimalHealth { get { return IsKorean ? KoreanStrings.ShowDecimalHealth : EnglishStrings.ShowDecimalHealth; } }
        internal static string MainGaugeColor { get { return IsKorean ? KoreanStrings.MainGaugeColor : EnglishStrings.MainGaugeColor; } }
        internal static string ResetColor { get { return IsKorean ? KoreanStrings.ResetColor : EnglishStrings.ResetColor; } }
        internal static string DebugShortcut { get { return IsKorean ? KoreanStrings.DebugShortcut : EnglishStrings.DebugShortcut; } }
        internal static string SetKey1 { get { return IsKorean ? KoreanStrings.SetKey1 : EnglishStrings.SetKey1; } }
        internal static string SetKey2 { get { return IsKorean ? KoreanStrings.SetKey2 : EnglishStrings.SetKey2; } }
        internal static string PressKey1 { get { return IsKorean ? KoreanStrings.PressKey1 : EnglishStrings.PressKey1; } }
        internal static string PressKey2 { get { return IsKorean ? KoreanStrings.PressKey2 : EnglishStrings.PressKey2; } }
        internal static string ResetShortcut { get { return IsKorean ? KoreanStrings.ResetShortcut : EnglishStrings.ResetShortcut; } }
        internal static string KeysMustDiffer { get { return IsKorean ? KoreanStrings.KeysMustDiffer : EnglishStrings.KeysMustDiffer; } }
        internal static string None { get { return IsKorean ? KoreanStrings.None : EnglishStrings.None; } }
        internal static string PixelUnit { get { return IsKorean ? KoreanStrings.PixelUnit : EnglishStrings.PixelUnit; } }
        internal static string TranslateAttributeDisplay { get { return IsKorean ? KoreanStrings.TranslateAttributeDisplay : EnglishStrings.TranslateAttributeDisplay; } }
        internal static string Blindfolded { get { return IsAttributeDisplayKorean ? KoreanStrings.Blindfolded : EnglishStrings.Blindfolded; } }
        internal static string IncreaseDisabled { get { return IsAttributeDisplayKorean ? KoreanStrings.IncreaseDisabled : EnglishStrings.IncreaseDisabled; } }
        internal static string RateReduced { get { return IsAttributeDisplayKorean ? KoreanStrings.RateReduced : EnglishStrings.RateReduced; } }
        internal static string RateAmplified { get { return IsAttributeDisplayKorean ? KoreanStrings.RateAmplified : EnglishStrings.RateAmplified; } }
        internal static string Increase { get { return IsAttributeDisplayKorean ? KoreanStrings.Increase : EnglishStrings.Increase; } }
        internal static string Decrease { get { return IsAttributeDisplayKorean ? KoreanStrings.Decrease : EnglishStrings.Decrease; } }
        internal static string NoFailDisabled { get { return IsAttributeDisplayKorean ? KoreanStrings.NoFailDisabled : EnglishStrings.NoFailDisabled; } }
        internal static string IncreaseLimited { get { return IsAttributeDisplayKorean ? KoreanStrings.IncreaseLimited : EnglishStrings.IncreaseLimited; } }
        internal static string ReducedEffect { get { return IsAttributeDisplayKorean ? KoreanStrings.ReducedEffect : EnglishStrings.ReducedEffect; } }
        internal static string AmplifiedEffect { get { return IsAttributeDisplayKorean ? KoreanStrings.AmplifiedEffect : EnglishStrings.AmplifiedEffect; } }
        internal static string GaugeEnabled { get { return IsKorean ? KoreanStrings.GaugeEnabled : EnglishStrings.GaugeEnabled; } }
        internal static string GaugeDisabled { get { return IsKorean ? KoreanStrings.GaugeDisabled : EnglishStrings.GaugeDisabled; } }
        internal static string PngImage { get { return IsKorean ? KoreanStrings.PngImage : EnglishStrings.PngImage; } }
        internal static string FileSelectionFailed { get { return IsKorean ? KoreanStrings.FileSelectionFailed : EnglishStrings.FileSelectionFailed; } }
        internal static string SelectHealthPngFirst { get { return IsKorean ? KoreanStrings.SelectHealthPngFirst : EnglishStrings.SelectHealthPngFirst; } }
        internal static string SkinLoadsWhenEnabled { get { return IsKorean ? KoreanStrings.SkinLoadsWhenEnabled : EnglishStrings.SkinLoadsWhenEnabled; } }
        internal static string CustomSkinApplied { get { return IsKorean ? KoreanStrings.CustomSkinApplied : EnglishStrings.CustomSkinApplied; } }
        internal static string SkinApplyFailed { get { return IsKorean ? KoreanStrings.SkinApplyFailed : EnglishStrings.SkinApplyFailed; } }
        internal static string UsingDefaultSkin { get { return IsKorean ? KoreanStrings.UsingDefaultSkin : EnglishStrings.UsingDefaultSkin; } }
        internal static string HealthPngAsset { get { return IsKorean ? KoreanStrings.HealthPngAsset : EnglishStrings.HealthPngAsset; } }
        internal static string FramePngAsset { get { return IsKorean ? KoreanStrings.FramePngAsset : EnglishStrings.FramePngAsset; } }
        internal static string FileNotFound { get { return IsKorean ? KoreanStrings.FileNotFound : EnglishStrings.FileNotFound; } }
        internal static string FileMustBePng { get { return IsKorean ? KoreanStrings.FileMustBePng : EnglishStrings.FileMustBePng; } }
        internal static string ImageVeryLarge { get { return IsKorean ? KoreanStrings.ImageVeryLarge : EnglishStrings.ImageVeryLarge; } }
        internal static string TextureConversionFailed { get { return IsKorean ? KoreanStrings.TextureConversionFailed : EnglishStrings.TextureConversionFailed; } }
        internal static string ImageFullyTransparent { get { return IsKorean ? KoreanStrings.ImageFullyTransparent : EnglishStrings.ImageFullyTransparent; } }
        internal static string LoadImageMethodMissing { get { return IsKorean ? KoreanStrings.LoadImageMethodMissing : EnglishStrings.LoadImageMethodMissing; } }
        internal static string PngHeaderTooShort { get { return IsKorean ? KoreanStrings.PngHeaderTooShort : EnglishStrings.PngHeaderTooShort; } }
        internal static string PngSignatureInvalid { get { return IsKorean ? KoreanStrings.PngSignatureInvalid : EnglishStrings.PngSignatureInvalid; } }
        internal static string PngIhdrMissing { get { return IsKorean ? KoreanStrings.PngIhdrMissing : EnglishStrings.PngIhdrMissing; } }
        internal static string PngDimensionsInvalid { get { return IsKorean ? KoreanStrings.PngDimensionsInvalid : EnglishStrings.PngDimensionsInvalid; } }
        internal static string MultiplierReuseNote { get { return IsKorean ? KoreanStrings.MultiplierReuseNote : EnglishStrings.MultiplierReuseNote; } }
        internal static string RecoveryAmountNote { get { return IsKorean ? KoreanStrings.RecoveryAmountNote : EnglishStrings.RecoveryAmountNote; } }
        internal static string GaugeEventName { get { return IsKorean ? KoreanStrings.GaugeEventName : EnglishStrings.GaugeEventName; } }
        internal static string SkinEventName { get { return IsKorean ? KoreanStrings.SkinEventName : EnglishStrings.SkinEventName; } }
        internal static string Horizontal { get { return IsKorean ? KoreanStrings.Horizontal : EnglishStrings.Horizontal; } }
        internal static string Vertical { get { return IsKorean ? KoreanStrings.Vertical : EnglishStrings.Vertical; } }
        internal static string Normal { get { return IsKorean ? KoreanStrings.Normal : EnglishStrings.Normal; } }
        internal static string BlockRecovery { get { return IsKorean ? KoreanStrings.BlockRecovery : EnglishStrings.BlockRecovery; } }
        internal static string AmplifyDecrease { get { return IsKorean ? KoreanStrings.AmplifyDecrease : EnglishStrings.AmplifyDecrease; } }
        internal static string AmplifyIncrease { get { return IsKorean ? KoreanStrings.AmplifyIncrease : EnglishStrings.AmplifyIncrease; } }
        internal static string AmplifyBoth { get { return IsKorean ? KoreanStrings.AmplifyBoth : EnglishStrings.AmplifyBoth; } }
        internal static string Blindfold { get { return IsKorean ? KoreanStrings.Blindfold : EnglishStrings.Blindfold; } }
        internal static string ForceRecovery { get { return IsKorean ? KoreanStrings.ForceRecovery : EnglishStrings.ForceRecovery; } }
        internal static string HideGaugeHud { get { return IsKorean ? KoreanStrings.HideGaugeHud : EnglishStrings.HideGaugeHud; } }
        internal static string AttributeModeLabel { get { return IsKorean ? KoreanStrings.AttributeModeLabel : EnglishStrings.AttributeModeLabel; } }
        internal static string AttributeEnabledLabel { get { return IsKorean ? KoreanStrings.AttributeEnabledLabel : EnglishStrings.AttributeEnabledLabel; } }
        internal static string DisableOtherAttributesLabel { get { return IsKorean ? KoreanStrings.DisableOtherAttributesLabel : EnglishStrings.DisableOtherAttributesLabel; } }
        internal static string HideGaugeBarLabel { get { return IsKorean ? KoreanStrings.HideGaugeBarLabel : EnglishStrings.HideGaugeBarLabel; } }
        internal static string HideGaugeValueLabel { get { return IsKorean ? KoreanStrings.HideGaugeValueLabel : EnglishStrings.HideGaugeValueLabel; } }
        internal static string HideAttributeTextLabel { get { return IsKorean ? KoreanStrings.HideAttributeTextLabel : EnglishStrings.HideAttributeTextLabel; } }
        internal static string HideRateTokenLabel { get { return IsKorean ? KoreanStrings.HideRateTokenLabel : EnglishStrings.HideRateTokenLabel; } }
        internal static string HideForceRecoveryVisualsLabel { get { return IsKorean ? KoreanStrings.HideForceRecoveryVisualsLabel : EnglishStrings.HideForceRecoveryVisualsLabel; } }
        internal static string MultiplierPercentLabel { get { return IsKorean ? KoreanStrings.MultiplierPercentLabel : EnglishStrings.MultiplierPercentLabel; } }
        internal static string RecoveryAmountLabel { get { return IsKorean ? KoreanStrings.RecoveryAmountLabel : EnglishStrings.RecoveryAmountLabel; } }
        internal static string WarningOffsetLabel { get { return IsKorean ? KoreanStrings.WarningOffsetLabel : EnglishStrings.WarningOffsetLabel; } }
        internal static string WarningPulseLabel { get { return IsKorean ? KoreanStrings.WarningPulseLabel : EnglishStrings.WarningPulseLabel; } }
        internal static string FailureProtectionLabel { get { return IsKorean ? KoreanStrings.FailureProtectionLabel : EnglishStrings.FailureProtectionLabel; } }
        internal static string RecoveryCapEnabledLabel { get { return IsKorean ? KoreanStrings.RecoveryCapEnabledLabel : EnglishStrings.RecoveryCapEnabledLabel; } }
        internal static string RecoveryCapLabel { get { return IsKorean ? KoreanStrings.RecoveryCapLabel : EnglishStrings.RecoveryCapLabel; } }
        internal static string ForceRecoveryCapLabel { get { return IsKorean ? KoreanStrings.ForceRecoveryCapLabel : EnglishStrings.ForceRecoveryCapLabel; } }
        internal static string AutoTileRecoveryLabel { get { return IsKorean ? KoreanStrings.AutoTileRecoveryLabel : EnglishStrings.AutoTileRecoveryLabel; } }
        internal static string TargetTagLabel { get { return IsKorean ? KoreanStrings.TargetTagLabel : EnglishStrings.TargetTagLabel; } }
        internal static string SkinEnabledLabel { get { return IsKorean ? KoreanStrings.SkinEnabledLabel : EnglishStrings.SkinEnabledLabel; } }
        internal static string GaugeTypeLabel { get { return IsKorean ? KoreanStrings.GaugeTypeLabel : EnglishStrings.GaugeTypeLabel; } }
        internal static string DebugGaugeLine { get { return IsKorean ? KoreanStrings.DebugGaugeLine : EnglishStrings.DebugGaugeLine; } }
        internal static string DebugRatesLine { get { return IsKorean ? KoreanStrings.DebugRatesLine : EnglishStrings.DebugRatesLine; } }
        internal static string DebugAttributesLine { get { return IsKorean ? KoreanStrings.DebugAttributesLine : EnglishStrings.DebugAttributesLine; } }
        internal static string DebugFailureProtectionLine { get { return IsKorean ? KoreanStrings.DebugFailureProtectionLine : EnglishStrings.DebugFailureProtectionLine; } }
        internal static string DebugRecoveryCapLine { get { return IsKorean ? KoreanStrings.DebugRecoveryCapLine : EnglishStrings.DebugRecoveryCapLine; } }
        internal static string DebugPlaybackLine { get { return IsKorean ? KoreanStrings.DebugPlaybackLine : EnglishStrings.DebugPlaybackLine; } }
        internal static string DebugRuntimeLine { get { return IsKorean ? KoreanStrings.DebugRuntimeLine : EnglishStrings.DebugRuntimeLine; } }
        internal static string DebugActiveLine { get { return IsKorean ? KoreanStrings.DebugActiveLine : EnglishStrings.DebugActiveLine; } }
        internal static string DebugHudHiddenLine { get { return IsKorean ? KoreanStrings.DebugHudHiddenLine : EnglishStrings.DebugHudHiddenLine; } }
        internal static string DebugHudAlphaLine { get { return IsKorean ? KoreanStrings.DebugHudAlphaLine : EnglishStrings.DebugHudAlphaLine; } }
        internal static string DebugSkinLine { get { return IsKorean ? KoreanStrings.DebugSkinLine : EnglishStrings.DebugSkinLine; } }
        internal static string DebugSkinProgressLine { get { return IsKorean ? KoreanStrings.DebugSkinProgressLine : EnglishStrings.DebugSkinProgressLine; } }
        internal static string DebugDecorationSkinLine { get { return IsKorean ? KoreanStrings.DebugDecorationSkinLine : EnglishStrings.DebugDecorationSkinLine; } }
        internal static string DebugTotals { get { return IsKorean ? KoreanStrings.DebugTotals : EnglishStrings.DebugTotals; } }
        internal static string DebugAuto { get { return IsKorean ? KoreanStrings.DebugAuto : EnglishStrings.DebugAuto; } }
        internal static string DebugDefaultRate { get { return IsKorean ? KoreanStrings.DebugDefaultRate : EnglishStrings.DebugDefaultRate; } }
        internal static string DebugActiveBlockRecovery { get { return IsKorean ? KoreanStrings.DebugActiveBlockRecovery : EnglishStrings.DebugActiveBlockRecovery; } }
        internal static string DebugActiveRecoveryRate { get { return IsKorean ? KoreanStrings.DebugActiveRecoveryRate : EnglishStrings.DebugActiveRecoveryRate; } }
        internal static string DebugActiveDamageRate { get { return IsKorean ? KoreanStrings.DebugActiveDamageRate : EnglishStrings.DebugActiveDamageRate; } }
        internal static string DebugActiveBlindfold { get { return IsKorean ? KoreanStrings.DebugActiveBlindfold : EnglishStrings.DebugActiveBlindfold; } }
        internal static string DebugActiveNoFailDisabled { get { return IsKorean ? KoreanStrings.DebugActiveNoFailDisabled : EnglishStrings.DebugActiveNoFailDisabled; } }
        internal static string DebugActiveRecoveryCap { get { return IsKorean ? KoreanStrings.DebugActiveRecoveryCap : EnglishStrings.DebugActiveRecoveryCap; } }
        internal static string DebugActiveAutoTileRecovery { get { return IsKorean ? KoreanStrings.DebugActiveAutoTileRecovery : EnglishStrings.DebugActiveAutoTileRecovery; } }
        internal static string DebugActiveHideGaugeHud { get { return IsKorean ? KoreanStrings.DebugActiveHideGaugeHud : EnglishStrings.DebugActiveHideGaugeHud; } }
        internal static string DefaultSkinDescription { get { return IsKorean ? KoreanStrings.DefaultSkinDescription : EnglishStrings.DefaultSkinDescription; } }
        internal static string CustomSkinDescription { get { return IsKorean ? KoreanStrings.CustomSkinDescription : EnglishStrings.CustomSkinDescription; } }
        internal static string NoFrameDescription { get { return IsKorean ? KoreanStrings.NoFrameDescription : EnglishStrings.NoFrameDescription; } }
        internal static string FrameDescription { get { return IsKorean ? KoreanStrings.FrameDescription : EnglishStrings.FrameDescription; } }
        internal static string Pending { get { return IsKorean ? KoreanStrings.Pending : EnglishStrings.Pending; } }
    }
}
