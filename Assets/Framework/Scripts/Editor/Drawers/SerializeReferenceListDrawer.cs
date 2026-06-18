/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SerializeReferenceListDrawer.cs
 * author:    taoye
 * created:   2026/5/25
 * descrip:   多态 SerializeReference 列表 PropertyDrawer 泛型抽象基类
 ***************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 多态 SerializeReference 列表 PropertyDrawer 泛型抽象基类。
    /// 提供折叠行 + 删除按钮 + GenericMenu 追加 + Tooltip HelpBox 的标准渲染模板方法。
    /// 子类只需 override 若干 hook 即可完成具体业务列表的绘制，无需重复实现骨架逻辑。
    /// </summary>
    /// <remarks>
    /// 重要约束：Unity 的 [CustomPropertyDrawer] 不能挂在泛型类型上。
    /// 子类必须各自标注 [CustomPropertyDrawer(typeof(具体包装类))]，基类不带此 Attribute。
    /// </remarks>
    /// <typeparam name="TInterface">列表元素的接口类型（如 IIAPStoreConfig / IAdChannelConfig）。</typeparam>
    /// <typeparam name="TWrapper">持有列表的 [Serializable] 包装类型（如 IAPStoreConfigList / AdChannelConfigList）。</typeparam>
    public abstract class SerializeReferenceListDrawer<TInterface, TWrapper> : PropertyDrawer
        where TInterface : class
        where TWrapper : class
    {
        /// <summary>
        /// 标题行高度。
        /// </summary>
        private const float c_HeaderHeight = 20f;

        /// <summary>
        /// 标题行底部附加间距。
        /// </summary>
        private const float c_HeaderPadding = 4f;

        /// <summary>
        /// 每个 entry 行之间的间距。
        /// </summary>
        private const float c_EntrySpacing = 2f;

        /// <summary>
        /// 底部 + 按钮行高度。
        /// </summary>
        private const float c_AddButtonHeight = 20f;

        /// <summary>
        /// 底部 + 按钮行顶部附加间距。
        /// </summary>
        private const float c_AddButtonPadding = 4f;

        /// <summary>
        /// - / + 操作按钮固定宽度。
        /// </summary>
        private const float c_ButtonWidth = 20f;

        /// <summary>
        /// 子字段绘制左侧缩进量。
        /// </summary>
        private const float c_FieldIndent = 12f;

        /// <summary>
        /// 列表头标题文案（如"渠道列表" / "Store 列表"）。
        /// </summary>
        protected abstract string HeaderTitle { get; }

        /// <summary>
        /// 从已实例化的列表元素提取折叠头展示文案。
        /// </summary>
        /// <param name="item">已实例化的 TInterface 元素。</param>
        /// <param name="index">元素在数组中的下标（用于回退文案）。</param>
        /// <returns>折叠头显示字符串。</returns>
        protected abstract string GetEntryLabel(TInterface item, int index);

        /// <summary>
        /// + 菜单的类型过滤谓词；返回 true 表示该具体类型可作为新元素添加。
        /// </summary>
        /// <param name="type">候选类型（已保证非抽象、非接口）。</param>
        /// <returns>是否允许添加该类型。</returns>
        protected abstract bool FilterAddableType(Type type);

        /// <summary>
        /// + 菜单无可用条目时显示的禁用项文案。
        /// </summary>
        protected abstract string EmptyAddMenuLabel { get; }

        /// <summary>
        /// 包装类内部存储列表的字段名；默认 "m_Items"。
        /// 子类包装类使用不同字段名时 override 此属性。
        /// </summary>
        protected virtual string ItemsPropertyName => "m_Items";

        /// <summary>
        /// 列表前置全局字段绘制（可选 hook，默认空实现）。
        /// 子类有全局配置字段时 override 此方法绘制，返回实际消耗的 y 推进量。
        /// </summary>
        /// <param name="position">Inspector 分配给本 Drawer 的完整矩形。</param>
        /// <param name="startY">本次绘制起始 y 坐标。</param>
        /// <param name="property">父级 SerializedProperty。</param>
        /// <param name="wrapper">包装类托管实例；路径解析失败时为 null，子类需自行短路。</param>
        /// <returns>绘制完成后的 y 推进量（不含 startY 本身）。</returns>
        protected virtual float DrawGlobalFields(Rect position, float startY, SerializedProperty property, TWrapper wrapper) => 0f;

        /// <summary>
        /// 列表前置全局字段所需总高度（可选 hook，默认 0）。
        /// 与 DrawGlobalFields 对称，字段集合与顺序必须严格一致。
        /// </summary>
        /// <param name="property">父级 SerializedProperty。</param>
        /// <param name="wrapper">包装类托管实例；路径解析失败时为 null，子类需自行短路。</param>
        /// <returns>全局字段区域所需总高度（像素）。</returns>
        protected virtual float GetGlobalFieldsHeight(SerializedProperty property, TWrapper wrapper) => 0f;

        /// <summary>
        /// 模板方法：渲染完整的多态列表 Inspector 区域。
        /// 子类不得 override 此方法；只 override hook 方法。
        /// 渲染流程：全局字段 → 标题行 → 列表 entry → + 按钮。
        /// </summary>
        /// <param name="position">Inspector 分配给本 Drawer 的矩形区域。</param>
        /// <param name="property">目标 SerializedProperty（包装类字段）。</param>
        /// <param name="label">显示标签（传给 BeginProperty）。</param>
        public sealed override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            TWrapper wrapper = ResolveWrapperInstance(property);
            SerializedProperty itemsProp = property.FindPropertyRelative(ItemsPropertyName);
            float y = position.y;

            y += DrawGlobalFields(position, y, property, wrapper);
            y = DrawHeader(position, y, itemsProp);
            y = DrawEntries(position, y, property, itemsProp);
            DrawAddButton(position, y, property, itemsProp);

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// 模板方法：计算本 Drawer 所需的总像素高度。
        /// 子类不得 override 此方法；只 override 高度 hook 方法。
        /// </summary>
        /// <param name="property">目标 SerializedProperty（包装类字段）。</param>
        /// <param name="label">显示标签（未使用，符合 PropertyDrawer 签名）。</param>
        /// <returns>本 Drawer 所需总高度（像素）。</returns>
        public sealed override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            TWrapper wrapper = ResolveWrapperInstance(property);
            float h = GetGlobalFieldsHeight(property, wrapper);
            h += c_HeaderHeight + c_HeaderPadding;
            SerializedProperty itemsProp = property.FindPropertyRelative(ItemsPropertyName);
            if (itemsProp != null)
            {
                for (int i = 0; i < itemsProp.arraySize; i++)
                {
                    h += GetEntryHeight(itemsProp.GetArrayElementAtIndex(i));
                    h += c_EntrySpacing;
                }
            }
            h += c_AddButtonPadding + c_AddButtonHeight;
            return h;
        }

        /// <summary>
        /// 绘制标题行（粗体标签 + 元素计数）。
        /// </summary>
        /// <param name="position">Inspector 完整矩形。</param>
        /// <param name="y">当前绘制起始 y。</param>
        /// <param name="itemsProp">m_Items SerializedProperty，用于读取 arraySize。</param>
        /// <returns>绘制后的新 y 值。</returns>
        private float DrawHeader(Rect position, float y, SerializedProperty itemsProp)
        {
            int count = itemsProp != null ? itemsProp.arraySize : 0;
            Rect headerRect = new Rect(position.x, y, position.width, c_HeaderHeight);
            EditorUtil.Draw.Label(headerRect, $"{HeaderTitle} ({count})", EditorStyles.boldLabel);
            return y + c_HeaderHeight + c_HeaderPadding;
        }

        /// <summary>
        /// 绘制所有 entry 行（折叠头 + 删除按钮 + 展开字段 + Tooltip HelpBox）。
        /// </summary>
        /// <param name="position">Inspector 完整矩形。</param>
        /// <param name="y">当前绘制起始 y。</param>
        /// <param name="property">父级 SerializedProperty（用于 ApplyModifiedProperties）。</param>
        /// <param name="itemsProp">m_Items SerializedProperty。</param>
        /// <returns>绘制完所有 entry 后的新 y 值。</returns>
        private float DrawEntries(Rect position, float y, SerializedProperty property, SerializedProperty itemsProp)
        {
            if (itemsProp == null)
            {
                return y;
            }

            for (int i = 0; i < itemsProp.arraySize; i++)
            {
                SerializedProperty entryProp = itemsProp.GetArrayElementAtIndex(i);

                Rect deleteRect = new Rect(position.x + position.width - c_ButtonWidth, y, c_ButtonWidth, EditorGUIUtility.singleLineHeight);

                string entryLabel = entryProp.managedReferenceValue is TInterface item
                    ? GetEntryLabel(item, i)
                    : "Element " + i;

                Rect foldoutRect = new Rect(position.x, y, position.width - c_ButtonWidth - 2f, EditorGUIUtility.singleLineHeight);
                bool expanded = entryProp.isExpanded;
                entryProp.isExpanded = EditorUtil.Draw.Foldout(foldoutRect, ref expanded, entryLabel, true, EditorStyles.foldout);
                y += EditorGUIUtility.singleLineHeight;

                if (EditorUtil.Draw.Button(deleteRect, "-", false))
                {
                    itemsProp.DeleteArrayElementAtIndex(i);
                    property.serializedObject.ApplyModifiedProperties();
                    break;
                }

                if (entryProp.isExpanded && entryProp.managedReferenceValue != null)
                {
                    object target = entryProp.managedReferenceValue;
                    SerializedProperty child = entryProp.Copy();
                    SerializedProperty end = entryProp.GetEndProperty();
                    bool enterChildren = true;
                    while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
                    {
                        float childHeight = EditorGUI.GetPropertyHeight(child, true);
                        Rect childRect = new Rect(position.x + c_FieldIndent, y, position.width - c_FieldIndent, childHeight);
                        EditorUtil.Draw.PropertyField(childRect, child, child.displayName, includeChildren: true);
                        y += childHeight + 2f;

                        string tooltip = EditorUtil.Reflect.GetFieldTooltip(target, child.name);
                        if (!string.IsNullOrEmpty(tooltip))
                        {
                            float helpHeight = EditorUtil.Draw.CalcHelpBoxHeight(MessageType.Info, tooltip, position.width - c_FieldIndent);
                            Rect helpRect = new Rect(position.x + c_FieldIndent, y, position.width - c_FieldIndent, helpHeight);
                            EditorUtil.Draw.HelpBox(helpRect, MessageType.Info, tooltip);
                            y += helpHeight + 4f;
                        }
                        enterChildren = false;
                    }
                }

                y += c_EntrySpacing;
            }

            return y;
        }

        /// <summary>
        /// 绘制底部 + 按钮行（居右）。
        /// </summary>
        /// <param name="position">Inspector 完整矩形。</param>
        /// <param name="y">当前绘制起始 y。</param>
        /// <param name="property">父级 SerializedProperty（用于 ApplyModifiedProperties）。</param>
        /// <param name="itemsProp">m_Items SerializedProperty。</param>
        private void DrawAddButton(Rect position, float y, SerializedProperty property, SerializedProperty itemsProp)
        {
            y += c_AddButtonPadding;
            Rect addRect = new Rect(position.x + position.width - c_ButtonWidth, y, c_ButtonWidth, c_AddButtonHeight);
            if (EditorUtil.Draw.Button(addRect, "+", false))
            {
                ShowAddMenu(itemsProp, property);
            }
        }

        /// <summary>
        /// 计算单个 entry 的总高度（折叠头 + 展开时的子字段行 + Tooltip HelpBox）。
        /// 使用 EditorGUIUtility.currentViewWidth 近似当前面板宽度，与 OnGUI 阶段绘制宽度保持一致，
        /// 避免因固定宽度导致高度估算偏差（折行过多或过少）。
        /// </summary>
        /// <param name="entryProp">entry 对应的 SerializedProperty。</param>
        /// <returns>该 entry 所需像素高度。</returns>
        private static float GetEntryHeight(SerializedProperty entryProp)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!entryProp.isExpanded || entryProp.managedReferenceValue == null)
            {
                return height;
            }

            float availableWidth = EditorGUIUtility.currentViewWidth - c_FieldIndent;
            object target = entryProp.managedReferenceValue;
            SerializedProperty child = entryProp.Copy();
            SerializedProperty end = entryProp.GetEndProperty();
            bool enterChildren = true;
            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                height += EditorGUI.GetPropertyHeight(child, true) + 2f;
                string tooltip = EditorUtil.Reflect.GetFieldTooltip(target, child.name);
                if (!string.IsNullOrEmpty(tooltip))
                {
                    height += EditorUtil.Draw.CalcHelpBoxHeight(MessageType.Info, tooltip, availableWidth) + 4f;
                }
                enterChildren = false;
            }
            return height;
        }

        /// <summary>
        /// 弹出 GenericMenu，列出满足 FilterAddableType 的全部 TInterface 派生具体类型。
        /// 已存在的类型显示为禁用项；菜单为空时显示 EmptyAddMenuLabel 禁用项。
        /// </summary>
        /// <param name="itemsProp">m_Items SerializedProperty，用于执行数组插入。</param>
        /// <param name="property">父级 SerializedProperty，用于提交修改。</param>
        private void ShowAddMenu(SerializedProperty itemsProp, SerializedProperty property)
        {
            var existingTypes = new HashSet<Type>();
            if (itemsProp != null)
            {
                for (int i = 0; i < itemsProp.arraySize; i++)
                {
                    object val = itemsProp.GetArrayElementAtIndex(i).managedReferenceValue;
                    if (val != null)
                    {
                        existingTypes.Add(val.GetType());
                    }
                }
            }

            GenericMenu menu = new GenericMenu();
            var types = TypeCache.GetTypesDerivedFrom<TInterface>();
            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsInterface)
                {
                    continue;
                }
                if (!FilterAddableType(type))
                {
                    continue;
                }
                var capturedType = type;
                if (existingTypes.Contains(type))
                {
                    menu.AddDisabledItem(new GUIContent(type.Name + " (已添加)"));
                }
                else
                {
                    menu.AddItem(new GUIContent(type.Name), false, () =>
                    {
                        itemsProp.InsertArrayElementAtIndex(itemsProp.arraySize);
                        SerializedProperty newElem = itemsProp.GetArrayElementAtIndex(itemsProp.arraySize - 1);
                        newElem.managedReferenceValue = Activator.CreateInstance(capturedType);
                        property.serializedObject.ApplyModifiedProperties();
                    });
                }
            }

            if (menu.GetItemCount() == 0)
            {
                menu.AddDisabledItem(new GUIContent(EmptyAddMenuLabel));
            }
            menu.ShowAsContext();
        }

        /// <summary>
        /// 通过反射 path-walking 从 SerializedProperty 路径中提取 TWrapper 托管实例。
        /// 支持普通字段路径与数组索引路径（".Array.data[n]" 格式自动规范化为 "[n]"）。
        /// 路径解析失败时返回 null；调用方（hook）须对 null 做短路处理。
        /// </summary>
        /// <param name="property">目标 SerializedProperty。</param>
        /// <returns>对应的 TWrapper 实例，或 null。</returns>
        private static TWrapper ResolveWrapperInstance(SerializedProperty property)
        {
            object current = property.serializedObject.targetObject;
            string path = property.propertyPath.Replace(".Array.data[", "[");
            string[] parts = path.Split('.');
            foreach (string part in parts)
            {
                if (current == null)
                {
                    return null;
                }
                if (part.Contains("["))
                {
                    int bracketIdx = part.IndexOf('[');
                    string fieldName = part[..bracketIdx];
                    int index = int.Parse(part[(bracketIdx + 1)..^1]);
                    FieldInfo field = current.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    IList list = field?.GetValue(current) as IList;
                    current = list?[index];
                }
                else
                {
                    FieldInfo field = current.GetType().GetField(part, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    current = field?.GetValue(current);
                }
            }
            return current as TWrapper;
        }
    }
}
