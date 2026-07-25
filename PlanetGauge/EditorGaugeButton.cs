using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlanetGauge
{
    internal static class EditorGaugeButton
    {
        private static readonly Color DisabledColor = new Color(0.22f, 0.22f, 0.22f, 0.9f);
        private static readonly Color EnabledColor = new Color(0.1f, 0.65f, 0.9f, 1f);

        private static scnEditor owner;
        private static GameObject buttonObject;
        private static Button button;
        private static Image image;
        private static TMP_Text label;

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

            Button template = editor.buttonNoFail;
            Transform parent = template.transform.parent;
            buttonObject = Object.Instantiate(template.gameObject, parent, false);
            buttonObject.name = "Button_PlanetGauge";
            buttonObject.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);

            button = buttonObject.GetComponent<Button>();
            if (button == null)
            {
                button = buttonObject.AddComponent<Button>();
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(Toggle);

            image = buttonObject.GetComponent<Image>();
            if (image == null)
            {
                image = buttonObject.AddComponent<Image>();
            }

            image.sprite = null;
            image.type = Image.Type.Simple;
            button.targetGraphic = image;

            for (int index = buttonObject.transform.childCount - 1; index >= 0; index--)
            {
                Object.Destroy(buttonObject.transform.GetChild(index).gameObject);
            }

            CreateLabel(buttonObject.transform);
            OffsetIfParentHasNoLayout(template);
            Sync();
        }

        internal static void Sync()
        {
            if (buttonObject == null || button == null || owner == null)
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
            }

            image.color = Main.EditorGaugeEnabled ? EnabledColor : DisabledColor;
            if (label != null)
            {
                label.text = Main.EditorGaugeEnabled ? "PG ON" : "PG";
            }
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
            image = null;
            label = null;
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

        private static void CreateLabel(Transform parent)
        {
            GameObject labelObject = new GameObject(
                "PlanetGaugeLabel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);

            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            label = labelObject.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 15f;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;

            if (TMP_Settings.defaultFontAsset != null)
            {
                label.font = TMP_Settings.defaultFontAsset;
            }
        }

        private static void OffsetIfParentHasNoLayout(Button template)
        {
            Transform parent = template.transform.parent;
            if (parent.GetComponent<HorizontalOrVerticalLayoutGroup>() != null
                || parent.GetComponent<GridLayoutGroup>() != null)
            {
                return;
            }

            RectTransform templateRect = template.GetComponent<RectTransform>();
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            if (templateRect != null && buttonRect != null)
            {
                float width = templateRect.rect.width > 0f
                    ? templateRect.rect.width
                    : templateRect.sizeDelta.x;
                buttonRect.anchoredPosition = templateRect.anchoredPosition
                    + new Vector2(width + 6f, 0f);
            }
        }
    }
}
