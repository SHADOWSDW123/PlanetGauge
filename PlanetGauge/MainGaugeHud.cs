using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlanetGauge
{
    internal sealed class MainGaugeHud : IDisposable
    {
        private const float BaseBarHeight = 18f;
        private const float BaseGapAboveMeter = 10f;
        private const float BaseTextGap = 5f;
        private const float BaseFontSize = 24f;
        private const float BaseChamferSize = 4f;

        private static readonly Color32 BorderColor = new Color32(0, 0, 0, 255);
        private static readonly Color32 DisabledColor = new Color32(184, 184, 184, 255);
        private static readonly Color32 DepletedColor = new Color32(0, 0, 0, 240);

        private readonly Vector3[] meterWorldCorners = new Vector3[4];

        private scrHitErrorMeter sourceMeter;
        private GameObject rootObject;
        private RectTransform rootRect;
        private GaugeBarGraphic gaugeGraphic;
        private TextMeshProUGUI valueText;
        private RectTransform valueTextRect;
        private Outline valueOutline;

        private Color32 lastGaugeColor;
        private bool hasLastGaugeColor;
        private string lastDisplayedValue;

        internal void Update()
        {
            scrHitErrorMeter meter;
            RectTransform meterRect;
            if (!TryGetVisibleMeter(out meter, out meterRect))
            {
                SetVisible(false);
                return;
            }

            EnsureCreated(meter);
            if (rootObject == null || rootRect == null || gaugeGraphic == null)
            {
                return;
            }

            SetVisible(true);
            UpdateStyle();
            UpdateLayout(meter, meterRect);
            UpdateValue();
        }

        public void Dispose()
        {
            if (rootObject != null)
            {
                UnityEngine.Object.Destroy(rootObject);
            }

            sourceMeter = null;
            rootObject = null;
            rootRect = null;
            gaugeGraphic = null;
            valueText = null;
            valueTextRect = null;
            valueOutline = null;
        }

        private void EnsureCreated(scrHitErrorMeter meter)
        {
            Transform desiredParent = meter.scaler != null
                ? meter.scaler.transform
                : meter.wrapperRectTransform.parent;
            if (desiredParent == null)
            {
                return;
            }

            if (sourceMeter == meter
                && rootObject != null
                && rootObject.transform.parent == desiredParent)
            {
                rootObject.transform.SetAsLastSibling();
                return;
            }

            Dispose();
            sourceMeter = meter;

            rootObject = new GameObject(
                "PlanetGauge.MainGauge",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(GaugeBarGraphic),
                typeof(LayoutElement));
            rootObject.transform.SetParent(desiredParent, false);
            rootObject.transform.SetAsLastSibling();

            rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.localRotation = Quaternion.identity;
            rootRect.localScale = Vector3.one;

            LayoutElement layoutElement = rootObject.GetComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            gaugeGraphic = rootObject.GetComponent<GaugeBarGraphic>();
            gaugeGraphic.raycastTarget = false;
            gaugeGraphic.SetChamferSize(BaseChamferSize);

            CreateValueText();
            hasLastGaugeColor = false;
            lastDisplayedValue = null;
        }

        private void CreateValueText()
        {
            GameObject textObject = new GameObject(
                "GaugeValue",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI),
                typeof(Outline));
            textObject.transform.SetParent(rootObject.transform, false);

            valueTextRect = textObject.GetComponent<RectTransform>();
            valueTextRect.anchorMin = new Vector2(0.5f, 0.5f);
            valueTextRect.anchorMax = new Vector2(0.5f, 0.5f);
            valueTextRect.pivot = new Vector2(0.5f, 0.5f);

            valueText = textObject.GetComponent<TextMeshProUGUI>();
            valueText.alignment = TextAlignmentOptions.Center;
            valueText.fontStyle = FontStyles.Bold;
            valueText.color = Color.white;
            valueText.textWrappingMode = TextWrappingModes.NoWrap;
            valueText.overflowMode = TextOverflowModes.Overflow;
            valueText.raycastTarget = false;

            if (TMP_Settings.defaultFontAsset != null)
            {
                valueText.font = TMP_Settings.defaultFontAsset;
            }

            valueOutline = textObject.GetComponent<Outline>();
            valueOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            valueOutline.useGraphicAlpha = true;
        }

        private void UpdateStyle()
        {
            PlanetGaugeSettings settings = Main.Settings ?? new PlanetGaugeSettings();
            Color32 gaugeColor = settings.GetMainGaugeColor();
            if (hasLastGaugeColor && lastGaugeColor.Equals(gaugeColor))
            {
                return;
            }

            lastGaugeColor = gaugeColor;
            hasLastGaugeColor = true;
            gaugeGraphic.SetStyle(
                BorderColor,
                DisabledColor,
                DepletedColor,
                gaugeColor,
                gaugeColor,
                gaugeColor,
                2f);
        }

        private void UpdateLayout(scrHitErrorMeter meter, RectTransform meterRect)
        {
            Transform parent = rootRect.parent;
            meterRect.GetWorldCorners(meterWorldCorners);

            float minimumX = float.PositiveInfinity;
            float maximumX = float.NegativeInfinity;
            float maximumY = float.NegativeInfinity;
            for (int index = 0; index < meterWorldCorners.Length; index++)
            {
                Vector3 localPoint = parent.InverseTransformPoint(meterWorldCorners[index]);
                minimumX = Mathf.Min(minimumX, localPoint.x);
                maximumX = Mathf.Max(maximumX, localPoint.x);
                maximumY = Mathf.Max(maximumY, localPoint.y);
            }

            float meterScale = Mathf.Clamp(Mathf.Abs(meter.meterScale), 0.5f, 2.5f);
            PlanetGaugeSettings settings = Main.Settings ?? new PlanetGaugeSettings();
            float widthScale = settings.MainGaugeWidthPercent / 100f;
            float barWidth = Mathf.Max(
                48f,
                (maximumX - minimumX) * widthScale);
            float barHeight = Mathf.Clamp(BaseBarHeight * meterScale, 14f, 36f);
            float gapAboveMeter = Mathf.Clamp(BaseGapAboveMeter * meterScale, 7f, 24f);

            float centerX = (minimumX + maximumX) * 0.5f + settings.MainGaugeOffsetX;
            float centerY = maximumY
                + gapAboveMeter
                + barHeight * 0.5f
                + settings.MainGaugeOffsetY;

            rootRect.sizeDelta = new Vector2(barWidth, barHeight);
            rootRect.localPosition = new Vector3(centerX, centerY, 0f);
            rootRect.localRotation = Quaternion.identity;
            rootRect.localScale = Vector3.one;

            float textSizeScale = settings.MainGaugeValueSizePercent / 100f;
            float fontSize = Mathf.Clamp(
                BaseFontSize * meterScale * textSizeScale,
                9f,
                84f);
            float textHeight = fontSize + 10f;
            valueText.fontSize = fontSize;
            valueTextRect.sizeDelta = new Vector2(
                Mathf.Max(barWidth, fontSize * 5f),
                textHeight);
            valueTextRect.anchoredPosition = new Vector2(
                settings.MainGaugeValueOffsetX,
                barHeight * 0.5f
                    + BaseTextGap * meterScale
                    + textHeight * 0.5f
                    + settings.MainGaugeValueOffsetY);
            valueOutline.effectDistance = new Vector2(
                Mathf.Clamp(meterScale, 1f, 2f),
                -Mathf.Clamp(meterScale, 1f, 2f));

            gaugeGraphic.SetChamferSize(
                Mathf.Clamp(BaseChamferSize * meterScale, 3f, 8f));
        }

        private void UpdateValue()
        {
            float normalizedValue = GaugeRuntime.MaximumGauge <= 0f
                ? 0f
                : GaugeRuntime.Current / GaugeRuntime.MaximumGauge;
            gaugeGraphic.SetState(true, normalizedValue);

            float displayValue = Mathf.Clamp(
                GaugeRuntime.Current,
                0f,
                GaugeRuntime.MaximumGauge);
            PlanetGaugeSettings settings = Main.Settings ?? new PlanetGaugeSettings();
            string formattedValue = settings.MainGaugeShowDecimalValue
                ? displayValue.ToString("0.0", CultureInfo.InvariantCulture)
                : Mathf.RoundToInt(displayValue).ToString(
                    CultureInfo.InvariantCulture);
            if (!string.Equals(
                lastDisplayedValue,
                formattedValue,
                StringComparison.Ordinal))
            {
                lastDisplayedValue = formattedValue;
                valueText.text = formattedValue;
            }
        }

        private void SetVisible(bool visible)
        {
            if (rootObject != null && rootObject.activeSelf != visible)
            {
                rootObject.SetActive(visible);
            }
        }

        private static bool TryGetVisibleMeter(
            out scrHitErrorMeter meter,
            out RectTransform meterRect)
        {
            meter = null;
            meterRect = null;

            if (!GaugeRuntime.ShouldHandle())
            {
                return false;
            }

            scrController controller = scrController.instance;
            if (controller == null || controller.errorMeter == null)
            {
                return false;
            }

            meter = controller.errorMeter;
            if (!meter.gameObject.activeInHierarchy
                || Persistence.hitErrorMeterSize == ErrorMeterSize.Off)
            {
                return false;
            }

            if (meter.straightMeter != null && meter.straightMeter.activeInHierarchy)
            {
                meterRect = meter.straightMeter.GetComponent<RectTransform>();
            }
            else if (meter.curvedMeter != null && meter.curvedMeter.activeInHierarchy)
            {
                meterRect = meter.curvedMeter.GetComponent<RectTransform>();
            }

            if (meterRect == null)
            {
                meterRect = meter.wrapperRectTransform;
            }

            return meterRect != null;
        }
    }
}
