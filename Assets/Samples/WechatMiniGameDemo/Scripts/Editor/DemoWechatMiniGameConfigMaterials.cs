/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoWechatMiniGameConfigMaterials.cs
 * author:    taoye
 * created:   2026/10/8
 * descrip:   微信 Demo 的正式购买表导入
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Reflection;
using NovaFramework.SDK.WeChatMiniGame.Editor;
using NovaFramework.SDK.WeChatMiniGame.Runtime;

namespace NovaFramework.Sdk.Wechat.Minigame.Samples.Editor
{
    /// <summary>仅初始化缺失的 Demo 购买表，不覆盖用户已经编辑的购买表。</summary>
    public static class DemoWechatMiniGameConfigMaterials
    {
        /// <summary>从正式 Excel 导入空购买表，不在正式配置写入 Demo 物料。</summary>
        public static void Ensure(WeChatMiniGamePluginConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (config.Purchases.Count == 0)
            {
                IReadOnlyList<WeChatMiniGamePurchaseEntry> entries = WeChatMiniGamePurchaseExcelImporter.ReadFile(
                    "Assets/Samples/WechatMiniGameDemo/Editor/Purchases/WeChatPurchases.xlsx");
                var list = new WeChatMiniGamePurchaseList();
                SetField(list, "m_Items", new List<WeChatMiniGamePurchaseEntry>(entries));
                SetField(config, "m_PurchaseList", list);
                if (config.GameCurrencyCoinsPerYuan == 0) SetField(config, "m_GameCurrencyCoinsPerYuan", 10);
                config.ValidatePurchaseConfiguration();
            }
        }

        /// <summary>仅 Editor 初始化内联配置字段；字段不存在时明确失败，防止反射静默遗漏。</summary>
        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(target.GetType().FullName, name);
            field.SetValue(target, value);
        }
    }
}
