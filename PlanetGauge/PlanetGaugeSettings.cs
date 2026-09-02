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

        private static readonly string[] SkinFillDirectionLabels =
        {
            "Horizontal (left to right)",
            "Vertical (bottom to top)"
        };

        private string frameOffsetXInput;
        private string frameOffsetYInput;
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
            MainGaugeOffsetX = DrawFloatSlider(
                "X",
                MainGaugeOffsetX,
                -500f,
                500f);
            MainGaugeOffsetY = DrawFloatSlider(
                "Y",
                MainGaugeOffsetY,
                -300f,
                300f);

            GUILayout.BeginHorizontal();
            GUILayout.Space(96f);
            if (GUILayout.Button("Reset position", GUILayout.Width(140f)))
            {
                MainGaugeOffsetX = 0f;
                MainGaugeOffsetY = -158f;
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
            MainGaugeOffsetX = SanitizeFloat(MainGaugeOffsetX, 0f, -500f, 500f);
            MainGaugeOffsetY = SanitizeFloat(MainGaugeOffsetY, -158f, -300f, 300f);
            MainGaugeSizePercent = SanitizeFloat(MainGaugeSizePercent, 100f, 25f, 200f);
            MainGaugeWidthPercent = SanitizeFloat(MainGaugeWidthPercent, 83f, 25f, 100f);
            MainGaugeValueOffsetX = SanitizeFloat(MainGaugeValueOffsetX, 0f, -500f, 500f);
            MainGaugeValueOffsetY = SanitizeFloat(MainGaugeValueOffsetY, -14f, -300f, 300f);
            MainGaugeValueSizePercent = SanitizeFloat(MainGaugeValueSizePercent, 111f, 50f, 200f);
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

        private static KeyCode SanitizeKeyCode(KeyCode value, KeyCode fallback)
        {
            return value != KeyCode.None && Enum.IsDefined(typeof(KeyCode), value)
                ? value
                : fallback;
        }
    }
}
