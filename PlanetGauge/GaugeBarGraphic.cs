using UnityEngine;
using UnityEngine.UI;

namespace PlanetGauge
{
    /// <summary>
    /// 텍스처 없이 테두리, 빈 영역, 단색/3색 채움을 그리는 재사용 게이지다.
    /// 에디터 버튼이나 PlanetGauge 상태를 직접 참조하지 않으므로 메인 HUD에서도 그대로 쓸 수 있다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class GaugeBarGraphic : MaskableGraphic
    {
        private const int HorizontalSegments = 32;
        private const float MiddleColorPosition = 0.35f;
        private const float MiddleColorFalloff = 2f;

        private bool gaugeEnabled;
        private float normalizedValue = 1f;
        private float borderThickness = 2f;
        private float chamferSize;

        private Color32 borderColor = new Color32(8, 8, 8, 255);
        private Color32 disabledColor = new Color32(184, 184, 184, 255);
        private Color32 depletedColor = new Color32(58, 58, 58, 255);
        private Color32 lowColor = new Color32(225, 51, 51, 255);
        private Color32 middleColor = new Color32(248, 173, 0, 255);
        private Color32 highColor = new Color32(57, 146, 255, 255);

        internal bool GaugeEnabled
        {
            get { return gaugeEnabled; }
        }

        internal float NormalizedValue
        {
            get { return normalizedValue; }
        }

        protected override void Awake()
        {
            base.Awake();
            color = Color.white;
            raycastTarget = true;
        }

        /// <summary>
        /// enabled가 false면 회색 OFF 바를, true면 normalizedValue만큼 그라데이션을 표시한다.
        /// 값만 공급하면 되므로 에디터용 미니 바와 게임 중 메인 게이지가 같은 렌더러를 공유할 수 있다.
        /// </summary>
        internal void SetState(bool enabled, float value)
        {
            float clampedValue = Mathf.Clamp01(value);
            if (gaugeEnabled == enabled && Mathf.Approximately(normalizedValue, clampedValue))
            {
                return;
            }

            gaugeEnabled = enabled;
            normalizedValue = clampedValue;
            SetVerticesDirty();
        }

        /// <summary>
        /// 다른 HUD에서 이 Graphic을 재사용할 때 색과 테두리만 교체할 수 있도록 분리한 스타일 진입점이다.
        /// </summary>
        internal void SetStyle(
            Color32 newBorderColor,
            Color32 newDisabledColor,
            Color32 newDepletedColor,
            Color32 newLowColor,
            Color32 newMiddleColor,
            Color32 newHighColor,
            float newBorderThickness)
        {
            float clampedBorderThickness = Mathf.Max(0f, newBorderThickness);
            if (borderColor.Equals(newBorderColor)
                && disabledColor.Equals(newDisabledColor)
                && depletedColor.Equals(newDepletedColor)
                && lowColor.Equals(newLowColor)
                && middleColor.Equals(newMiddleColor)
                && highColor.Equals(newHighColor)
                && Mathf.Approximately(borderThickness, clampedBorderThickness))
            {
                return;
            }

            borderColor = newBorderColor;
            disabledColor = newDisabledColor;
            depletedColor = newDepletedColor;
            lowColor = newLowColor;
            middleColor = newMiddleColor;
            highColor = newHighColor;
            borderThickness = clampedBorderThickness;
            SetVerticesDirty();
        }

        /// <summary>
        /// 0이면 둥근 모서리, 0보다 크면 해당 픽셀만큼 꼭짓점을 직선으로 깎는다.
        /// </summary>
        internal void SetChamferSize(float size)
        {
            float clampedSize = Mathf.Max(0f, size);
            if (Mathf.Approximately(chamferSize, clampedSize))
            {
                return;
            }

            chamferSize = clampedSize;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            Rect outerRect = GetPixelAdjustedRect();
            if (outerRect.width <= 0f || outerRect.height <= 0f)
            {
                return;
            }

            float outerRadius = Mathf.Min(outerRect.width, outerRect.height) * 0.5f;
            AddBarRect(vertexHelper, outerRect, outerRadius, borderColor, false, outerRect);

            float actualBorder = Mathf.Min(
                borderThickness,
                Mathf.Min(outerRect.width, outerRect.height) * 0.5f);
            Rect innerRect = new Rect(
                outerRect.xMin + actualBorder,
                outerRect.yMin + actualBorder,
                Mathf.Max(0f, outerRect.width - actualBorder * 2f),
                Mathf.Max(0f, outerRect.height - actualBorder * 2f));
            if (innerRect.width <= 0f || innerRect.height <= 0f)
            {
                return;
            }

            float innerRadius = Mathf.Min(innerRect.width, innerRect.height) * 0.5f;
            Color32 trackColor = gaugeEnabled ? depletedColor : disabledColor;
            AddBarRect(vertexHelper, innerRect, innerRadius, trackColor, false, innerRect);

            if (!gaugeEnabled || normalizedValue <= 0f)
            {
                return;
            }

            Rect fillRect = innerRect;
            fillRect.width *= normalizedValue;
            if (fillRect.width <= 0f)
            {
                return;
            }

            float fillRadius = Mathf.Min(fillRect.width, fillRect.height) * 0.5f;
            AddBarRect(vertexHelper, fillRect, fillRadius, Color.white, true, innerRect);
        }

        private void AddBarRect(
            VertexHelper vertexHelper,
            Rect rect,
            float roundedRadius,
            Color32 solidColor,
            bool useGradient,
            Rect gradientReference)
        {
            if (chamferSize > 0f)
            {
                AddChamferedRect(
                    vertexHelper,
                    rect,
                    chamferSize,
                    solidColor,
                    useGradient,
                    gradientReference);
                return;
            }

            AddRoundedRect(
                vertexHelper,
                rect,
                roundedRadius,
                solidColor,
                useGradient,
                gradientReference);
        }

        private void AddRoundedRect(
            VertexHelper vertexHelper,
            Rect rect,
            float radius,
            Color32 solidColor,
            bool useGradient,
            Rect gradientReference)
        {
            float clampedRadius = Mathf.Clamp(
                radius,
                0f,
                Mathf.Min(rect.width, rect.height) * 0.5f);

            for (int segment = 0; segment < HorizontalSegments; segment++)
            {
                float x0 = Mathf.Lerp(rect.xMin, rect.xMax, segment / (float)HorizontalSegments);
                float x1 = Mathf.Lerp(
                    rect.xMin,
                    rect.xMax,
                    (segment + 1) / (float)HorizontalSegments);

                float bottom0;
                float top0;
                float bottom1;
                float top1;
                GetVerticalBounds(rect, clampedRadius, x0, out bottom0, out top0);
                GetVerticalBounds(rect, clampedRadius, x1, out bottom1, out top1);

                Color32 bottomLeft = useGradient
                    ? EvaluateGradient(x0, bottom0, gradientReference)
                    : solidColor;
                Color32 topLeft = useGradient
                    ? EvaluateGradient(x0, top0, gradientReference)
                    : solidColor;
                Color32 topRight = useGradient
                    ? EvaluateGradient(x1, top1, gradientReference)
                    : solidColor;
                Color32 bottomRight = useGradient
                    ? EvaluateGradient(x1, bottom1, gradientReference)
                    : solidColor;

                AddQuad(
                    vertexHelper,
                    new Vector2(x0, bottom0),
                    new Vector2(x0, top0),
                    new Vector2(x1, top1),
                    new Vector2(x1, bottom1),
                    bottomLeft,
                    topLeft,
                    topRight,
                    bottomRight);
            }
        }

        private void AddChamferedRect(
            VertexHelper vertexHelper,
            Rect rect,
            float requestedChamfer,
            Color32 solidColor,
            bool useGradient,
            Rect gradientReference)
        {
            float cut = Mathf.Clamp(
                requestedChamfer,
                0f,
                Mathf.Min(rect.width, rect.height) * 0.5f);
            Vector2 center = rect.center;
            Vector2[] points =
            {
                new Vector2(rect.xMin + cut, rect.yMin),
                new Vector2(rect.xMax - cut, rect.yMin),
                new Vector2(rect.xMax, rect.yMin + cut),
                new Vector2(rect.xMax, rect.yMax - cut),
                new Vector2(rect.xMax - cut, rect.yMax),
                new Vector2(rect.xMin + cut, rect.yMax),
                new Vector2(rect.xMin, rect.yMax - cut),
                new Vector2(rect.xMin, rect.yMin + cut)
            };

            int centerIndex = vertexHelper.currentVertCount;
            Color32 centerColor = useGradient
                ? EvaluateGradient(center.x, center.y, gradientReference)
                : solidColor;
            vertexHelper.AddVert(center, centerColor, Vector2.one * 0.5f);

            for (int index = 0; index < points.Length; index++)
            {
                Vector2 point = points[index];
                Color32 pointColor = useGradient
                    ? EvaluateGradient(point.x, point.y, gradientReference)
                    : solidColor;
                vertexHelper.AddVert(point, pointColor, Vector2.zero);
            }

            for (int index = 0; index < points.Length; index++)
            {
                int current = centerIndex + 1 + index;
                int next = centerIndex + 1 + ((index + 1) % points.Length);
                vertexHelper.AddTriangle(centerIndex, current, next);
            }
        }

        private Color32 EvaluateGradient(float x, float y, Rect referenceRect)
        {
            float denominator = referenceRect.width + referenceRect.height;
            float projection = denominator <= 0f
                ? 0f
                : ((x - referenceRect.xMin) + (y - referenceRect.yMin)) / denominator;
            float t = Mathf.Clamp01(projection);

            if (t <= MiddleColorPosition)
            {
                float blend = t / MiddleColorPosition;
                return Color32.Lerp(
                    lowColor,
                    middleColor,
                    Mathf.Pow(blend, MiddleColorFalloff));
            }

            float highBlend =
                (t - MiddleColorPosition) / (1f - MiddleColorPosition);
            return Color32.Lerp(
                middleColor,
                highColor,
                1f - Mathf.Pow(1f - highBlend, MiddleColorFalloff));
        }

        private static void GetVerticalBounds(
            Rect rect,
            float radius,
            float x,
            out float bottom,
            out float top)
        {
            if (radius <= 0f)
            {
                bottom = rect.yMin;
                top = rect.yMax;
                return;
            }

            if (x < rect.xMin + radius)
            {
                float delta = x - (rect.xMin + radius);
                float extent = Mathf.Sqrt(Mathf.Max(0f, radius * radius - delta * delta));
                bottom = rect.yMin + radius - extent;
                top = rect.yMax - radius + extent;
                return;
            }

            if (x > rect.xMax - radius)
            {
                float delta = x - (rect.xMax - radius);
                float extent = Mathf.Sqrt(Mathf.Max(0f, radius * radius - delta * delta));
                bottom = rect.yMin + radius - extent;
                top = rect.yMax - radius + extent;
                return;
            }

            bottom = rect.yMin;
            top = rect.yMax;
        }

        private static void AddQuad(
            VertexHelper vertexHelper,
            Vector2 bottomLeft,
            Vector2 topLeft,
            Vector2 topRight,
            Vector2 bottomRight,
            Color32 bottomLeftColor,
            Color32 topLeftColor,
            Color32 topRightColor,
            Color32 bottomRightColor)
        {
            int startIndex = vertexHelper.currentVertCount;
            vertexHelper.AddVert(bottomLeft, bottomLeftColor, Vector2.zero);
            vertexHelper.AddVert(topLeft, topLeftColor, Vector2.up);
            vertexHelper.AddVert(topRight, topRightColor, Vector2.one);
            vertexHelper.AddVert(bottomRight, bottomRightColor, Vector2.right);
            vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vertexHelper.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
        }
    }
}
