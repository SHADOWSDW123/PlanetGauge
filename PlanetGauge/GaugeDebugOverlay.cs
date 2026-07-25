using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlanetGauge
{
    internal sealed class GaugeDebugOverlay : IDisposable
    {
        private readonly GameObject canvasObject;
        private readonly TextMeshProUGUI text;

        private float lastValue = float.NaN;

        internal GaugeDebugOverlay(Transform parent)
        {
            canvasObject = new GameObject(
                "PlanetGauge.DebugOverlay",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10001;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            GameObject textObject = new GameObject(
                "GaugeText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(canvasObject.transform, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -24f);
            rect.sizeDelta = new Vector2(420f, 90f);

            text = textObject.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.TopLeft;
            text.fontSize = 30f;
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;

            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            canvasObject.SetActive(false);
        }

        internal void Update()
        {
            bool visible = ShouldBeVisible();
            if (canvasObject.activeSelf != visible)
            {
                canvasObject.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            float value = GaugeRuntime.Current;
            if (!Mathf.Approximately(value, lastValue))
            {
                lastValue = value;
                text.text = "PLANET GAUGE [DEBUG]\n" + value.ToString("0.0");
            }
        }

        public void Dispose()
        {
            if (canvasObject != null)
            {
                UnityEngine.Object.Destroy(canvasObject);
            }
        }

        private static bool ShouldBeVisible()
        {
            if (!Main.IsEnabled || !Main.EditorGaugeEnabled)
            {
                return false;
            }

            scnEditor editor = scnEditor.instance;
            scrController controller = scrController.instance;
            return editor != null
                && controller != null
                && controller.gameworld
                && !controller.paused
                && scrPlayerManager.playerCount == 1;
        }
    }
}
