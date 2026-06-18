/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ObjectPoolComponentInspector.Methods.cs
 * author:    taoye
 * created:   2026/3/4
 * descrip:   对象池组件编辑器面板定制 —— 私有方法
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class ObjectPoolComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 绘制配置信息。
        /// </summary>
        private void DrawConfigs()
        {
            EditorUtil.Draw.TypesSelector("ObjectPool 管理器", m_ManagerTypeNames, m_CurManagerTypeName, true, null, GUILayout.Width(180f));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "支持自定义类型，实现框架层 IObjectPoolManager 接口后，该类型将自动出现在此列表中。" });
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制运行时信息。
        /// </summary>
        private void DrawRuntimeInfos()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            ObjectPoolComponent t = (ObjectPoolComponent)target;
            EditorUtil.Draw.Label("对象池数量", t.Count.ToString(), false);
            EditorUtil.Draw.Line();

            DrawSearchBox();

            ObjectPoolBase[] objectPools = t.GetAllObjectPools(true);
            foreach (ObjectPoolBase objectPool in objectPools)
            {
                if (!string.IsNullOrEmpty(m_SearchText) && objectPool.FullName.IndexOf(m_SearchText, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                DrawObjectPool(objectPool);
            }
        }

        /// <summary>
        /// 绘制池列表搜索框。
        /// </summary>
        private void DrawSearchBox()
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label("搜索：", false, GUILayout.Width(40));
                string newSearch = EditorUtil.Draw.TextField(m_SearchText, EditorStyles.toolbarSearchField, false, GUILayout.ExpandWidth(true));
                if (newSearch != m_SearchText)
                {
                    m_SearchText = newSearch;
                }
                if (!string.IsNullOrEmpty(m_SearchText))
                {
                    EditorUtil.Draw.Button("\u2715", false, () => { m_SearchText = string.Empty; }, GUILayout.Width(20));
                }
            });
        }

        /// <summary>
        /// 绘制对象列表分页控件。
        /// </summary>
        /// <param name="poolFullName">池的 FullName，用作 m_PageIndices 的键。</param>
        /// <param name="currentPage">当前页码（从 0 开始）。</param>
        /// <param name="totalPages">总页数。</param>
        private void DrawObjectListPagination(string poolFullName, int currentPage, int totalPages)
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label($"第 {currentPage + 1}/{totalPages} 页", false, GUILayout.ExpandWidth(true));
                EditorUtil.Draw.Button("上一页", currentPage <= 0, () => { m_PageIndices[poolFullName] = currentPage - 1; }, GUILayout.Width(60));
                EditorUtil.Draw.Button("下一页", currentPage >= totalPages - 1, () => { m_PageIndices[poolFullName] = currentPage + 1; }, GUILayout.Width(60));
            });
        }

        /// <summary>
        /// 绘制单个对象池信息。
        /// </summary>
        private void DrawObjectPool(ObjectPoolBase objectPool)
        {
            bool expanded = EditorUtil.Draw.Foldout(objectPool.FullName, objectPool.FullName);
            if (!expanded)
            {
                return;
            }

            ObjectInfo[] objectInfos = objectPool.GetAllObjectInfos();
            EditorUtil.Draw.Layout.Vertical("box", () =>
            {
                EditorUtil.Draw.Label("名称", objectPool.Name, false);
                EditorUtil.Draw.Label("类型", objectPool.ObjectType.FullName, false);
                EditorUtil.Draw.Label("自动释放间隔", FormatAutoReleaseInterval(objectPool.AutoReleaseInterval, objectPool.AutoReleaseTimeCounter), false);
                EditorUtil.Draw.Label("容量", objectPool.Capacity.ToString(), false);
                EditorUtil.Draw.Label("对象数量", objectPool.Count.ToString(), false);
                EditorUtil.Draw.Label("可释放数量", objectPool.CanReleaseCount.ToString(), false);
                EditorUtil.Draw.Label("对象过期时间", objectPool.ExpireTime.ToString(), false);
                EditorUtil.Draw.Label("优先级", objectPool.Priority.ToString(), false);

                if (objectInfos.Length > 0)
                {
                    int totalCount = objectInfos.Length;
                    bool usePaging = totalCount > c_PageSize;
                    int startIndex = 0;
                    int endIndex = totalCount;

                    if (usePaging)
                    {
                        int totalPages = (totalCount + c_PageSize - 1) / c_PageSize;
                        if (!m_PageIndices.TryGetValue(objectPool.FullName, out int currentPage))
                        {
                            currentPage = 0;
                        }
                        if (currentPage >= totalPages)
                        {
                            currentPage = totalPages - 1;
                        }

                        startIndex = currentPage * c_PageSize;
                        endIndex = Math.Min(startIndex + c_PageSize, totalCount);

                        DrawObjectListPagination(objectPool.FullName, currentPage, totalPages);
                    }

                    string inUseHeader = objectPool.AllowMultiGet ? "Refs" : "InUse";
                    float[] colWidths = { c_ColLocked, c_ColInUse, c_ColFlag, c_ColPriority, c_ColLastUse, c_ColExpire };
                    EditorUtil.Draw.TableRow("Name", new[] { "Locked", inUseHeader, "Flag", "Priority", "LastUse", "Expire" }, colWidths);

                    for (int i = startIndex; i < endIndex; i++)
                    {
                        ObjectInfo objectInfo = objectInfos[i];
                        string expireText = FormatExpireCountdown(objectPool.ExpireTime, objectInfo);
                        string objName = string.IsNullOrEmpty(objectInfo.Name) ? "<None>" : objectInfo.Name;
                        string inUseValue = objectPool.AllowMultiGet ? objectInfo.RefCount.ToString() : objectInfo.IsInUse.ToString();
                        EditorUtil.Draw.TableRow(objName, new[]
                        {
                            objectInfo.Locked.ToString(),
                            inUseValue,
                            objectInfo.CustomCanReleaseFlag.ToString(),
                            objectInfo.Priority.ToString(),
                            objectInfo.LastUseTime.ToLocalTime().ToString("HH:mm:ss"),
                            expireText,
                        }, colWidths);
                    }

                    EditorUtil.Draw.Button("释放", false, () => objectPool.Release());
                    EditorUtil.Draw.Button("释放所有未使用", false, () => objectPool.ReleaseAllUnused());
                    EditorUtil.Draw.Button("导出 CSV", false, () => ExportObjectPoolCsv(objectPool, objectInfos));
                }
                else
                {
                    EditorUtil.Draw.Label("对象池为空 ...", false);
                }
            });

            EditorUtil.Draw.Separator();
        }

        /// <summary>
        /// 格式化自动释放间隔文本，括号内附实时倒计时。
        /// </summary>
        /// <param name="interval">自动释放间隔秒数。</param>
        /// <param name="counter">当前计时器累计值。</param>
        /// <returns>格式化后的倒计时字符串。</returns>
        private string FormatAutoReleaseInterval(float interval, float counter)
        {
            if (interval <= 0f) return interval.ToString();
            float remaining = interval - counter;
            if (remaining < 0f) remaining = 0f;
            int h = (int)(remaining / 3600);
            int m = (int)(remaining % 3600 / 60);
            int s = (int)(remaining % 60);
            string countdown = h > 0 ? $"{h}h {m:D2}m {s:D2}s" : m > 0 ? $"{m}m {s:D2}s" : $"{s}s";
            return $"{interval} ({countdown})";
        }

        /// <summary>
        /// 格式化对象过期倒计时文本。
        /// 仅当对象未被使用、未被锁定、自定义标记允许释放时，倒计时才有实际意义。
        /// </summary>
        /// <param name="expireTime">对象池过期时间（秒）。</param>
        /// <param name="objectInfo">对象信息。</param>
        /// <returns>格式化后的过期倒计时字符串。</returns>
        private string FormatExpireCountdown(float expireTime, ObjectInfo objectInfo)
        {
            if (expireTime <= 0f) return "—";
            if (objectInfo.IsInUse) return "—";
            if (objectInfo.Locked) return "Locked";
            if (!objectInfo.CustomCanReleaseFlag) return "Flag=No";

            double remaining = expireTime - (DateTime.UtcNow - objectInfo.LastUseTime).TotalSeconds;
            if (remaining <= 0) return "Expired";

            int h = (int)(remaining / 3600);
            int m = (int)(remaining % 3600 / 60);
            int s = (int)(remaining % 60);
            return h > 0 ? $"{h}h {m:D2}m {s:D2}s" : m > 0 ? $"{m}m {s:D2}s" : $"{s}s";
        }

        /// <summary>
        /// 导出对象池数据为 CSV 文件。
        /// </summary>
        /// <param name="objectPool">要导出的对象池。</param>
        /// <param name="objectInfos">对象信息数组。</param>
        private void ExportObjectPoolCsv(ObjectPoolBase objectPool, ObjectInfo[] objectInfos)
        {
            string header = objectPool.AllowMultiGet ? "Name,Locked,RefCount,Custom Can Release Flag,Priority,Last Use Time,Expire" : "Name,Locked,In Use,Custom Can Release Flag,Priority,Last Use Time,Expire";

            string[] rows = new string[objectInfos.Length];
            for (int i = 0; i < objectInfos.Length; i++)
            {
                ObjectInfo objectInfo = objectInfos[i];
                string expireText = FormatExpireCountdown(objectPool.ExpireTime, objectInfo);
                rows[i] = objectPool.AllowMultiGet ? $"{objectInfo.Name},{objectInfo.Locked},{objectInfo.RefCount},{objectInfo.CustomCanReleaseFlag},{objectInfo.Priority},{objectInfo.LastUseTime.ToLocalTime():yyyy-MM-dd HH:mm:ss},{expireText}" : $"{objectInfo.Name},{objectInfo.Locked},{objectInfo.IsInUse},{objectInfo.CustomCanReleaseFlag},{objectInfo.Priority},{objectInfo.LastUseTime.ToLocalTime():yyyy-MM-dd HH:mm:ss},{expireText}";
            }

            EditorUtil.CsvExporter.Export($"对象池数据 - {objectPool.Name}", header, rows);
        }

    }
}
