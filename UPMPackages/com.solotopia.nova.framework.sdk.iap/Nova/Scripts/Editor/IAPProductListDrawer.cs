/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPProductListDrawer.cs
 * author:    yingzheng
 * created:   2026/6/3
 * descrip:   IAPProductList 自定义 PropertyDrawer，对齐 IAPConfigWindow 布局规则与交互逻辑
 ***************************************************************/

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using NovaFramework.SDK.IAP.Runtime;
using EdUtil = NovaFramework.Editor.EditorUtil;

namespace NovaFramework.SDK.IAP.Editor
{
    /// <summary>
    /// IAPProductList 的自定义 PropertyDrawer。
    /// 绘制搜索栏 + 列头行（含全选 Toggle） + 水平/垂直双向可滚动商品列表（行内 Toggle 选中 + 行内删除按钮）
    /// + 底部添加/批量删除工具栏，列宽与交互逻辑完整对齐 IAPConfigWindow。
    /// </summary>
    [CustomPropertyDrawer(typeof(IAPProductList))]
    public sealed class IAPProductListDrawer : PropertyDrawer
    {
        /// <summary>
        /// 单行行高（像素），与 IAPConfigWindow.c_RowHeight 保持一致。
        /// </summary>
        private const float c_RowHeight = 20f;

        /// <summary>
        /// 搜索栏行高（像素）。
        /// </summary>
        private const float c_ToolbarHeight = 20f;

        /// <summary>
        /// 底部工具栏行高（像素）。
        /// </summary>
        private const float c_FooterHeight = 22f;

        /// <summary>
        /// 商品滚动区域固定高度（像素）。
        /// </summary>
        private const float c_ProductsAreaHeight = 300f;

        /// <summary>
        /// 行首选择 Toggle 宽度（像素），与 IAPConfigWindow.DrawListHeader / DrawRow 首列对齐。
        /// </summary>
        private const float c_ToggleWidth = 20f;

        /// <summary>
        /// 末尾"删除"按钮宽度（像素），与 IAPConfigWindow.DrawRow 末尾按钮对齐。
        /// </summary>
        private const float c_OperationWidth = 56f;

        /// <summary>
        /// 每行总像素宽度 = Toggle(20) + 各列宽之和(1090) + 删除按钮(56) = 1166。
        /// 作为 ScrollView viewRect 的固定宽度，支持水平滚动。
        /// </summary>
        private const float c_TotalRowWidth = 1166f;

        /// <summary>
        /// 各列宽度数组（像素），顺序与 s_ColHeaders 及 IAPConfigWindow.s_ColWidths 严格对齐：
        /// 表ID / 名称 / 商品ID / 第三方商品id / 类型 / 价格 / 货币 / 订阅组 / 备注。
        /// 商品ID 与 第三方商品id 两列加宽至 240px，便于查看与编辑较长的商品标识。
        /// </summary>
        private static readonly float[] s_ColWidths = { 120f, 100f, 240f, 240f, 80f, 60f, 50f, 60f, 140f };

        /// <summary>
        /// 列头标题数组，与 s_ColWidths 一一对应。
        /// </summary>
        private static readonly string[] s_ColHeaders = { "表ID", "名称", "商品ID", "第三方商品id", "类型", "价格", "货币", "订阅组", "备注" };

        /// <summary>
        /// 奇数行斑马纹背景色，与 IAPConfigWindow.DrawListBody 视觉一致。
        /// </summary>
        private static readonly Color s_ZebraRowColor = new Color(0f, 0f, 0f, 0.08f);

        /// <summary>
        /// 选中行高亮色（蓝色半透明），与 IAPConfigWindow.DrawRow 选中视觉一致。
        /// </summary>
        private static readonly Color s_SelectedRowColor = new Color(0.27f, 0.5f, 0.81f, 0.3f);

        /// <summary>
        /// 商品列表滚动位置（各 DrawerInstance 独立持有，水平 + 垂直均支持）。
        /// </summary>
        private Vector2 m_ScrollPos;

        /// <summary>
        /// 搜索文本（各 DrawerInstance 独立持有）；变化时同步清空 m_SelectedIndices。
        /// </summary>
        private string m_SearchText = string.Empty;

        /// <summary>
        /// 选中行的真实数组索引集合（各 DrawerInstance 独立持有）；用于全选 Toggle 和批量删除。
        /// </summary>
        private HashSet<int> m_SelectedIndices = new HashSet<int>();

        /// <summary>
        /// 上一帧已知的 m_Items 数组长度；用于检测 arraySize 变化，按需重建过滤索引和重复 ID 集合。
        /// </summary>
        private int m_CachedArraySize = -1;

        /// <summary>
        /// 上一帧已知的搜索文本；变化时重建过滤索引。
        /// </summary>
        private string m_CachedSearchText = null;

        /// <summary>
        /// 缓存的过滤索引列表；仅在 arraySize 或 searchText 变化时重建，避免每帧遍历 SP。
        /// </summary>
        private List<int> m_CachedFilteredIndices = new List<int>();

        /// <summary>
        /// 存在重复 tableId 的 id 集合；由 RebuildDuplicateIds 构建，行绘制时查询以决定是否爆红。
        /// </summary>
        private HashSet<long> m_DuplicateTableIds = new HashSet<long>();

        /// <summary>
        /// 重复 ID 集合是否需要重建；arraySize 变化或行字段编辑时置 true，OnGUI 开始时检查并重建。
        /// </summary>
        private bool m_DuplicatesDirty = true;

        /// <summary>
        /// 计算 IAPProductList 属性的总绘制高度：
        /// 搜索栏 + 列头行 + 滚动区 + 底部工具栏及各行间距之和。
        /// </summary>
        /// <param name="property">对应 IAPProductList 的 SerializedProperty。</param>
        /// <param name="label">显示标签（保留供基类接口使用）。</param>
        /// <returns>整个属性绘制所需的像素高度。</returns>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return c_ToolbarHeight + EditorGUIUtility.standardVerticalSpacing
                + c_RowHeight + EditorGUIUtility.standardVerticalSpacing
                + c_ProductsAreaHeight + EditorGUIUtility.standardVerticalSpacing
                + c_FooterHeight;
        }

        /// <summary>
        /// 绘制 IAPProductList 属性：
        /// 整体 helpBox 背景 → 搜索栏 → 列头行（含全选 Toggle）→ 水平/垂直双向可滚动商品列表 → 底部工具栏。
        /// filteredIndices 在 OnGUI 层一次性计算，传给 Header、List、Footer 各子方法，避免重复遍历。
        /// </summary>
        /// <param name="position">Inspector 分配的绘制 Rect。</param>
        /// <param name="property">对应 IAPProductList 的 SerializedProperty。</param>
        /// <param name="label">显示标签。</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // 整体背景：helpBox 样式提供边框与背景色
            GUI.Box(position, GUIContent.none, EditorStyles.helpBox);

            SerializedProperty itemsProp = property.FindPropertyRelative("m_Items");
            float y = position.y + 2f;
            float x = position.x + 2f;
            float w = position.width - 4f;

            // ── 搜索栏 ──
            DrawSearchBar(new Rect(x, y, w, c_ToolbarHeight));
            y += c_ToolbarHeight + EditorGUIUtility.standardVerticalSpacing;

            // 仅在 arraySize 或搜索文本变化时重建过滤索引（避免每帧遍历 8000 条 SP）
            int currentArraySize = itemsProp?.arraySize ?? 0;
            if (m_CachedArraySize != currentArraySize || m_CachedSearchText != m_SearchText)
            {
                m_CachedArraySize = currentArraySize;
                m_CachedSearchText = m_SearchText;
                m_CachedFilteredIndices.Clear();
                m_CachedFilteredIndices.AddRange(GetFilteredIndices(itemsProp));
                m_DuplicatesDirty = true;
            }
            List<int> filteredIndices = m_CachedFilteredIndices;

            // 重复 ID 集合懒重建（arraySize/searchText 变化 或 行字段编辑后）
            if (m_DuplicatesDirty)
            {
                RebuildDuplicateIds(itemsProp);
                m_DuplicatesDirty = false;
            }

            // ── 列头行（含全选 Toggle）──
            int totalCount = itemsProp?.arraySize ?? 0;
            string countLabel = string.IsNullOrEmpty(m_SearchText)
                ? $"({totalCount})"
                : $"({filteredIndices.Count}/{totalCount})";
            DrawListHeader(new Rect(x, y, w, c_RowHeight), countLabel, filteredIndices);
            y += c_RowHeight + EditorGUIUtility.standardVerticalSpacing;

            // ── 水平 + 垂直双向可滚动商品列表 ──
            DrawProductsList(new Rect(x, y, w, c_ProductsAreaHeight), itemsProp, filteredIndices);
            y += c_ProductsAreaHeight + EditorGUIUtility.standardVerticalSpacing;

            // ── 底部工具栏 ──
            DrawFooter(new Rect(x, y, w, c_FooterHeight), itemsProp, filteredIndices.Count);
        }

        /// <summary>
        /// 绘制搜索栏：toolbar 背景 + 右对齐"搜索："标签（36px）+ 固定 220px toolbarSearchField + "×" 清除按钮（20px）。
        /// 布局策略对齐 IAPConfigWindow.DrawSearchBar：FlexibleSpace 推右，三控件紧贴右侧。
        /// 关键词变化时同步清空 m_SelectedIndices，保证选中态与过滤结果一致。
        /// </summary>
        /// <param name="rect">搜索栏绘制区域。</param>
        private void DrawSearchBar(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.toolbar);

            // 右对齐：搜索: 36 + TextField 220 + × 20 = 276px，从右端回退
            float labelW = 36f;
            float fieldW = 220f;
            float clearW = 20f;
            float lx = rect.xMax - labelW - fieldW - clearW;

            EdUtil.Draw.Label(new Rect(lx, rect.y, labelW, rect.height), "搜索：", false);
            lx += labelW;

            // toolbarSearchField 风格搜索框（Rect 版 EditorGUI.TextField，PropertyDrawer 中合规）
            string newSearch = EditorGUI.TextField(
                new Rect(lx, rect.y + 1f, fieldW, rect.height - 2f),
                m_SearchText, EditorStyles.toolbarSearchField);
            if (newSearch.Trim() != m_SearchText)
            {
                m_SearchText = newSearch.Trim();
                m_SelectedIndices.Clear();
            }
            lx += fieldW;

            // "×" 清除按钮（toolbarButton 样式，GUI.Button Rect 版合规）
            if (GUI.Button(new Rect(lx, rect.y, clearW, rect.height), "×", EditorStyles.toolbarButton))
            {
                m_SearchText = string.Empty;
                GUI.FocusControl(null);
                m_SelectedIndices.Clear();
            }
        }

        /// <summary>
        /// 绘制列头行：helpBox 背景 + 全选 Toggle + 各列标题（粗体） + 操作列标题 + 条数标签。
        /// 全选 Toggle 逻辑对齐 IAPConfigWindow.DrawListHeader：
        /// filteredIndices 全部命中 m_SelectedIndices 时为选中态；点击全选/取消全选操作 m_SelectedIndices。
        /// </summary>
        /// <param name="rect">列头行绘制区域。</param>
        /// <param name="countLabel">数量标签，格式 "(n)" 或 "(filtered/total)"。</param>
        /// <param name="filteredIndices">当前过滤后的真实数组索引列表，供全选判断使用。</param>
        private void DrawListHeader(Rect rect, string countLabel, List<int> filteredIndices)
        {
            GUI.Box(rect, GUIContent.none, "helpBox");

            float x = rect.x + 2f;

            // ── 全选 Toggle（对齐 IAPConfigWindow.DrawListHeader 首列 Toggle）──
            bool allSelected = filteredIndices.Count > 0;
            if (allSelected)
            {
                foreach (int idx in filteredIndices)
                {
                    if (!m_SelectedIndices.Contains(idx))
                    {
                        allSelected = false;
                        break;
                    }
                }
            }

            bool newAll = GUI.Toggle(new Rect(x, rect.y, c_ToggleWidth, rect.height), allSelected, GUIContent.none);
            if (newAll != allSelected)
            {
                if (newAll)
                {
                    foreach (int idx in filteredIndices)
                    {
                        m_SelectedIndices.Add(idx);
                    }
                }
                else
                {
                    m_SelectedIndices.Clear();
                }
            }
            x += c_ToggleWidth;

            // ── 各列标题 ──
            for (int i = 0; i < s_ColHeaders.Length; i++)
            {
                EdUtil.Draw.Label(new Rect(x, rect.y, s_ColWidths[i], rect.height), s_ColHeaders[i], EditorStyles.boldLabel, false);
                x += s_ColWidths[i];
            }

            // ── 操作列标题 ──
            EdUtil.Draw.Label(new Rect(x, rect.y, c_OperationWidth, rect.height), "操作", EditorStyles.boldLabel, false);
            x += c_OperationWidth;

            // ── 剩余空间显示条数 ──
            if (x < rect.xMax)
            {
                EdUtil.Draw.Label(new Rect(x, rect.y, rect.xMax - x, rect.height), countLabel, false);
            }
        }

        /// <summary>
        /// 绘制商品列表滚动区域：水平 + 垂直双向可滚动（viewRect 宽 = c_TotalRowWidth），
        /// 逐行绘制斑马纹、选中高亮、行内 Toggle 及可编辑字段，行内删除收集到 pendingDeleteRealIdx，
        /// 在 GUI.EndScrollView 之后再处理删除确认以避免打断 IMGUI 布局。
        /// 删除后同步修正 m_SelectedIndices（对齐 IAPConfigWindow.DrawListBody 索引修正逻辑）。
        /// </summary>
        /// <param name="rect">滚动区绘制 Rect。</param>
        /// <param name="productsProp">对应 m_Items 的 SerializedProperty（数组）。</param>
        /// <param name="filteredIndices">已由 OnGUI 计算好的过滤索引列表。</param>
        private void DrawProductsList(Rect rect, SerializedProperty productsProp, List<int> filteredIndices)
        {
            if (productsProp == null)
            {
                EdUtil.Draw.Label(rect, "m_Items 属性未找到", false);
                return;
            }

            float totalContentH = Mathf.Max(filteredIndices.Count * c_RowHeight, rect.height);
            // viewRect 宽度固定为 c_TotalRowWidth，支持水平滚动
            Rect viewRect = new Rect(0f, 0f, c_TotalRowWidth, totalContentH);

            m_ScrollPos = GUI.BeginScrollView(rect, m_ScrollPos, viewRect);

            int pendingDeleteRealIdx = -1;

            // 虚拟滚动：仅渲染当前可视范围内的行，保证 8000 条数据无卡顿
            // c_ProductsAreaHeight 固定，可见行数上限 = ceil(height / rowHeight) + 2（前后各留一行缓冲）
            int visibleRowCount = Mathf.CeilToInt(c_ProductsAreaHeight / c_RowHeight) + 2;
            int firstVisible = Mathf.Max(0, Mathf.FloorToInt(m_ScrollPos.y / c_RowHeight));
            int lastVisible = Mathf.Min(filteredIndices.Count - 1, firstVisible + visibleRowCount - 1);

            for (int fi = firstVisible; fi <= lastVisible; fi++)
            {
                int realIdx = filteredIndices[fi];
                SerializedProperty entryProp = productsProp.GetArrayElementAtIndex(realIdx);
                Rect rowRect = new Rect(0f, fi * c_RowHeight, c_TotalRowWidth, c_RowHeight);

                // 奇数行斑马纹
                if (fi % 2 == 1)
                {
                    EditorGUI.DrawRect(rowRect, s_ZebraRowColor);
                }

                // 选中行高亮（覆盖斑马纹）
                if (m_SelectedIndices.Contains(realIdx))
                {
                    EditorGUI.DrawRect(rowRect, s_SelectedRowColor);
                }

                // 重复 ID 红色高亮（对齐 IAPConfigWindow.DrawRow 的 isDup 逻辑）
                long tableId = entryProp.FindPropertyRelative("m_TableId")?.longValue ?? 0;
                if (m_DuplicateTableIds.Contains(tableId))
                {
                    EditorGUI.DrawRect(rowRect, new Color(1f, 0.3f, 0.3f, 0.25f));
                }

                DrawProductRow(rowRect, entryProp, realIdx, ref pendingDeleteRealIdx);
            }

            GUI.EndScrollView();

            // 行内删除：EndScrollView 后处理，避免打断 IMGUI 布局
            if (pendingDeleteRealIdx >= 0)
            {
                if (EditorUtility.DisplayDialog("确认删除", "即将删除该商品，此操作不可撤销。", "确认删除", "取消"))
                {
                    productsProp.DeleteArrayElementAtIndex(pendingDeleteRealIdx);
                    productsProp.serializedObject.ApplyModifiedProperties();
                    // 修正 m_SelectedIndices：移除被删除项，大于删除索引的项减一，对齐 IAPConfigWindow.DrawListBody
                    HashSet<int> newSelected = new HashSet<int>();
                    foreach (int idx in m_SelectedIndices)
                    {
                        if (idx < pendingDeleteRealIdx)
                        {
                            newSelected.Add(idx);
                        }
                        else if (idx > pendingDeleteRealIdx)
                        {
                            newSelected.Add(idx - 1);
                        }
                    }
                    m_SelectedIndices = newSelected;
                    m_CachedArraySize = -1;
                    m_DuplicatesDirty = true;
                }
            }
        }

        /// <summary>
        /// 绘制单行商品数据：行首选择 Toggle + 各字段可编辑控件 + 行尾"删除"按钮。
        /// 字段控件与列顺序完整对齐 IAPConfigWindow.DrawRow：
        /// LongField(表ID) / TextField(名称) / TextField(ProductID) / TextField(第三方ID) /
        /// EnumPopup(类型) / TextField(价格) / TextField(货币) / IntField(订阅组,仅 Subscription 启用) / TextField(备注)。
        /// 点击"删除"时写入 pendingDeleteRealIdx，由调用方在 EndScrollView 后处理（同 IAPConfigWindow.DrawRow 的 ref removeIndex 模式）。
        /// </summary>
        /// <param name="rowRect">行绘制区域。</param>
        /// <param name="entryProp">对应 IAPProductEntry 的 SerializedProperty。</param>
        /// <param name="realIdx">该行在 m_Items 数组中的真实索引，用于 Toggle 选中集合操作。</param>
        /// <param name="pendingDeleteRealIdx">待删除的真实索引输出；点击删除时设为 realIdx，否则保持 -1。</param>
        private void DrawProductRow(Rect rowRect, SerializedProperty entryProp, int realIdx, ref int pendingDeleteRealIdx)
        {
            float x = rowRect.x + 2f;
            float y = rowRect.y;
            float h = rowRect.height;

            // ── 行首选择 Toggle（对齐 IAPConfigWindow.DrawRow 首列 Toggle）──
            bool selected = m_SelectedIndices.Contains(realIdx);
            bool newSelected = GUI.Toggle(new Rect(x, y, c_ToggleWidth, h), selected, GUIContent.none);
            if (newSelected != selected)
            {
                if (newSelected)
                {
                    m_SelectedIndices.Add(realIdx);
                }
                else
                {
                    m_SelectedIndices.Remove(realIdx);
                }
            }
            x += c_ToggleWidth;

            // ── 各列可编辑字段（Rect 版 EditorGUI 控件，PropertyDrawer 中合规）──
            SerializedProperty tableIdProp = entryProp.FindPropertyRelative("m_TableId");
            SerializedProperty nameProp = entryProp.FindPropertyRelative("m_Name");
            SerializedProperty productIdProp = entryProp.FindPropertyRelative("m_ProductID");
            SerializedProperty thirdIdProp = entryProp.FindPropertyRelative("m_ThirdProductID");
            SerializedProperty typeProp = entryProp.FindPropertyRelative("m_ProductType");
            SerializedProperty priceProp = entryProp.FindPropertyRelative("m_Price");
            SerializedProperty currencyProp = entryProp.FindPropertyRelative("m_Currency");
            SerializedProperty subGroupProp = entryProp.FindPropertyRelative("m_SubGroupID");
            SerializedProperty noteProp = entryProp.FindPropertyRelative("m_EditorNote");

            // 外层：任何字段变化时 ApplyModifiedProperties
            EditorGUI.BeginChangeCheck();

            // 内层 A：仅检测 tableId 变化，用于重建重复 ID 集合（避免每次非 tableId 编辑都触发全量 FindPropertyRelative 遍历）
            EditorGUI.BeginChangeCheck();
            if (tableIdProp != null)
            {
                tableIdProp.longValue = EditorGUI.LongField(new Rect(x, y, s_ColWidths[0], h), tableIdProp.longValue);
            }

            bool tableIdChanged = EditorGUI.EndChangeCheck();
            x += s_ColWidths[0];

            // 内层 B：检测可搜索字段（name/productId/thirdId）变化，用于搜索开启时失效过滤缓存
            EditorGUI.BeginChangeCheck();
            if (nameProp != null)
            {
                nameProp.stringValue = EditorGUI.TextField(new Rect(x, y, s_ColWidths[1], h), nameProp.stringValue).Trim();
            }

            x += s_ColWidths[1];
            if (productIdProp != null)
            {
                productIdProp.stringValue = EditorGUI.TextField(new Rect(x, y, s_ColWidths[2], h), productIdProp.stringValue).Trim();
            }

            x += s_ColWidths[2];
            if (thirdIdProp != null)
            {
                thirdIdProp.stringValue = EditorGUI.TextField(new Rect(x, y, s_ColWidths[3], h), thirdIdProp.stringValue).Trim();
            }

            x += s_ColWidths[3];
            bool searchableChanged = EditorGUI.EndChangeCheck();

            // 非搜索字段（type/price/currency/subGroup/note）：不需要额外追踪
            if (typeProp != null)
            {
                IAPProductType newType = (IAPProductType)EditorGUI.EnumPopup(
                    new Rect(x, y, s_ColWidths[4], h), (IAPProductType)typeProp.enumValueIndex);
                typeProp.enumValueIndex = (int)newType;
            }
            x += s_ColWidths[4];

            if (priceProp != null)
            {
                priceProp.stringValue = EditorGUI.TextField(new Rect(x, y, s_ColWidths[5], h), priceProp.stringValue).Trim();
            }

            x += s_ColWidths[5];

            if (currencyProp != null)
            {
                currencyProp.stringValue = EditorGUI.TextField(new Rect(x, y, s_ColWidths[6], h), currencyProp.stringValue).Trim();
            }

            x += s_ColWidths[6];

            // 订阅组仅 Subscription 类型可编辑（对齐 IAPConfigWindow.DrawRow 中的 BeginDisabledGroup）
            bool isSub = typeProp != null && (IAPProductType)typeProp.enumValueIndex == IAPProductType.Subscription;
            EditorGUI.BeginDisabledGroup(!isSub);
            if (subGroupProp != null)
            {
                subGroupProp.intValue = EditorGUI.IntField(new Rect(x, y, s_ColWidths[7], h), subGroupProp.intValue);
            }

            EditorGUI.EndDisabledGroup();
            x += s_ColWidths[7];

            if (noteProp != null)
            {
                noteProp.stringValue = EditorGUI.TextField(new Rect(x, y, s_ColWidths[8], h), noteProp.stringValue).Trim();
            }

            x += s_ColWidths[8];

            if (EditorGUI.EndChangeCheck())
            {
                entryProp.serializedObject.ApplyModifiedProperties();
                // 只有 tableId 变化才需要重建重复 ID 集合（8000 条 FindPropertyRelative 遍历，不能每次都触发）
                if (tableIdChanged)
                {
                    m_DuplicatesDirty = true;
                }

                // 只有可搜索字段变化且搜索开启时才需要失效过滤缓存
                if ((tableIdChanged || searchableChanged) && !string.IsNullOrEmpty(m_SearchText))
                {
                    m_CachedArraySize = -1;
                }
            }

            // ── 行内删除按钮（对齐 IAPConfigWindow.DrawRow 末尾"删除"按钮，ref 传出索引由调用方处理）──
            if (GUI.Button(new Rect(x, y, c_OperationWidth, h), "删除", EditorStyles.miniButton))
            {
                pendingDeleteRealIdx = realIdx;
            }
        }

        /// <summary>
        /// 绘制底部工具栏：helpBox 背景 + "+ 添加商品"按钮 + "删除所选 (n)"按钮（无选中时禁用） + 右侧条数统计。
        /// filteredCount 由调用方传入（OnGUI 层已计算好），避免重复遍历。
        /// 对齐 IAPConfigWindow.DrawListFooter 布局与交互逻辑。
        /// </summary>
        /// <param name="rect">底部工具栏绘制区域。</param>
        /// <param name="itemsProp">对应 m_Items 的 SerializedProperty（数组）。</param>
        /// <param name="filteredCount">经过滤后的当前条目数，用于右侧统计标签。</param>
        private void DrawFooter(Rect rect, SerializedProperty itemsProp, int filteredCount)
        {
            GUI.Box(rect, GUIContent.none, "helpBox");

            float btnH = rect.height - 4f;
            float btnY = rect.y + 2f;
            float x = rect.x + 4f;

            // "+ 添加商品" 按钮
            float addW = 100f;
            if (GUI.Button(new Rect(x, btnY, addW, btnH), "+ 添加商品", EditorStyles.miniButton))
            {
                AddProduct(itemsProp);
            }

            x += addW + 4f;

            // "删除所选 (n)" 按钮（无选中时禁用，对齐 IAPConfigWindow.DrawListFooter）
            int selectedCount = m_SelectedIndices.Count;
            EditorGUI.BeginDisabledGroup(selectedCount == 0);
            float delW = 120f;
            if (GUI.Button(new Rect(x, btnY, delW, btnH), $"删除所选 ({selectedCount})", EditorStyles.miniButton))
            {
                DeleteSelected(itemsProp);
            }

            EditorGUI.EndDisabledGroup();

            // 右侧条数统计（对齐 IAPConfigWindow.DrawListFooter 统计文本）
            int totalCount = itemsProp?.arraySize ?? 0;
            string countInfo = string.IsNullOrEmpty(m_SearchText)
                ? $"共 {totalCount} 条"
                : $"共 {totalCount} 条  过滤后 {filteredCount} 条";
            float infoW = 200f;
            EdUtil.Draw.Label(new Rect(rect.xMax - infoW - 4f, btnY, infoW, btnH), countInfo, false);
        }

        /// <summary>
        /// 在 m_Items 末尾追加一条新商品：TableId 自动设为当前最大值 +1，其余字段清空，清空选中集合，滚动到底部。
        /// InsertArrayElementAtIndex 会复制上一行内容，追加后须显式清空所有字段以保证干净初始状态。
        /// </summary>
        /// <param name="itemsProp">对应 m_Items 的 SerializedProperty（数组）。</param>
        private void AddProduct(SerializedProperty itemsProp)
        {
            if (itemsProp == null)
            {
                return;
            }

            long maxId = 0;
            for (int i = 0; i < itemsProp.arraySize; i++)
            {
                long tid = itemsProp.GetArrayElementAtIndex(i).FindPropertyRelative("m_TableId").longValue;
                if (tid > maxId)
                {
                    maxId = tid;
                }
            }

            itemsProp.InsertArrayElementAtIndex(itemsProp.arraySize);
            SerializedProperty newEntry = itemsProp.GetArrayElementAtIndex(itemsProp.arraySize - 1);
            newEntry.FindPropertyRelative("m_TableId").longValue = maxId + 1;
            newEntry.FindPropertyRelative("m_Name").stringValue = string.Empty;
            newEntry.FindPropertyRelative("m_ProductID").stringValue = string.Empty;
            newEntry.FindPropertyRelative("m_ThirdProductID").stringValue = string.Empty;
            newEntry.FindPropertyRelative("m_ProductType").enumValueIndex = 0;
            newEntry.FindPropertyRelative("m_Price").stringValue = string.Empty;
            newEntry.FindPropertyRelative("m_Currency").stringValue = string.Empty;
            newEntry.FindPropertyRelative("m_SubGroupID").intValue = 0;
            SerializedProperty noteProp = newEntry.FindPropertyRelative("m_EditorNote");
            if (noteProp != null)
            {
                noteProp.stringValue = string.Empty;
            }

            itemsProp.serializedObject.ApplyModifiedProperties();
            m_CachedArraySize = -1;   // 失效缓存，下一帧重建
            m_DuplicatesDirty = true;
            m_SelectedIndices.Clear();
            m_ScrollPos = new Vector2(0f, itemsProp.arraySize * c_RowHeight);
        }

        /// <summary>
        /// 批量删除选中行：弹 EditorUtility.DisplayDialog 确认后，按索引降序移除以避免位移，清空选中集合，应用修改。
        /// 对齐 IAPConfigWindow.DeleteSelected 实现逻辑。
        /// </summary>
        /// <param name="itemsProp">对应 m_Items 的 SerializedProperty（数组）。</param>
        private void DeleteSelected(SerializedProperty itemsProp)
        {
            if (itemsProp == null || m_SelectedIndices.Count == 0)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog("确认删除", $"即将删除 {m_SelectedIndices.Count} 条商品，此操作不可撤销。", "确认删除", "取消"))
            {
                return;
            }

            List<int> sortedDesc = new List<int>(m_SelectedIndices);
            sortedDesc.Sort((a, b) => b.CompareTo(a));
            foreach (int idx in sortedDesc)
            {
                itemsProp.DeleteArrayElementAtIndex(idx);
            }

            itemsProp.serializedObject.ApplyModifiedProperties();
            m_CachedArraySize = -1;
            m_DuplicatesDirty = true;
            m_SelectedIndices.Clear();
        }

        /// <summary>
        /// 获取经搜索过滤后的条目总数。
        /// 搜索文本为空时直接返回 arraySize，避免无谓的列表构建。
        /// 保留供外部或 DrawFooter 旧路径调用；OnGUI 主流程直接传 filteredIndices.Count。
        /// </summary>
        /// <param name="itemsProp">对应 m_Items 的 SerializedProperty（数组）。</param>
        /// <returns>过滤后条数；搜索文本为空则返回 arraySize。</returns>
        private int GetFilteredCount(SerializedProperty itemsProp)
        {
            if (itemsProp == null)
            {
                return 0;
            }

            if (string.IsNullOrEmpty(m_SearchText))
            {
                return itemsProp.arraySize;
            }

            return GetFilteredIndices(itemsProp).Count;
        }

        /// <summary>
        /// 获取经搜索过滤后的真实数组索引列表。
        /// 搜索文本为空时返回全部索引；否则按 m_TableId / m_Name / m_ProductID / m_ThirdProductID 大小写不敏感过滤，
        /// 对齐 IAPConfigWindow.RebuildFilteredSortedIndices 的匹配字段范围。
        /// </summary>
        /// <param name="itemsProp">对应 m_Items 的 SerializedProperty（数组）。</param>
        /// <returns>满足过滤条件的真实数组索引列表。</returns>
        private List<int> GetFilteredIndices(SerializedProperty itemsProp)
        {
            var result = new List<int>();
            if (itemsProp == null)
            {
                return result;
            }

            int count = itemsProp.arraySize;
            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrEmpty(m_SearchText) || MatchesSearch(itemsProp.GetArrayElementAtIndex(i)))
                {
                    result.Add(i);
                }
            }
            return result;
        }

        /// <summary>
        /// 判断单条商品是否匹配当前搜索文本（大小写不敏感，ToLowerInvariant 对齐 IAPConfigWindow.MatchesSearchViewModel 实现）。
        /// 匹配字段：m_TableId / m_Name / m_ProductID / m_ThirdProductID。
        /// </summary>
        /// <param name="entryProp">对应 IAPProductEntry 的 SerializedProperty。</param>
        /// <returns>任一字段包含搜索文本时返回 true。</returns>
        private bool MatchesSearch(SerializedProperty entryProp)
        {
            string keyword = m_SearchText.ToLowerInvariant();
            long tableId = entryProp.FindPropertyRelative("m_TableId")?.longValue ?? 0;
            string name = entryProp.FindPropertyRelative("m_Name")?.stringValue ?? string.Empty;
            string productId = entryProp.FindPropertyRelative("m_ProductID")?.stringValue ?? string.Empty;
            string thirdId = entryProp.FindPropertyRelative("m_ThirdProductID")?.stringValue ?? string.Empty;
            return tableId.ToString().Contains(keyword)
                || name.ToLowerInvariant().Contains(keyword)
                || productId.ToLowerInvariant().Contains(keyword)
                || thirdId.ToLowerInvariant().Contains(keyword);
        }

        /// <summary>
        /// 重建重复 tableId 集合：遍历 m_Items 所有条目统计 tableId 频次，频次 > 1 的加入集合。
        /// 仅在 arraySize 变化或行字段编辑后调用，避免每帧遍历大量 SerializedProperty，对齐 IAPConfigWindow.RebuildDuplicateIds。
        /// </summary>
        /// <param name="itemsProp">对应 m_Items 的 SerializedProperty（数组）。</param>
        private void RebuildDuplicateIds(SerializedProperty itemsProp)
        {
            m_DuplicateTableIds.Clear();
            if (itemsProp == null)
            {
                return;
            }

            var counts = new Dictionary<long, int>();
            int count = itemsProp.arraySize;
            for (int i = 0; i < count; i++)
            {
                long id = itemsProp.GetArrayElementAtIndex(i).FindPropertyRelative("m_TableId")?.longValue ?? 0;
                counts.TryGetValue(id, out int cnt);
                counts[id] = cnt + 1;
            }
            foreach (KeyValuePair<long, int> kv in counts)
            {
                if (kv.Value > 1)
                {
                    m_DuplicateTableIds.Add(kv.Key);
                }
            }
        }
    }
}
