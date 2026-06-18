/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifyWindow.LeftList.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   Pipify 窗口左侧 Batch 列表（搜索 / ScrollView / 拖拽排序 / 右键菜单 / 新建）
 ***************************************************************/

using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class PipifyWindow : EditorWindow
    {
        /// <summary>
        /// 绘制左侧 Batch 列表面板（固定宽度 c_LeftPanelWidth）。
        /// 未绑定 Settings 时显示 HelpBox 提示；绑定后显示搜索框、可点击列表与底部新建按钮。
        /// </summary>
        private void DrawLeftList()
        {
            EditorUtil.Draw.Layout.Vertical(EditorStyles.helpBox, () =>
            {
                if (m_Settings == null)
                {
                    EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "请先创建或选择 PipifySettingsSO" }, false);
                    return;
                }

                DrawLeftSearchBar();
                DrawLeftScrollView();
                DrawLeftNewBatchButton();
            }, GUILayout.Width(c_LeftPanelWidth));
        }

        /// <summary>
        /// 绘制顶部搜索框。
        /// </summary>
        private void DrawLeftSearchBar()
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label("搜索", false, GUILayout.Width(36f));
                string newFilter = EditorUtil.Draw.TextField(m_Filter, false, GUILayout.ExpandWidth(true));
                if (newFilter != m_Filter)
                {
                    m_Filter = newFilter;
                    Repaint();
                }
            });
            EditorUtil.Draw.Space(2f);
        }

        /// <summary>
        /// 绘制中部 Batch 列表 ScrollView；委托给 EditorUtil.Draw.ReorderableRowList 完成行渲染、拖拽手柄、单击/双击/右键事件。
        /// </summary>
        private void DrawLeftScrollView()
        {
            if (m_LeftRowList == null) m_LeftRowList = new EditorUtil.Draw.ReorderableRowList { RowHeight = c_LeftRowHeight };
            m_LeftRowList.DraggableNow = string.IsNullOrEmpty(m_Filter);

            m_LeftScroll = EditorUtil.Draw.Layout.ScrollView(m_LeftScroll, () =>
            {
                int count = m_Settings.Batches.Count;
                m_LeftRowList.BeginFrame(count);

                for (int i = 0; i < count; i++)
                {
                    Batch batch = m_Settings.Batches[i];
                    if (!string.IsNullOrEmpty(m_Filter) && !batch.Name.ToLower().Contains(m_Filter.ToLower()))
                    {
                        continue;
                    }
                    bool selected = m_SelectedBatchIndex == i;
                    int captured = i;
                    m_LeftRowList.DrawRow(i, "• " + batch.Name, selected,
                        onLeftClick: idx => { SelectBatch(idx); Repaint(); },
                        onLeftDoubleClick: idx => { SelectBatch(idx); OnRenameBatch(idx); },
                        onRightClick: idx => { SelectBatch(idx); ShowBatchContextMenu(idx); Repaint(); });
                }

                if (m_LeftRowList.EndFrame((from, to) => { MoveBatch(from, to); MarkDirty(); }))
                {
                    Repaint();
                }
            }, GUILayout.ExpandHeight(true));
        }

        /// <summary>
        /// 将 Batches[from] 移动到目标位置 to，并同步修正 m_SelectedBatchIndex 以保持选中项不变。
        /// </summary>
        /// <param name="from">源索引。</param>
        /// <param name="to">目标索引。</param>
        private void MoveBatch(int from, int to)
        {
            if (m_Settings == null) return;
            int count = m_Settings.Batches.Count;
            if (from < 0 || from >= count || to < 0 || to >= count || from == to) return;
            Batch moving = m_Settings.Batches[from];
            m_Settings.Batches.RemoveAt(from);
            m_Settings.Batches.Insert(to, moving);
            if (m_SelectedBatchIndex == from)
            {
                m_SelectedBatchIndex = to;
            }
            else if (from < m_SelectedBatchIndex && to >= m_SelectedBatchIndex)
            {
                m_SelectedBatchIndex--;
            }
            else if (from > m_SelectedBatchIndex && to <= m_SelectedBatchIndex)
            {
                m_SelectedBatchIndex++;
            }
            // 选中 Batch 的相对位置发生过变化时，右侧 ReorderableList 需重建
            m_ItemsListBoundBatchIndex = -1;
        }

        /// <summary>
        /// 切换选中 Batch；切换前释放键盘焦点，避免 IMGUI TextField 的 editing buffer 被带到新 Batch 的同位置控件上。
        /// </summary>
        /// <param name="index">目标 Batch 索引。</param>
        private void SelectBatch(int index)
        {
            if (m_SelectedBatchIndex != index)
            {
                GUI.FocusControl(null);
            }
            m_SelectedBatchIndex = index;
        }

        /// <summary>
        /// 绘制底部"＋ 新建 Batch"按钮。
        /// </summary>
        private void DrawLeftNewBatchButton()
        {
            EditorUtil.Draw.Space(4f);
            EditorUtil.Draw.Button("＋ 新建 Batch", false, OnClickNewBatch, GUILayout.ExpandWidth(true));
        }
    }
}
