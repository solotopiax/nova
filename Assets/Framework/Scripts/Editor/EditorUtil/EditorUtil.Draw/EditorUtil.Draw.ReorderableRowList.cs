/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Draw.ReorderableRowList.cs
 * author:    taoye
 * created:   2026/5/19
 * descrip:   编辑器绘制工具-可拖拽排序列表（行级，左侧手柄列承担拖拽，文字区承担单击/双击/右键）
 ***************************************************************/

using System;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// 绘制工具。
        /// </summary>
        public static partial class Draw
        {
            /// <summary>
            /// 可拖拽排序的行级列表绘制原语。封装左侧 ≡ 手柄列、整行选中高亮、单击/双击/右键事件、拖拽过程中的插入指示线，
            /// 业务侧只需要提供"行内文字 + 选中态"和事件回调，底层 List 在 OnReorder 中由调用方一次性 RemoveAt+Insert 即可。
            /// 与 UnityEditorInternal.ReorderableList 不同，本原语在过滤场景下可由调用方直接控制可见行集合。
            /// </summary>
            public sealed class ReorderableRowList
            {
                /// <summary>
                /// 行高（像素）。
                /// </summary>
                public float RowHeight = 22f;

                /// <summary>
                /// 左侧手柄列宽度（像素）。
                /// </summary>
                public float HandleWidth = 18f;

                /// <summary>
                /// 当前是否允许拖拽（如搜索过滤态下可置 false 提示用户拖拽不可用）。
                /// </summary>
                public bool DraggableNow = true;

                /// <summary>
                /// 拖拽状态：源索引 -1 表示未处于拖拽态。
                /// </summary>
                private int m_FromIndex = -1;

                /// <summary>
                /// 拖拽状态：当前指针位置对应的"插入位置 insertIndex"，取值 0..count（== count 表示尾插）。
                /// </summary>
                private int m_InsertIndex = -1;

                /// <summary>
                /// 拖拽阶段持有的 GUI hotControl id，绑定 MouseUp/Ignore，避免拖出窗口外丢事件。
                /// </summary>
                private int m_HotControlId;

                /// <summary>
                /// 行文字样式（延迟初始化）。
                /// </summary>
                private GUIStyle m_RowStyle;

                /// <summary>
                /// 选中行文字样式（延迟初始化）。
                /// </summary>
                private GUIStyle m_RowSelectedStyle;

                /// <summary>
                /// 拖拽手柄文字样式（延迟初始化），居中绘制 ≡ 图标。
                /// </summary>
                private GUIStyle m_HandleStyle;

                /// <summary>
                /// 单帧绘制的行 Rect 缓存（在 BeginFrame 重置，DrawRow 累加，EndFrame 消费）。
                /// </summary>
                private Rect[] m_RowRects = Array.Empty<Rect>();

                /// <summary>
                /// 与 m_RowRects 对齐的命中标记。
                /// </summary>
                private bool[] m_RowDrawn = Array.Empty<bool>();

                /// <summary>
                /// 起始一帧的列表绘制；必须在 DrawRow / EndFrame 之前调用。
                /// </summary>
                /// <param name="totalCount">列表元素总数（含被过滤掉的）。</param>
                public void BeginFrame(int totalCount)
                {
                    EnsureStyles();
                    if (m_RowRects.Length != totalCount)
                    {
                        m_RowRects = new Rect[totalCount];
                        m_RowDrawn = new bool[totalCount];
                    }
                    else
                    {
                        for (int i = 0; i < totalCount; i++)
                        {
                            m_RowRects[i] = default;
                            m_RowDrawn[i] = false;
                        }
                    }
                }

                /// <summary>
                /// 绘制单行；调用顺序与可见性由调用方决定（过滤态下跳过即可）。
                /// </summary>
                /// <param name="index">行的原始索引（与 BeginFrame 提供的 totalCount 同维）。</param>
                /// <param name="text">行内显示文本。</param>
                /// <param name="selected">是否选中。</param>
                /// <param name="onLeftClick">左键单击回调（参数：原始索引）。</param>
                /// <param name="onLeftDoubleClick">左键双击回调（参数：原始索引）。</param>
                /// <param name="onRightClick">右键回调（参数：原始索引）。</param>
                public void DrawRow(int index, string text, bool selected,
                    Action<int> onLeftClick, Action<int> onLeftDoubleClick, Action<int> onRightClick)
                {
                    GUIStyle rowStyle = selected ? m_RowSelectedStyle : m_RowStyle;
                    int rowControlId = GUIUtility.GetControlID(FocusType.Passive);
                    Rect rowRect = GUILayoutUtility.GetRect(
                        new GUIContent(text), rowStyle,
                        GUILayout.ExpandWidth(true), GUILayout.Height(RowHeight));

                    Rect handleRect = new Rect(rowRect.x, rowRect.y, HandleWidth, rowRect.height);
                    Rect textRect = new Rect(rowRect.x + HandleWidth, rowRect.y, rowRect.width - HandleWidth, rowRect.height);

                    if (Event.current.type == EventType.Repaint)
                    {
                        if (selected)
                        {
                            Color highlight = EditorGUIUtility.isProSkin
                                ? new Color(0.24f, 0.48f, 0.90f, 0.55f)
                                : new Color(0.26f, 0.52f, 0.96f, 0.40f);
                            EditorGUI.DrawRect(rowRect, highlight);
                        }
                        rowStyle.Draw(textRect, text, false, false, selected, false);

                        Color savedColor = GUI.color;
                        GUI.color = DraggableNow ? savedColor : new Color(savedColor.r, savedColor.g, savedColor.b, 0.35f);
                        m_HandleStyle.Draw(handleRect, "≡", false, false, false, false);
                        GUI.color = savedColor;
                    }

                    if (DraggableNow)
                    {
                        EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.ResizeVertical);
                    }

                    HandleRowMouse(index, rowControlId, rowRect, handleRect, textRect,
                        onLeftClick, onLeftDoubleClick, onRightClick);

                    m_RowRects[index] = rowRect;
                    m_RowDrawn[index] = true;
                }

                /// <summary>
                /// 收尾一帧绘制：处理拖拽中的 MouseDrag/MouseUp/Ignore 事件并在 Repaint 阶段画插入指示线。
                /// </summary>
                /// <param name="onReorder">松手落地时执行：参数（fromIndex, toIndex）；toIndex 为目标行索引（已扣除 from 在前的偏移）。</param>
                /// <returns>本帧是否需要 Repaint（事件已 Use 时返回 true）。</returns>
                public bool EndFrame(Action<int, int> onReorder)
                {
                    if (m_FromIndex < 0 || GUIUtility.hotControl != m_HotControlId) return false;

                    Event evt = Event.current;
                    if (evt.type == EventType.MouseDrag)
                    {
                        m_InsertIndex = FindInsertIndexAtY(evt.mousePosition.y);
                        evt.Use();
                        return true;
                    }
                    if (evt.type == EventType.MouseUp)
                    {
                        if (m_InsertIndex >= 0 && m_InsertIndex != m_FromIndex && m_InsertIndex != m_FromIndex + 1)
                        {
                            int target = m_InsertIndex > m_FromIndex ? m_InsertIndex - 1 : m_InsertIndex;
                            onReorder?.Invoke(m_FromIndex, target);
                        }
                        ResetDragState();
                        evt.Use();
                        return true;
                    }
                    if (evt.type == EventType.Ignore)
                    {
                        ResetDragState();
                        return true;
                    }
                    if (evt.type == EventType.Repaint)
                    {
                        DrawIndicator();
                    }
                    return false;
                }

                /// <summary>
                /// 处理单行的鼠标事件：手柄区左键按下 → 进入拖拽态；文字区按外部回调响应单击 / 双击 / 右键。
                /// </summary>
                private void HandleRowMouse(int index, int rowControlId, Rect rowRect, Rect handleRect, Rect textRect,
                    Action<int> onLeftClick, Action<int> onLeftDoubleClick, Action<int> onRightClick)
                {
                    Event evt = Event.current;
                    if (evt.type != EventType.MouseDown || !rowRect.Contains(evt.mousePosition)) return;

                    if (evt.button == 0 && handleRect.Contains(evt.mousePosition) && DraggableNow)
                    {
                        m_FromIndex = index;
                        m_HotControlId = rowControlId;
                        GUIUtility.hotControl = rowControlId;
                        onLeftClick?.Invoke(index);
                        evt.Use();
                        return;
                    }

                    if (evt.button == 1)
                    {
                        onRightClick?.Invoke(index);
                        evt.Use();
                    }
                    else if (evt.button == 0 && textRect.Contains(evt.mousePosition))
                    {
                        if (evt.clickCount == 2) onLeftDoubleClick?.Invoke(index);
                        else onLeftClick?.Invoke(index);
                        evt.Use();
                    }
                }

                /// <summary>
                /// 按行中线离散切换插入位置（避免每帧重判带来的视觉抖动）。
                /// </summary>
                private int FindInsertIndexAtY(float y)
                {
                    int lastDrawn = -1;
                    for (int i = 0; i < m_RowRects.Length; i++)
                    {
                        if (!m_RowDrawn[i]) continue;
                        Rect r = m_RowRects[i];
                        float midY = r.y + r.height * 0.5f;
                        if (y < midY) return i;
                        lastDrawn = i;
                    }
                    return lastDrawn < 0 ? 0 : lastDrawn + 1;
                }

                /// <summary>
                /// 绘制 2px 蓝色插入指示线；位置由 m_InsertIndex 唯一确定。
                /// </summary>
                private void DrawIndicator()
                {
                    if (m_InsertIndex < 0) return;
                    Rect refRect = default;
                    bool refFound = false;
                    for (int i = 0; i < m_RowRects.Length; i++)
                    {
                        if (m_RowDrawn[i]) { refRect = m_RowRects[i]; refFound = true; break; }
                    }
                    if (!refFound) return;

                    float lineY;
                    if (m_InsertIndex < m_RowRects.Length && m_RowDrawn[m_InsertIndex])
                    {
                        lineY = m_RowRects[m_InsertIndex].y;
                    }
                    else
                    {
                        int lastDrawn = -1;
                        for (int i = m_RowRects.Length - 1; i >= 0; i--)
                        {
                            if (m_RowDrawn[i]) { lastDrawn = i; break; }
                        }
                        if (lastDrawn < 0) return;
                        lineY = m_RowRects[lastDrawn].yMax - 2f;
                    }
                    Rect line = new Rect(refRect.x, lineY, refRect.width, 2f);
                    Color indicator = EditorGUIUtility.isProSkin
                        ? new Color(0.36f, 0.62f, 1f, 0.95f)
                        : new Color(0.18f, 0.45f, 0.95f, 0.95f);
                    EditorGUI.DrawRect(line, indicator);
                }

                /// <summary>
                /// 重置拖拽态并归还 hotControl。
                /// </summary>
                private void ResetDragState()
                {
                    m_FromIndex = -1;
                    m_InsertIndex = -1;
                    if (GUIUtility.hotControl == m_HotControlId)
                    {
                        GUIUtility.hotControl = 0;
                    }
                }

                /// <summary>
                /// 延迟初始化所有 GUIStyle；每个独立判空，防止域重载后 native 对象失效。
                /// </summary>
                private void EnsureStyles()
                {
                    if (m_RowStyle == null)
                    {
                        Color normalText = EditorGUIUtility.isProSkin ? new Color(0.85f, 0.85f, 0.85f) : new Color(0.1f, 0.1f, 0.1f);
                        m_RowStyle = new GUIStyle(EditorStyles.label)
                        {
                            fontSize = 12,
                            alignment = TextAnchor.MiddleLeft,
                            padding = new RectOffset(6, 4, 2, 2),
                            normal = { textColor = normalText },
                            hover = { textColor = normalText },
                            active = { textColor = normalText },
                            focused = { textColor = normalText },
                        };
                    }
                    if (m_HandleStyle == null)
                    {
                        Color handleColor = EditorGUIUtility.isProSkin ? new Color(0.62f, 0.62f, 0.62f) : new Color(0.32f, 0.32f, 0.32f);
                        m_HandleStyle = new GUIStyle(EditorStyles.label)
                        {
                            fontSize = 14,
                            alignment = TextAnchor.MiddleCenter,
                            padding = new RectOffset(0, 0, 0, 0),
                            normal = { textColor = handleColor },
                            hover = { textColor = handleColor },
                            active = { textColor = handleColor },
                            focused = { textColor = handleColor },
                        };
                    }
                    if (m_RowSelectedStyle == null)
                    {
                        Color accentText = EditorGUIUtility.isProSkin ? Color.white : new Color(0.05f, 0.05f, 0.05f);
                        m_RowSelectedStyle = new GUIStyle(m_RowStyle)
                        {
                            fontStyle = FontStyle.Bold,
                            normal = { textColor = accentText },
                            hover = { textColor = accentText },
                            active = { textColor = accentText },
                            focused = { textColor = accentText },
                        };
                    }
                }
            }
        }
    }
}
