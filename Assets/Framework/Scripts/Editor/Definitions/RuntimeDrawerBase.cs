/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  RuntimeDrawerBase.cs
 * author:    taoye
 * created:   2026/4/24
 * descrip:   编辑器运行时绘制器泛型基类
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 编辑器运行时绘制器泛型基类，封装 Foldout 管理、类型转换、Dispose 清理和 CSV 导出。
    /// 子类仅需提供列表名称、表头定义、数据获取和行格式化。
    /// </summary>
    /// <typeparam name="TComponent">目标组件类型。</typeparam>
    public abstract class RuntimeDrawerBase<TComponent> : IEditorRuntimeDrawer<TComponent> where TComponent : MonoBehaviour
    {
        /// <summary>
        /// 列表名称，用作 Foldout 标识和 CSV 文件名。
        /// </summary>
        private readonly string m_ListName;

        /// <summary>
        /// 是否已释放。
        /// </summary>
        private bool m_IsDisposed;

        /// <summary>
        /// 初始化运行时绘制器的新实例。
        /// </summary>
        /// <param name="listName">列表名称。</param>
        protected RuntimeDrawerBase(string listName)
        {
            m_ListName = listName;
        }

        /// <summary>
        /// 获取列表名称。
        /// </summary>
        protected string ListName => m_ListName;

        /// <summary>
        /// 释放资源，清理 Foldout 状态。在 Inspector OnDisable 中调用。
        /// </summary>
        public void Dispose()
        {
            if (m_IsDisposed)
            {
                return;
            }

            m_IsDisposed = true;
            EditorUtil.Draw.CleanFoldout(m_ListName);
            OnDispose();
        }

        /// <summary>
        /// 子类可覆写以清理额外的 Foldout 状态或资源。
        /// </summary>
        protected virtual void OnDispose()
        {
        }

        /// <summary>
        /// 非泛型绘制入口，执行类型转换和 null 检查后调用 DrawTyped。
        /// </summary>
        /// <param name="target">目标对象。</param>
        public void Draw(Object target)
        {
            TComponent component = target as TComponent;
            if (component == null)
            {
                return;
            }

            DrawTyped(component);
        }

        /// <summary>
        /// 类型安全的绘制方法，子类必须实现。
        /// </summary>
        /// <param name="component">目标组件。</param>
        public abstract void DrawTyped(TComponent component);

        /// <summary>
        /// 绘制标准列表（Foldout + 表头 + 数据行 + CSV 导出按钮）。
        /// </summary>
        /// <typeparam name="TItem">列表元素类型。</typeparam>
        /// <param name="items">数据列表。</param>
        /// <param name="headerLabel">表头左侧标签。</param>
        /// <param name="headerValue">表头右侧标签。</param>
        /// <param name="csvHeader">CSV 列头。</param>
        /// <param name="formatLabel">行标签格式化委托。</param>
        /// <param name="formatValue">行值格式化委托。</param>
        /// <param name="formatCsvRow">CSV 行格式化委托。</param>
        /// <param name="foldoutKey">自定义 Foldout key，为 null 时使用默认标题。</param>
        protected void DrawStandardList<TItem>(IReadOnlyList<TItem> items, string headerLabel, string headerValue, string csvHeader, Func<TItem, string> formatLabel, Func<TItem, string> formatValue, Func<TItem, string> formatCsvRow, string foldoutKey = null)
        {
            if (items == null)
            {
                return;
            }

            string title = $"{m_ListName}({items.Count})";
            bool foldout = foldoutKey != null ? EditorUtil.Draw.Foldout(title, foldoutKey) : EditorUtil.Draw.Foldout(title);
            if (!foldout)
            {
                return;
            }

            EditorUtil.Draw.Layout.Vertical("box", () =>
            {
                EditorUtil.Draw.Label(headerLabel, headerValue, false);

                foreach (var item in items)
                {
                    EditorUtil.Draw.Label(formatLabel(item), formatValue(item), false);
                }

                EditorUtil.Draw.Button("导出 CSV 数据", false, () =>
                {
                    var snapshot = items.ToList();
                    string filePath = EditorUtil.CsvExporter.Export(m_ListName, csvHeader, snapshot.Select(formatCsvRow));
                    EditorUtil.FileSystem.OpenFolder(filePath);
                });
            });

            EditorUtil.Draw.Separator();
        }
    }
}
