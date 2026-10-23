/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PrivacyPanelExtensions.cs
 * author:    Codex
 * created:   2026/10/21
 * descrip:   供可选框架包注册隐私页扩展绘制能力
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 隐私页通用扩展注册点；主框架不引用扩展类型，所有写入仍由当前工作副本的统一保存链处理。
    /// </summary>
    public static class PrivacyPanelExtensions
    {
        private sealed class Registration
        {
            internal string Key;
            internal int Order;
            internal Action<SerializedProperty, Action> Drawer;
        }

        private static readonly List<Registration> s_Registrations = new();

        /// <summary>
        /// 注册或替换一个扩展；按顺序值与键稳定排序，回调仅可编辑传入的 Privacy 工作副本属性。
        /// </summary>
        /// <param name="extensionKey">稳定且唯一的扩展键，建议使用包标识。</param>
        /// <param name="order">AES 区域后、iOS 区域前的绘制顺序，较小值先绘制。</param>
        /// <param name="drawer">接收当前 Privacy 属性与窗口重绘回调，不应直接保存真实资产。</param>
        public static void Register(string extensionKey, int order, Action<SerializedProperty, Action> drawer)
        {
            if (string.IsNullOrWhiteSpace(extensionKey)) throw new ArgumentException("扩展键不能为空。", nameof(extensionKey));
            if (drawer == null) throw new ArgumentNullException(nameof(drawer));
            Unregister(extensionKey);
            s_Registrations.Add(new Registration { Key = extensionKey, Order = order, Drawer = drawer });
            s_Registrations.Sort(CompareRegistrations);
        }

        /// <summary>移除指定扩展；不存在时保持注册集合不变。</summary>
        /// <param name="extensionKey">注册时使用的稳定键。</param>
        public static void Unregister(string extensionKey)
        {
            s_Registrations.RemoveAll(item => string.Equals(item.Key, extensionKey, StringComparison.Ordinal));
        }

        /// <summary>按稳定顺序调用扩展，传入的序列化对象继续由 ConfigWindow 统一应用并广播。</summary>
        /// <param name="privacy">当前坐标的 Privacy 工作副本属性。</param>
        /// <param name="repaint">窗口重绘回调。</param>
        internal static void Draw(SerializedProperty privacy, Action repaint)
        {
            if (privacy == null) return;
            foreach (Registration item in s_Registrations.ToArray()) item.Drawer(privacy.Copy(), repaint);
        }

        /// <summary>先比较绘制顺序，再以稳定键排序，避免程序集加载顺序影响布局。</summary>
        /// <param name="left">左侧注册记录。</param>
        /// <param name="right">右侧注册记录。</param>
        /// <returns>稳定排序的比较结果。</returns>
        private static int CompareRegistrations(Registration left, Registration right)
        {
            int order = left.Order.CompareTo(right.Order);
            return order != 0 ? order : string.CompareOrdinal(left.Key, right.Key);
        }
    }
}
