using UnityEngine;
using UnityEngine.UI;

namespace PlanetGauge
{
    internal sealed class GaugeSkinLayerGraphic : MaskableGraphic
    {
        private Texture2D layerTexture;
        private Rect localRect;
        private Rect uvRect;

        public override Texture mainTexture
        {
            get { return layerTexture == null ? s_WhiteTexture : layerTexture; }
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        internal void SetLayer(Texture2D texture, Rect destination, Rect uv, Color tint)
        {
            if (layerTexture == texture
                && RectApproximately(localRect, destination)
                && RectApproximately(uvRect, uv)
                && color.Equals(tint))
            {
                return;
            }

            layerTexture = texture;
            localRect = destination;
            uvRect = uv;
            color = tint;
            SetMaterialDirty();
            SetVerticesDirty();
        }

        internal void ClearLayer()
        {
            SetLayer(null, default(Rect), default(Rect), Color.clear);
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (layerTexture == null
                || color.a <= 0f
                || localRect.width <= 0f
                || localRect.height <= 0f
                || uvRect.width <= 0f
                || uvRect.height <= 0f)
            {
                return;
            }

            Color32 vertexColor = color;
            vertexHelper.AddVert(
                new Vector2(localRect.xMin, localRect.yMin),
                vertexColor,
                new Vector2(uvRect.xMin, uvRect.yMin));
            vertexHelper.AddVert(
                new Vector2(localRect.xMin, localRect.yMax),
                vertexColor,
                new Vector2(uvRect.xMin, uvRect.yMax));
            vertexHelper.AddVert(
                new Vector2(localRect.xMax, localRect.yMax),
                vertexColor,
                new Vector2(uvRect.xMax, uvRect.yMax));
            vertexHelper.AddVert(
                new Vector2(localRect.xMax, localRect.yMin),
                vertexColor,
                new Vector2(uvRect.xMax, uvRect.yMin));
            vertexHelper.AddTriangle(0, 1, 2);
            vertexHelper.AddTriangle(2, 3, 0);
        }

        private static bool RectApproximately(Rect left, Rect right)
        {
            return Mathf.Approximately(left.x, right.x)
                && Mathf.Approximately(left.y, right.y)
                && Mathf.Approximately(left.width, right.width)
                && Mathf.Approximately(left.height, right.height);
        }
    }

    internal sealed class GaugeSkinRenderer
    {
        private readonly GameObject layerRootObject;
        private readonly GaugeSkinLayerGraphic healthGraphic;
        private readonly GaugeSkinLayerGraphic frameGraphic;
        private readonly GaugeSkinLayerGraphic blindfoldGraphic;

        internal GaugeSkinRenderer(Transform parent)
        {
            layerRootObject = new GameObject(
                "PlanetGauge.SkinLayers",
                typeof(RectTransform));
            layerRootObject.transform.SetParent(parent, false);
            Stretch(layerRootObject.GetComponent<RectTransform>());

            // 같은 컨테이너의 동일 좌표계를 사용한다. 형제 순서만 프레임 뒤/체력 앞으로 고정한다.
            frameGraphic = CreateLayerGraphic(
                "PlanetGauge.SkinFrame",
                layerRootObject.transform);
            healthGraphic = CreateLayerGraphic(
                "PlanetGauge.SkinHealth",
                layerRootObject.transform);
            blindfoldGraphic = CreateLayerGraphic(
                "PlanetGauge.SkinBlindfold",
                layerRootObject.transform);

            SetActive(false);
        }

        internal void Update(
            RectTransform rootRect,
            float progress,
            float blindfoldOpacity,
            float barAlpha)
        {
            GaugeSkinAsset skin = GaugeSkinManager.Current;
            if (skin == null || rootRect == null)
            {
                healthGraphic.ClearLayer();
                frameGraphic.ClearLayer();
                blindfoldGraphic.ClearLayer();
                SetActive(false);
                return;
            }

            SetActive(true);
            float contentWidth = Mathf.Max(0.0001f, skin.ContentRect.Width);
            float pixelsToLocal = rootRect.rect.width / contentWidth;
            float clampedProgress = Mathf.Clamp01(progress);

            GaugeSkinRect visibleHealth = skin.HealthRect;
            Rect healthUv = GetUvRect(skin.Health);
            if (skin.Direction == GaugeSkinFillDirection.Vertical)
            {
                visibleHealth = new GaugeSkinRect(
                    visibleHealth.Left,
                    visibleHealth.Bottom,
                    visibleHealth.Right,
                    Mathf.Lerp(visibleHealth.Bottom, visibleHealth.Top, clampedProgress));
                healthUv.height *= clampedProgress;
            }
            else
            {
                visibleHealth = new GaugeSkinRect(
                    visibleHealth.Left,
                    visibleHealth.Bottom,
                    Mathf.Lerp(visibleHealth.Left, visibleHealth.Right, clampedProgress),
                    visibleHealth.Top);
                healthUv.width *= clampedProgress;
            }

            float clampedAlpha = Mathf.Clamp01(barAlpha);
            float clampedBlindfoldOpacity = Mathf.Clamp01(blindfoldOpacity);
            if (clampedBlindfoldOpacity >= 0.999f)
            {
                // 실제 채움 레이어를 제거해야 반투명 PNG에서도 현재 체력의 경계가 비치지 않는다.
                healthGraphic.ClearLayer();
            }
            else
            {
                Color healthTint = Color.white;
                healthTint.a = clampedAlpha;
                healthGraphic.SetLayer(
                    skin.Health.Texture,
                    ToLocalRect(visibleHealth, skin.ContentRect, pixelsToLocal),
                    healthUv,
                    healthTint);
            }

            if (clampedBlindfoldOpacity <= 0f)
            {
                blindfoldGraphic.ClearLayer();
            }
            else
            {
                Color blindfoldTint = Color.black;
                blindfoldTint.a = clampedAlpha * clampedBlindfoldOpacity;
                blindfoldGraphic.SetLayer(
                    skin.Health.Texture,
                    ToLocalRect(skin.HealthRect, skin.ContentRect, pixelsToLocal),
                    GetUvRect(skin.Health),
                    blindfoldTint);
            }

            if (skin.Frame == null)
            {
                frameGraphic.ClearLayer();
            }
            else
            {
                Color frameTint = Color.white;
                frameTint.a = clampedAlpha;
                PlanetGaugeSettings settings = Main.Settings;
                Vector2 frameOffset = settings == null
                    ? Vector2.zero
                    : new Vector2(
                        settings.FrameSkinOffsetX,
                        settings.FrameSkinOffsetY);
                frameGraphic.SetLayer(
                    skin.Frame.Texture,
                    ToLocalRect(
                        skin.FrameRect.Offset(frameOffset),
                        skin.ContentRect,
                        pixelsToLocal),
                    GetUvRect(skin.Frame),
                    frameTint);
            }
        }

        internal void SetVisible(bool visible)
        {
            if (layerRootObject.activeSelf != visible)
            {
                layerRootObject.SetActive(visible);
            }
        }

        private void SetActive(bool active)
        {
            SetVisible(active);
        }

        private static GaugeSkinLayerGraphic CreateLayerGraphic(
            string name,
            Transform parent)
        {
            GameObject layer = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(GaugeSkinLayerGraphic));
            layer.transform.SetParent(parent, false);
            RectTransform rect = layer.GetComponent<RectTransform>();
            Stretch(rect);
            GaugeSkinLayerGraphic graphic = layer.GetComponent<GaugeSkinLayerGraphic>();
            graphic.raycastTarget = false;
            return graphic;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        private static Rect GetUvRect(GaugeSkinTexture texture)
        {
            GaugeSkinPixelBounds bounds = texture.AlphaBounds;
            return new Rect(
                bounds.MinX / (float)texture.Width,
                bounds.MinY / (float)texture.Height,
                bounds.Width / (float)texture.Width,
                bounds.Height / (float)texture.Height);
        }

        private static Rect ToLocalRect(
            GaugeSkinRect source,
            GaugeSkinRect content,
            float scale)
        {
            Vector2 center = content.Center;
            return new Rect(
                (source.Left - center.x) * scale,
                (source.Bottom - center.y) * scale,
                source.Width * scale,
                source.Height * scale);
        }
    }
}
