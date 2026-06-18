/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TypedDimensionMask.cs
 * author:    taoye
 * created:   2026/6/1
 * descrip:   带类型全名的配置面板维度掩码条目
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 带配置类型全名的维度掩码条目；
    /// 将指定配置类型（SDK Plugin 或 Kit）与其对应的面板维度掩码绑定，
    /// 供 ConfigMasterSO.SDKMasks / KitMasks 列表使用。
    /// </summary>
    [Serializable]
    public sealed class TypedDimensionMask
    {
        /// <summary>
        /// 配置类型全名；与 EnabledSDKs / EnabledKits 中的元素同口径，
        /// 格式为 Namespace.ClassName，例如 NovaFramework.WeChat.WeChatSDKPluginConfig。
        /// </summary>
        public string TypeName;

        /// <summary>
        /// 该配置类型对应的面板维度掩码；默认全不勾（全局唯一）。
        /// </summary>
        public PanelDimensionMask Mask = new();
    }
}
