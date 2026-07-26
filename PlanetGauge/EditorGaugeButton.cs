using UnityEngine;
using UnityEngine.UI;

namespace PlanetGauge
{
    internal static class EditorGaugeButton
    {
        /*
         * UI 위치/크기 직접 조정 지점
         * - BarWidth, BarHeight: 체력 바 자체의 크기
         * - GapAboveShield: 실패 방지 방패의 위쪽 끝과 체력 바 사이 간격
         * - ManualPositionOffset: 게임 버전별 UI 차이가 있을 때 마지막으로 더할 수동 보정값
         */
        private const float BarWidth = 72f;
        private const float BarHeight = 14f;
        private const float GapAboveShield = 8f;
        private static readonly Vector2 ManualPositionOffset = new Vector2(0f, 0f);

        private static scnEditor owner;
        private static GameObject buttonObject;
        private static Button button;
        private static GaugeBarGraphic gaugeGraphic;
        private static RectTransform buttonRect;
        private static RectTransform shieldRect;
        private static readonly Vector3[] ShieldWorldCorners = new Vector3[4];

        internal static void Ensure(scnEditor editor)
        {
            if (editor == null || editor.buttonNoFail == null)
            {
                return;
            }

            if (owner == editor && buttonObject != null)
            {
                return;
            }

            Destroy();
            owner = editor;

            Button shieldButton = editor.buttonNoFail;
            Transform parent = shieldButton.transform.parent;
            buttonObject = new GameObject(
                "Button_PlanetGaugeBar",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(GaugeBarGraphic),
                typeof(Button),
                typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.transform.SetSiblingIndex(shieldButton.transform.GetSiblingIndex() + 1);

            buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(BarWidth, BarHeight);
            buttonRect.localRotation = Quaternion.identity;
            buttonRect.localScale = Vector3.one;

            LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            shieldRect = shieldButton.GetComponent<RectTransform>();
            button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(Toggle);
            button.transition = Selectable.Transition.None;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            gaugeGraphic = buttonObject.GetComponent<GaugeBarGraphic>();
            button.targetGraphic = gaugeGraphic;
            Sync();
        }

        internal static void Sync()
        {
            if (buttonObject == null
                || button == null
                || gaugeGraphic == null
                || owner == null)
            {
                return;
            }

            bool templateActive = owner.buttonNoFail != null
                && owner.buttonNoFail.gameObject.activeSelf;
            if (buttonObject.activeSelf != templateActive)
            {
                buttonObject.SetActive(templateActive);
            }

            if (owner.buttonNoFail != null)
            {
                button.interactable = owner.buttonNoFail.interactable;
                shieldRect = owner.buttonNoFail.GetComponent<RectTransform>();
                PositionAboveShield();
            }

            float normalizedValue = GaugeRuntime.MaximumGauge <= 0f
                ? 0f
                : GaugeRuntime.Current / GaugeRuntime.MaximumGauge;
            gaugeGraphic.SetState(Main.EditorGaugeEnabled, normalizedValue);
        }

        internal static void Destroy()
        {
            if (buttonObject != null)
            {
                Object.Destroy(buttonObject);
            }

            owner = null;
            buttonObject = null;
            button = null;
            gaugeGraphic = null;
            buttonRect = null;
            shieldRect = null;
        }

        private static void Toggle()
        {
            bool enabled = !Main.EditorGaugeEnabled;
            Main.SetEditorGaugeEnabled(enabled);
            Sync();

            if (owner != null)
            {
                owner.ShowNotification(enabled
                    ? "Planet Gauge enabled"
                    : "Planet Gauge disabled");
            }
        }

        private static void PositionAboveShield()
        {
            if (buttonRect == null || shieldRect == null || shieldRect.parent == null)
            {
                return;
            }

            // 방패의 실제 월드 모서리를 매 프레임 읽으므로 레이아웃/해상도/방패 애니메이션을 따라간다.
            shieldRect.GetWorldCorners(ShieldWorldCorners);
            Vector3 shieldTopCenterWorld = (ShieldWorldCorners[1] + ShieldWorldCorners[2]) * 0.5f;
            Vector3 shieldTopCenterLocal =
                shieldRect.parent.InverseTransformPoint(shieldTopCenterWorld);
            Vector3 manualOffset = new Vector3(
                ManualPositionOffset.x,
                ManualPositionOffset.y,
                0f);

            buttonRect.localPosition = shieldTopCenterLocal
                + new Vector3(0f, GapAboveShield + BarHeight * 0.5f, 0f)
                + manualOffset;
            buttonRect.localRotation = Quaternion.identity;
            buttonRect.localScale = Vector3.one;
        }
    }
}
