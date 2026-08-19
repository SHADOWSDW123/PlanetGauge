using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlanetGauge
{
    /// <summary>
    /// 플레이 중 판정 오차 미터 위에 표시되는 메인 게이지의 생성, 배치, 값 갱신을 담당한다.
    /// 장면 객체를 소유하므로 호스트가 파괴될 때 반드시 <see cref="Dispose"/>해야 한다.
    /// </summary>
    internal sealed class MainGaugeHud : IDisposable
    {
        private const float BaseBarHeight = 18f;
        private const float BaseGapAboveMeter = 10f;
        private const float BaseTextGap = 5f;
        private const float BaseEffectTextGap = 2f;
        private const float BaseFontSize = 24f;
        private const float EffectFontSizeRatio = 0.46f;
        private const float RateFontSizeRatio = 0.54f;
        private const float ColorTransitionDuration = 0.5f;
        private const float BaseChamferSize = 4f;

        private static readonly Color32 BorderColor = new Color32(0, 0, 0, 255);
        private static readonly Color32 DisabledColor = new Color32(184, 184, 184, 255);
        private static readonly Color32 DepletedColor = new Color32(0, 0, 0, 240);

        /*
         * 이벤트 상태별 HUD 강조색. 색상을 조정하려면 아래 RGB 값만 변경하면 된다.
         * 앞의 네 값은 사용자와 합의한 HEX 색상이며, 마지막 두 값은 각각 짙은 파랑과 보라다.
         */
        private static readonly Color32 BlockRecoveryColor = new Color32(176, 32, 32, 255);       // #B02020
        private static readonly Color32 AmplifyIncreaseColor = new Color32(69, 214, 107, 255);    // #45D66B
        private static readonly Color32 AmplifyDecreaseColor = new Color32(255, 159, 28, 255);    // #FF9F1C
        private static readonly Color32 AmplifyBothColor = new Color32(255, 227, 110, 255);       // #FFE36E
        private static readonly Color32 ReduceIncreaseColor = new Color32(60, 207, 207, 255);     // #3CCFCF
        private static readonly Color32 ReduceDecreaseColor = new Color32(93, 173, 226, 255);     // #5DADE2
        private static readonly Color32 ReduceBothColor = new Color32(183, 148, 244, 255);        // #B794F4
        private static readonly Color32 NoFailDisabledColor = new Color32(40, 80, 167, 255);      // #2850A7
        private static readonly Color32 IncreaseLimitedColor = new Color32(155, 89, 208, 255);    // #9B59D0

        private readonly Vector3[] meterWorldCorners = new Vector3[4];

        private scrHitErrorMeter sourceMeter;
        private GameObject rootObject;
        private RectTransform rootRect;
        private GaugeBarGraphic gaugeGraphic;
        private TextMeshProUGUI valueText;
        private RectTransform valueTextRect;
        private Outline valueOutline;
        private TextMeshProUGUI rateText;
        private RectTransform rateTextRect;
        private Outline rateOutline;
        private TextMeshProUGUI effectText;
        private RectTransform effectTextRect;
        private Outline effectOutline;

        private Color32 lastUserGaugeColor;
        private int lastStyleRevision = -1;
        private bool hasLastStyle;
        private int activeEffectCount;
        private string lastDisplayedValue;
        private Color currentGaugeColor;
        private Color transitionStartColor;
        private Color transitionTargetColor;
        private float transitionElapsed;

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
            // 스타일은 값 캐시로 변경 시에만 메시를 갱신하고, 레이아웃은 게임 UI를 따라 매 프레임 계산한다.
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
            rateText = null;
            rateTextRect = null;
            rateOutline = null;
            effectText = null;
            effectTextRect = null;
            effectOutline = null;
            hasLastStyle = false;
            activeEffectCount = 0;
        }

        private void EnsureCreated(scrHitErrorMeter meter)
        {
            // scaler 아래에 두어 게임의 판정 미터 확대/축소와 동일한 좌표계를 사용한다.
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
                // 다른 HUD가 런타임에 추가되어도 게이지가 가려지지 않게 렌더 순서를 복구한다.
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
            CreateRateText();
            CreateEffectText();
            hasLastStyle = false;
            activeEffectCount = 0;
            lastDisplayedValue = null;
            currentGaugeColor = Main.Settings.GetMainGaugeColor();
            transitionStartColor = currentGaugeColor;
            transitionTargetColor = currentGaugeColor;
            transitionElapsed = ColorTransitionDuration;
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

        private void CreateEffectText()
        {
            GameObject textObject = new GameObject(
                "GaugeEffects",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI),
                typeof(Outline));
            textObject.transform.SetParent(rootObject.transform, false);

            effectTextRect = textObject.GetComponent<RectTransform>();
            effectTextRect.anchorMin = new Vector2(0.5f, 0.5f);
            effectTextRect.anchorMax = new Vector2(0.5f, 0.5f);
            effectTextRect.pivot = new Vector2(0.5f, 0.5f);

            effectText = textObject.GetComponent<TextMeshProUGUI>();
            effectText.alignment = TextAlignmentOptions.Center;
            effectText.fontStyle = FontStyles.Bold;
            effectText.color = Color.white;
            effectText.richText = true;
            effectText.textWrappingMode = TextWrappingModes.NoWrap;
            effectText.overflowMode = TextOverflowModes.Overflow;
            effectText.raycastTarget = false;
            effectText.enabled = false;

            if (TMP_Settings.defaultFontAsset != null)
            {
                effectText.font = TMP_Settings.defaultFontAsset;
            }

            effectOutline = textObject.GetComponent<Outline>();
            effectOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            effectOutline.useGraphicAlpha = true;
        }

        private void CreateRateText()
        {
            GameObject textObject = new GameObject(
                "GaugeRates",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI),
                typeof(Outline));
            textObject.transform.SetParent(rootObject.transform, false);

            rateTextRect = textObject.GetComponent<RectTransform>();
            rateTextRect.anchorMin = new Vector2(0.5f, 0.5f);
            rateTextRect.anchorMax = new Vector2(0.5f, 0.5f);
            rateTextRect.pivot = new Vector2(0.5f, 0.5f);

            rateText = textObject.GetComponent<TextMeshProUGUI>();
            rateText.alignment = TextAlignmentOptions.Center;
            rateText.fontStyle = FontStyles.Bold;
            rateText.richText = true;
            rateText.textWrappingMode = TextWrappingModes.NoWrap;
            rateText.overflowMode = TextOverflowModes.Overflow;
            rateText.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null)
            {
                rateText.font = TMP_Settings.defaultFontAsset;
            }

            rateOutline = textObject.GetComponent<Outline>();
            rateOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            rateOutline.useGraphicAlpha = true;
        }

        private void UpdateStyle()
        {
            PlanetGaugeSettings settings = Main.Settings;
            PlanetGaugeEventSettings eventSettings = GaugeRuntime.EventSettings;
            Color32 userGaugeColor = settings.GetMainGaugeColor();
            bool styleChanged = !hasLastStyle
                || !lastUserGaugeColor.Equals(userGaugeColor)
                || lastStyleRevision != GaugeRuntime.StyleRevision;
            if (styleChanged)
            {
                lastUserGaugeColor = userGaugeColor;
                lastStyleRevision = GaugeRuntime.StyleRevision;
                hasLastStyle = true;

                transitionStartColor = currentGaugeColor;
                transitionTargetColor = ResolveGaugeColor(eventSettings, userGaugeColor);
                transitionElapsed = 0f;

                string effects = BuildEffectText(eventSettings, out activeEffectCount);
                effectText.text = effects;
                effectText.enabled = activeEffectCount > 0;
                rateText.text = BuildRateText(eventSettings);
                rateText.enabled = !string.IsNullOrEmpty(rateText.text);
            }

            transitionElapsed = Mathf.Min(
                ColorTransitionDuration,
                transitionElapsed + Time.unscaledDeltaTime);
            float progress = ColorTransitionDuration <= 0f
                ? 1f
                : transitionElapsed / ColorTransitionDuration;
            float eased = 1f - (1f - progress) * (1f - progress);
            currentGaugeColor = Color.Lerp(transitionStartColor, transitionTargetColor, eased);
            gaugeGraphic.SetStyle(
                BorderColor, DisabledColor, DepletedColor,
                currentGaugeColor, currentGaugeColor, currentGaugeColor, 2f);
            valueText.color = currentGaugeColor;
        }

        private void UpdateLayout(scrHitErrorMeter meter, RectTransform meterRect)
        {
            Transform parent = rootRect.parent;
            meterRect.GetWorldCorners(meterWorldCorners);

            // 월드 모서리를 게이지 부모의 로컬 좌표로 변환해 해상도와 Canvas 스케일 변화에 대응한다.
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
            PlanetGaugeSettings settings = Main.Settings;
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
            float rateFontSize = Mathf.Clamp(fontSize * RateFontSizeRatio, 8f, 40f);
            float rateHeight = rateFontSize + 2f;
            float visibleRateHeight = rateText.enabled ? rateHeight : 0f;
            valueText.fontSize = fontSize;
            valueTextRect.sizeDelta = new Vector2(
                Mathf.Max(barWidth, fontSize * 5f),
                textHeight);
            valueTextRect.anchoredPosition = new Vector2(
                settings.MainGaugeValueOffsetX,
                barHeight * 0.5f
                    + BaseTextGap * meterScale
                    + visibleRateHeight
                    + textHeight * 0.5f
                    + settings.MainGaugeValueOffsetY);
            valueOutline.effectDistance = new Vector2(
                Mathf.Clamp(meterScale, 1f, 2f),
                -Mathf.Clamp(meterScale, 1f, 2f));

            rateText.fontSize = rateFontSize;
            rateTextRect.sizeDelta = new Vector2(
                Mathf.Max(barWidth, rateFontSize * 9f),
                rateHeight);
            rateTextRect.anchoredPosition = new Vector2(
                settings.MainGaugeValueOffsetX,
                valueTextRect.anchoredPosition.y
                    - textHeight * 0.5f
                    - rateHeight * 0.5f);
            rateOutline.effectDistance = new Vector2(
                Mathf.Clamp(meterScale * 0.7f, 1f, 1.5f),
                -Mathf.Clamp(meterScale * 0.7f, 1f, 1.5f));

            float effectFontSize = Mathf.Clamp(
                fontSize * EffectFontSizeRatio,
                8f,
                36f);
            float effectLineHeight = effectFontSize + 2f;
            float effectHeight = effectLineHeight * Mathf.Max(activeEffectCount, 1);
            effectText.fontSize = effectFontSize;
            effectTextRect.sizeDelta = new Vector2(
                Mathf.Max(barWidth, effectFontSize * 18f),
                effectHeight);
            effectTextRect.anchoredPosition = new Vector2(
                settings.MainGaugeValueOffsetX,
                valueTextRect.anchoredPosition.y
                    + textHeight * 0.5f
                    + BaseEffectTextGap * meterScale
                    + effectHeight * 0.5f);
            effectOutline.effectDistance = new Vector2(
                Mathf.Clamp(meterScale * 0.75f, 1f, 1.5f),
                -Mathf.Clamp(meterScale * 0.75f, 1f, 1.5f));

            gaugeGraphic.SetChamferSize(
                Mathf.Clamp(BaseChamferSize * meterScale, 3f, 8f));
        }

        private static Color32 ResolveGaugeColor(
            PlanetGaugeEventSettings settings,
            Color32 userGaugeColor)
        {
            /*
             * 여러 효과가 동시에 켜졌을 때 게이지와 숫자는 한 색만 가질 수 있다.
             * 우선순위를 바꾸려면 아래 검사 순서를 옮기면 된다. 모든 활성 효과 문구는 별도로 표시된다.
             */
            if (settings.RecoveryBlocked)
            {
                return BlockRecoveryColor;
            }

            if (IsNonNeutral(settings.RecoveryRate))
            {
                return GetRateColor(settings.RecoveryRate);
            }

            if (IsNonNeutral(settings.DamageRate))
            {
                return GetRateColor(settings.DamageRate);
            }

            if (settings.RecoveryCapEnabled)
            {
                return IncreaseLimitedColor;
            }

            if (!settings.FailureProtection)
            {
                return NoFailDisabledColor;
            }

            return userGaugeColor;
        }

        private static string BuildEffectText(
            PlanetGaugeEventSettings settings,
            out int effectCount)
        {
            string result = string.Empty;
            effectCount = 0;

            if (settings.RecoveryBlocked)
            {
                AppendEffect(ref result, ref effectCount, BlockRecoveryColor, "Increase Disabled");
            }

            bool combinedBoth = IsSameBothChannel(settings.RecoveryRate, settings.DamageRate);
            if (combinedBoth && IsNonNeutral(settings.RecoveryRate))
            {
                AppendEffect(
                    ref result,
                    ref effectCount,
                    GetRateColor(settings.RecoveryRate),
                    settings.RecoveryRate.Percent < 100f ? "Rate Reduced" : "Rate Amplified");
            }
            else
            {
                AppendRateEffect(ref result, ref effectCount, settings.RecoveryRate, "Increase");
                AppendRateEffect(ref result, ref effectCount, settings.DamageRate, "Decrease");
            }

            if (!settings.FailureProtection)
            {
                AppendEffect(ref result, ref effectCount, NoFailDisabledColor, "No-Fail Disabled");
            }

            if (settings.RecoveryCapEnabled)
            {
                AppendEffect(ref result, ref effectCount, IncreaseLimitedColor, "Increase Limited");
            }

            return result;
        }

        private static string BuildRateText(PlanetGaugeEventSettings settings)
        {
            bool combinedBoth = IsSameBothChannel(settings.RecoveryRate, settings.DamageRate);
            if (combinedBoth && IsNonNeutral(settings.RecoveryRate))
            {
                return FormatRateToken(settings.RecoveryRate);
            }

            string result = string.Empty;
            if (IsNonNeutral(settings.RecoveryRate))
            {
                result = FormatRateToken(settings.RecoveryRate);
            }
            if (IsNonNeutral(settings.DamageRate))
            {
                if (result.Length > 0) result += "  ";
                result += FormatRateToken(settings.DamageRate);
            }
            return result;
        }

        private static string FormatRateToken(PlanetGaugeRateChannel channel)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(GetRateColor(channel)) + ">"
                + channel.Percent.ToString("0.#", CultureInfo.InvariantCulture) + "%</color>";
        }

        private static void AppendRateEffect(
            ref string text,
            ref int effectCount,
            PlanetGaugeRateChannel channel,
            string prefix)
        {
            if (!IsNonNeutral(channel)) return;
            AppendEffect(
                ref text,
                ref effectCount,
                GetRateColor(channel),
                prefix + (channel.Percent < 100f ? " Reduced" : " Amplified"));
        }

        private static bool IsNonNeutral(PlanetGaugeRateChannel channel)
        {
            return channel.Enabled && !Mathf.Approximately(channel.Percent, 100f);
        }

        private static bool IsSameBothChannel(
            PlanetGaugeRateChannel recovery,
            PlanetGaugeRateChannel damage)
        {
            return recovery.Enabled && damage.Enabled
                && recovery.Source == PlanetGaugeRateSource.Both
                && damage.Source == PlanetGaugeRateSource.Both
                && Mathf.Approximately(recovery.Percent, damage.Percent);
        }

        private static Color32 GetRateColor(PlanetGaugeRateChannel channel)
        {
            bool reduced = channel.Percent < 100f;
            switch (channel.Source)
            {
                case PlanetGaugeRateSource.Increase:
                    return reduced ? ReduceIncreaseColor : AmplifyIncreaseColor;
                case PlanetGaugeRateSource.Decrease:
                    return reduced ? ReduceDecreaseColor : AmplifyDecreaseColor;
                case PlanetGaugeRateSource.Both:
                    return reduced ? ReduceBothColor : AmplifyBothColor;
                default:
                    return reduced ? ReduceBothColor : AmplifyBothColor;
            }
        }

        private static void AppendEffect(
            ref string text,
            ref int effectCount,
            Color32 color,
            string label)
        {
            if (effectCount > 0)
            {
                text += "\n";
            }

            text += "<color=#"
                + ColorUtility.ToHtmlStringRGB(color)
                + ">"
                + label
                + "</color>";
            effectCount++;
        }

        private void UpdateValue()
        {
            float normalizedValue = GaugeRuntime.RecoveryMaximum <= 0f
                ? 0f 
                : GaugeRuntime.Current / GaugeRuntime.RecoveryMaximum;
            gaugeGraphic.SetState(true, normalizedValue);

            float displayValue = Mathf.Max(0f, GaugeRuntime.Current);
            PlanetGaugeSettings settings = Main.Settings;
            string formattedValue = settings.MainGaugeShowDecimalValue
                ? displayValue.ToString("0.0", CultureInfo.InvariantCulture)
                : Mathf.RoundToInt(displayValue).ToString(
                    CultureInfo.InvariantCulture);
            if (!string.Equals(
                lastDisplayedValue,
                formattedValue,
                StringComparison.Ordinal))
            {
                // TMP 텍스트 재빌드는 비용이 있으므로 표시 문자열이 달라질 때만 할당한다.
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
                // 게임 버전별 세부 미터 구조가 달라도 공통 wrapper를 최후 기준점으로 사용한다.
                meterRect = meter.wrapperRectTransform;
            }

            return meterRect != null;
        }
    }
}
