/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoWechatMiniGameSetup.cs
 * author:    Codex
 * created:   2026/09/20
 * descrip:   微信小游戏 Demo 配置与入口 Prefab 构建器
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NovaFramework.Editor;
using NovaFramework.Kit.Network.GameBind.Runtime;
using NovaFramework.Kit.Network.GameLogin.Runtime;
using NovaFramework.Runtime;
using NovaFramework.SDK.TGAPlugin.Runtime;
using NovaFramework.SDK.WeChatMiniGame.Runtime;
using NovaFramework.Sdk.Wechat.Minigame.Samples.Runtime;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Sdk.Wechat.Minigame.Samples.Editor
{
    /// <summary>
    /// 生成入口 View 的真实 Prefab Variant，并把微信 Plugin 写入 Demo 的 Master/Runtime 配置。
    /// </summary>
    public static class DemoWechatMiniGameSetup
    {
        private const string c_MasterPath = "Assets/Samples/WechatMiniGameDemo/Editor/ConfigMaster.asset";
        private const string c_RuntimeConfigPath = "Assets/Samples/WechatMiniGameDemo/Configs/ConfigRuntime.asset";
        private const string c_PipifySettingsPath = "Assets/Samples/WechatMiniGameDemo/Editor/PipifySettings.asset";
        private const string c_BasePrefabPath = "Assets/Samples/WechatMiniGameDemo/Prefabs/UIs/BaseDemoView/BaseDemoView.prefab";
        private const string c_TargetPrefabPath = "Assets/Samples/WechatMiniGameDemo/Prefabs/UIs/DemoWechatMiniGameView/DemoWechatMiniGameView.prefab";
        private const string c_WechatAppId = "wxec64551daa21353f";
        private static bool s_Scheduled;

        [InitializeOnLoadMethod]
        private static void ScheduleInitialBuild()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(c_TargetPrefabPath);
            DemoWechatMiniGameView view = prefab != null ? prefab.GetComponent<DemoWechatMiniGameView>() : null;
            var serialized = view != null ? new SerializedObject(view) : null;
            bool hasWechatLoginButton = serialized?.FindProperty("m_LoginButton")?.objectReferenceValue != null;
            bool hasGameLoginButton = serialized?.FindProperty("m_GameLoginButton")?.objectReferenceValue != null;
            ConfigRuntimeSO runtime = AssetDatabase.LoadAssetAtPath<ConfigRuntimeSO>(c_RuntimeConfigPath);
            bool hasTgaConfig = runtime?.GetSDKPluginConfig<TGAPluginConfig>() != null;
            bool hasGameLoginConfig = runtime?.GetKitConfig<LoginKitConfig>() != null;
            bool hasGameBindConfig = runtime?.GetKitConfig<BindKitConfig>() != null;
            bool hasWechatAppId = string.Equals(
                runtime?.GetSDKPluginConfig<WeChatMiniGamePluginConfig>()?.AppId,
                c_WechatAppId,
                StringComparison.Ordinal);
            bool hasWechatPipifyParams = HasWechatPipifyParams();
            if ((hasWechatLoginButton && hasGameLoginButton && hasTgaConfig && hasGameLoginConfig &&
                 hasGameBindConfig && hasWechatAppId && !hasWechatPipifyParams) || s_Scheduled)
            {
                return;
            }
            s_Scheduled = true;
            EditorApplication.delayCall += Build;
        }

        public static void Build()
        {
            s_Scheduled = false;
            ConfigureWechatPlugin();
            MigrateWechatPipifyParams();
            BuildViewPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("WechatMiniGameDemo 已生成：微信配置与 DemoWechatMiniGameView Prefab Variant 均已就绪。");
        }

        private static void ConfigureWechatPlugin()
        {
            ConfigMasterSO master = AssetDatabase.LoadAssetAtPath<ConfigMasterSO>(c_MasterPath);
            ConfigRuntimeSO runtime = AssetDatabase.LoadAssetAtPath<ConfigRuntimeSO>(c_RuntimeConfigPath);
            if (master == null || runtime == null)
            {
                throw new FileNotFoundException("WechatMiniGameDemo 缺少 ConfigMaster 或 ConfigRuntime。", c_MasterPath);
            }

            string wechatConfigTypeName = typeof(WeChatMiniGamePluginConfig).FullName;
            string tgaConfigTypeName = typeof(TGAPluginConfig).FullName;
            string loginConfigTypeName = typeof(LoginKitConfig).FullName;
            string bindConfigTypeName = typeof(BindKitConfig).FullName;
            master.EnabledSDKs ??= new List<string>();
            EnableUnique(master.EnabledSDKs, tgaConfigTypeName);
            EnableUnique(master.EnabledSDKs, wechatConfigTypeName);
            master.EnabledKits ??= new List<string>();
            EnableUnique(master.EnabledKits, loginConfigTypeName);
            EnableUnique(master.EnabledKits, bindConfigTypeName);
            master.GetSDKMask(tgaConfigTypeName);
            master.GetSDKMask(wechatConfigTypeName);
            master.GetKitMask(loginConfigTypeName);
            master.GetKitMask(bindConfigTypeName);
            foreach (PlatformChannelEntry entry in master.EditorEntries)
            {
                EditorUtil.Config.SDKPluginScanner.EnsureInstance(entry, DevelopMode.Debug, typeof(WeChatMiniGamePluginConfig));
                EditorUtil.Config.SDKPluginScanner.EnsureInstance(entry, DevelopMode.Release, typeof(WeChatMiniGamePluginConfig));
                EditorUtil.Config.SDKPluginScanner.EnsureInstance(entry, DevelopMode.Debug, typeof(TGAPluginConfig));
                EditorUtil.Config.SDKPluginScanner.EnsureInstance(entry, DevelopMode.Release, typeof(TGAPluginConfig));
                EditorUtil.Config.KitConfigScanner.EnsureInstance(entry, DevelopMode.Debug, typeof(LoginKitConfig));
                EditorUtil.Config.KitConfigScanner.EnsureInstance(entry, DevelopMode.Release, typeof(LoginKitConfig));
                EditorUtil.Config.KitConfigScanner.EnsureInstance(entry, DevelopMode.Debug, typeof(BindKitConfig));
                EditorUtil.Config.KitConfigScanner.EnsureInstance(entry, DevelopMode.Release, typeof(BindKitConfig));
                ConfigureProtocolNames(entry, DevelopMode.Debug);
                ConfigureProtocolNames(entry, DevelopMode.Release);
            }
            EnsureAotMetadata(master);
            master.CurrentChannel = ChannelType.WeChat;
            master.CurrentDevelopMode = DevelopMode.Debug;
            EditorUtility.SetDirty(master);
            ConfigRuntimeSO exported = EditorUtil.Config.Exporter.Export(
                master, PlatformType.WebGL, ChannelType.WeChat, DevelopMode.Debug, c_RuntimeConfigPath);
            if (exported == null)
            {
                throw new InvalidOperationException("WechatMiniGameDemo 无法导出 WebGL × WeChat × Debug 运行时配置。");
            }
        }

        private static void EnableUnique(List<string> enabledTypes, string typeName)
        {
            enabledTypes.RemoveAll(value => value == typeName);
            enabledTypes.Add(typeName);
        }

        private static void ConfigureProtocolNames(PlatformChannelEntry entry, DevelopMode mode)
        {
            List<ISDKPluginConfig> sdkConfigs = entry.GetSDKConfigs(mode);
            WeChatMiniGamePluginConfig wechatConfig = sdkConfigs.OfType<WeChatMiniGamePluginConfig>().First();
            SetPrivateString(wechatConfig, "m_AppId", c_WechatAppId);

            TGAPluginConfig tgaConfig = sdkConfigs.OfType<TGAPluginConfig>().First();
            SetPrivateString(tgaConfig, "m_ServerCmdName", "TGAServerUrl");
            SetPrivateString(tgaConfig, "m_ReportCmdName", "TGAReport");

            List<IKitConfig> kitConfigs = entry.GetKitConfigs(mode);
            LoginKitConfig loginConfig = kitConfigs.OfType<LoginKitConfig>().First();
            SetPrivateString(loginConfig, "m_LoginCmdName", "GameAccountLogin");

            BindKitConfig bindConfig = kitConfigs.OfType<BindKitConfig>().First();
            SetPrivateString(bindConfig, "m_BindCmdName", "GameAccountBind");
            SetPrivateString(bindConfig, "m_BindConflictCmdName", "GameAccountBindConflict");
            SetPrivateString(bindConfig, "m_BindResolveCmdName", "GameAccountBindResolve");
        }

        /// <summary>
        /// 判断 Demo Pipify 的无参微信导出 Step 是否仍保存旧参数。
        /// </summary>
        private static bool HasWechatPipifyParams()
        {
            PipifySettingsSO settings = AssetDatabase.LoadAssetAtPath<PipifySettingsSO>(c_PipifySettingsPath);
            if (settings == null) return false;
            return settings.Batches.Any(batch => batch.Items.Any(item =>
                item.StepId == "wechat.minigame.export" &&
                !string.IsNullOrWhiteSpace(item.ParamsJson)));
        }

        /// <summary>
        /// 清空微信导出 Step 的旧参数；转换配置统一读取 SDKConfig 与 MiniGameConfig。
        /// </summary>
        private static void MigrateWechatPipifyParams()
        {
            PipifySettingsSO settings = AssetDatabase.LoadAssetAtPath<PipifySettingsSO>(c_PipifySettingsPath);
            if (settings == null)
            {
                throw new FileNotFoundException("WechatMiniGameDemo 缺少 PipifySettings。", c_PipifySettingsPath);
            }

            bool changed = false;
            foreach (Batch batch in settings.Batches)
            {
                foreach (BatchItem item in batch.Items)
                {
                    if (item.StepId != "wechat.minigame.export" || string.IsNullOrWhiteSpace(item.ParamsJson)) continue;
                    item.ParamsJson = string.Empty;
                    changed = true;
                }
            }
            if (changed) EditorUtility.SetDirty(settings);
        }

        private static void SetPrivateString(object target, string fieldName, string value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(target.GetType().FullName, fieldName);
            }
            field.SetValue(target, value);
        }

        private static void EnsureAotMetadata(ConfigMasterSO master)
        {
            var serialized = new SerializedObject(master);
            serialized.Update();
            EnsureAotMetadata(
                serialized.FindProperty("HybridEditorConfigs")?.FindPropertyRelative("AotMetadataDlls"));

            SerializedProperty overrides = serialized.FindProperty("HybridEditorConfigsOverrides");
            if (overrides != null)
            {
                for (int i = 0; i < overrides.arraySize; i++)
                {
                    EnsureAotMetadata(
                        overrides.GetArrayElementAtIndex(i).FindPropertyRelative("AotMetadataDlls"));
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureAotMetadata(SerializedProperty entries)
        {
            if (entries == null)
            {
                return;
            }

            string[] assemblyNames =
            {
                "NovaFramework.Kit.Network.GameLogin.Runtime.dll",
                "NovaFramework.Kit.Network.GameBind.Runtime.dll",
                "NovaFramework.SDK.TGAPlugin.Runtime.dll"
            };
            foreach (string assemblyName in assemblyNames)
            {
                bool exists = false;
                for (int i = 0; i < entries.arraySize; i++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                    if (entry.FindPropertyRelative("m_AssetLocation").stringValue == assemblyName)
                    {
                        exists = true;
                        break;
                    }
                }
                if (exists)
                {
                    continue;
                }

                int index = entries.arraySize;
                entries.InsertArrayElementAtIndex(index);
                SerializedProperty added = entries.GetArrayElementAtIndex(index);
                added.FindPropertyRelative("m_SourceLocation").stringValue =
                    $"HybridCLRData/AssembliesPostIl2CppStrip/{{ActiveBuildTarget}}/{assemblyName}";
                added.FindPropertyRelative("m_TargetLocation").stringValue =
                    $"Assets/Samples/WechatMiniGameDemo/Dlls/AOTMetas/{assemblyName}.bytes";
                added.FindPropertyRelative("m_AssetLocation").stringValue = assemblyName;
            }
        }

        private static void BuildViewPrefab()
        {
            GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(c_BasePrefabPath);
            if (basePrefab == null)
            {
                throw new FileNotFoundException("BaseDemoView.prefab 不存在。", c_BasePrefabPath);
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            instance.name = nameof(DemoWechatMiniGameView);
            BaseDemoView baseView = instance.GetComponent<BaseDemoView>();
            DemoWechatMiniGameView view = instance.AddComponent<DemoWechatMiniGameView>();
            CopyBaseReferences(baseView, view, out RectTransform interactionRoot, out TMP_Text titleText);
            BindActionButtons(view, interactionRoot, titleText);
            UnityEngine.Object.DestroyImmediate(baseView, true);

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(c_TargetPrefabPath));
            PrefabUtility.SaveAsPrefabAsset(instance, c_TargetPrefabPath);
            UnityEngine.Object.DestroyImmediate(instance);

            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(c_TargetPrefabPath);
            if (saved == null || PrefabUtility.GetPrefabAssetType(saved) != PrefabAssetType.Variant ||
                saved.GetComponents(typeof(BaseDemoView)).Length != 1)
            {
                throw new InvalidOperationException("DemoWechatMiniGameView 必须是 BaseDemoView 的有效 Prefab Variant。 ");
            }
        }

        private static void CopyBaseReferences(
            BaseDemoView baseView,
            DemoWechatMiniGameView view,
            out RectTransform interactionRoot,
            out TMP_Text titleText)
        {
            var source = new SerializedObject(baseView);
            var target = new SerializedObject(view);
            source.Update();
            target.Update();
            string[] fields =
            {
                "m_TitleText", "m_CloseButton", "m_InteractionRoot", "m_FeedbackContent",
                "m_FeedbackLineTemplate", "m_ClearFeedbackButton", "m_FeedbackScrollRect"
            };
            foreach (string field in fields)
            {
                target.FindProperty(field).objectReferenceValue = source.FindProperty(field).objectReferenceValue;
            }
            interactionRoot = source.FindProperty("m_InteractionRoot").objectReferenceValue as RectTransform;
            titleText = source.FindProperty("m_TitleText").objectReferenceValue as TMP_Text;
            if (interactionRoot == null || titleText?.font == null)
            {
                throw new InvalidOperationException("BaseDemoView 的交互区或 Sample 内字体绑定无效。");
            }
            target.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BindActionButtons(DemoWechatMiniGameView view, RectTransform root, TMP_Text titleText)
        {
            var serialized = new SerializedObject(view);
            serialized.Update();
            (string field, string objectName, string label)[] buttons =
            {
                ("m_LoginButton", "LoginButton", "微信登录校验"),
                ("m_GameLoginButton", "GameLoginButton", "登录游戏服务器"),
                ("m_RuntimeInfoButton", "RuntimeInfoButton", "运行环境信息"),
                ("m_PrivacyStatusButton", "PrivacyStatusButton", "查询隐私状态"),
                ("m_PrivacyAuthorizeButton", "PrivacyAuthorizeButton", "请求隐私授权"),
                ("m_ReadClipboardButton", "ReadClipboardButton", "读取剪贴板"),
                ("m_WriteClipboardButton", "WriteClipboardButton", "写入剪贴板"),
                ("m_VibrateButton", "VibrateButton", "短振动"),
                ("m_ShareButton", "ShareButton", "主动分享"),
                ("m_PaymentSupportButton", "PaymentSupportButton", "检查支付能力"),
                ("m_PayCurrencyButton", "PayCurrencyButton", "购买游戏币"),
                ("m_PayItemButton", "PayItemButton", "购买道具"),
                ("m_QueryOrderButton", "QueryOrderButton", "查询最近订单"),
                ("m_RecoverOrderButton", "RecoverOrderButton", "验证最近订单"),
                ("m_RecoverPendingButton", "RecoverPendingButton", "验证全部订单"),
                ("m_SubscribeMessageButton", "SubscribeMessageButton", "开启消息提醒")
            };
            foreach ((string field, string objectName, string label) in buttons)
            {
                serialized.FindProperty(field).objectReferenceValue = CreateButton(root, titleText, objectName, label);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Button CreateButton(Transform parent, TMP_Text titleText, string objectName, string label)
        {
            var buttonObject = new GameObject(objectName, typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.AddComponent<Image>().color = Color.white;
            Button button = buttonObject.AddComponent<Button>();
            buttonObject.AddComponent<LayoutElement>().minHeight = 108f;

            TextMeshProUGUI text = CreateText(buttonObject.transform, titleText, "Text", label, 28f, Color.black);
            RectTransform textTransform = (RectTransform)text.transform;
            textTransform.anchorMin = Vector2.zero;
            textTransform.anchorMax = Vector2.one;
            textTransform.offsetMin = new Vector2(0f, 18f);
            textTransform.offsetMax = Vector2.zero;

            TextMeshProUGUI hint = CreateText(
                buttonObject.transform,
                titleText,
                "ApiHintText",
                string.Empty,
                18f,
                new Color32(0x1A, 0x3A, 0x8C, 0xFF));
            RectTransform hintTransform = (RectTransform)hint.transform;
            hintTransform.anchorMin = Vector2.zero;
            hintTransform.anchorMax = new Vector2(1f, 0f);
            hintTransform.pivot = new Vector2(0.5f, 0f);
            hintTransform.anchoredPosition = new Vector2(0f, 8f);
            hintTransform.sizeDelta = new Vector2(0f, 26f);
            return button;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            TMP_Text titleText,
            string objectName,
            string value,
            float fontSize,
            Color color)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.color = color;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.font = titleText.font;
            return text;
        }
    }
}
