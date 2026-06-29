/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoAppleSigninViewPrefabBuilder.cs
 * author:    Codex
 * created:   2026/06/25
 * descrip:   Apple 登录 Prefab 构建器
 ***************************************************************/

using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Sdk.Applesignin.Samples.Editor
{
    public static class DemoAppleSigninViewPrefabBuilder
    {
        private const string c_ViewTypeName = "NovaFramework.Sdk.Applesignin.Samples.Runtime.DemoAppleSigninView";
        private const string c_BaseTypeName = "NovaFramework.Sdk.Applesignin.Samples.Runtime.BaseDemoView";
        private const string c_BasePrefabPath = "Assets/Samples/AppleSigninDemo/Prefabs/UIs/BaseDemoView/BaseDemoView.prefab";
        private const string c_TargetPrefabPath = "Assets/Samples/AppleSigninDemo/Prefabs/UIs/DemoAppleSigninView/DemoAppleSigninView.prefab";

        public static void Build()
        {
            Type baseType = FindType(c_BaseTypeName);
            Type viewType = FindType(c_ViewTypeName);
            if (baseType == null || viewType == null)
            {
                throw new InvalidOperationException($"入口类型缺失：Base={baseType != null} View={viewType != null}");
            }

            GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(c_BasePrefabPath);
            if (basePrefab == null)
            {
                throw new FileNotFoundException("BaseDemoView.prefab 不存在。", c_BasePrefabPath);
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            instance.name = "DemoAppleSigninView";

            Component baseComponent = instance.GetComponent(baseType);
            Component viewComponent = instance.AddComponent(viewType);
            CopyBaseReferences(baseComponent, viewComponent, out RectTransform interactionRoot, out TMP_Text titleText);
            BindActionButtons(viewComponent, interactionRoot, titleText);
            UnityEngine.Object.DestroyImmediate(baseComponent, true);

            Directory.CreateDirectory(Path.GetDirectoryName(c_TargetPrefabPath));
            PrefabUtility.SaveAsPrefabAsset(instance, c_TargetPrefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();

            GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(c_TargetPrefabPath);
            Debug.Log($"OK {PrefabUtility.GetPrefabAssetType(savedPrefab)} baseDerived={savedPrefab.GetComponents(baseType).Length}");
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static void CopyBaseReferences(Component baseComponent, Component viewComponent, out RectTransform interactionRoot, out TMP_Text titleText)
        {
            SerializedObject baseObject = new SerializedObject(baseComponent);
            SerializedObject viewObject = new SerializedObject(viewComponent);
            baseObject.Update();
            viewObject.Update();

            string[] fields =
            {
                "m_TitleText",
                "m_CloseButton",
                "m_InteractionRoot",
                "m_FeedbackContent",
                "m_FeedbackLineTemplate",
                "m_ClearFeedbackButton",
                "m_FeedbackScrollRect"
            };

            foreach (string field in fields)
            {
                viewObject.FindProperty(field).objectReferenceValue = baseObject.FindProperty(field).objectReferenceValue;
            }

            interactionRoot = baseObject.FindProperty("m_InteractionRoot").objectReferenceValue as RectTransform;
            titleText = baseObject.FindProperty("m_TitleText").objectReferenceValue as TMP_Text;
            viewObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BindActionButtons(Component viewComponent, RectTransform interactionRoot, TMP_Text titleText)
        {
            Button loginButton = CreateButton(interactionRoot, titleText, "LoginButton", "Apple 登录");
            Button logoutButton = CreateButton(interactionRoot, titleText, "LogoutButton", "Apple 登出");
            Button currentUserButton = CreateButton(interactionRoot, titleText, "CurrentUserButton", "当前用户");

            SerializedObject viewObject = new SerializedObject(viewComponent);
            viewObject.Update();
            viewObject.FindProperty("m_LoginButton").objectReferenceValue = loginButton;
            viewObject.FindProperty("m_LogoutButton").objectReferenceValue = logoutButton;
            viewObject.FindProperty("m_CurrentUserButton").objectReferenceValue = currentUserButton;
            viewObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Button CreateButton(RectTransform parent, TMP_Text titleText, string name, string label)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.AddComponent<Image>().color = Color.white;
            Button button = buttonObject.AddComponent<Button>();
            buttonObject.AddComponent<LayoutElement>().minHeight = 108;

            TextMeshProUGUI text = CreateText(buttonObject.transform, titleText, "Text", label, 28, Color.black, TextAlignmentOptions.Center);
            RectTransform textTransform = (RectTransform)text.transform;
            textTransform.anchorMin = Vector2.zero;
            textTransform.anchorMax = Vector2.one;
            textTransform.offsetMin = new Vector2(0, 18);
            textTransform.offsetMax = Vector2.zero;

            TextMeshProUGUI apiHintText = CreateText(buttonObject.transform, titleText, "ApiHintText", string.Empty, 18, new Color32(0x1A, 0x3A, 0x8C, 0xFF), TextAlignmentOptions.Center);
            RectTransform hintTransform = (RectTransform)apiHintText.transform;
            hintTransform.anchorMin = Vector2.zero;
            hintTransform.anchorMax = new Vector2(1, 0);
            hintTransform.pivot = new Vector2(0.5f, 0);
            hintTransform.anchoredPosition = new Vector2(0, 8);
            hintTransform.sizeDelta = new Vector2(0, 26);

            return button;
        }

        private static TextMeshProUGUI CreateText(Transform parent, TMP_Text titleText, string name, string value, float fontSize, Color color, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.color = color;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.raycastTarget = false;
            if (titleText != null && titleText.font != null)
            {
                text.font = titleText.font;
            }

            return text;
        }
    }
}
