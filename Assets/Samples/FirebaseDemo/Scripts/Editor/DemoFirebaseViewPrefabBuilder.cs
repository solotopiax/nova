/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoFirebaseViewPrefabBuilder.cs
 * author:    Codex
 * created:   2026/8/14
 * descrip:   Firebase DemoView Prefab 增量构建工具
 ***************************************************************/

using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using NovaFramework.Sdk.Firebase.Samples.Runtime;

namespace NovaFramework.Sdk.Firebase.Samples.Editor
{
    /// <summary>
    /// 只在现有 DemoFirebaseView.prefab 的 Content 节点中增量维护 push task 测试区。
    /// </summary>
    public static class DemoFirebaseViewPrefabBuilder
    {
        private const string c_PrefabPath = "Assets/Samples/FirebaseDemo/Prefabs/UIs/DemoFirebaseView/DemoFirebaseView.prefab";
        private static readonly Color32 s_ButtonColor = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        private static readonly Color32 s_PanelColor = new Color32(0xF3, 0xF6, 0xFA, 0xFF);
        private static readonly Color32 s_LabelColor = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        private static readonly Color32 s_TextColor = new Color32(0x11, 0x18, 0x27, 0xFF);
        private static readonly Color32 s_MutedColor = new Color32(0x47, 0x55, 0x69, 0xFF);
        private static readonly Color32 s_SectionMutedColor = new Color32(0xC9, 0xD6, 0xE8, 0xFF);
        private static readonly Color32 s_ApiHintColor = new Color32(0x1A, 0x3A, 0x8C, 0xFF);
        private const float c_SectionHeight = 560f;
        private const float c_SectionPaddingX = 20f;
        private const float c_RowHeight = 58f;
        private const float c_RowSpacing = 10f;
        private const float c_LabelWidth = 174f;
        private const float c_ControlLeft = 196f;

        /// <summary>
        /// 增量重建 Firebase push task 测试区，供需要维护示例 Prefab 的编辑器流程显式调用。
        /// </summary>
        public static void Build()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(c_PrefabPath);
            try
            {
                DemoFirebaseView view = root.GetComponent<DemoFirebaseView>();
                if (view == null)
                {
                    throw new InvalidOperationException("DemoFirebaseView.prefab 缺少 DemoFirebaseView 组件。");
                }

                SerializedObject serializedView = new SerializedObject(view);
                serializedView.Update();
                TMP_Text fontSource = serializedView.FindProperty("m_TitleText")?.objectReferenceValue as TMP_Text;
                RectTransform content = root.transform.Find("InteractionArea/Viewport/Content") as RectTransform;
                if (content == null)
                {
                    throw new InvalidOperationException("DemoFirebaseView.prefab 缺少 InteractionArea/Viewport/Content。");
                }

                RemoveExistingSection(content);
                BuildPushTaskSection(content, fontSource, serializedView);
                serializedView.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, c_PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(c_PrefabPath, ImportAssetOptions.ForceUpdate);
                Debug.Log("DemoFirebaseView push task 测试区已更新：" + c_PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void BuildPushTaskSection(RectTransform content, TMP_Text fontSource, SerializedObject view)
        {
            RectTransform section = CreateRect(content, "PushTaskSection");
            var sectionElement = section.gameObject.AddComponent<LayoutElement>();
            sectionElement.minHeight = c_SectionHeight;
            sectionElement.preferredHeight = c_SectionHeight;
            sectionElement.flexibleWidth = 1f;

            float top = 18f;
            TextMeshProUGUI titleText = CreateText(section, fontSource, "TitleText", "Push Task 测试", 28f, s_LabelColor, 42f, TextAlignmentOptions.Left);
            SetStretchTop(titleText.rectTransform, c_SectionPaddingX, c_SectionPaddingX, top, 42f);
            top += 52f;

            TMP_Dropdown keyDropdown = CreateDropdownRow(section, fontSource, "TaskKeyRow", "TaskKey", "TaskKeyDropdown",
                new[] { "demo_push_task_1", "demo_push_task_2", "demo_push_task_3", "demo_push_task_4" }, top);
            top += c_RowHeight + c_RowSpacing;

            TMP_Dropdown triggerDropdown = CreateDropdownRow(section, fontSource, "TriggerTimeRow", "UTC+0 时间", "TriggerTimeDropdown",
                new[]
                {
                    "UTC+0 当前时间",
                    "UTC+0 1 分钟后",
                    "UTC+0 5 分钟后",
                    "UTC+0 10 分钟后",
                    "UTC+0 1 小时后",
                    "UTC+0 3 小时后",
                    "UTC+0 12 小时后",
                    "UTC+0 24 小时后",
                }, top);
            top += c_RowHeight + c_RowSpacing;

            Toggle cancelToggle = CreateToggleRow(section, fontSource, "CancelRow", "取消任务", "CancelToggle", top);
            top += c_RowHeight + c_RowSpacing;

            TMP_Dropdown templateDropdown = CreateDropdownRow(section, fontSource, "TemplateIdRow", "模板 ID", "TemplateIdDropdown",
                new[] { "1", "2", "3", "4" }, top);
            top += c_RowHeight + 14f;

            TextMeshProUGUI previewText = CreateText(section, fontSource, "PushTaskPreviewText",
                "PushTask: task_key=demo_push_task_1, utc0=当前时间, cancel=False, template_id=1", 18f, s_SectionMutedColor, 42f, TextAlignmentOptions.Left);
            SetStretchTop(previewText.rectTransform, c_SectionPaddingX, c_SectionPaddingX, top, 42f);
            top += 58f;

            Button sendButton = CreateButton(section, fontSource, "SendPushTaskButton", "发送 Push Task");
            sendButton.interactable = false;
            SetStretchTop(sendButton.GetComponent<RectTransform>(), c_SectionPaddingX, c_SectionPaddingX, top, 108f);

            Assign(view, "m_PushTaskKeyDropdown", keyDropdown);
            Assign(view, "m_PushTaskTriggerTimeDropdown", triggerDropdown);
            Assign(view, "m_PushTaskCancelToggle", cancelToggle);
            Assign(view, "m_PushTaskTemplateIdDropdown", templateDropdown);
            Assign(view, "m_SendPushTaskButton", sendButton);
            Assign(view, "m_PushTaskPreviewText", previewText);
        }

        private static TMP_Dropdown CreateDropdownRow(Transform parent, TMP_Text fontSource, string rowName, string label,
            string dropdownName, IReadOnlyList<string> options, float top)
        {
            RectTransform row = CreateRow(parent, rowName, top, c_RowHeight);
            TextMeshProUGUI rowLabel = CreateRowLabel(row, fontSource, label);
            SetFixedTopLeft(rowLabel.rectTransform, 0f, 0f, c_LabelWidth, c_RowHeight);
            TMP_Dropdown dropdown = CreateDropdown(row, fontSource, dropdownName, options);
            SetStretchTop(dropdown.GetComponent<RectTransform>(), c_ControlLeft, 0f, 0f, c_RowHeight);
            return dropdown;
        }

        private static Toggle CreateToggleRow(Transform parent, TMP_Text fontSource, string rowName, string label, string toggleName, float top)
        {
            RectTransform row = CreateRow(parent, rowName, top, c_RowHeight);
            TextMeshProUGUI rowLabel = CreateRowLabel(row, fontSource, label);
            SetFixedTopLeft(rowLabel.rectTransform, 0f, 0f, c_LabelWidth, c_RowHeight);

            RectTransform toggleRect = CreateRect(row, toggleName);
            SetStretchTop(toggleRect, c_ControlLeft, 0f, 0f, c_RowHeight);
            Image background = toggleRect.gameObject.AddComponent<Image>();
            background.color = s_ButtonColor;
            Toggle toggle = toggleRect.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.isOn = false;

            RectTransform mark = CreateRect(toggleRect, "Checkmark");
            mark.anchorMin = new Vector2(0f, 0.5f);
            mark.anchorMax = new Vector2(0f, 0.5f);
            mark.pivot = new Vector2(0.5f, 0.5f);
            mark.anchoredPosition = new Vector2(32f, 0f);
            mark.sizeDelta = new Vector2(28f, 28f);
            Image markImage = mark.gameObject.AddComponent<Image>();
            markImage.color = new Color32(0x22, 0xC5, 0x5E, 0xFF);
            toggle.graphic = markImage;

            TextMeshProUGUI text = CreateText(toggleRect, fontSource, "Text", "Cancel = false 时创建任务，true 时取消任务", 18f,
                s_TextColor, c_RowHeight, TextAlignmentOptions.Left);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(62f, 0f);
            textRect.offsetMax = new Vector2(-12f, 0f);
            return toggle;
        }

        private static TMP_Dropdown CreateDropdown(Transform parent, TMP_Text fontSource, string name, IReadOnlyList<string> optionTexts)
        {
            RectTransform rect = CreateRect(parent, name);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 64f;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = s_ButtonColor;
            TMP_Dropdown dropdown = rect.gameObject.AddComponent<TMP_Dropdown>();
            dropdown.targetGraphic = image;

            TextMeshProUGUI caption = CreateText(rect, fontSource, "Label", optionTexts.Count > 0 ? optionTexts[0] : string.Empty, 22f,
                s_TextColor, 64f, TextAlignmentOptions.Left);
            caption.rectTransform.offsetMin = new Vector2(16f, 0f);
            caption.rectTransform.offsetMax = new Vector2(-46f, 0f);
            TextMeshProUGUI arrow = CreateText(rect, fontSource, "Arrow", "▼", 18f, s_MutedColor, 64f, TextAlignmentOptions.Center);
            arrow.rectTransform.anchorMin = new Vector2(1f, 0f);
            arrow.rectTransform.anchorMax = Vector2.one;
            arrow.rectTransform.offsetMin = new Vector2(-42f, 0f);
            arrow.rectTransform.offsetMax = new Vector2(-8f, 0f);

            RectTransform template = CreateDropdownTemplate(rect, fontSource, out TMP_Text itemText);
            dropdown.captionText = caption;
            dropdown.itemText = itemText;
            dropdown.template = template;
            dropdown.options.Clear();
            for (int i = 0; i < optionTexts.Count; i++)
            {
                dropdown.options.Add(new TMP_Dropdown.OptionData(optionTexts[i]));
            }

            dropdown.value = 0;
            dropdown.RefreshShownValue();
            return dropdown;
        }

        private static RectTransform CreateDropdownTemplate(Transform parent, TMP_Text fontSource, out TMP_Text itemText)
        {
            RectTransform template = CreateRect(parent, "Template");
            template.gameObject.SetActive(false);
            template.anchorMin = new Vector2(0f, 0f);
            template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(0.5f, 1f);
            template.anchoredPosition = new Vector2(0f, -2f);
            template.sizeDelta = new Vector2(0f, 224f);
            Image templateImage = template.gameObject.AddComponent<Image>();
            templateImage.color = s_ButtonColor;
            ScrollRect scrollRect = template.gameObject.AddComponent<ScrollRect>();

            RectTransform viewport = CreateRect(template, "Viewport");
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = s_ButtonColor;
            Mask mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            RectTransform content = CreateRect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 56f);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            RectTransform item = CreateRect(content, "Item");
            item.sizeDelta = new Vector2(0f, 56f);
            item.gameObject.AddComponent<LayoutElement>().preferredHeight = 56f;
            Toggle toggle = item.gameObject.AddComponent<Toggle>();
            Image itemBackground = item.gameObject.AddComponent<Image>();
            itemBackground.color = s_ButtonColor;
            toggle.targetGraphic = itemBackground;
            itemText = CreateText(item, fontSource, "Item Label", "Option", 20f, s_TextColor, 56f, TextAlignmentOptions.Left);
            itemText.rectTransform.offsetMin = new Vector2(16f, 0f);
            itemText.rectTransform.offsetMax = new Vector2(-16f, 0f);

            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            return template;
        }

        private static Button CreateButton(Transform parent, TMP_Text fontSource, string name, string label)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.gameObject.AddComponent<Image>().color = s_ButtonColor;
            Button button = rect.gameObject.AddComponent<Button>();
            rect.gameObject.AddComponent<LayoutElement>().minHeight = 108f;

            TextMeshProUGUI text = CreateText(rect, fontSource, "Text", label, 28f, s_TextColor, 108f, TextAlignmentOptions.Center);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;

            TextMeshProUGUI apiHint = CreateText(rect, fontSource, "ApiHintText", string.Empty, 18f, s_ApiHintColor, 26f, TextAlignmentOptions.Center);
            RectTransform hintRect = apiHint.rectTransform;
            hintRect.anchorMin = Vector2.zero;
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.anchoredPosition = new Vector2(0f, 8f);
            hintRect.sizeDelta = new Vector2(0f, 26f);
            return button;
        }

        private static RectTransform CreateRow(Transform parent, string name, float top, float height)
        {
            RectTransform row = CreateRect(parent, name);
            SetStretchTop(row, c_SectionPaddingX, c_SectionPaddingX, top, height);
            return row;
        }

        private static TextMeshProUGUI CreateRowLabel(Transform parent, TMP_Text fontSource, string label)
        {
            TextMeshProUGUI text = CreateText(parent, fontSource, "Label", label, 22f, s_LabelColor, 64f, TextAlignmentOptions.Left);
            var element = text.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 150f;
            element.preferredHeight = 64f;
            return text;
        }

        private static TextMeshProUGUI CreateText(Transform parent, TMP_Text fontSource, string name, string value, float fontSize,
            Color color, float preferredHeight, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(parent, name);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.color = color;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            if (fontSource != null && fontSource.font != null)
            {
                text.font = fontSource.font;
            }

            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = preferredHeight;
            return text;
        }

        private static void SetStretchTop(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void SetFixedTopLeft(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void Assign(SerializedObject owner, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = owner.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException("DemoFirebaseView 缺少序列化字段：" + propertyName);
            }

            property.objectReferenceValue = value;
        }

        private static void RemoveExistingSection(Transform content)
        {
            Transform existing = content.Find("PushTaskSection");
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }
        }
    }
}
