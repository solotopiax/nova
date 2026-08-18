/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoFirebaseViewPushTaskTests.cs
 * author:    Codex
 * created:   2026/8/14
 * descrip:   Firebase Demo push task UI contract tests
 ***************************************************************/

using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Sdk.Firebase.Samples.Tests.Editor
{
    /// <summary>
    /// Firebase Demo push task UI 的源码与 Prefab 绑定契约测试。
    /// </summary>
    public sealed class DemoFirebaseViewPushTaskTests
    {
        private const string c_ViewSourcePath = "Assets/Samples/FirebaseDemo/Scripts/Runtime/UIs/DemoFirebaseView/DemoFirebaseView.cs";
        private const string c_MethodsSourcePath = "Assets/Samples/FirebaseDemo/Scripts/Runtime/UIs/DemoFirebaseView/DemoFirebaseView.Methods.cs";
        private const string c_VisitorsSourcePath = "Assets/Samples/FirebaseDemo/Scripts/Runtime/UIs/DemoFirebaseView/DemoFirebaseView.Visitors.cs";
        private const string c_PrefabPath = "Assets/Samples/FirebaseDemo/Prefabs/UIs/DemoFirebaseView/DemoFirebaseView.prefab";
        private const string c_ConfigRuntimePath = "Assets/Samples/FirebaseDemo/Configs/ConfigRuntime.asset";
        private const string c_ViewTypeName = "NovaFramework.Sdk.Firebase.Samples.Runtime.DemoFirebaseView";

        [Test]
        public void DemoFirebaseView_SourceDefinesPushTaskFixedOptionsAndSendApi()
        {
            string viewSource = File.ReadAllText(c_ViewSourcePath);
            string methodsSource = File.ReadAllText(c_MethodsSourcePath);
            string visitorsSource = File.ReadAllText(c_VisitorsSourcePath);

            StringAssert.Contains("m_PushTaskKeyDropdown", visitorsSource);
            StringAssert.Contains("m_PushTaskTriggerTimeDropdown", visitorsSource);
            StringAssert.Contains("m_PushTaskCancelToggle", visitorsSource);
            StringAssert.Contains("m_PushTaskTemplateIdDropdown", visitorsSource);
            StringAssert.Contains("m_SendPushTaskButton", visitorsSource);

            StringAssert.Contains("demo_push_task_1", methodsSource + viewSource);
            StringAssert.Contains("demo_push_task_2", methodsSource + viewSource);
            StringAssert.Contains("demo_push_task_3", methodsSource + viewSource);
            StringAssert.Contains("demo_push_task_4", methodsSource + viewSource);
            StringAssert.Contains("new TMP_Dropdown.OptionData(\"1\")", viewSource);
            StringAssert.Contains("new TMP_Dropdown.OptionData(\"4\")", viewSource);
            StringAssert.Contains("UTC+0 1 分钟后", viewSource);
            StringAssert.Contains("UTC+0 5 分钟后", viewSource);
            StringAssert.Contains("UTC+0 10 分钟后", viewSource);
            StringAssert.Contains("UTC+0 1 小时后", viewSource);
            StringAssert.Contains("UTC+0 3 小时后", viewSource);
            StringAssert.Contains("UTC+0 12 小时后", viewSource);
            StringAssert.Contains("UTC+0 24 小时后", viewSource);
            StringAssert.Contains("DateTimeOffset.UtcNow", methodsSource);
            StringAssert.Contains("TimeSpan.FromMinutes(1)", methodsSource);
            StringAssert.Contains("QueuePushTaskAsync(task)", methodsSource);
            StringAssert.Contains("TimeSpan.FromMinutes(10)", methodsSource);
            StringAssert.Contains("TimeSpan.FromHours(3)", methodsSource);
            StringAssert.Contains("TimeSpan.FromHours(12)", methodsSource);
            StringAssert.Contains("IFirebasePushTaskPlugin.QueuePushTaskAsync(FirebasePushTask)", viewSource);
            StringAssert.Contains("PushTask 已写入本地缓存", methodsSource);
            StringAssert.Contains("m_HasLoggedIn", visitorsSource);
            StringAssert.Contains("请先登录", methodsSource);
            StringAssert.Contains("m_SendPushTaskButton.interactable = interactable", viewSource);
            StringAssert.Contains("SetPushTaskSendButtonInteractable(true)", methodsSource);
        }

        [Test]
        public void DemoFirebaseView_ConfigFlushesSinglePushTaskForManualTesting()
        {
            string configSource = File.ReadAllText(c_ConfigRuntimePath);

            StringAssert.Contains("m_PushCmdName: FirebasePush", configSource);
            StringAssert.Contains("m_PushFlushBatchSize: 1", configSource);
        }

        [Test]
        public void DemoFirebaseViewPrefab_BindsPushTaskControls()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(c_PrefabPath);
            Assert.IsNotNull(prefab, "DemoFirebaseView prefab should exist.");

            Component view = prefab.GetComponent(c_ViewTypeName);
            Assert.IsNotNull(view, "DemoFirebaseView component should exist.");

            SerializedObject serializedView = new SerializedObject(view);
            AssertBoundComponent<TMP_Dropdown>(serializedView, "m_PushTaskKeyDropdown");
            AssertBoundComponent<TMP_Dropdown>(serializedView, "m_PushTaskTriggerTimeDropdown");
            AssertBoundComponent<Toggle>(serializedView, "m_PushTaskCancelToggle");
            AssertBoundComponent<TMP_Dropdown>(serializedView, "m_PushTaskTemplateIdDropdown");
            Button sendButton = AssertBoundComponent<Button>(serializedView, "m_SendPushTaskButton");

            Transform apiHint = sendButton.transform.Find("ApiHintText");
            Assert.IsNotNull(apiHint, "Send push task button should have direct ApiHintText.");
            Assert.IsNotNull(apiHint.GetComponent<TMP_Text>(), "ApiHintText should use TMP text.");
            Assert.IsFalse(sendButton.interactable, "Send push task button should be disabled until demo login succeeds.");
        }

        [Test]
        public void DemoFirebaseViewPrefab_PushTaskSectionUsesSerializedVerticalLayout()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(c_PrefabPath);
            Assert.IsNotNull(prefab, "DemoFirebaseView prefab should exist.");

            RectTransform section = prefab.transform.Find("InteractionArea/Viewport/Content/PushTaskSection") as RectTransform;
            Assert.IsNotNull(section, "PushTaskSection should exist under Content.");
            Assert.IsNull(section.GetComponent<VerticalLayoutGroup>(), "PushTaskSection should not rely on runtime VerticalLayoutGroup.");

            float previousY = float.PositiveInfinity;
            foreach (string childName in new[]
            {
                "TitleText",
                "TaskKeyRow",
                "TriggerTimeRow",
                "CancelRow",
                "TemplateIdRow",
                "PushTaskPreviewText",
                "SendPushTaskButton",
            })
            {
                RectTransform child = section.Find(childName) as RectTransform;
                Assert.IsNotNull(child, childName + " should exist.");
                Assert.That(child.anchorMin.y, Is.EqualTo(1f).Within(0.001f), childName + " should use top anchor min.");
                Assert.That(child.anchorMax.y, Is.EqualTo(1f).Within(0.001f), childName + " should use top anchor max.");
                Assert.Greater(child.sizeDelta.y, 20f, childName + " should have a serialized height.");
                Assert.Less(child.anchoredPosition.y, previousY - 4f, childName + " should be placed below previous row.");
                previousY = child.anchoredPosition.y;
            }
        }

        private static T AssertBoundComponent<T>(SerializedObject owner, string propertyName) where T : Component
        {
            SerializedProperty property = owner.FindProperty(propertyName);
            Assert.IsNotNull(property, propertyName + " should be serialized.");
            Assert.IsNotNull(property.objectReferenceValue, propertyName + " should be bound in prefab.");
            T component = property.objectReferenceValue as T;
            Assert.IsNotNull(component, propertyName + " should be a " + typeof(T).Name + ".");
            return component;
        }
    }
}
