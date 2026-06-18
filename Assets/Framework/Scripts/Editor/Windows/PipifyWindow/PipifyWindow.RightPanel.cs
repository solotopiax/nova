/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifyWindow.RightPanel.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   Pipify 窗口右侧详情面板（Batch 信息 / Item ReorderableList / 参数内联编辑）
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class PipifyWindow : EditorWindow
    {
        /// <summary>
        /// 绘制右侧详情面板入口。
        /// 未选中 Batch 时居中显示引导 HelpBox；选中后依次绘制标题区、Items 列表、底部执行区。
        /// </summary>
        private void DrawRightPanel()
        {
            EditorUtil.Draw.Layout.Vertical(EditorStyles.helpBox, () =>
            {
                if (m_Settings == null || m_SettingsSO == null)
                {
                    EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "请先创建或选择 PipifySettingsSO" }, false);
                    return;
                }

                if (m_SelectedBatchIndex < 0 || m_SelectedBatchIndex >= m_Settings.Batches.Count)
                {
                    EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "请先在左侧选择或新建 Batch" }, false);
                    return;
                }

                m_SettingsSO.Update();
                Batch batch = m_Settings.Batches[m_SelectedBatchIndex];

                EnsureItemsListForSelectedBatch();

                m_RightScroll = EditorGUILayout.BeginScrollView(m_RightScroll, false, false);
                DrawBatchHeader(batch);
                EditorUtil.Draw.Space(4f);
                DrawItemsList();
                EditorUtil.Draw.Space(4f);
                DrawExecute();
                EditorGUILayout.EndScrollView();

                m_SettingsSO.ApplyModifiedProperties();
            }, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        }

        /// <summary>
        /// 绘制 Batch 标题区：Name 行（唯一性校验）+ Description 行。
        /// Name 编辑失焦或按 Enter/Tab 后提交；重复名称恢复原值并弹提示。
        /// </summary>
        /// <param name="batch">当前选中的 Batch。</param>
        private void DrawBatchHeader(Batch batch)
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label("名称：", false, GUILayout.Width(40));
                // GUI.SetNextControlName 是焦点标记，非绘制 API，允许裸用
                GUI.SetNextControlName("PipifyBatchNameField");
                string editedName = EditorUtil.Draw.TextField(batch.Name, false, GUILayout.ExpandWidth(true));
                if (editedName != batch.Name)
                {
                    // 实时校验唯一性：重复则恢复原值并弹提示
                    if (IsBatchNameDuplicate(editedName, m_SelectedBatchIndex))
                    {
                        Log.Warning(LogTag.Editor, "{0} Batch 名称 \"{1}\" 已存在，已恢复原名。", c_LogTag, editedName);
                        EditorUtility.DisplayDialog("名称重复", $"已存在名为 \"{editedName}\" 的 Batch，请使用其他名称。", "确认");
                        GUI.FocusControl(null);
                    }
                    else
                    {
                        batch.Name = editedName;
                        MarkDirty();
                    }
                }
            });

            EditorUtil.Draw.Space(2f);

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label("描述：", false, GUILayout.Width(40f));
                string editedDesc = EditorUtil.Draw.TextField(batch.Description ?? string.Empty, false, GUILayout.ExpandWidth(true));
                if (editedDesc != (batch.Description ?? string.Empty))
                {
                    batch.Description = editedDesc;
                    MarkDirty();
                }
            });
        }

        /// <summary>
        /// 绘制 Items ReorderableList（包含 header / element / height / addDropdown / remove 回调）。
        /// </summary>
        private void DrawItemsList()
        {
            if (m_ItemsList == null) return;
            m_ItemsList.DoLayoutList();
        }

        /// <summary>
        /// 绘制 ReorderableList 中单个 Item 行及其展开参数区。
        /// </summary>
        /// <param name="rect">Unity 分配的行 Rect（高度已由 elementHeightCallback 决定）。</param>
        /// <param name="index">Item 在列表中的索引。</param>
        /// <param name="isActive">是否为激活行。</param>
        /// <param name="isFocused">是否聚焦。</param>
        private void DrawItemElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            Batch batch = m_Settings.Batches[m_SelectedBatchIndex];
            if (index < 0 || index >= batch.Items.Count) return;
            BatchItem item = batch.Items[index];

            PipifyStepInfo info = EditorUtil.Pipify.Registry.FindById(item.StepId);
            string displayLabel = info != null
                ? $"[{info.Category}] {info.DisplayName}"
                : $"(未知 Step: {item.StepId})";

            // ── 主行区域 ──
            float rowY = rect.y + 1f;
            float rowH = c_ItemRowHeight;

            // 序号
            Rect indexRect = new Rect(rect.x, rowY, 24f, rowH);
            GUI.Label(indexRect, (index + 1).ToString(), EditorStyles.centeredGreyMiniLabel);

            // Step 名称
            Rect labelRect = new Rect(rect.x + 26f, rowY, rect.width - 26f - 56f, rowH);
            GUI.Label(labelRect, displayLabel, EditorStyles.label);

            // "参数配置" 按钮（仅当 Step 有参数时显示）
            bool hasParams = info != null && info.ParamsType != null;
            Rect foldRect = new Rect(rect.xMax - 103f, rowY, 70f, rowH);
            if (hasParams)
            {
                bool expanded = m_ExpandedItemIndices.Contains(index);
                // 参数配置按钮依赖 if(GUI.Button) 闭包控制展开态，需裸用
                if (GUI.Button(foldRect, "参数配置", EditorStyles.miniButton))
                {
                    if (expanded) m_ExpandedItemIndices.Remove(index);
                    else m_ExpandedItemIndices.Add(index);
                    Repaint();
                }
            }

            // 删除按钮
            Rect deleteRect = new Rect(rect.xMax - 24f, rowY, 22f, rowH);
            Color prevColor = GUI.color;
            GUI.color = new Color(0.75f, 0.25f, 0.25f);
            // 删除按钮依赖 index 闭包 + 需要立即修改列表，允许裸用 GUI.Button
            if (GUI.Button(deleteRect, "×", EditorStyles.miniButton))
            {
                // 延迟到 Repaint 之外执行，避免在 Layout/Repaint 阶段直接修改集合
                int capturedIndex = index;
                EditorApplication.delayCall += () =>
                {
                    if (m_Settings == null || m_SelectedBatchIndex < 0 || m_SelectedBatchIndex >= m_Settings.Batches.Count) return;
                    Batch b = m_Settings.Batches[m_SelectedBatchIndex];
                    if (capturedIndex < 0 || capturedIndex >= b.Items.Count) return;
                    b.Items.RemoveAt(capturedIndex);
                    // 删除后所有 > capturedIndex 的 key 均移位，整表清空最安全
                    InvalidateParamsCache(-1);
                    m_ExpandedItemIndices.Clear();
                    // 强制重建 ReorderableList（更新 itemsProp 绑定长度）
                    m_ItemsListBoundBatchIndex = -1;
                    EnsureItemsListForSelectedBatch();
                    MarkDirty();
                };
            }
            GUI.color = prevColor;

            // ── 展开参数区 ──
            if (!hasParams || !m_ExpandedItemIndices.Contains(index)) return;

            float paramsY = rect.y + c_ItemRowHeight + 2f;
            DrawItemParams(rect, paramsY, index, item, info);
        }

        /// <summary>
        /// 绘制单个 Item 的参数区（反射遍历参数类 public fields，按类型分发到对应控件）。
        /// 支持 PipifyVisibleWhen 显隐、PipifyDropdown 下拉、PipifyDynamicDefault 占位。
        /// </summary>
        /// <param name="rowRect">整行 Rect（用于定位）。</param>
        /// <param name="startY">参数区起始 Y 坐标。</param>
        /// <param name="index">Item 索引。</param>
        /// <param name="item">BatchItem 数据。</param>
        /// <param name="info">Step 元信息。</param>
        private void DrawItemParams(Rect rowRect, float startY, int index, BatchItem item, PipifyStepInfo info)
        {
            object paramsInstance = EnsureParamsInstance(index, item, info);
            if (paramsInstance == null) return;

            FieldInfo[] fields = info.ParamsType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            // 主 Step 文本起点约为 rect.x + 26f（序号列 24f + 2f 间距）；参数区整体相对主文本再错开一个汉字（c_ParamsInset）。
            float fieldX = rowRect.x + 26f + c_ParamsInset;
            float fieldW = rowRect.width - (fieldX - rowRect.x) - 8f;
            float labelW = 200f;

            EditorGUI.BeginChangeCheck();
            int rowIndex = 0;
            for (int f = 0; f < fields.Length; f++)
            {
                FieldInfo field = fields[f];
                if (!IsFieldVisible(field, paramsInstance, info.ParamsType)) continue;
                Rect fieldRect = new Rect(fieldX, startY + rowIndex * (c_ParamFieldHeight + 2f), fieldW, c_ParamFieldHeight);
                DrawParamField(fieldRect, field, paramsInstance, labelW);
                rowIndex++;
            }

            if (EditorGUI.EndChangeCheck())
            {
                item.ParamsJson = Util.Json.Serialize(paramsInstance);
                MarkDirty();
            }
        }

        /// <summary>
        /// 取或构建参数实例，并在首次构建时扫描不支持字段输出 Warning（每个 Item 仅打印一次）。
        /// </summary>
        /// <param name="index">Item 索引。</param>
        /// <param name="item">BatchItem 数据。</param>
        /// <param name="info">Step 元信息。</param>
        /// <returns>缓存的参数实例；ParamsType 为 null 时返回 null。</returns>
        private object EnsureParamsInstance(int index, BatchItem item, PipifyStepInfo info)
        {
            if (m_ParamsCache.TryGetValue(index, out object cached) && cached != null) return cached;

            object paramsInstance = BuildParamsInstance(item, info.ParamsType);
            m_ParamsCache[index] = paramsInstance;
            if (paramsInstance != null)
            {
                foreach (FieldInfo f in info.ParamsType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (IsSupportedFieldType(f)) continue;
                    Log.Warning(LogTag.Editor, "{0} 参数字段 {1} 类型 {2} 暂不支持内联编辑，已跳过。", c_LogTag, f.Name, f.FieldType.Name);
                }
            }
            return paramsInstance;
        }

        /// <summary>
        /// 判断字段类型是否被 PipifyWindow 内联编辑器支持（基础类型 / 枚举 / 标注了 PipifyDropdown 的 string）。
        /// </summary>
        /// <param name="field">字段元信息。</param>
        /// <returns>支持时返回 true，否则 false。</returns>
        private static bool IsSupportedFieldType(FieldInfo field)
        {
            Type ft = field.FieldType;
            if (ft == typeof(string) || ft == typeof(bool) || ft == typeof(int) || ft == typeof(float) || ft.IsEnum) return true;
            return false;
        }

        /// <summary>
        /// 按 PipifyVisibleWhen 特性判断字段是否可见；无特性时恒可见。
        /// 支持传递性可见：若依赖字段本身被 PipifyVisibleWhen 隐藏，则本字段一并隐藏，
        /// 避免父字段隐藏后子字段错误可见（例如切平台后 BuildAppBundle 隐藏但 SplitApplicationBinary 仍显示）。
        /// 内置防环机制：通过已访问字段名 HashSet 避免循环依赖死循环。
        /// </summary>
        /// <param name="field">字段元信息。</param>
        /// <param name="paramsInstance">参数对象实例。</param>
        /// <param name="paramsType">参数对象类型。</param>
        /// <returns>可见时返回 true。</returns>
        private static bool IsFieldVisible(FieldInfo field, object paramsInstance, Type paramsType)
        {
            return IsFieldVisibleInternal(field, paramsInstance, paramsType, new HashSet<string>());
        }

        /// <summary>
        /// IsFieldVisible 递归内部实现，携带已访问字段名集合防止循环依赖。
        /// </summary>
        /// <param name="field">字段元信息。</param>
        /// <param name="paramsInstance">参数对象实例。</param>
        /// <param name="paramsType">参数对象类型。</param>
        /// <param name="visited">已访问字段名集合（防环）。</param>
        /// <returns>可见时返回 true。</returns>
        private static bool IsFieldVisibleInternal(FieldInfo field, object paramsInstance, Type paramsType, HashSet<string> visited)
        {
            PipifyVisibleWhenAttribute attr = field.GetCustomAttribute<PipifyVisibleWhenAttribute>();
            if (attr == null) return true;
            FieldInfo dep = paramsType.GetField(attr.DependsOn, BindingFlags.Public | BindingFlags.Instance);
            if (dep == null) return true;
            // 防环：依赖字段已在本次递归路径中 → 视为可见，避免死循环
            if (!visited.Add(dep.Name)) return true;
            // 传递性：依赖字段自身不可见 → 本字段一并隐藏
            if (!IsFieldVisibleInternal(dep, paramsInstance, paramsType, visited)) return false;
            object depValue = dep.GetValue(paramsInstance);
            if (depValue == null) return false;
            int intValue;
            try { intValue = Convert.ToInt32(depValue); }
            catch { return true; }
            return attr.AnyOf != null && attr.AnyOf.Contains(intValue);
        }

        /// <summary>
        /// 绘制单个参数字段，按 FieldInfo 类型分发控件；不支持的类型绘制只读占位（Warning 由首次缓存构建时输出）。
        /// </summary>
        /// <param name="rect">绘制区域。</param>
        /// <param name="field">目标字段元信息。</param>
        /// <param name="paramsInstance">参数对象实例。</param>
        /// <param name="labelW">标签列宽度（像素）。</param>
        private void DrawParamField(Rect rect, FieldInfo field, object paramsInstance, float labelW)
        {
            const float c_LabelGap = 6f;
            Rect labelRect = new Rect(rect.x, rect.y, labelW - c_LabelGap, rect.height);
            Rect valueRect = new Rect(rect.x + labelW, rect.y, rect.width - labelW, rect.height);
            GUI.Label(labelRect, field.Name, EditorStyles.miniLabel);

            Type fieldType = field.FieldType;
            object currentValue = field.GetValue(paramsInstance);

            if (fieldType == typeof(string))
            {
                DrawStringParamField(valueRect, field, paramsInstance, currentValue);
            }
            else if (fieldType == typeof(bool))
            {
                bool v = (bool)(currentValue ?? false);
                bool newV = EditorGUI.Toggle(valueRect, v);
                field.SetValue(paramsInstance, newV);
            }
            else if (fieldType == typeof(int))
            {
                int v = (int)(currentValue ?? 0);
                int newV = EditorGUI.IntField(valueRect, v);
                field.SetValue(paramsInstance, newV);
            }
            else if (fieldType == typeof(float))
            {
                float v = (float)(currentValue ?? 0f);
                float newV = EditorGUI.FloatField(valueRect, v);
                field.SetValue(paramsInstance, newV);
            }
            else if (fieldType.IsEnum)
            {
                Enum v = currentValue as Enum ?? (Enum)Enum.GetValues(fieldType).GetValue(0);
                Enum newV = EditorGUI.EnumPopup(valueRect, v);
                field.SetValue(paramsInstance, newV);
            }
            else
            {
                // 不支持的类型（列表 / 复杂对象等）：仅绘制只读占位，Warning 由调用方在缓存构建时输出，此处不重复打印以避免每帧日志垃圾
                GUI.Label(valueRect, $"(不支持: {fieldType.Name})", EditorStyles.miniLabel);
            }
        }

        /// <summary>
        /// 绘制 string 字段：根据 PipifyDropdown / PipifyDynamicDefault 选择绘制方式。
        /// </summary>
        /// <param name="valueRect">值区 Rect。</param>
        /// <param name="field">字段元信息。</param>
        /// <param name="paramsInstance">参数对象实例。</param>
        /// <param name="currentValue">当前字段值（object 形式）。</param>
        private void DrawStringParamField(Rect valueRect, FieldInfo field, object paramsInstance, object currentValue)
        {
            string v = currentValue as string ?? string.Empty;
            PipifyDropdownAttribute dropdown = field.GetCustomAttribute<PipifyDropdownAttribute>();
            if (dropdown != null)
            {
                string selected = DrawDropdownStringField(valueRect, dropdown.InterfaceType, v);
                if (!string.Equals(selected, v, StringComparison.Ordinal))
                {
                    field.SetValue(paramsInstance, selected);
                }
                return;
            }

            PipifyDynamicDropdownAttribute dynamicDropdown = field.GetCustomAttribute<PipifyDynamicDropdownAttribute>();
            if (dynamicDropdown != null)
            {
                string selected = DrawDynamicDropdownStringField(valueRect, dynamicDropdown, v);
                if (!string.Equals(selected, v, StringComparison.Ordinal))
                {
                    field.SetValue(paramsInstance, selected);
                }
                return;
            }

            PipifyDynamicDefaultAttribute dyn = field.GetCustomAttribute<PipifyDynamicDefaultAttribute>();
            if (dyn != null && string.IsNullOrEmpty(v))
            {
                string placeholder = ResolveDynamicDefault(dyn);
                string newV = EditorGUI.TextField(valueRect, v);
                if (!string.IsNullOrEmpty(placeholder) && string.IsNullOrEmpty(newV) && Event.current.type == EventType.Repaint)
                {
                    GUIStyle phStyle = EnsurePlaceholderStyle();
                    Rect phRect = new Rect(valueRect.x + 4f, valueRect.y, valueRect.width - 4f, valueRect.height);
                    GUI.Label(phRect, placeholder, phStyle);
                }
                field.SetValue(paramsInstance, newV);
                return;
            }

            string plain = EditorGUI.TextField(valueRect, v);
            field.SetValue(paramsInstance, plain);
        }

        /// <summary>
        /// 绘制接口实现类下拉框，存储值为选中类型的 FullName；选中"未配置"则存空串。
        /// </summary>
        /// <param name="valueRect">值区 Rect。</param>
        /// <param name="interfaceType">用于收集实现类的接口或基类。</param>
        /// <param name="currentClassName">当前选中的类型 FullName。</param>
        /// <returns>新选中的 FullName（"未配置" 返回空串）。</returns>
        private string DrawDropdownStringField(Rect valueRect, Type interfaceType, string currentClassName)
        {
            string[] displays = ResolveDropdownDisplays(interfaceType, out string[] fullNames);
            if (displays == null || displays.Length == 0)
            {
                GUI.Label(valueRect, $"(未发现 {interfaceType.Name} 实现)", EditorStyles.miniLabel);
                return currentClassName;
            }

            int curIndex = 0;
            for (int i = 0; i < fullNames.Length; i++)
            {
                if (fullNames[i] == currentClassName) { curIndex = i; break; }
            }
            int newIndex = EditorGUI.Popup(valueRect, curIndex, displays);
            if (newIndex < 0 || newIndex >= fullNames.Length) return currentClassName;
            return fullNames[newIndex];
        }

        /// <summary>
        /// 取或构建指定接口类型的下拉显示项缓存；选项为该接口/基类的全部可实例化实现类型 FullName。
        /// </summary>
        /// <param name="interfaceType">接口或基类类型。</param>
        /// <param name="fullNames">对应每个显示项的 FullName 数组。</param>
        /// <returns>显示项数组。</returns>
        private string[] ResolveDropdownDisplays(Type interfaceType, out string[] fullNames)
        {
            if (m_DropdownDisplaysCache.TryGetValue(interfaceType, out string[] cachedDisplays)
                && m_DropdownFullNamesCache.TryGetValue(interfaceType, out string[] cachedFullNames))
            {
                fullNames = cachedFullNames;
                return cachedDisplays;
            }

            string[] typeNames = EditorUtil.TypeCache.GetTypeNames(interfaceType);
            int total = typeNames?.Length ?? 0;
            string[] displays = new string[total];
            string[] names = new string[total];
            for (int i = 0; i < total; i++)
            {
                displays[i] = typeNames[i];
                names[i] = typeNames[i];
            }
            m_DropdownDisplaysCache[interfaceType] = displays;
            m_DropdownFullNamesCache[interfaceType] = names;
            fullNames = names;
            return displays;
        }

        /// <summary>
        /// 绘制动态选项下拉框（每帧调用 ProviderType.MethodName 取选项数组）；存储值为选项字符串本身。
        /// 选项数组为空或调用失败时退化为只读 TextField，避免阻塞参数填写。
        /// </summary>
        /// <param name="valueRect">值区 Rect。</param>
        /// <param name="attr">动态下拉特性。</param>
        /// <param name="currentValue">当前字段值。</param>
        /// <returns>新选中的选项字符串；选项不可用时原样返回。</returns>
        private static string DrawDynamicDropdownStringField(Rect valueRect, PipifyDynamicDropdownAttribute attr, string currentValue)
        {
            string[] options = ResolveDynamicDropdownOptions(attr);
            if (options == null || options.Length == 0)
            {
                return EditorGUI.TextField(valueRect, currentValue);
            }
            int curIndex = 0;
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] == currentValue) { curIndex = i; break; }
            }
            int newIndex = EditorGUI.Popup(valueRect, curIndex, options);
            if (newIndex < 0 || newIndex >= options.Length) return currentValue;
            return options[newIndex];
        }

        /// <summary>
        /// 反射调用 PipifyDynamicDropdown 指向的无参 static 方法获取动态选项数组；任何异常返回 null。
        /// </summary>
        /// <param name="attr">动态下拉特性。</param>
        /// <returns>选项数组；失败时返回 null。</returns>
        private static string[] ResolveDynamicDropdownOptions(PipifyDynamicDropdownAttribute attr)
        {
            try
            {
                MethodInfo m = attr.ProviderType.GetMethod(attr.MethodName, BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                if (m == null) return null;
                object result = m.Invoke(null, null);
                return result as string[];
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.Editor, "[PipifyWindow] 解析动态下拉选项失败：{0}.{1} 异常：{2}", attr.ProviderType.Name, attr.MethodName, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 反射调用 PipifyDynamicDefault 指向的无参 static 方法获取动态默认值字符串；任何异常返回空串。
        /// </summary>
        /// <param name="attr">动态默认值特性。</param>
        /// <returns>默认值字符串；调用失败返回空串。</returns>
        private static string ResolveDynamicDefault(PipifyDynamicDefaultAttribute attr)
        {
            try
            {
                MethodInfo m = attr.ProviderType.GetMethod(attr.MethodName, BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                if (m == null) return string.Empty;
                object result = m.Invoke(null, null);
                return result as string ?? string.Empty;
            }
            catch (Exception ex)
            {
                Log.Warning(LogTag.Editor, "[PipifyWindow] 解析动态默认值失败：{0}.{1} 异常：{2}", attr.ProviderType.Name, attr.MethodName, ex.Message);
                return string.Empty;
            }
        }

        /// <summary>
        /// 构建占位文本绘制样式（灰度斜体），延迟初始化复用。
        /// </summary>
        /// <returns>占位文本样式。</returns>
        private GUIStyle EnsurePlaceholderStyle()
        {
            if (m_PlaceholderStyle == null)
            {
                m_PlaceholderStyle = new GUIStyle(EditorStyles.label)
                {
                    fontStyle = FontStyle.Italic,
                    normal = { textColor = new Color(0.55f, 0.55f, 0.55f) }
                };
            }
            return m_PlaceholderStyle;
        }
    }
}
