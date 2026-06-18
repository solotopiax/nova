/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPStoreConfigListDrawer.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   IAPStoreConfigList 自定义 PropertyDrawer，继承 SerializeReferenceListDrawer 基类
 ***************************************************************/

using System;
using System.Reflection;
using UnityEditor;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Editor
{
    /// <summary>
    /// IAPStoreConfigList 的自定义 PropertyDrawer。
    /// 继承 SerializeReferenceListDrawer 泛型基类，只实现业务相关的 4 个抽象 hook。
    /// 扫描条件：非抽象、非接口且标注 [Serializable] 的 IIAPStoreConfig 实现类。
    /// </summary>
    [CustomPropertyDrawer(typeof(IAPStoreConfigList))]
    public sealed class IAPStoreConfigListDrawer : NovaFramework.Editor.SerializeReferenceListDrawer<IIAPStoreConfig, IAPStoreConfigList>
    {
        /// <summary>
        /// 列表头标题文案。
        /// </summary>
        protected override string HeaderTitle => "Store 列表";

        /// <summary>
        /// + 菜单无可用条目时显示的禁用项文案。
        /// </summary>
        protected override string EmptyAddMenuLabel => "无可用 Store 配置实现";

        /// <summary>
        /// 从已实例化的 store 配置元素提取折叠头展示文案（优先 StoreType 枚举名）。
        /// </summary>
        /// <param name="item">已实例化的 IIAPStoreConfig 元素。</param>
        /// <param name="index">元素在数组中的下标（用于回退文案）。</param>
        /// <returns>折叠头显示字符串。</returns>
        protected override string GetEntryLabel(IIAPStoreConfig item, int index) => item.StoreType.ToString();

        /// <summary>
        /// + 菜单类型过滤谓词：标注 [Serializable] 的 IIAPStoreConfig 实现类（基类已过滤抽象/接口）。
        /// </summary>
        /// <param name="type">候选类型（已保证非抽象、非接口）。</param>
        /// <returns>是否允许添加该类型。</returns>
        protected override bool FilterAddableType(Type type)
            => type.GetCustomAttribute<SerializableAttribute>() != null;
    }
}
