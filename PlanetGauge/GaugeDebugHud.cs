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
            builder.Append("Gauge: ").Append(Format(GaugeRuntime.Current))
                .Append(" / ").Append(Format(GaugeRuntime.RecoveryMaximum))
                .Append("  BaseMax: ").Append(Format(GaugeRuntime.MaximumGauge)).AppendLine();
            builder.Append("Recovery: ").Append(FormatChannel(settings.RecoveryRate))
                .Append("  Damage: ").Append(FormatChannel(settings.DamageRate)).AppendLine();
            builder.Append("BlockRecovery: ").Append(settings.RecoveryBlocked)
                .Append("  Blindfold: ").Append(settings.BlindfoldEnabled)
                .Append("  Revealed: ").Append(GaugeRuntime.IsBlindfoldRevealed).AppendLine();
            builder.Append("FailureProtection: ").Append(settings.FailureProtection).AppendLine();
            builder.Append("RecoveryCap: ").Append(settings.RecoveryCapEnabled)
                .Append(" @ ").Append(Format(settings.RecoveryCapPercent))
                .Append("  AutoTileRecovery: ").Append(settings.AutoTileRecovery).AppendLine();
            builder.Append("ActualAutoPlay: ").Append(GaugeRuntime.IsAutoPlay())
                .Append("  Paused: ").Append(controller != null && controller.paused).AppendLine();
            builder.Append("Frozen: ").Append(GaugeRuntime.IsFrozen)
                .Append("  RecoveryDepth: ").Append(GaugeRuntime.FailureRecoveryDepth)
                .Append("  PendingDie: ").Append(GaugeRuntime.HasPendingDieCharge)
                .Append("  ForcingDeath: ").Append(GaugeRuntime.IsForcingDeath).AppendLine();
            builder.Append("Active: ").Append(BuildActiveList(settings)).AppendLine();
            builder.Append("HUD TargetHidden: Bar=").Append(GaugeHudVisibilityTransitions.GaugeBarHidden)
                .Append(" Value=").Append(GaugeHudVisibilityTransitions.GaugeValueHidden)
                .Append(" Attribute=").Append(GaugeHudVisibilityTransitions.AttributeTextHidden)
                .Append(" Rate=").Append(GaugeHudVisibilityTransitions.RateTokenHidden)
                .Append(" Force=").Append(GaugeHudVisibilityTransitions.ForceRecoveryVisualsHidden)
                .AppendLine();
            builder.Append("HUD Alpha: Bar=").Append(Format(GaugeHudVisibilityTransitions.GaugeBarAlpha))
                .Append(" Value=").Append(Format(GaugeHudVisibilityTransitions.GaugeValueAlpha))
                .Append(" Attribute=").Append(Format(GaugeHudVisibilityTransitions.AttributeTextAlpha))
                .Append(" Rate=").Append(Format(GaugeHudVisibilityTransitions.RateTokenAlpha))
                .Append(" Force=").Append(Format(GaugeHudVisibilityTransitions.ForceRecoveryVisualsAlpha))
                .AppendLine();
            builder.Append("Skin: ").Append(GaugeSkinManager.DescribeCurrent()).AppendLine();
            if (GaugeSkinManager.Current != null)
            {
                float skinProgress = GaugeRuntime.RecoveryMaximum <= 0f
                    ? 0f
                    : Mathf.Clamp01(GaugeRuntime.Current / GaugeRuntime.RecoveryMaximum);
                builder.Append("SkinProgress: ").Append(Format(skinProgress))
                    .Append("  FrameOffset: ")
                    .Append(Format(Main.Settings.FrameSkinOffsetX))
                    .Append(",")
                    .Append(Format(Main.Settings.FrameSkinOffsetY))
                    .Append("  ForceOverlay: Disabled")
                    .AppendLine();
            }
            builder.Append("DecorationSkin: ")
                .Append(PlanetGaugeDecorationSkinRuntime.DescribeCurrent())
                .Append("  Tags=").Append(PlanetGaugeDecorationSkinRuntime.ActiveTagCount)
                .Append("  Images=").Append(PlanetGaugeDecorationSkinRuntime.BoundDecorationCount)
                .Append("  Legacy=").Append(PlanetGaugeDecorationSkinRuntime.LegacyRenderDecorationCount)
                .Append("  Alpha=").Append(PlanetGaugeDecorationSkinRuntime.DescribeAlphaRange())
                .Append("  Progress=").Append(Format(PlanetGaugeDecorationSkinRuntime.Progress))
                .AppendLine();
            builder.AppendLine("Totals (applied)");
            AppendTotal(builder, "TooEarly", HitMargin.TooEarly);
            AppendTotal(builder, "VeryEarly", HitMargin.VeryEarly);
            AppendTotal(builder, "EarlyPerfect", HitMargin.EarlyPerfect);
            AppendTotal(builder, "Perfect", HitMargin.Perfect);
            AppendTotal(builder, "LatePerfect", HitMargin.LatePerfect);
            AppendTotal(builder, "VeryLate", HitMargin.VeryLate);
            AppendTotal(builder, "FailMiss", HitMargin.FailMiss);
            AppendTotal(builder, "FailOverload", HitMargin.FailOverload);
            builder.Append("Auto: ").Append(FormatSigned(GaugeRuntime.AutoTotal));
            return builder.ToString();
        }

        private static string BuildActiveList(PlanetGaugeEventSettings settings)
        {
            StringBuilder builder = new StringBuilder();
            if (settings.RecoveryBlocked) builder.Append("BlockRecovery, ");
            if (settings.RecoveryRate.Enabled) builder.Append("RecoveryRate, ");
            if (settings.DamageRate.Enabled) builder.Append("DamageRate, ");
            if (settings.BlindfoldEnabled) builder.Append("Blindfold, ");
            if (!settings.FailureProtection) builder.Append("NoFailDisabled, ");
            if (settings.RecoveryCapEnabled) builder.Append("RecoveryCap, ");
            if (settings.AutoTileRecovery) builder.Append("AutoTileRecovery, ");
            if (GaugeHudVisibilityTransitions.GaugeBarHidden
                || GaugeHudVisibilityTransitions.GaugeValueHidden
                || GaugeHudVisibilityTransitions.AttributeTextHidden
                || GaugeHudVisibilityTransitions.RateTokenHidden
                || GaugeHudVisibilityTransitions.ForceRecoveryVisualsHidden)
            {
                builder.Append("HideGaugeHud, ");
            }
            if (builder.Length == 0) return "None";
            builder.Length -= 2;
            return builder.ToString();
        }

        private static string FormatChannel(PlanetGaugeRateChannel channel)
        {
            return channel.Enabled
                ? channel.Source + " " + Format(channel.Percent) + "%"
                : "Default 100%";
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
