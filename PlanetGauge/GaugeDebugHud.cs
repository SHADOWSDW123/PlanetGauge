using System;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlanetGauge
{
    internal sealed class GaugeDebugHud : IDisposable
    {
        private const float RefreshInterval = 0.1f;
        private GameObject rootObject;
        private TextMeshProUGUI text;
        private float refreshRemaining;

        internal bool Visible { get; private set; }

        internal void Toggle()
        {
            Visible = !Visible;
            refreshRemaining = 0f;
        }

        internal void ResetVisibility()
        {
            Visible = false;
            refreshRemaining = 0f;
            SetActive(false);
        }

        internal void Update()
        {
            bool show = Visible && GaugeRuntime.IsGameplayContext(true);
            if (!show)
            {
                SetActive(false);
                return;
            }

            EnsureCreated();
            SetActive(true);
            refreshRemaining -= Time.unscaledDeltaTime;
            if (refreshRemaining > 0f || text == null)
            {
                return;
            }

            refreshRemaining = RefreshInterval;
            text.text = BuildText();
        }

        public void Dispose()
        {
            if (rootObject != null)
            {
                UnityEngine.Object.Destroy(rootObject);
            }
            rootObject = null;
            text = null;
        }

        private void EnsureCreated()
        {
            if (rootObject != null) return;

            rootObject = new GameObject(
                "PlanetGauge.DebugHud",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            UnityEngine.Object.DontDestroyOnLoad(rootObject);

            Canvas canvas = rootObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            rootObject.GetComponent<GraphicRaycaster>().enabled = false;

            GameObject textObject = new GameObject(
                "DebugText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI),
                typeof(Outline));
            textObject.transform.SetParent(rootObject.transform, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(12f, -12f);
            rect.sizeDelta = new Vector2(720f, 900f);

            text = textObject.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.TopLeft;
            text.fontSize = 18f;
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;

            Outline outline = textObject.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;
        }

        private static string BuildText()
        {
            PlanetGaugeEventSettings settings = GaugeRuntime.EventSettings;
            scrController controller = scrController.instance;
            StringBuilder builder = new StringBuilder(768);
            builder.Append(Main.Settings.DebugKey1)
                .Append("+")
                .Append(Main.Settings.DebugKey2)
                .AppendLine();
            builder.AppendLine(LocalizedStrings.Format(
                LocalizedStrings.DebugGaugeLine,
                Format(GaugeRuntime.Current),
                Format(GaugeRuntime.RecoveryMaximum),
                Format(GaugeRuntime.MaximumGauge)));
            builder.AppendLine(LocalizedStrings.Format(
                LocalizedStrings.DebugRatesLine,
                FormatChannel(settings.RecoveryRate),
                FormatChannel(settings.DamageRate)));
            builder.AppendLine(LocalizedStrings.Format(
                LocalizedStrings.DebugAttributesLine,
                settings.RecoveryBlocked,
                settings.BlindfoldEnabled,
                GaugeRuntime.IsBlindfoldRevealed));
            builder.AppendLine(LocalizedStrings.Format(
                LocalizedStrings.DebugFailureProtectionLine,
                settings.FailureProtection));
            builder.AppendLine(LocalizedStrings.Format(
                LocalizedStrings.DebugRecoveryCapLine,
                settings.RecoveryCapEnabled,
                Format(settings.RecoveryCapPercent),
                settings.AutoTileRecovery));
            builder.AppendLine(LocalizedStrings.Format(
                LocalizedStrings.DebugPlaybackLine,
                GaugeRuntime.IsAutoPlay(),
                controller != null && controller.paused));
            builder.AppendLine(LocalizedStrings.Format(
                LocalizedStrings.DebugRuntimeLine,
                GaugeRuntime.IsFrozen,
                GaugeRuntime.FailureRecoveryDepth,
                GaugeRuntime.HasPendingDieCharge,
                GaugeRuntime.IsForcingDeath));
            builder.AppendLine(LocalizedStrings.Format(
                LocalizedStrings.DebugActiveLine,
                BuildActiveList(settings)));
            builder.AppendLine(LocalizedStrings.Format(
                LocalizedStrings.DebugHudHiddenLine,
                GaugeHudVisibilityTransitions.GaugeBarHidden,
                GaugeHudVisibilityTransitions.GaugeValueHidden,
                GaugeHudVisibilityTransitions.AttributeTextHidden,
                GaugeHudVisibilityTransitions.RateTokenHidden,
                GaugeHudVisibilityTransitions.ForceRecoveryVisualsHidden));
            builder.AppendLine(LocalizedStrings.Format(
                LocalizedStrings.DebugHudAlphaLine,
                Format(GaugeHudVisibilityTransitions.GaugeBarAlpha),
                Format(GaugeHudVisibilityTransitions.GaugeValueAlpha),
                Format(GaugeHudVisibilityTransitions.AttributeTextAlpha),
                Format(GaugeHudVisibilityTransitions.RateTokenAlpha),
                Format(GaugeHudVisibilityTransitions.ForceRecoveryVisualsAlpha)));
            builder.AppendLine(LocalizedStrings.Format(
                LocalizedStrings.DebugSkinLine,
                GaugeSkinManager.DescribeCurrent()));
            if (GaugeSkinManager.Current != null)
            {
                float skinProgress = GaugeRuntime.RecoveryMaximum <= 0f
                    ? 0f
                    : Mathf.Clamp01(GaugeRuntime.Current / GaugeRuntime.RecoveryMaximum);
                builder.AppendLine(LocalizedStrings.Format(
                    LocalizedStrings.DebugSkinProgressLine,
                    Format(skinProgress),
                    Format(Main.Settings.FrameSkinOffsetX),
                    Format(Main.Settings.FrameSkinOffsetY)));
            }
            builder.AppendLine(LocalizedStrings.Format(
                LocalizedStrings.DebugDecorationSkinLine,
                PlanetGaugeDecorationSkinRuntime.DescribeCurrent(),
                PlanetGaugeDecorationSkinRuntime.ActiveTagCount,
                PlanetGaugeDecorationSkinRuntime.BoundDecorationCount,
                PlanetGaugeDecorationSkinRuntime.LegacyRenderDecorationCount,
                PlanetGaugeDecorationSkinRuntime.DescribeAlphaRange(),
                Format(PlanetGaugeDecorationSkinRuntime.Progress)));
            builder.AppendLine(LocalizedStrings.DebugTotals);
            AppendTotal(builder, "TooEarly", HitMargin.TooEarly);
            AppendTotal(builder, "VeryEarly", HitMargin.VeryEarly);
            AppendTotal(builder, "EarlyPerfect", HitMargin.EarlyPerfect);
            AppendTotal(builder, "Perfect", HitMargin.Perfect);
            AppendTotal(builder, "LatePerfect", HitMargin.LatePerfect);
            AppendTotal(builder, "VeryLate", HitMargin.VeryLate);
            AppendTotal(builder, "FailMiss", HitMargin.FailMiss);
            AppendTotal(builder, "FailOverload", HitMargin.FailOverload);
            builder.Append(LocalizedStrings.DebugAuto)
                .Append(": ")
                .Append(FormatSigned(GaugeRuntime.AutoTotal));
            return builder.ToString();
        }

        private static string BuildActiveList(PlanetGaugeEventSettings settings)
        {
            StringBuilder builder = new StringBuilder();
            if (settings.RecoveryBlocked) AppendActive(builder, LocalizedStrings.DebugActiveBlockRecovery);
            if (settings.RecoveryRate.Enabled) AppendActive(builder, LocalizedStrings.DebugActiveRecoveryRate);
            if (settings.DamageRate.Enabled) AppendActive(builder, LocalizedStrings.DebugActiveDamageRate);
            if (settings.BlindfoldEnabled) AppendActive(builder, LocalizedStrings.DebugActiveBlindfold);
            if (!settings.FailureProtection) AppendActive(builder, LocalizedStrings.DebugActiveNoFailDisabled);
            if (settings.RecoveryCapEnabled) AppendActive(builder, LocalizedStrings.DebugActiveRecoveryCap);
            if (settings.AutoTileRecovery) AppendActive(builder, LocalizedStrings.DebugActiveAutoTileRecovery);
            if (GaugeHudVisibilityTransitions.GaugeBarHidden
                || GaugeHudVisibilityTransitions.GaugeValueHidden
                || GaugeHudVisibilityTransitions.AttributeTextHidden
                || GaugeHudVisibilityTransitions.RateTokenHidden
                || GaugeHudVisibilityTransitions.ForceRecoveryVisualsHidden)
            {
                AppendActive(builder, LocalizedStrings.DebugActiveHideGaugeHud);
            }
            if (builder.Length == 0) return LocalizedStrings.None;
            builder.Length -= 2;
            return builder.ToString();
        }

        private static string FormatChannel(PlanetGaugeRateChannel channel)
        {
            return channel.Enabled
                ? channel.Source + " " + Format(channel.Percent) + "%"
                : LocalizedStrings.DebugDefaultRate;
        }

        private static void AppendActive(StringBuilder builder, string label)
        {
            builder.Append(label).Append(", ");
        }

        private static void AppendTotal(StringBuilder builder, string label, HitMargin judgement)
        {
            builder.Append(label).Append(": ")
                .Append(FormatSigned(GaugeRuntime.GetJudgementTotal(judgement))).AppendLine();
        }

        private static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatSigned(float value)
        {
            return value.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture);
        }

        private void SetActive(bool active)
        {
            if (rootObject != null && rootObject.activeSelf != active)
            {
                rootObject.SetActive(active);
            }
        }
    }
}
