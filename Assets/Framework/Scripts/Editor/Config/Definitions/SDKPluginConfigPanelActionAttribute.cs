/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SDKPluginConfigPanelActionAttribute.cs
 * author:    taoye
 * created:   2026/9/20
 * descrip:   SDK Config 面板快捷动作声明
 ***************************************************************/

using System;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 由 SDK Editor 程序集声明的 ConfigWindow 快捷动作。
    /// ConfigWindow 按 ConfigType 匹配并通过 Unity 菜单路径执行，不直接依赖具体 SDK Editor 类型。
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class SDKPluginConfigPanelActionAttribute : Attribute
    {
        /// <summary>
        /// 创建一个显示在指定 SDK Config 面板顶部的菜单快捷动作。
        /// </summary>
        /// <param name="configType">动作所属的 SDK Plugin Config 类型。</param>
        /// <param name="buttonLabel">ConfigWindow 中显示的按钮名称。</param>
        /// <param name="menuItemPath">点击按钮时执行的 Unity 菜单路径。</param>
        /// <param name="order">同一 Config 下多个动作的显示顺序。</param>
        public SDKPluginConfigPanelActionAttribute(
            Type configType,
            string buttonLabel,
            string menuItemPath,
            int order = 0)
        {
            ConfigType = configType;
            ButtonLabel = buttonLabel;
            MenuItemPath = menuItemPath;
            Order = order;
        }

        /// <summary>动作所属的 SDK Plugin Config 类型。</summary>
        public Type ConfigType { get; }

        /// <summary>ConfigWindow 中显示的按钮名称。</summary>
        public string ButtonLabel { get; }

        /// <summary>点击按钮时执行的 Unity 菜单路径。</summary>
        public string MenuItemPath { get; }

        /// <summary>同一 Config 下多个动作的显示顺序。</summary>
        public int Order { get; }
    }
}
