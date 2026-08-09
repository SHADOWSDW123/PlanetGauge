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
        public float MainGaugeOffsetX;
        public float MainGaugeOffsetY = -158f;
        public float MainGaugeWidthPercent = 83f;

        public float MainGaugeValueOffsetX;
        public float MainGaugeValueOffsetY = -14f;
        public float MainGaugeValueSizePercent = 111f;
        public bool MainGaugeShowDecimalValue;

        public int MainGaugeColorR = 255;
        public int MainGaugeColorG = 255;
        public int MainGaugeColorB = 255;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

        internal void DrawGui()
        {
            // UMM의 OnGUI에서 매 프레임 호출되는 즉시 모드(IMGUI) 설정 화면이다.
            GUILayout.Label("Main gauge size");
            MainGaugeWidthPercent = DrawFloatSlider(
                "Width",
                MainGaugeWidthPercent,
                25f,
                100f,
                "%");

            GUILayout.BeginHorizontal();
            GUILayout.Space(96f);
            if (GUILayout.Button("Reset width", GUILayout.Width(140f)))
            {
                MainGaugeWidthPercent = 83f;
            }
            GUILayout.EndHorizontal();

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
            MainGaugeOffsetX = Mathf.Clamp(MainGaugeOffsetX, -500f, 500f);
            MainGaugeOffsetY = Mathf.Clamp(MainGaugeOffsetY, -300f, 300f);
            MainGaugeWidthPercent = Mathf.Clamp(
                MainGaugeWidthPercent,
                25f,
                100f);
            MainGaugeValueOffsetX = Mathf.Clamp(
                MainGaugeValueOffsetX,
                -500f,
                500f);
            MainGaugeValueOffsetY = Mathf.Clamp(
                MainGaugeValueOffsetY,
                -300f,
                300f);
            MainGaugeValueSizePercent = Mathf.Clamp(
                MainGaugeValueSizePercent,
                50f,
                200f);
            MainGaugeColorR = Mathf.Clamp(MainGaugeColorR, 0, 255);
            MainGaugeColorG = Mathf.Clamp(MainGaugeColorG, 0, 255);
            MainGaugeColorB = Mathf.Clamp(MainGaugeColorB, 0, 255);
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
    }
}
