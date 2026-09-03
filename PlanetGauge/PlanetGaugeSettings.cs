using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityModManagerNet;

namespace PlanetGauge
{
    public enum AttributeTextPositionAnchor
    {
        Top = 0,
        Center = 1,
        Bottom = 2
    }

    /// <summary>
    /// Unity Mod Manager가 직렬화하는 사용자 설정과 설정 GUI를 소유한다.
    /// public 필드 이름은 저장 파일의 키이므로 이름 변경 시 기존 설정 마이그레이션이 필요하다.
    /// </summary>
    public sealed class PlanetGaugeSettings : UnityModManager.ModSettings
    {
        private const float FrameOffsetLimit = 4096f;
        private const float PositionSliderLimit = 1000f;
        private const float PositionSafetyLimit = 100000f;

        private static readonly string[] SkinFillDirectionLabels =
        {
            "Horizontal (left to right)",
            "Vertical (bottom to top)"
        };

        private static readonly string[] AttributeTextAnchorLabels =
        {
            "Top",
            "Center",
            "Bottom"
        };

        private string frameOffsetXInput;
        private string frameOffsetYInput;
        private string mainGaugeOffsetXInput;
        private string mainGaugeOffsetYInput;
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
        public float AttributeTextScreenOffsetX;
        public float AttributeTextScreenOffsetY;
        public float AttributeTextSizePercent = 100f;
        public AttributeTextPositionAnchor AttributeTextAnchor =
            AttributeTextPositionAnchor.Center;
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

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

        internal void DrawGui()
        {
            // UMM의 OnGUI에서 매 프레임 호출되는 즉시 모드(IMGUI) 설정 화면이다.
            GUILayout.Label("Main gauge size");
            MainGaugeSizePercent = DrawFloatSlider(
                "Scale",
                MainGaugeSizePercent,
                25f,
                200f,
                "%");
            MainGaugeWidthPercent = DrawFloatSlider(
                "Width",
                MainGaugeWidthPercent,
                25f,
                100f,
                "%");

            GUILayout.BeginHorizontal();
            GUILayout.Space(96f);
            if (GUILayout.Button("Reset size", GUILayout.Width(140f)))
            {
                MainGaugeSizePercent = 100f;
                MainGaugeWidthPercent = 83f;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(14f);
            GUILayout.Label("Custom gauge skin");
            GUILayout.Label("Health PNG: " + GetDisplayFileName(HealthSkinImagePath));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Select health PNG", GUILayout.Width(180f)))
            {
                HealthSkinImagePath = GaugeSkinManager.PickPng(
                    HealthSkinImagePath,
                    "Select PlanetGauge health PNG");
            }
            if (GUILayout.Button("Clear health PNG", GUILayout.Width(160f)))
            {
                HealthSkinImagePath = string.Empty;
                GaugeSkinManager.ResetToDefault(this);
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("Frame PNG (optional): " + GetDisplayFileName(FrameSkinImagePath));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Select frame PNG", GUILayout.Width(180f)))
            {
                string selectedFramePath = GaugeSkinManager.PickPng(
                    FrameSkinImagePath,
                    "Select optional PlanetGauge frame PNG");
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
            if (GUILayout.Button("Clear frame PNG", GUILayout.Width(160f)))
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

            GUILayout.Label("Fill direction");
            SkinFillDirection = (GaugeSkinFillDirection)GUILayout.SelectionGrid(
                (int)SkinFillDirection,
                SkinFillDirectionLabels,
                2,
                GUILayout.Width(420f));
            GUILayout.Label("Frame offset (updates live)");
            FrameSkinOffsetX = DrawFloatField(
                "Frame X",
                FrameSkinOffsetX,
                ref frameOffsetXInput,
                "PlanetGauge.FrameOffsetX");
            FrameSkinOffsetY = DrawFloatField(
                "Frame Y",
                FrameSkinOffsetY,
                ref frameOffsetYInput,
                "PlanetGauge.FrameOffsetY");
            GUILayout.BeginHorizontal();
            GUILayout.Space(96f);
            if (GUILayout.Button("Reset frame offset", GUILayout.Width(180f)))
            {
                FrameSkinOffsetX = 0f;
                FrameSkinOffsetY = 0f;
                frameOffsetXInput = null;
                frameOffsetYInput = null;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply custom skin", GUILayout.Width(180f)))
            {
                GaugeSkinManager.TryApply(this, true);
            }
            if (GUILayout.Button("Use default skin", GUILayout.Width(180f)))
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
                GUILayout.Label("Warning: " + GaugeSkinManager.LastWarning);
                GUI.contentColor = previousColor;
            }

            GUILayout.Space(10f);
            GUILayout.Label("Main gauge position");
            MainGaugeOffsetX = DrawPositionSlider(
                "X",
                MainGaugeOffsetX,
                ref mainGaugeOffsetXInput,
                "PlanetGauge.MainGaugeOffsetX");
            MainGaugeOffsetY = DrawPositionSlider(
                "Y",
                MainGaugeOffsetY,
                ref mainGaugeOffsetYInput,
                "PlanetGauge.MainGaugeOffsetY");

            GUILayout.BeginHorizontal();
            GUILayout.Space(96f);
            if (GUILayout.Button("Reset position", GUILayout.Width(140f)))
            {
                MainGaugeOffsetX = 0f;
                MainGaugeOffsetY = -158f;
                mainGaugeOffsetXInput = null;
                mainGaugeOffsetYInput = null;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.Label("Gauge value text");
            MainGaugeValueOffsetX = DrawFloatSlider(
                "Text X",
                MainGaugeValueOffsetX,
                -500f,
                500f);
            MainGaugeValueOffsetY = DrawFloatSlider(
                "Text Y",
                MainGaugeValueOffsetY,
                -300f,
                300f);
            MainGaugeValueSizePercent = DrawFloatSlider(
                "Text size",
                MainGaugeValueSizePercent,
                50f,
                200f,
                "%");

            GUILayout.BeginHorizontal();
            GUILayout.Space(96f);
            if (GUILayout.Button("Reset text", GUILayout.Width(140f)))
            {
                MainGaugeValueOffsetX = 0f;
                MainGaugeValueOffsetY = -14f;
                MainGaugeValueSizePercent = 111f;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.Label("Attribute text (screen-centered position)");
            AttributeTextScreenOffsetX = DrawPositionSlider(
                "Attribute X",
                AttributeTextScreenOffsetX,
                ref attributeTextOffsetXInput,
                "PlanetGauge.AttributeTextOffsetX");
            AttributeTextScreenOffsetY = DrawPositionSlider(
                "Attribute Y",
                AttributeTextScreenOffsetY,
                ref attributeTextOffsetYInput,
                "PlanetGauge.AttributeTextOffsetY");
            AttributeTextSizePercent = DrawFloatSlider(
                "Attribute size",
                AttributeTextSizePercent,
                25f,
                500f,
                "%");
            GUILayout.Label("Multi-line position anchor");
            AttributeTextAnchor = (AttributeTextPositionAnchor)GUILayout.SelectionGrid(
                (int)AttributeTextAnchor,
                AttributeTextAnchorLabels,
                3,
                GUILayout.Width(420f));
            GUILayout.BeginHorizontal();
            GUILayout.Space(96f);
            if (GUILayout.Button("Reset attribute position", GUILayout.Width(190f)))
            {
                AttributeTextScreenOffsetX = 0f;
                AttributeTextScreenOffsetY = 0f;
                AttributeTextAnchor = AttributeTextPositionAnchor.Center;
                attributeTextOffsetXInput = null;
                attributeTextOffsetYInput = null;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.Label("Rate token");
            RateTokenAttachedToMainGauge = GUILayout.Toggle(
                RateTokenAttachedToMainGauge,
                "Attach rate token to main gauge");
            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = previousGuiEnabled && !RateTokenAttachedToMainGauge;
            RateTokenScreenOffsetX = DrawPositionSlider(
                "Rate X",
                RateTokenScreenOffsetX,
                ref rateTokenOffsetXInput,
                "PlanetGauge.RateTokenOffsetX");
            RateTokenScreenOffsetY = DrawPositionSlider(
                "Rate Y",
                RateTokenScreenOffsetY,
                ref rateTokenOffsetYInput,
                "PlanetGauge.RateTokenOffsetY");
            GUI.enabled = previousGuiEnabled;
            RateTokenSizePercent = DrawFloatSlider(
                "Rate size",
                RateTokenSizePercent,
                25f,
                500f,
                "%");
            GUILayout.BeginHorizontal();
            GUILayout.Space(96f);
            if (GUILayout.Button("Reset rate position", GUILayout.Width(190f)))
            {
                RateTokenScreenOffsetX = 0f;
                RateTokenScreenOffsetY = 0f;
                rateTokenOffsetXInput = null;
                rateTokenOffsetYInput = null;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            GUILayout.Label("Advanced");
            DrawDebugShortcut();
            MainGaugeShowDecimalValue = GUILayout.Toggle(
                MainGaugeShowDecimalValue,
                "Show decimal health (e.g. 73.4)");

            GUILayout.Space(10f);
            GUILayout.Label("Main gauge color (RGB)");
            MainGaugeColorR = DrawColorSlider("R", MainGaugeColorR);
            MainGaugeColorG = DrawColorSlider("G", MainGaugeColorG);
            MainGaugeColorB = DrawColorSlider("B", MainGaugeColorB);

            GUILayout.BeginHorizontal();
            GUILayout.Space(96f);
            if (GUILayout.Button("Reset color (#FFFFFF)", GUILayout.Width(180f)))
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
            AttributeTextScreenOffsetX = SanitizePositionFloat(AttributeTextScreenOffsetX, 0f);
            AttributeTextScreenOffsetY = SanitizePositionFloat(AttributeTextScreenOffsetY, 0f);
            AttributeTextSizePercent = SanitizeFloat(AttributeTextSizePercent, 100f, 25f, 500f);
            if (!Enum.IsDefined(typeof(AttributeTextPositionAnchor), AttributeTextAnchor))
            {
                AttributeTextAnchor = AttributeTextPositionAnchor.Center;
            }
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

            bool previousChanged = GUI.changed;
            GUI.changed = false;
            float sliderValue = Mathf.Clamp(
                value,
                -PositionSliderLimit,
                PositionSliderLimit);
            float sliderResult = GUILayout.HorizontalSlider(
                sliderValue,
                -PositionSliderLimit,
                PositionSliderLimit,
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
            GUILayout.Label("px", GUILayout.Width(28f));
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
            GUILayout.Label("Debug shortcut: " + DebugKey1 + " + " + DebugKey2);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Set key 1", GUILayout.Width(120f)))
            {
                debugKeyCaptureSlot = 1;
                debugKeyCaptureMessage = "Press key 1 (Escape to cancel)";
            }
            if (GUILayout.Button("Set key 2", GUILayout.Width(120f)))
            {
                debugKeyCaptureSlot = 2;
                debugKeyCaptureMessage = "Press key 2 (Escape to cancel)";
            }
            if (GUILayout.Button("Reset shortcut", GUILayout.Width(140f)))
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
                debugKeyCaptureMessage = "Key 1 and key 2 must be different.";
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
            GUILayout.Label("px", GUILayout.Width(32f));
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
                return "None";
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
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                value = fallback;
            }

            return Mathf.Clamp(value, -PositionSafetyLimit, PositionSafetyLimit);
        }

        private static KeyCode SanitizeKeyCode(KeyCode value, KeyCode fallback)
        {
            return value != KeyCode.None && Enum.IsDefined(typeof(KeyCode), value)
                ? value
                : fallback;
        }
    }
}
