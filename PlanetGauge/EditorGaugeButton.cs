using UnityEngine;
using UnityEngine.UI;

namespace PlanetGauge
{
    /// <summary>
    /// 에디터의 실패 방지 버튼을 기준점으로 삼아 게이지 토글 버튼을 생성하고 동기화한다.
    /// 에디터 인스턴스는 장면 전환 때 교체되므로 모든 Unity 참조는 <see cref="Destroy"/>에서 함께 해제한다.
    /// </summary>
    internal static class EditorGaugeButton
    {
        /*
         * UI 위치/크기 직접 조정 지점
         * - BarWidth, BarHeight: 체력 바 자체의 크기
         * - GapAboveShield: 실패 방지 방패의 위쪽 끝과 체력 바 사이 간격
         */
        private const float BarWidth = 72f;
        private const float BarHeight = 14f;
        private const float GapAboveShield = 8f;

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

            // 원본 실패 방지 버튼과 같은 부모/형제 계층을 사용해 UI 스케일 체계를 공유한다.
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
            // 게임이 실패 방지 버튼을 숨기는 화면에서는 모드 버튼도 함께 숨긴다.
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
            // Object.Destroy는 프레임 끝에 처리되므로 정적 참조는 즉시 비워 재사용을 막는다.
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
                    ? LocalizedStrings.GaugeEnabled
                    : LocalizedStrings.GaugeDisabled);
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

            buttonRect.localPosition = shieldTopCenterLocal
                + new Vector3(0f, GapAboveShield + BarHeight * 0.5f, 0f);
            buttonRect.localRotation = Quaternion.identity;
            buttonRect.localScale = Vector3.one;
        }
    }
}
