using System;
using System.Collections.Generic;
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
        private const float DefaultValueOffsetY = -14f;
        private const float CompactSpacingRatio = 2f / 3f;
        private const float BaseFontSize = 24f;
        private const float DefaultValueSizeScale = 1.11f;
        private const float EffectFontSizeRatio = 0.46f;
        private const float RateFontSizeRatio = 0.54f;
        private const float ColorTransitionDuration = 0.5f;
        private const float BaseChamferSize = 4f;
        private const int HudSortingOrder = short.MaxValue - 1;
        private static readonly Vector2 HudReferenceResolution = new Vector2(1920f, 1080f);
        private static readonly Vector2 FallbackMeterSize = new Vector2(600f, 24f);

        private static readonly Color32 BorderColor = new Color32(0, 0, 0, 255);
        private static readonly Color32 DisabledColor = new Color32(184, 184, 184, 255);
        private static readonly Color32 DepletedColor = new Color32(0, 0, 0, 240);
        private static readonly Color32 BlindfoldGaugeColor = new Color32(0, 0, 0, 255);
        private static readonly Color32 BlindfoldTextColor = new Color32(230, 230, 230, 255);
        private static readonly Color32 BlindfoldEffectColor = new Color32(112, 112, 112, 255);

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
        private readonly List<GaugeOverlaySegment> warningSegments =
            new List<GaugeOverlaySegment>();

        private GameObject canvasObject;
        private RectTransform canvasRect;
        private CanvasScaler canvasScaler;
        private CanvasScaler sourceCanvasScaler;
        private GameObject rootObject;
        private RectTransform rootRect;
        private RectTransform screenReferenceRect;
        private GaugeBarGraphic gaugeGraphic;
        private GaugeSkinRenderer skinRenderer;
        private TextMeshProUGUI valueText;
        private RectTransform valueTextRect;
        private Outline valueOutline;
        private CanvasGroup valueCanvasGroup;
        private TextMeshProUGUI rateText;
        private RectTransform rateTextRect;
        private Outline rateOutline;
        private CanvasGroup rateCanvasGroup;
        private TextMeshProUGUI effectText;
        private RectTransform effectTextRect;
        private Outline effectOutline;
        private CanvasGroup effectCanvasGroup;

        private Color32 lastUserGaugeColor;
        private int lastStyleRevision = -1;
        private int lastLocalizationRevision = -1;
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
            if (!TryGetLayoutReference(out meter, out meterRect))
            {
                SetVisible(false);
                return;
            }

            EnsureCreated();
            if (rootObject == null || rootRect == null || gaugeGraphic == null)
            {
                return;
            }

            SetVisible(true);
            // 스타일은 값 캐시로 변경 시에만 메시를 갱신하고, 레이아웃은 게임 UI를 따라 매 프레임 계산한다.
            UpdateStyle();
            UpdateLayout(meter, meterRect);
            UpdateValue();
            UpdateVisibility();
        }

        public void Dispose()
        {
            if (canvasObject != null)
            {
                UnityEngine.Object.Destroy(canvasObject);
            }

            canvasObject = null;
            canvasRect = null;
            canvasScaler = null;
            sourceCanvasScaler = null;
            rootObject = null;
            rootRect = null;
            screenReferenceRect = null;
            gaugeGraphic = null;
            skinRenderer = null;
            valueText = null;
            valueTextRect = null;
            valueOutline = null;
            valueCanvasGroup = null;
            rateText = null;
            rateTextRect = null;
            rateOutline = null;
            rateCanvasGroup = null;
            effectText = null;
            effectTextRect = null;
            effectOutline = null;
            effectCanvasGroup = null;
            hasLastStyle = false;
            activeEffectCount = 0;
            warningSegments.Clear();
        }

        private void EnsureCreated()
        {
            if (canvasObject != null && rootObject != null)
            {
                return;
            }

            Dispose();

            canvasObject = new GameObject(
                "PlanetGauge.MainHudCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            UnityEngine.Object.DontDestroyOnLoad(canvasObject);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = HudSortingOrder;

            canvasScaler = canvasObject.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = HudReferenceResolution;
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;
            canvasObject.GetComponent<GraphicRaycaster>().enabled = false;
            canvasRect = canvasObject.transform as RectTransform;

            rootObject = new GameObject(
                "PlanetGauge.MainGauge",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(GaugeBarGraphic),
                typeof(LayoutElement));
            rootObject.transform.SetParent(canvasObject.transform, false);

            rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.localRotation = Quaternion.identity;
            rootRect.localScale = Vector3.one;

            screenReferenceRect = canvasRect;

            LayoutElement layoutElement = rootObject.GetComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            gaugeGraphic = rootObject.GetComponent<GaugeBarGraphic>();
            gaugeGraphic.raycastTarget = false;
            gaugeGraphic.SetChamferSize(BaseChamferSize);

            skinRenderer = new GaugeSkinRenderer(rootObject.transform);
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
                typeof(Outline),
                typeof(CanvasGroup));
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
            valueCanvasGroup = textObject.GetComponent<CanvasGroup>();
        }

        private void CreateEffectText()
        {
            GameObject textObject = new GameObject(
                "GaugeEffects",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI),
                typeof(Outline),
                typeof(CanvasGroup));
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
            effectCanvasGroup = textObject.GetComponent<CanvasGroup>();
        }

        private void CreateRateText()
        {
            GameObject textObject = new GameObject(
                "GaugeRates",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI),
                typeof(Outline),
                typeof(CanvasGroup));
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
            rateCanvasGroup = textObject.GetComponent<CanvasGroup>();
        }

        private void UpdateStyle()
        {
            PlanetGaugeSettings settings = Main.Settings;
            PlanetGaugeEventSettings eventSettings = GaugeRuntime.EventSettings;
            Color32 userGaugeColor = settings.GetMainGaugeColor();
            bool styleChanged = !hasLastStyle
                || !lastUserGaugeColor.Equals(userGaugeColor)
                || lastStyleRevision != GaugeRuntime.StyleRevision
                || lastLocalizationRevision != LocalizedStrings.Revision;
            if (styleChanged)
            {
                lastUserGaugeColor = userGaugeColor;
                lastStyleRevision = GaugeRuntime.StyleRevision;
                lastLocalizationRevision = LocalizedStrings.Revision;
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
            valueText.color = GaugeRuntime.IsBlindfolded
                ? (Color)BlindfoldTextColor
                : currentGaugeColor;
        }

        private void UpdateLayout(scrHitErrorMeter meter, RectTransform meterRect)
        {
            SyncCanvasScaler(meter);

            float minimumX;
            float maximumX;
            float maximumY;
            if (!TryGetMeterBounds(meterRect, out minimumX, out maximumX, out maximumY))
            {
                minimumX = -FallbackMeterSize.x * 0.5f;
                maximumX = FallbackMeterSize.x * 0.5f;
                maximumY = FallbackMeterSize.y * 0.5f;
            }

            float meterScale = meter == null
                ? 1f
                : Mathf.Clamp(Mathf.Abs(meter.meterScale), 0.5f, 2.5f);
            PlanetGaugeSettings settings = Main.Settings;
            float gaugeScale = settings.MainGaugeSizePercent / 100f;
            float widthScale = settings.MainGaugeWidthPercent / 100f;
            float barWidth = Mathf.Max(
                48f * gaugeScale,
                (maximumX - minimumX) * widthScale * gaugeScale);
            GaugeSkinAsset skin = GaugeSkinManager.Current;
            float barHeight;
            if (skin != null && skin.ContentRect.Width > 0f && skin.ContentRect.Height > 0f)
            {
                barHeight = Mathf.Max(
                    1f,
                    barWidth * skin.ContentRect.Height / skin.ContentRect.Width);
            }
            else
            {
                barHeight = Mathf.Clamp(
                    BaseBarHeight * meterScale * gaugeScale,
                    5f,
                    72f);
            }
            float gapAboveMeter = Mathf.Clamp(
                BaseGapAboveMeter * meterScale * gaugeScale,
                2f,
                48f);

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
            float textHeight = fontSize + 4f;
            float rateFontSize = Mathf.Clamp(
                BaseFontSize
                    * DefaultValueSizeScale
                    * RateFontSizeRatio
                    * settings.RateTokenSizePercent / 100f,
                3f,
                240f);
            float rateHeight = rateFontSize + 1f;
            float compactTextGap = Mathf.Clamp(
                BaseTextGap * meterScale * CompactSpacingRatio,
                1f,
                8f);
            float textOffsetY = settings.MainGaugeValueOffsetY - DefaultValueOffsetY;
            valueText.fontSize = fontSize;
            valueTextRect.sizeDelta = new Vector2(
                Mathf.Max(barWidth, fontSize * 5f),
                textHeight);
            valueOutline.effectDistance = new Vector2(
                Mathf.Clamp(meterScale, 1f, 2f),
                -Mathf.Clamp(meterScale, 1f, 2f));

            rateText.fontSize = rateFontSize;
            rateTextRect.sizeDelta = new Vector2(
                Mathf.Max(barWidth, rateFontSize * 9f),
                rateHeight);
            rateOutline.effectDistance = new Vector2(
                Mathf.Clamp(rateFontSize / 14f, 1f, 5f),
                -Mathf.Clamp(rateFontSize / 14f, 1f, 5f));

            float effectFontSize = Mathf.Clamp(
                BaseFontSize
                    * DefaultValueSizeScale
                    * EffectFontSizeRatio
                    * settings.AttributeTextSizePercent / 100f,
                3f,
                240f);
            float effectLineHeight = (effectFontSize + 2f) * CompactSpacingRatio;
            float effectHeight = effectLineHeight * Mathf.Max(activeEffectCount, 1);
            effectText.fontSize = effectFontSize;
            effectText.lineSpacing = -effectFontSize * (1f - CompactSpacingRatio);
            effectTextRect.sizeDelta = new Vector2(
                Mathf.Max(48f, effectFontSize * 18f),
                effectHeight);
            effectOutline.effectDistance = new Vector2(
                Mathf.Clamp(effectFontSize / 12f, 1f, 5f),
                -Mathf.Clamp(effectFontSize / 12f, 1f, 5f));

            float attachedX = settings.MainGaugeValueOffsetX;
            float stackBottom = barHeight * 0.5f + compactTextGap + textOffsetY;
            bool hasAttachedElement = false;

            rateTextRect.pivot = new Vector2(0.5f, 0.5f);
            if (settings.RateTokenAttachedToMainGauge)
            {
                if (rateText.enabled)
                {
                    rateTextRect.anchoredPosition = new Vector2(
                        attachedX,
                        stackBottom + rateHeight * 0.5f);
                    stackBottom += rateHeight;
                    hasAttachedElement = true;
                }
            }
            else
            {
                SetScreenCenteredPosition(
                    rateTextRect,
                    settings.RateTokenScreenOffsetX,
                    settings.RateTokenScreenOffsetY);
            }

            valueTextRect.pivot = new Vector2(0.5f, 0.5f);
            if (settings.MainGaugeValueAttachedToMainGauge)
            {
                if (hasAttachedElement)
                {
                    stackBottom += compactTextGap;
                }
                valueTextRect.anchoredPosition = new Vector2(
                    attachedX,
                    stackBottom + textHeight * 0.5f);
                stackBottom += textHeight;
                hasAttachedElement = true;
            }
            else
            {
                SetScreenCenteredPosition(
                    valueTextRect,
                    settings.MainGaugeValueScreenOffsetX,
                    settings.MainGaugeValueScreenOffsetY);
            }

            effectTextRect.pivot = new Vector2(0.5f, 0.5f);
            if (settings.AttributeTextAttachedToMainGauge)
            {
                if (hasAttachedElement)
                {
                    stackBottom += BaseEffectTextGap * meterScale * CompactSpacingRatio;
                }
                effectTextRect.anchoredPosition = new Vector2(
                    attachedX,
                    stackBottom + effectHeight * 0.5f);
            }
            else
            {
                SetScreenCenteredPosition(
                    effectTextRect,
                    settings.AttributeTextScreenOffsetX,
                    settings.AttributeTextScreenOffsetY);
            }

            gaugeGraphic.SetChamferSize(
                Mathf.Clamp(BaseChamferSize * meterScale * gaugeScale, 1f, 16f));
        }

        private void SyncCanvasScaler(scrHitErrorMeter meter)
        {
            CanvasScaler source = meter != null ? meter.scaler : null;
            if (source == null || canvasScaler == null || source == sourceCanvasScaler)
            {
                return;
            }

            // 별도 Canvas를 소유하되 게임 HUD와 같은 배율 규칙을 사용해 기존 크기를 보존한다.
            sourceCanvasScaler = source;
            canvasScaler.uiScaleMode = source.uiScaleMode;
            canvasScaler.referencePixelsPerUnit = source.referencePixelsPerUnit;
            canvasScaler.scaleFactor = source.scaleFactor;
            canvasScaler.referenceResolution = source.referenceResolution;
            canvasScaler.screenMatchMode = source.screenMatchMode;
            canvasScaler.matchWidthOrHeight = source.matchWidthOrHeight;
            canvasScaler.physicalUnit = source.physicalUnit;
            canvasScaler.fallbackScreenDPI = source.fallbackScreenDPI;
            canvasScaler.defaultSpriteDPI = source.defaultSpriteDPI;
            canvasScaler.dynamicPixelsPerUnit = source.dynamicPixelsPerUnit;
        }

        private void SetScreenCenteredPosition(RectTransform target, float offsetX, float offsetY)
        {
            if (target == null || rootRect == null || screenReferenceRect == null)
            {
                return;
            }

            Vector2 screenCenter = screenReferenceRect.rect.center;
            Vector3 worldPoint = screenReferenceRect.TransformPoint(new Vector3(
                screenCenter.x + offsetX,
                screenCenter.y + offsetY,
                0f));
            Vector3 rootLocalPoint = rootRect.InverseTransformPoint(worldPoint);
            target.localPosition = new Vector3(rootLocalPoint.x, rootLocalPoint.y, 0f);
        }

        private bool TryGetMeterBounds(
            RectTransform meterRect,
            out float minimumX,
            out float maximumX,
            out float maximumY)
        {
            minimumX = float.PositiveInfinity;
            maximumX = float.NegativeInfinity;
            maximumY = float.NegativeInfinity;
            if (meterRect == null || canvasRect == null)
            {
                return false;
            }

            Canvas sourceCanvas = meterRect.GetComponentInParent<Canvas>();
            Camera sourceCamera = sourceCanvas != null
                && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? sourceCanvas.worldCamera
                : null;
            meterRect.GetWorldCorners(meterWorldCorners);
            for (int index = 0; index < meterWorldCorners.Length; index++)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
                    sourceCamera,
                    meterWorldCorners[index]);
                Vector2 localPoint;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPoint,
                    null,
                    out localPoint))
                {
                    return false;
                }

                minimumX = Mathf.Min(minimumX, localPoint.x);
                maximumX = Mathf.Max(maximumX, localPoint.x);
                maximumY = Mathf.Max(maximumY, localPoint.y);
            }

            return IsFinite(minimumX)
                && IsFinite(maximumX)
                && IsFinite(maximumY)
                && maximumX > minimumX;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void UpdateVisibility()
        {
            bool customSkin = GaugeSkinManager.Current != null;
            gaugeGraphic.SetVisibilityAlphas(
                customSkin ? 0f : GaugeHudVisibilityTransitions.GaugeBarAlpha,
                customSkin ? 0f : GaugeHudVisibilityTransitions.ForceRecoveryVisualsAlpha);
            SetCanvasGroupAlpha(
                valueCanvasGroup,
                GaugeHudVisibilityTransitions.GaugeValueAlpha);
            SetCanvasGroupAlpha(
                effectCanvasGroup,
                GaugeHudVisibilityTransitions.AttributeTextAlpha);
            SetCanvasGroupAlpha(
                rateCanvasGroup,
                GaugeHudVisibilityTransitions.RateTokenAlpha);
        }

        private static void SetCanvasGroupAlpha(CanvasGroup canvasGroup, float alpha)
        {
            float clampedAlpha = Mathf.Clamp01(alpha);
            if (canvasGroup != null && !Mathf.Approximately(canvasGroup.alpha, clampedAlpha))
            {
                canvasGroup.alpha = clampedAlpha;
            }
        }

        private static Color32 ResolveGaugeColor(
            PlanetGaugeEventSettings settings,
            Color32 userGaugeColor)
        {
            /*
             * 여러 효과가 동시에 켜졌을 때 게이지와 숫자는 한 색만 가질 수 있다.
             * 우선순위를 바꾸려면 아래 검사 순서를 옮기면 된다. 모든 활성 효과 문구는 별도로 표시된다.
             */
            if (GaugeRuntime.IsBlindfolded)
            {
                return BlindfoldGaugeColor;
            }

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

            if (settings.BlindfoldEnabled)
            {
                AppendEffect(ref result, ref effectCount, BlindfoldEffectColor, LocalizedStrings.Blindfolded);
            }

            if (settings.RecoveryBlocked)
            {
                AppendEffect(ref result, ref effectCount, BlockRecoveryColor, LocalizedStrings.IncreaseDisabled);
            }

            bool combinedBoth = IsSameBothChannel(settings.RecoveryRate, settings.DamageRate);
            if (combinedBoth && IsNonNeutral(settings.RecoveryRate))
            {
                AppendEffect(
                    ref result,
                    ref effectCount,
                    GetRateColor(settings.RecoveryRate),
                    settings.RecoveryRate.Percent < 100f
                        ? LocalizedStrings.RateReduced
                        : LocalizedStrings.RateAmplified);
            }
            else
            {
                AppendRateEffect(
                    ref result,
                    ref effectCount,
                    settings.RecoveryRate,
                    LocalizedStrings.Increase);
                AppendRateEffect(
                    ref result,
                    ref effectCount,
                    settings.DamageRate,
                    LocalizedStrings.Decrease);
            }

            if (!settings.FailureProtection)
            {
                AppendEffect(ref result, ref effectCount, NoFailDisabledColor, LocalizedStrings.NoFailDisabled);
            }

            if (settings.RecoveryCapEnabled)
            {
                AppendEffect(ref result, ref effectCount, IncreaseLimitedColor, LocalizedStrings.IncreaseLimited);
            }

            return result;
        }

        private static string BuildRateText(PlanetGaugeEventSettings settings)
        {
            bool combinedBoth = IsSameBothChannel(settings.RecoveryRate, settings.DamageRate);
            string result = string.Empty;
            if (IsNonNeutral(settings.RecoveryRate))
            {
                result = FormatRateToken(settings.RecoveryRate);
            }
            if (!combinedBoth && IsNonNeutral(settings.DamageRate))
            {
                if (result.Length > 0) result += "  ";
                result += FormatRateToken(settings.DamageRate);
            }
            if (settings.RecoveryCapEnabled)
            {
                if (result.Length > 0) result += "  ";
                result += "<color=#" + ColorUtility.ToHtmlStringRGB(IncreaseLimitedColor) + ">"
                    + PlanetGaugeValueRules.SanitizeRecoveryCap(settings.RecoveryCapPercent)
                        .ToString("0.#", CultureInfo.InvariantCulture) + "%</color>";
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
                LocalizedStrings.Format(
                    channel.Percent < 100f
                        ? LocalizedStrings.ReducedEffect
                        : LocalizedStrings.AmplifiedEffect,
                    prefix));
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
            bool customSkin = GaugeSkinManager.Current != null && skinRenderer != null;
            bool blindfolded = GaugeRuntime.IsBlindfolded;
            float blindfoldAlpha = GaugeVisualTransitions.BlindfoldAlpha;
            bool fullyBlindfolded = blindfolded && blindfoldAlpha >= 0.999f;
            bool hideForceVisuals = blindfolded || customSkin;
            float displayedCurrent = hideForceVisuals
                ? GaugeRuntime.Current
                : GaugeVisualTransitions.GetDisplayedCurrent();
            float normalizedValue = fullyBlindfolded
                ? 1f
                : GaugeRuntime.RecoveryMaximum <= 0f
                ? 0f 
                : displayedCurrent / GaugeRuntime.RecoveryMaximum;
            gaugeGraphic.SetBlindfoldOpacity(blindfoldAlpha);
            gaugeGraphic.SetState(true, normalizedValue);

            GaugeOverlaySegment transitionSegment;
            bool showTransition = false;
            warningSegments.Clear();
            if (!hideForceVisuals)
            {
                showTransition = GaugeVisualTransitions.TryGetTransitionSegment(
                    out transitionSegment);
                GaugeVisualTransitions.FillWarningSegments(warningSegments);
            }
            else
            {
                transitionSegment = default(GaugeOverlaySegment);
            }
            if (!customSkin)
            {
                gaugeGraphic.SetOverlayVertical(false);
                gaugeGraphic.SetOverlays(showTransition, transitionSegment, warningSegments);
                if (skinRenderer != null)
                {
                    skinRenderer.Update(rootRect, normalizedValue, 0f, 0f);
                }
            }
            else
            {
                gaugeGraphic.SetOverlays(false, default(GaugeOverlaySegment), null);
                skinRenderer.Update(
                    rootRect,
                    normalizedValue,
                    blindfoldAlpha,
                    GaugeHudVisibilityTransitions.GaugeBarAlpha);
            }

            float displayValue = Mathf.Max(0f, GaugeRuntime.Current);
            PlanetGaugeSettings settings = Main.Settings;
            string formattedValue;
            if (GaugeRuntime.IsBlindfolded)
            {
                formattedValue = "???";
            }
            else
            {
                if (settings.MainGaugeShowDecimalValue)
                {
                    formattedValue = displayValue.ToString("0.0", CultureInfo.InvariantCulture);
                }
                else
                {
                    int rounded = Mathf.RoundToInt(displayValue);
                    if (GaugeRuntime.Current > 0f && rounded == 0)
                    {
                        rounded = 1;
                    }

                    formattedValue = rounded.ToString(CultureInfo.InvariantCulture);
                }
            }
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
            if (canvasObject != null && canvasObject.activeSelf != visible)
            {
                canvasObject.SetActive(visible);
            }
        }

        private static bool TryGetLayoutReference(
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
            if (controller == null)
            {
                return false;
            }

            meter = controller.errorMeter;
            if (meter == null)
            {
                // HUD 자체 Canvas와 폴백 배치는 입력 계기판 객체 없이도 동작한다.
                return true;
            }

            if (meter.straightMeter != null && meter.straightMeter.activeSelf)
            {
                meterRect = meter.straightMeter.GetComponent<RectTransform>();
            }
            else if (meter.curvedMeter != null && meter.curvedMeter.activeSelf)
            {
                meterRect = meter.curvedMeter.GetComponent<RectTransform>();
            }

            if (meterRect == null && meter.wrapperRectTransform != null)
            {
                meterRect = meter.wrapperRectTransform;
            }

            // 입력 계기판 OFF/비활성은 HUD 표시 조건이 아니다. meterRect는 위치 재현용 선택 입력이다.
            return true;
        }
    }
}
