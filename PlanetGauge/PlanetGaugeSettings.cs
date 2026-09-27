using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityModManagerNet;

namespace PlanetGauge
{
    /// <summary>
    /// Unity Mod Manager가 직렬화하는 사용자 설정과 설정 GUI를 소유한다.
    /// public 필드 이름은 저장 파일의 키이므로 이름 변경 시 기존 설정 마이그레이션이 필요하다.
    /// </summary>
    public sealed class PlanetGaugeSettings : UnityModManager.ModSettings
    {
        private const float FrameOffsetLimit = 4096f;
        private const int CurrentHudTextDefaultsVersion = 2;
        private const float DefaultMainGaugeWidthPercent = 55f;
        private const float DefaultMainGaugeValueSizePercent = 120f;
        private const float DefaultAttributeTextSizePercent = 300f;
        private const float DefaultRateTokenSizePercent = 200f;
        private const float MinMainGaugeWidthPercent = 25f;
        private const float MaxMainGaugeWidthPercent = 150f;
        private const float MinAttributeTextSizePercent = 125f;
        private const float MaxAttributeTextSizePercent = 800f;

        private static readonly string[] LanguageLabels =
        {
            "한글",
            "ENG"
        };

        private static readonly string[] SightreadModeLabels = { "SR-1", "SR-2", "SR-3" };
        private static readonly KeyCode[] SightreadDefaultActionKeys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3
        };

        private string[] skinFillDirectionLabels;
        private int skinFillDirectionLabelsRevision = -1;

        private string frameOffsetXInput;
        private string frameOffsetYInput;
        private string mainGaugeOffsetXInput;
        private string mainGaugeOffsetYInput;
        private string valueTextOffsetXInput;
        private string valueTextOffsetYInput;
        private string attributeTextOffsetXInput;
        private string attributeTextOffsetYInput;
        private string rateTokenOffsetXInput;
        private string rateTokenOffsetYInput;
        private int debugKeyCaptureSlot;
        private string debugKeyCaptureMessage;
        private bool debugKeySettingsExpanded;
        private int sightreadKeyCaptureSlot;
        private string sightreadKeyCaptureMessage;
        private bool commonKeySettingsExpanded;
        private bool sightreadKeySettingsExpanded;
        private bool xKeySettingsExpanded;
        private bool xKeyCaptureActive;
        private string xKeyCaptureMessage;
        private GUIStyle categoryHeadingStyle;
        private GUISkin categoryHeadingSkin;

        public float MainGaugeOffsetX;
        public float MainGaugeOffsetY = -158f;
        public float MainGaugeSizePercent = 100f;
        public float MainGaugeWidthPercent = DefaultMainGaugeWidthPercent;

        public float MainGaugeValueOffsetX;
        public float MainGaugeValueOffsetY = -14f;
        public float MainGaugeValueSizePercent = DefaultMainGaugeValueSizePercent;
        public bool MainGaugeShowDecimalValue = true;
        public bool MainGaugeValueAttachedToMainGauge = true;
        public float MainGaugeValueScreenOffsetX;
        public float MainGaugeValueScreenOffsetY;
        public bool AttributeTextAttachedToMainGauge = true;
        public float AttributeTextScreenOffsetX;
        public float AttributeTextScreenOffsetY;
        public float AttributeTextSizePercent = DefaultAttributeTextSizePercent;
        public bool RateTokenAttachedToMainGauge = true;
        public float RateTokenScreenOffsetX;
        public float RateTokenScreenOffsetY;
        public float RateTokenSizePercent = DefaultRateTokenSizePercent;
        public int HudTextDefaultsVersion;
        public KeyCode DebugKey1 = KeyCode.LeftShift; // 구 설정 파일 호환용. 현재 디버그 공통 키는 SightreadKey1/2다.
        public KeyCode DebugKey2 = KeyCode.F3;
        public KeyCode SightreadKey1 = KeyCode.LeftShift;
        public KeyCode SightreadKey2 = KeyCode.P;
        public KeyCode SightreadKey3 = KeyCode.Alpha1;
        public KeyCode SightreadKey4 = KeyCode.Alpha2;
        public KeyCode SightreadKey5 = KeyCode.Alpha3;
        public KeyCode XPlanetGaugeKey = KeyCode.X;

        public int MainGaugeColorR = 255;
        public int MainGaugeColorG = 255;
        public int MainGaugeColorB = 255;
        public int MainGaugeValueColorR = 255;
        public int MainGaugeValueColorG = 255;
        public int MainGaugeValueColorB = 255;
        public bool MainGaugeValueColorIndependent;

        public bool CustomGaugeSkinEnabled;
        public bool CustomSkinSettingsVisible = true;
        public bool GaugePositionSettingsVisible = true;
        public bool AdvancedSettingsVisible = true;
        public bool ColorSettingsVisible = true;
        public string HealthSkinImagePath = string.Empty;
        public string FrameSkinImagePath = string.Empty;
        public GaugeSkinFillDirection SkinFillDirection = GaugeSkinFillDirection.Horizontal;
        public float FrameSkinOffsetX;
        public float FrameSkinOffsetY;
        public PlanetGaugeLanguage Language;
        public bool LanguageInitialized;
        public bool TranslateAttributeDisplayToKorean;
        public bool XPlanetGaugeMode;
        internal bool IsCapturingShortcut { get { return debugKeyCaptureSlot != 0 || sightreadKeyCaptureSlot != 0 || xKeyCaptureActive; } }

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

        internal void DrawGui()
        {
            // UMM의 OnGUI에서 매 프레임 호출되는 즉시 모드(IMGUI) 설정 화면이다.
            DrawLanguageSelector();
            GUILayout.Space(14f);

            DrawCategoryHeading(LocalizedStrings.PlanetGaugeSettingsCategory);
            bool selectionLocked = Main.IsSightreadSelectionLocked;
            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = previousGuiEnabled && !selectionLocked;
            GUILayout.Label(GetXPlanetGaugeShortcutLabel());
            GUILayout.Label(GetSightreadShortcutLabel());
            GUILayout.Space(10f);

            XPlanetGaugeMode = GUILayout.Toggle(
                XPlanetGaugeMode,
                LocalizedStrings.XPlanetGaugeMode);
            GUI.enabled = previousGuiEnabled;

            if (XPlanetGaugeMode)
                GUILayout.Label(LocalizedStrings.XPlanetGaugeModeRequirement);
            DrawSightreadSettings();
            GUILayout.Space(14f);

            CustomSkinSettingsVisible = DrawCategoryVisibilityHeader(
                LocalizedStrings.CustomGaugeSkin, CustomSkinSettingsVisible);
            if (CustomSkinSettingsVisible)
            {
                GUILayout.Label(LocalizedStrings.Format(
                    LocalizedStrings.HealthPng,
                    GetDisplayFileName(HealthSkinImagePath)));
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(LocalizedStrings.SelectHealthPng, GUILayout.Width(180f)))
                {
                    HealthSkinImagePath = GaugeSkinManager.PickPng(
                        HealthSkinImagePath,
                        LocalizedStrings.SelectHealthPngDialog);
                }
                if (GUILayout.Button(LocalizedStrings.ClearHealthPng, GUILayout.Width(160f)))
                {
                    HealthSkinImagePath = string.Empty;
                    GaugeSkinManager.ResetToDefault(this);
                }
                GUILayout.EndHorizontal();

                GUILayout.Label(LocalizedStrings.Format(
                    LocalizedStrings.FramePngOptional,
                    GetDisplayFileName(FrameSkinImagePath)));
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(LocalizedStrings.SelectFramePng, GUILayout.Width(180f)))
                {
                    string selectedFramePath = GaugeSkinManager.PickPng(
                        FrameSkinImagePath,
                        LocalizedStrings.SelectFramePngDialog);
                    if (!string.Equals(
                        FrameSkinImagePath,
                        selectedFramePath,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        FrameSkinOffsetX = 0f;
                        FrameSkinOffsetY = 0f;
                        frameOffsetXInput = null;
                        frameOffsetYInput = null;
                    }
                    FrameSkinImagePath = selectedFramePath;
                }
                if (GUILayout.Button(LocalizedStrings.ClearFramePng, GUILayout.Width(160f)))
                {
                    FrameSkinImagePath = string.Empty;
                    FrameSkinOffsetX = 0f;
                    FrameSkinOffsetY = 0f;
                    frameOffsetXInput = null;
                    frameOffsetYInput = null;
                    if (CustomGaugeSkinEnabled
                        && !string.IsNullOrWhiteSpace(HealthSkinImagePath))
                    {
                        GaugeSkinManager.TryApply(this, true);
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.Label(LocalizedStrings.FillDirection);
                RefreshSkinFillDirectionLabels();
                SkinFillDirection = (GaugeSkinFillDirection)GUILayout.SelectionGrid(
                    (int)SkinFillDirection,
                    skinFillDirectionLabels,
                    2,
                    GUILayout.Width(420f));
                GUILayout.Label(LocalizedStrings.FrameOffset);
                FrameSkinOffsetX = DrawFloatField(
                    LocalizedStrings.FrameX,
                    FrameSkinOffsetX,
                    ref frameOffsetXInput,
                    "PlanetGauge.FrameOffsetX");
                FrameSkinOffsetY = DrawFloatField(
                    LocalizedStrings.FrameY,
                    FrameSkinOffsetY,
                    ref frameOffsetYInput,
                    "PlanetGauge.FrameOffsetY");
                GUILayout.BeginHorizontal();
                GUILayout.Space(96f);
                if (GUILayout.Button(LocalizedStrings.ResetFrameOffset, GUILayout.Width(180f)))
                {
                    FrameSkinOffsetX = 0f;
                    FrameSkinOffsetY = 0f;
                    frameOffsetXInput = null;
                    frameOffsetYInput = null;
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(LocalizedStrings.ApplyCustomSkin, GUILayout.Width(180f)))
                {
                    GaugeSkinManager.TryApply(this, true);
                }
                if (GUILayout.Button(LocalizedStrings.UseDefaultSkin, GUILayout.Width(180f)))
                {
                    GaugeSkinManager.ResetToDefault(this);
                }
                GUILayout.EndHorizontal();

                if (!string.IsNullOrWhiteSpace(GaugeSkinManager.LastMessage))
                {
                    GUILayout.Label(GaugeSkinManager.LastMessage);
                }
                if (!string.IsNullOrWhiteSpace(GaugeSkinManager.LastWarning))
                {
                    Color previousColor = GUI.contentColor;
                    GUI.contentColor = new Color(1f, 0.72f, 0.2f, 1f);
                    GUILayout.Label(LocalizedStrings.Format(
                        LocalizedStrings.Warning,
                        GaugeSkinManager.LastWarning));
                    GUI.contentColor = previousColor;
                }
            }

            GaugePositionSettingsVisible = DrawCategoryVisibilityHeader(
                LocalizedStrings.GaugePositionCategory, GaugePositionSettingsVisible);
            if (GaugePositionSettingsVisible)
            {
                GUILayout.Label(LocalizedStrings.MainGaugeSize);
                MainGaugeSizePercent = DrawFloatSlider(
                    LocalizedStrings.Scale,
                    MainGaugeSizePercent,
                    25f,
                    200f,
                    "%");
                MainGaugeWidthPercent = DrawFloatSlider(
                    LocalizedStrings.Width,
                    MainGaugeWidthPercent,
                    MinMainGaugeWidthPercent,
                    MaxMainGaugeWidthPercent,
                    "%");

                GUILayout.BeginHorizontal();
                GUILayout.Space(96f);
                if (GUILayout.Button(LocalizedStrings.ResetSize, GUILayout.Width(140f)))
                {
                    MainGaugeSizePercent = 100f;
                    MainGaugeWidthPercent = DefaultMainGaugeWidthPercent;
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(14f);
                GUILayout.Space(10f);
                GUILayout.Label(LocalizedStrings.MainGaugePosition);
                MainGaugeOffsetX = DrawPositionSlider(
                    "X",
                    MainGaugeOffsetX,
                    ref mainGaugeOffsetXInput,
                    "PlanetGauge.MainGaugeOffsetX",
                    -1000f,
                    1000f);
                MainGaugeOffsetY = DrawPositionSlider(
                    "Y",
                    MainGaugeOffsetY,
                    ref mainGaugeOffsetYInput,
                    "PlanetGauge.MainGaugeOffsetY",
                    -500f,
                    1400f);

                GUILayout.BeginHorizontal();
                GUILayout.Space(96f);
                if (GUILayout.Button(LocalizedStrings.ResetPosition, GUILayout.Width(140f)))
                {
                    MainGaugeOffsetX = 0f;
                    MainGaugeOffsetY = -158f;
                    mainGaugeOffsetXInput = null;
                    mainGaugeOffsetYInput = null;
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(10f);
                GUILayout.Label(LocalizedStrings.GaugeValueText);
                MainGaugeValueSizePercent = DrawFloatSlider(
                    LocalizedStrings.TextSize,
                    MainGaugeValueSizePercent,
                    50f,
                    200f,
                    "%");
                bool valueTextIndependent = !MainGaugeValueAttachedToMainGauge;
                valueTextIndependent = GUILayout.Toggle(
                    valueTextIndependent,
                    LocalizedStrings.IndependentPosition);
                MainGaugeValueAttachedToMainGauge = !valueTextIndependent;
                if (valueTextIndependent)
                {
                    MainGaugeValueScreenOffsetX = DrawPositionSlider(
                        LocalizedStrings.TextX,
                        MainGaugeValueScreenOffsetX,
                        ref valueTextOffsetXInput,
                        "PlanetGauge.ValueTextOffsetX",
                        -800f,
                        800f);
                    MainGaugeValueScreenOffsetY = DrawPositionSlider(
                        LocalizedStrings.TextY,
                        MainGaugeValueScreenOffsetY,
                        ref valueTextOffsetYInput,
                        "PlanetGauge.ValueTextOffsetY",
                        -800f,
                        800f);
                }

                GUILayout.BeginHorizontal();
                GUILayout.Space(96f);
                if (GUILayout.Button(LocalizedStrings.ResetTextPosition, GUILayout.Width(180f)))
                {
                    MainGaugeValueAttachedToMainGauge = true;
                    MainGaugeValueOffsetX = 0f;
                    MainGaugeValueOffsetY = -14f;
                    MainGaugeValueSizePercent = DefaultMainGaugeValueSizePercent;
                    MainGaugeValueScreenOffsetX = 0f;
                    MainGaugeValueScreenOffsetY = 0f;
                    valueTextOffsetXInput = null;
                    valueTextOffsetYInput = null;
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(10f);
                GUILayout.Label(LocalizedStrings.AttributeText);
                AttributeTextSizePercent = DrawFloatSlider(
                    LocalizedStrings.AttributeSize,
                    AttributeTextSizePercent,
                    MinAttributeTextSizePercent,
                    MaxAttributeTextSizePercent,
                    "%");
                bool attributeTextIndependent = !AttributeTextAttachedToMainGauge;
                attributeTextIndependent = GUILayout.Toggle(
                    attributeTextIndependent,
                    LocalizedStrings.IndependentPosition);
                AttributeTextAttachedToMainGauge = !attributeTextIndependent;
                if (attributeTextIndependent)
                {
                    AttributeTextScreenOffsetX = DrawPositionSlider(
                        LocalizedStrings.AttributeX,
                        AttributeTextScreenOffsetX,
                        ref attributeTextOffsetXInput,
                        "PlanetGauge.AttributeTextOffsetX",
                        -1000f,
                        1000f);
                    AttributeTextScreenOffsetY = DrawPositionSlider(
                        LocalizedStrings.AttributeY,
                        AttributeTextScreenOffsetY,
                        ref attributeTextOffsetYInput,
                        "PlanetGauge.AttributeTextOffsetY",
                        -1000f,
                        1000f);
                }
                GUILayout.BeginHorizontal();
                GUILayout.Space(96f);
                if (GUILayout.Button(LocalizedStrings.ResetAttributePosition, GUILayout.Width(190f)))
                {
                    AttributeTextAttachedToMainGauge = true;
                    AttributeTextScreenOffsetX = 0f;
                    AttributeTextScreenOffsetY = 0f;
                    AttributeTextSizePercent = DefaultAttributeTextSizePercent;
                    attributeTextOffsetXInput = null;
                    attributeTextOffsetYInput = null;
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(10f);
                GUILayout.Label(LocalizedStrings.RateToken);
                RateTokenSizePercent = DrawFloatSlider(
                    LocalizedStrings.RateSize,
                    RateTokenSizePercent,
                    25f,
                    500f,
                    "%");
                bool rateTokenIndependent = !RateTokenAttachedToMainGauge;
                rateTokenIndependent = GUILayout.Toggle(
                    rateTokenIndependent,
                    LocalizedStrings.IndependentPosition);
                RateTokenAttachedToMainGauge = !rateTokenIndependent;
                if (rateTokenIndependent)
                {
                    RateTokenScreenOffsetX = DrawPositionSlider(
                        LocalizedStrings.RateX,
                        RateTokenScreenOffsetX,
                        ref rateTokenOffsetXInput,
                        "PlanetGauge.RateTokenOffsetX",
                        -1000f,
                        1000f);
                    RateTokenScreenOffsetY = DrawPositionSlider(
                        LocalizedStrings.RateY,
                        RateTokenScreenOffsetY,
                        ref rateTokenOffsetYInput,
                        "PlanetGauge.RateTokenOffsetY",
                        -1000f,
                        1000f);
                }
                GUILayout.BeginHorizontal();
                GUILayout.Space(96f);
                if (GUILayout.Button(LocalizedStrings.ResetRatePosition, GUILayout.Width(190f)))
                {
                    RateTokenAttachedToMainGauge = true;
                    RateTokenScreenOffsetX = 0f;
                    RateTokenScreenOffsetY = 0f;
                    RateTokenSizePercent = DefaultRateTokenSizePercent;
                    rateTokenOffsetXInput = null;
                    rateTokenOffsetYInput = null;
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10f);
            AdvancedSettingsVisible = DrawCategoryVisibilityHeader(
                LocalizedStrings.Advanced, AdvancedSettingsVisible);
            if (AdvancedSettingsVisible)
            {
                GUILayout.Label(GetDebugShortcutLabel());
                GUILayout.Label(GetSightreadShortcutLabel());
                GUILayout.Label(GetXPlanetGaugeShortcutLabel());
                DrawCommonShortcut();
                DrawDebugShortcut();
                DrawSightreadShortcut();
                DrawXPlanetGaugeShortcut();
                MainGaugeShowDecimalValue = GUILayout.Toggle(
                    MainGaugeShowDecimalValue,
                    LocalizedStrings.ShowDecimalHealth);
                MainGaugeValueColorIndependent = GUILayout.Toggle(
                    MainGaugeValueColorIndependent,
                    LocalizedStrings.IndependentGaugeValueColor);
            }
            else
            {
                debugKeyCaptureSlot = 0;
                sightreadKeyCaptureSlot = 0;
                xKeyCaptureActive = false;
            }

            GUILayout.Space(10f);
            ColorSettingsVisible = DrawCategoryVisibilityHeader(
                LocalizedStrings.ColorCategory, ColorSettingsVisible);
            if (ColorSettingsVisible)
            {
                GUILayout.Label(LocalizedStrings.MainGaugeColor);
                MainGaugeColorR = DrawColorSlider("R", MainGaugeColorR);
                MainGaugeColorG = DrawColorSlider("G", MainGaugeColorG);
                MainGaugeColorB = DrawColorSlider("B", MainGaugeColorB);

                GUILayout.BeginHorizontal();
                GUILayout.Space(96f);
                if (GUILayout.Button(LocalizedStrings.ResetColor, GUILayout.Width(180f)))
                {
                    MainGaugeColorR = 255;
                    MainGaugeColorG = 255;
                    MainGaugeColorB = 255;
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(10f);
                GUILayout.Label(LocalizedStrings.GaugeValueColor);
                MainGaugeValueColorR = DrawColorSlider("R", MainGaugeValueColorR);
                MainGaugeValueColorG = DrawColorSlider("G", MainGaugeValueColorG);
                MainGaugeValueColorB = DrawColorSlider("B", MainGaugeValueColorB);

                GUILayout.BeginHorizontal();
                GUILayout.Space(96f);
                if (GUILayout.Button(LocalizedStrings.ResetGaugeValueColor, GUILayout.Width(220f)))
                {
                    MainGaugeValueColorR = 255;
                    MainGaugeValueColorG = 255;
                    MainGaugeValueColorB = 255;
                }
                GUILayout.EndHorizontal();
            }

            Sanitize();
        }

        internal void Sanitize()
        {
            MigrateHudTextDefaultsIfNeeded();

            // 저장 파일을 사용자가 직접 수정했거나 이전 버전 값이 남아 있어도 UI 범위를 보장한다.
            MainGaugeOffsetX = SanitizePositionFloat(MainGaugeOffsetX, 0f);
            MainGaugeOffsetY = SanitizePositionFloat(MainGaugeOffsetY, -158f);
            MainGaugeSizePercent = SanitizeFloat(MainGaugeSizePercent, 100f, 25f, 200f);
            MainGaugeWidthPercent = SanitizeFloat(
                MainGaugeWidthPercent,
                DefaultMainGaugeWidthPercent,
                MinMainGaugeWidthPercent,
                MaxMainGaugeWidthPercent);
            MainGaugeValueOffsetX = SanitizeFloat(MainGaugeValueOffsetX, 0f, -500f, 500f);
            MainGaugeValueOffsetY = SanitizeFloat(MainGaugeValueOffsetY, -14f, -300f, 300f);
            MainGaugeValueSizePercent = SanitizeFloat(
                MainGaugeValueSizePercent,
                DefaultMainGaugeValueSizePercent,
                50f,
                200f);
            MainGaugeValueScreenOffsetX = SanitizePositionFloat(MainGaugeValueScreenOffsetX, 0f);
            MainGaugeValueScreenOffsetY = SanitizePositionFloat(MainGaugeValueScreenOffsetY, 0f);
            AttributeTextScreenOffsetX = SanitizePositionFloat(AttributeTextScreenOffsetX, 0f);
            AttributeTextScreenOffsetY = SanitizePositionFloat(AttributeTextScreenOffsetY, 0f);
            AttributeTextSizePercent = SanitizeFloat(
                AttributeTextSizePercent,
                DefaultAttributeTextSizePercent,
                MinAttributeTextSizePercent,
                MaxAttributeTextSizePercent);
            RateTokenScreenOffsetX = SanitizePositionFloat(RateTokenScreenOffsetX, 0f);
            RateTokenScreenOffsetY = SanitizePositionFloat(RateTokenScreenOffsetY, 0f);
            RateTokenSizePercent = SanitizeFloat(
                RateTokenSizePercent,
                DefaultRateTokenSizePercent,
                25f,
                500f);
            MainGaugeColorR = Mathf.Clamp(MainGaugeColorR, 0, 255);
            MainGaugeColorG = Mathf.Clamp(MainGaugeColorG, 0, 255);
            MainGaugeColorB = Mathf.Clamp(MainGaugeColorB, 0, 255);
            MainGaugeValueColorR = Mathf.Clamp(MainGaugeValueColorR, 0, 255);
            MainGaugeValueColorG = Mathf.Clamp(MainGaugeValueColorG, 0, 255);
            MainGaugeValueColorB = Mathf.Clamp(MainGaugeValueColorB, 0, 255);
            DebugKey1 = SanitizeKeyCode(DebugKey1, KeyCode.LeftShift);
            DebugKey2 = SanitizeKeyCode(DebugKey2, KeyCode.F3);
            SightreadKey1 = SanitizeKeyCode(SightreadKey1, KeyCode.LeftShift);
            SightreadKey2 = SanitizeKeyCode(SightreadKey2, KeyCode.P);
            SightreadKey3 = SanitizeKeyCode(SightreadKey3, KeyCode.Alpha1);
            SightreadKey4 = SanitizeKeyCode(SightreadKey4, KeyCode.Alpha2);
            SightreadKey5 = SanitizeKeyCode(SightreadKey5, KeyCode.Alpha3);
            XPlanetGaugeKey = SanitizeKeyCode(XPlanetGaugeKey, KeyCode.X);
            for (int i = 1; i <= 5; i++)
            {
                for (int j = 1; j < i; j++)
                {
                    if (GetSightreadKey(i) == GetSightreadKey(j))
                    {
                        ResetSightreadKeys();
                        i = 6;
                        break;
                    }
                }
            }
            if (IsSightreadKey(DebugKey2))
                DebugKey2 = FindAvailableActionKey(KeyCode.F3, XPlanetGaugeKey);
            if (IsSightreadKey(XPlanetGaugeKey) || XPlanetGaugeKey == DebugKey2)
                XPlanetGaugeKey = FindAvailableActionKey(KeyCode.X, DebugKey2);
            HealthSkinImagePath = HealthSkinImagePath ?? string.Empty;
            FrameSkinImagePath = FrameSkinImagePath ?? string.Empty;
            if (!Enum.IsDefined(typeof(GaugeSkinFillDirection), SkinFillDirection))
            {
                SkinFillDirection = GaugeSkinFillDirection.Horizontal;
            }
            if (!Enum.IsDefined(typeof(PlanetGaugeLanguage), Language))
            {
                Language = LocalizedStrings.DetectGameLanguage();
            }
            FrameSkinOffsetX = SanitizeFloat(
                FrameSkinOffsetX,
                0f,
                -FrameOffsetLimit,
                FrameOffsetLimit);
            FrameSkinOffsetY = SanitizeFloat(
                FrameSkinOffsetY,
                0f,
                -FrameOffsetLimit,
                FrameOffsetLimit);
        }

        private void MigrateHudTextDefaultsIfNeeded()
        {
            if (HudTextDefaultsVersion >= CurrentHudTextDefaultsVersion)
            {
                return;
            }

            // 각 단계의 정확한 구 기본값만 올리고 다른 사용자 지정값은 보존한다.
            if (HudTextDefaultsVersion < 1)
            {
                if (Mathf.Approximately(AttributeTextSizePercent, 100f))
                {
                    AttributeTextSizePercent = DefaultAttributeTextSizePercent;
                }

                if (Mathf.Approximately(RateTokenSizePercent, 100f))
                {
                    RateTokenSizePercent = DefaultRateTokenSizePercent;
                }
            }

            if (HudTextDefaultsVersion < 2)
            {
                if (Mathf.Approximately(MainGaugeWidthPercent, 83f))
                {
                    MainGaugeWidthPercent = DefaultMainGaugeWidthPercent;
                }

                if (Mathf.Approximately(MainGaugeValueSizePercent, 111f))
                {
                    MainGaugeValueSizePercent = DefaultMainGaugeValueSizePercent;
                }
            }

            HudTextDefaultsVersion = CurrentHudTextDefaultsVersion;
        }

        internal void InitializeLanguageFromGameIfNeeded()
        {
            if (LanguageInitialized || !RDString.initialized)
            {
                return;
            }

            Language = LocalizedStrings.DetectGameLanguage();
            LanguageInitialized = true;
            LocalizedStrings.NotifyLanguageChanged();
        }

        private void DrawLanguageSelector()
        {
            int selected = Language == PlanetGaugeLanguage.Korean ? 0 : 1;
            int next = GUILayout.SelectionGrid(
                selected,
                LanguageLabels,
                2,
                GUILayout.Width(220f));
            PlanetGaugeLanguage nextLanguage = next == 0
                ? PlanetGaugeLanguage.Korean
                : PlanetGaugeLanguage.English;
            if (nextLanguage != Language)
            {
                Language = nextLanguage;
                LanguageInitialized = true;
                if (debugKeyCaptureSlot != 0)
                    debugKeyCaptureMessage = LocalizedStrings.DebugPressKey;
                if (sightreadKeyCaptureSlot != 0)
                    sightreadKeyCaptureMessage = LocalizedStrings.Format(
                        sightreadKeyCaptureSlot <= 2
                            ? LocalizedStrings.CommonPressKey
                            : LocalizedStrings.SightreadPressKey,
                        sightreadKeyCaptureSlot);
                if (xKeyCaptureActive)
                    xKeyCaptureMessage = LocalizedStrings.XPlanetGaugePressKey;
                LocalizedStrings.NotifyLanguageChanged();
            }

            DrawAttributeDisplayLanguageToggle();
        }

        private void DrawAttributeDisplayLanguageToggle()
        {
            if (Language != PlanetGaugeLanguage.Korean)
            {
                return;
            }

            GUILayout.Space(4f);
            bool next = GUILayout.Toggle(
                TranslateAttributeDisplayToKorean,
                LocalizedStrings.TranslateAttributeDisplay);
            if (next == TranslateAttributeDisplayToKorean)
            {
                return;
            }

            TranslateAttributeDisplayToKorean = next;
            LocalizedStrings.NotifyAttributeDisplayLanguageChanged();
        }

        private void RefreshSkinFillDirectionLabels()
        {
            if (skinFillDirectionLabels != null
                && skinFillDirectionLabelsRevision == LocalizedStrings.Revision)
            {
                return;
            }

            skinFillDirectionLabels = new[]
            {
                LocalizedStrings.HorizontalFill,
                LocalizedStrings.VerticalFill
            };
            skinFillDirectionLabelsRevision = LocalizedStrings.Revision;
        }

        internal Color32 GetMainGaugeColor()
        {
            // Sanitize 이후 호출된다는 전제에서 int 설정값을 손실 없이 byte로 변환한다.
            return new Color32(
                (byte)MainGaugeColorR,
                (byte)MainGaugeColorG,
                (byte)MainGaugeColorB,
                255);
        }

        internal Color32 GetMainGaugeValueColor()
        {
            return new Color32(
                (byte)MainGaugeValueColorR,
                (byte)MainGaugeValueColorG,
                (byte)MainGaugeValueColorB,
                255);
        }

        private static float DrawFloatSlider(
            string label,
            float value,
            float minimum,
            float maximum,
            string suffix = "")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(90f));
            float result = GUILayout.HorizontalSlider(
                value,
                minimum,
                maximum,
                GUILayout.Width(320f));
            GUILayout.Label(
                Mathf.RoundToInt(result) + suffix,
                GUILayout.Width(56f));
            GUILayout.EndHorizontal();
            return result;
        }

        private static float DrawPositionSlider(
            string label,
            float value,
            ref string input,
            string controlName,
            float minimum,
            float maximum)
        {
            bool focused = string.Equals(
                GUI.GetNameOfFocusedControl(),
                controlName,
                StringComparison.Ordinal);
            if (input == null || !focused)
            {
                input = value.ToString("0.###", CultureInfo.InvariantCulture);
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(90f));

            bool previousChanged = GUI.changed;
            GUI.changed = false;
            float sliderValue = Mathf.Clamp(
                value,
                minimum,
                maximum);
            float sliderResult = GUILayout.HorizontalSlider(
                sliderValue,
                minimum,
                maximum,
                GUILayout.Width(240f));
            bool sliderChanged = GUI.changed;
            GUI.changed = previousChanged || sliderChanged;
            if (sliderChanged)
            {
                value = sliderResult;
                input = value.ToString("0.###", CultureInfo.InvariantCulture);
            }

            GUI.SetNextControlName(controlName);
            input = GUILayout.TextField(input, GUILayout.Width(82f));
            GUILayout.Label(LocalizedStrings.PixelUnit, GUILayout.Width(28f));
            GUILayout.EndHorizontal();

            float parsed;
            if (float.TryParse(
                input,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out parsed))
            {
                return SanitizePositionFloat(parsed, value);
            }

            return value;
        }

        private void DrawCategoryHeading(string title)
        {
            if (categoryHeadingStyle == null || categoryHeadingSkin != GUI.skin)
            {
                categoryHeadingSkin = GUI.skin;
                categoryHeadingStyle = new GUIStyle(GUI.skin.label);
                categoryHeadingStyle.fontSize = Math.Max(18, GUI.skin.label.fontSize + 4);
            }
            GUILayout.Label(title, categoryHeadingStyle);
        }

        private bool DrawCategoryVisibilityHeader(string title, bool visible)
        {
            GUILayout.BeginHorizontal();
            DrawCategoryHeading(title);
            visible = GUILayout.Toggle(visible, LocalizedStrings.SettingsVisibility);
            GUILayout.EndHorizontal();
            return visible;
        }

        private string GetSightreadShortcutLabel()
        {
            return LocalizedStrings.Format(LocalizedStrings.SightreadShortcut,
                FormatSightreadKey(SightreadKey1), FormatSightreadKey(SightreadKey2),
                FormatSightreadKey(SightreadKey3), FormatSightreadKey(SightreadKey4),
                FormatSightreadKey(SightreadKey5));
        }

        private string GetDebugShortcutLabel()
        {
            return LocalizedStrings.Format(LocalizedStrings.DebugShortcut,
                FormatSightreadKey(SightreadKey1), FormatSightreadKey(SightreadKey2),
                FormatSightreadKey(DebugKey2));
        }

        private string GetXPlanetGaugeShortcutLabel()
        {
            return LocalizedStrings.Format(LocalizedStrings.XPlanetGaugeShortcut,
                FormatSightreadKey(SightreadKey1), FormatSightreadKey(SightreadKey2),
                FormatSightreadKey(XPlanetGaugeKey));
        }

        private void DrawCommonShortcut()
        {
            commonKeySettingsExpanded = GUILayout.Toggle(
                commonKeySettingsExpanded, LocalizedStrings.EditCommonKeys,
                GUILayout.Width(165f));
            if (!commonKeySettingsExpanded)
            {
                if (sightreadKeyCaptureSlot <= 2)
                    CancelSightreadKeyCapture();
                return;
            }

            GUILayout.BeginHorizontal();
            for (int slot = 1; slot <= 2; slot++)
            {
                int captureSlot = slot;
                if (GUILayout.Button(LocalizedStrings.Format(LocalizedStrings.SightreadSetKey,
                    slot, FormatSightreadKey(GetSightreadKey(slot))),
                    GUILayout.Width(80f), GUILayout.Height(18f)))
                    BeginSightreadKeyCapture(captureSlot);
            }
            if (GUILayout.Button(LocalizedStrings.ResetShortcut,
                GUILayout.Width(90f), GUILayout.Height(18f)))
            {
                ResetCommonKeys();
                if (sightreadKeyCaptureSlot <= 2)
                    CancelSightreadKeyCapture();
            }
            GUILayout.EndHorizontal();
            if (sightreadKeyCaptureSlot >= 1 && sightreadKeyCaptureSlot <= 2)
                DrawSightreadKeyCapture();
        }

        private void DrawDebugShortcut()
        {
            debugKeySettingsExpanded = GUILayout.Toggle(
                debugKeySettingsExpanded, LocalizedStrings.EditDebugKeys,
                GUILayout.Width(165f));
            if (!debugKeySettingsExpanded)
            {
                debugKeyCaptureSlot = 0;
                debugKeyCaptureMessage = null;
                return;
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(LocalizedStrings.Format(
                LocalizedStrings.DebugSetKey, FormatSightreadKey(DebugKey2)),
                GUILayout.Width(100f), GUILayout.Height(18f)))
            {
                sightreadKeyCaptureSlot = 0;
                xKeyCaptureActive = false;
                debugKeyCaptureSlot = 2;
                debugKeyCaptureMessage = LocalizedStrings.DebugPressKey;
            }
            if (GUILayout.Button(LocalizedStrings.ResetShortcut,
                GUILayout.Width(90f), GUILayout.Height(18f)))
            {
                DebugKey2 = FindAvailableActionKey(KeyCode.F3, XPlanetGaugeKey);
                debugKeyCaptureSlot = 0;
                debugKeyCaptureMessage = null;
            }
            GUILayout.EndHorizontal();

            if (debugKeyCaptureSlot == 0)
            {
                return;
            }

            GUILayout.Label(debugKeyCaptureMessage);
            Event currentEvent = Event.current;
            if (currentEvent == null || currentEvent.type != EventType.KeyDown)
            {
                return;
            }

            KeyCode pressedKey = currentEvent.keyCode;
            if (pressedKey == KeyCode.Escape)
            {
                debugKeyCaptureSlot = 0;
                debugKeyCaptureMessage = null;
                currentEvent.Use();
                return;
            }

            if (pressedKey == KeyCode.None)
            {
                return;
            }

            if (IsSightreadKey(pressedKey) || pressedKey == XPlanetGaugeKey)
            {
                debugKeyCaptureMessage = LocalizedStrings.ShortcutKeysMustDiffer;
                currentEvent.Use();
                return;
            }

            DebugKey2 = pressedKey;

            debugKeyCaptureSlot = 0;
            debugKeyCaptureMessage = null;
            currentEvent.Use();
        }

        private void DrawSightreadSettings()
        {
            GUILayout.Space(8f);
            bool selectionLocked = Main.IsSightreadSelectionLocked;
            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = previousGuiEnabled && !selectionLocked;
            bool enabled = Main.SightreadMode != 0;
            bool requestedEnabled = GUILayout.Toggle(enabled, LocalizedStrings.SightreadMode);
            if (!selectionLocked && requestedEnabled != enabled)
                Main.SetSightreadMode(requestedEnabled ? Main.LastSightreadMode : 0);

            if (Main.SightreadMode != 0)
            {
                int selected = GUILayout.Toolbar(Main.SightreadMode - 1, SightreadModeLabels,
                    GUILayout.Width(180f), GUILayout.Height(22f));
                if (!selectionLocked && selected + 1 != Main.SightreadMode)
                    Main.SetSightreadMode(selected + 1);
            }
            GUI.enabled = previousGuiEnabled;

            if (Main.SightreadMode != 0)
            {
                GUILayout.Label(Main.SightreadMode == 1
                    ? LocalizedStrings.SightreadMode1Description
                    : Main.SightreadMode == 2
                    ? LocalizedStrings.SightreadMode2Description
                    : LocalizedStrings.SightreadMode3Description);
                GUILayout.Label(LocalizedStrings.SightreadEventOverrideNote);
            }
        }

        private void DrawSightreadShortcut()
        {
            sightreadKeySettingsExpanded = GUILayout.Toggle(
                sightreadKeySettingsExpanded, LocalizedStrings.EditSightreadKeys,
                GUILayout.Width(165f));
            if (!sightreadKeySettingsExpanded)
            {
                if (sightreadKeyCaptureSlot >= 3)
                    CancelSightreadKeyCapture();
                return;
            }
            GUILayout.BeginHorizontal();
            for (int slot = 3; slot <= 5; slot++)
            {
                int captureSlot = slot;
                if (GUILayout.Button(LocalizedStrings.Format(LocalizedStrings.SightreadSetKey,
                    slot, FormatSightreadKey(GetSightreadKey(slot))),
                    GUILayout.Width(80f), GUILayout.Height(18f)))
                    BeginSightreadKeyCapture(captureSlot);
            }
            if (GUILayout.Button(LocalizedStrings.ResetShortcut,
                GUILayout.Width(90f), GUILayout.Height(18f)))
            {
                ResetSightreadActionKeys();
                if (sightreadKeyCaptureSlot >= 3)
                    CancelSightreadKeyCapture();
            }
            GUILayout.EndHorizontal();
            if (sightreadKeyCaptureSlot >= 3)
                DrawSightreadKeyCapture();
        }

        private void BeginSightreadKeyCapture(int slot)
        {
            debugKeyCaptureSlot = 0;
            xKeyCaptureActive = false;
            sightreadKeyCaptureSlot = slot;
            sightreadKeyCaptureMessage = LocalizedStrings.Format(
                slot <= 2 ? LocalizedStrings.CommonPressKey : LocalizedStrings.SightreadPressKey,
                slot);
        }

        private void CancelSightreadKeyCapture()
        {
            sightreadKeyCaptureSlot = 0;
            sightreadKeyCaptureMessage = null;
        }

        private void DrawSightreadKeyCapture()
        {
            GUILayout.Label(sightreadKeyCaptureMessage);
            Event currentEvent = Event.current;
            if (currentEvent == null || currentEvent.type != EventType.KeyDown) return;
            KeyCode pressedKey = currentEvent.keyCode;
            if (pressedKey == KeyCode.Escape)
            {
                CancelSightreadKeyCapture();
                currentEvent.Use();
                return;
            }
            if (pressedKey == KeyCode.None) return;
            for (int i = 1; i <= 5; i++)
            {
                if ((i != sightreadKeyCaptureSlot && GetSightreadKey(i) == pressedKey)
                    || pressedKey == DebugKey2 || pressedKey == XPlanetGaugeKey)
                {
                    sightreadKeyCaptureMessage = LocalizedStrings.ShortcutKeysMustDiffer;
                    currentEvent.Use();
                    return;
                }
            }
            SetSightreadKey(sightreadKeyCaptureSlot, pressedKey);
            CancelSightreadKeyCapture();
            currentEvent.Use();
        }

        private void DrawXPlanetGaugeShortcut()
        {
            xKeySettingsExpanded = GUILayout.Toggle(
                xKeySettingsExpanded, LocalizedStrings.EditXPlanetGaugeKey,
                GUILayout.Width(165f));
            if (!xKeySettingsExpanded)
            {
                xKeyCaptureActive = false;
                xKeyCaptureMessage = null;
                return;
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(LocalizedStrings.Format(LocalizedStrings.XPlanetGaugeSetKey,
                FormatSightreadKey(XPlanetGaugeKey)), GUILayout.Width(100f), GUILayout.Height(18f)))
            {
                debugKeyCaptureSlot = 0;
                sightreadKeyCaptureSlot = 0;
                xKeyCaptureActive = true;
                xKeyCaptureMessage = LocalizedStrings.XPlanetGaugePressKey;
            }
            if (GUILayout.Button(LocalizedStrings.ResetShortcut,
                GUILayout.Width(90f), GUILayout.Height(18f)))
            {
                XPlanetGaugeKey = FindAvailableActionKey(KeyCode.X, DebugKey2);
                xKeyCaptureActive = false;
                xKeyCaptureMessage = null;
            }
            GUILayout.EndHorizontal();
            if (!xKeyCaptureActive) return;
            GUILayout.Label(xKeyCaptureMessage);
            Event currentEvent = Event.current;
            if (currentEvent == null || currentEvent.type != EventType.KeyDown) return;
            KeyCode pressedKey = currentEvent.keyCode;
            if (pressedKey == KeyCode.Escape)
            {
                xKeyCaptureActive = false;
                xKeyCaptureMessage = null;
                currentEvent.Use();
                return;
            }
            if (pressedKey == KeyCode.None) return;
            if (IsSightreadKey(pressedKey) || pressedKey == DebugKey2)
            {
                xKeyCaptureMessage = LocalizedStrings.ShortcutKeysMustDiffer;
                currentEvent.Use();
                return;
            }
            XPlanetGaugeKey = pressedKey;
            xKeyCaptureActive = false;
            xKeyCaptureMessage = null;
            currentEvent.Use();
        }

        private bool IsSightreadKey(KeyCode key)
        {
            for (int slot = 1; slot <= 5; slot++)
            {
                if (GetSightreadKey(slot) == key) return true;
            }
            return false;
        }

        private KeyCode FindAvailableActionKey(KeyCode preferred, KeyCode otherActionKey)
        {
            if (!IsSightreadKey(preferred) && preferred != otherActionKey)
                return preferred;
            for (int candidate = (int)KeyCode.F1; candidate <= (int)KeyCode.F12; candidate++)
            {
                KeyCode key = (KeyCode)candidate;
                if (!IsSightreadKey(key) && key != otherActionKey) return key;
            }
            return preferred;
        }

        private KeyCode GetSightreadKey(int slot)
        {
            switch (slot)
            {
                case 1: return SightreadKey1;
                case 2: return SightreadKey2;
                case 3: return SightreadKey3;
                case 4: return SightreadKey4;
                default: return SightreadKey5;
            }
        }

        private static string FormatSightreadKey(KeyCode key)
        {
            if (key == KeyCode.LeftShift) return "Shift";
            return key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9
                ? ((int)key - (int)KeyCode.Alpha0).ToString(CultureInfo.InvariantCulture)
                : key.ToString();
        }

        private void SetSightreadKey(int slot, KeyCode key)
        {
            switch (slot)
            {
                case 1: SightreadKey1 = key; break;
                case 2: SightreadKey2 = key; break;
                case 3: SightreadKey3 = key; break;
                case 4: SightreadKey4 = key; break;
                case 5: SightreadKey5 = key; break;
            }
        }

        private void ResetSightreadKeys()
        {
            SightreadKey1 = KeyCode.LeftShift;
            SightreadKey2 = KeyCode.P;
            SightreadKey3 = KeyCode.Alpha1;
            SightreadKey4 = KeyCode.Alpha2;
            SightreadKey5 = KeyCode.Alpha3;
        }

        private void ResetCommonKeys()
        {
            SightreadKey1 = KeyCode.LeftShift;
            SightreadKey2 = KeyCode.P;
            for (int slot = 3; slot <= 5; slot++)
            {
                KeyCode key = GetSightreadKey(slot);
                if (key == SightreadKey1 || key == SightreadKey2)
                    SetSightreadKey(slot, FindAvailableSightreadKey(
                        SightreadDefaultActionKeys[slot - 3], slot));
            }
            if (IsSightreadKey(DebugKey2))
                DebugKey2 = FindAvailableActionKey(KeyCode.F3, XPlanetGaugeKey);
            if (IsSightreadKey(XPlanetGaugeKey) || XPlanetGaugeKey == DebugKey2)
                XPlanetGaugeKey = FindAvailableActionKey(KeyCode.X, DebugKey2);
        }

        private void ResetSightreadActionKeys()
        {
            SightreadKey3 = KeyCode.None;
            SightreadKey4 = KeyCode.None;
            SightreadKey5 = KeyCode.None;
            for (int slot = 3; slot <= 5; slot++)
                SetSightreadKey(slot, FindAvailableSightreadKey(
                    SightreadDefaultActionKeys[slot - 3], slot));
        }

        private KeyCode FindAvailableSightreadKey(KeyCode preferred, int slot)
        {
            if (IsAvailableSightreadKey(preferred, slot)) return preferred;
            for (int candidate = (int)KeyCode.F1; candidate <= (int)KeyCode.F12; candidate++)
            {
                KeyCode key = (KeyCode)candidate;
                if (IsAvailableSightreadKey(key, slot)) return key;
            }
            return GetSightreadKey(slot);
        }

        private bool IsAvailableSightreadKey(KeyCode key, int slot)
        {
            if (key == KeyCode.None || key == DebugKey2 || key == XPlanetGaugeKey)
                return false;
            for (int i = 1; i <= 5; i++)
            {
                if (i != slot && GetSightreadKey(i) == key) return false;
            }
            return true;
        }

        private static float DrawFloatField(
            string label,
            float value,
            ref string input,
            string controlName)
        {
            bool focused = string.Equals(
                GUI.GetNameOfFocusedControl(),
                controlName,
                StringComparison.Ordinal);
            if (input == null || !focused)
            {
                input = value.ToString("0.###", CultureInfo.InvariantCulture);
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(90f));
            GUI.SetNextControlName(controlName);
            input = GUILayout.TextField(input, GUILayout.Width(160f));
            GUILayout.Label(LocalizedStrings.PixelUnit, GUILayout.Width(32f));
            GUILayout.EndHorizontal();

            float parsed;
            if (float.TryParse(
                input,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out parsed))
            {
                return SanitizeFloat(
                    parsed,
                    value,
                    -FrameOffsetLimit,
                    FrameOffsetLimit);
            }

            return value;
        }

        private static int DrawColorSlider(string label, int value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(90f));
            float result = GUILayout.HorizontalSlider(
                value,
                0f,
                255f,
                GUILayout.Width(320f));
            int rounded = Mathf.RoundToInt(result);
            GUILayout.Label(rounded.ToString(), GUILayout.Width(56f));
            GUILayout.EndHorizontal();
            return rounded;
        }

        private static string GetDisplayFileName(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return LocalizedStrings.None;
            }

            try
            {
                return Path.GetFileName(path);
            }
            catch
            {
                return path;
            }
        }

        private static float SanitizeFloat(
            float value,
            float fallback,
            float minimum,
            float maximum)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                value = fallback;
            }

            return Mathf.Clamp(value, minimum, maximum);
        }

        private static float SanitizePositionFloat(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? fallback
                : value;
        }

        private static KeyCode SanitizeKeyCode(KeyCode value, KeyCode fallback)
        {
            return value != KeyCode.None && Enum.IsDefined(typeof(KeyCode), value)
                ? value
                : fallback;
        }
    }
}
