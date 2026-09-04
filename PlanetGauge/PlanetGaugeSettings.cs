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

        private static readonly string[] LanguageLabels =
        {
            "한글",
            "ENG"
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

        public float MainGaugeOffsetX;
        public float MainGaugeOffsetY = -158f;
        public float MainGaugeSizePercent = 100f;
        public float MainGaugeWidthPercent = 83f;

        public float MainGaugeValueOffsetX;
        public float MainGaugeValueOffsetY = -14f;
        public float MainGaugeValueSizePercent = 111f;
        public bool MainGaugeShowDecimalValue = true;
        public bool MainGaugeValueAttachedToMainGauge = true;
        public float MainGaugeValueScreenOffsetX;
        public float MainGaugeValueScreenOffsetY;
        public bool AttributeTextAttachedToMainGauge = true;
        public float AttributeTextScreenOffsetX;
        public float AttributeTextScreenOffsetY;
        public float AttributeTextSizePercent = 100f;
        public bool RateTokenAttachedToMainGauge = true;
        public float RateTokenScreenOffsetX;
        public float RateTokenScreenOffsetY;
        public float RateTokenSizePercent = 100f;
        public KeyCode DebugKey1 = KeyCode.LeftShift;
        public KeyCode DebugKey2 = KeyCode.F3;

        public int MainGaugeColorR = 255;
        public int MainGaugeColorG = 255;
        public int MainGaugeColorB = 255;

        public bool CustomGaugeSkinEnabled;
        public string HealthSkinImagePath = string.Empty;
        public string FrameSkinImagePath = string.Empty;
        public GaugeSkinFillDirection SkinFillDirection = GaugeSkinFillDirection.Horizontal;
        public float FrameSkinOffsetX;
        public float FrameSkinOffsetY;
        public PlanetGaugeLanguage Language;
        public bool LanguageInitialized;
        public bool TranslateAttributeDisplayToKorean;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

        internal void DrawGui()
        {
            // UMM의 OnGUI에서 매 프레임 호출되는 즉시 모드(IMGUI) 설정 화면이다.
            DrawLanguageSelector();
            GUILayout.Space(14f);

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
                25f,
                100f,
                "%");

            GUILayout.BeginHorizontal();
            GUILayout.Space(96f);
            if (GUILayout.Button(LocalizedStrings.ResetSize, GUILayout.Width(140f)))
            {
                MainGaugeSizePercent = 100f;
                MainGaugeWidthPercent = 83f;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(14f);
            GUILayout.Label(LocalizedStrings.CustomGaugeSkin);
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
                25f,
                500f,
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
                rateTokenOffsetXInput = null;
                rateTokenOffsetYInput = null;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.Label(LocalizedStrings.Advanced);
            DrawDebugShortcut();
            MainGaugeShowDecimalValue = GUILayout.Toggle(
                MainGaugeShowDecimalValue,
                LocalizedStrings.ShowDecimalHealth);

            GUILayout.Space(10f);
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

            Sanitize();
        }

        internal void Sanitize()
        {
            // 저장 파일을 사용자가 직접 수정했거나 이전 버전 값이 남아 있어도 UI 범위를 보장한다.
            MainGaugeOffsetX = SanitizePositionFloat(MainGaugeOffsetX, 0f);
            MainGaugeOffsetY = SanitizePositionFloat(MainGaugeOffsetY, -158f);
            MainGaugeSizePercent = SanitizeFloat(MainGaugeSizePercent, 100f, 25f, 200f);
            MainGaugeWidthPercent = SanitizeFloat(MainGaugeWidthPercent, 83f, 25f, 100f);
            MainGaugeValueOffsetX = SanitizeFloat(MainGaugeValueOffsetX, 0f, -500f, 500f);
            MainGaugeValueOffsetY = SanitizeFloat(MainGaugeValueOffsetY, -14f, -300f, 300f);
            MainGaugeValueSizePercent = SanitizeFloat(MainGaugeValueSizePercent, 111f, 50f, 200f);
            MainGaugeValueScreenOffsetX = SanitizePositionFloat(MainGaugeValueScreenOffsetX, 0f);
            MainGaugeValueScreenOffsetY = SanitizePositionFloat(MainGaugeValueScreenOffsetY, 0f);
            AttributeTextScreenOffsetX = SanitizePositionFloat(AttributeTextScreenOffsetX, 0f);
            AttributeTextScreenOffsetY = SanitizePositionFloat(AttributeTextScreenOffsetY, 0f);
            AttributeTextSizePercent = SanitizeFloat(AttributeTextSizePercent, 100f, 25f, 500f);
            RateTokenScreenOffsetX = SanitizePositionFloat(RateTokenScreenOffsetX, 0f);
            RateTokenScreenOffsetY = SanitizePositionFloat(RateTokenScreenOffsetY, 0f);
            RateTokenSizePercent = SanitizeFloat(RateTokenSizePercent, 100f, 25f, 500f);
            MainGaugeColorR = Mathf.Clamp(MainGaugeColorR, 0, 255);
            MainGaugeColorG = Mathf.Clamp(MainGaugeColorG, 0, 255);
            MainGaugeColorB = Mathf.Clamp(MainGaugeColorB, 0, 255);
            DebugKey1 = SanitizeKeyCode(DebugKey1, KeyCode.LeftShift);
            DebugKey2 = SanitizeKeyCode(DebugKey2, KeyCode.F3);
            if (DebugKey1 == DebugKey2)
            {
                if (DebugKey1 == KeyCode.F3)
                {
                    DebugKey1 = KeyCode.LeftShift;
                }
                else
                {
                    DebugKey2 = KeyCode.F3;
                }
            }
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
                if (debugKeyCaptureSlot == 1)
                {
                    debugKeyCaptureMessage = LocalizedStrings.PressKey1;
                }
                else if (debugKeyCaptureSlot == 2)
                {
                    debugKeyCaptureMessage = LocalizedStrings.PressKey2;
                }
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

        private void DrawDebugShortcut()
        {
            GUILayout.Label(LocalizedStrings.Format(
                LocalizedStrings.DebugShortcut,
                DebugKey1,
                DebugKey2));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(LocalizedStrings.SetKey1, GUILayout.Width(120f)))
            {
                debugKeyCaptureSlot = 1;
                debugKeyCaptureMessage = LocalizedStrings.PressKey1;
            }
            if (GUILayout.Button(LocalizedStrings.SetKey2, GUILayout.Width(120f)))
            {
                debugKeyCaptureSlot = 2;
                debugKeyCaptureMessage = LocalizedStrings.PressKey2;
            }
            if (GUILayout.Button(LocalizedStrings.ResetShortcut, GUILayout.Width(140f)))
            {
                DebugKey1 = KeyCode.LeftShift;
                DebugKey2 = KeyCode.F3;
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

            KeyCode otherKey = debugKeyCaptureSlot == 1 ? DebugKey2 : DebugKey1;
            if (pressedKey == otherKey)
            {
                debugKeyCaptureMessage = LocalizedStrings.KeysMustDiffer;
                currentEvent.Use();
                return;
            }

            if (debugKeyCaptureSlot == 1)
            {
                DebugKey1 = pressedKey;
            }
            else
            {
                DebugKey2 = pressedKey;
            }

            debugKeyCaptureSlot = 0;
            debugKeyCaptureMessage = null;
            currentEvent.Use();
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
