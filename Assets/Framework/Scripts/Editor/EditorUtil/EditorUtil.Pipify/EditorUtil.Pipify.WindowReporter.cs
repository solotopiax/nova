/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Pipify.WindowReporter.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   Window 宿主进度 Reporter：EditorUtility 模态进度条实现
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Pipify
        {
            /// <summary>
            /// Window 宿主进度 Reporter：用 EditorUtility.DisplayCancelableProgressBar 模态阻塞主线程。
            /// 模态期间 Unity 不会触发资源变更导致的域重载，保证 Window 不被销毁。
            /// Batch 结束时通过宿主窗口 ShowNotification 弹一行右下角浮窗（不抢焦点，不被 Finder/Explorer 抢焦点）。
            /// </summary>
            internal sealed class WindowReporter : IPipifyProgressReporter
            {
                /// <summary>
                /// 通知成功色（亮绿）。Batch 全部成功时覆盖 EditorStyles.notificationText 文字颜色。
                /// </summary>
                private static readonly Color c_NotificationSuccessColor = new Color(0.40f, 1.00f, 0.60f, 1f);

                /// <summary>
                /// 通知失败色（亮红）。Batch 失败时覆盖 EditorStyles.notificationText 文字颜色。
                /// </summary>
                private static readonly Color c_NotificationFailureColor = new Color(1.00f, 0.40f, 0.40f, 1f);

                /// <summary>
                /// EditorStyles.notificationText 的反射缓存（该属性是 internal，无法直接访问）。
                /// 只解析一次，失败则后续不再重试，颜色降级为系统默认。
                /// </summary>
                private static GUIStyle s_NotificationTextStyle;

                /// <summary>
                /// 是否已尝试解析 NotificationText 样式（无论成功失败都置 true，避免重复反射）。
                /// </summary>
                private static bool s_NotificationStyleResolved;

                /// <summary>
                /// 宿主窗口；Batch 结束时调用其 ShowNotification 给出最终结果通知。null 时仅写日志。
                /// </summary>
                private readonly EditorWindow m_Host;

                /// <summary>
                /// 当前 Batch 名。
                /// </summary>
                private string m_BatchName;

                /// <summary>
                /// 当前 Batch 步骤总数。
                /// </summary>
                private int m_TotalSteps;

                /// <summary>
                /// 构造 WindowReporter 并绑定宿主窗口。
                /// </summary>
                /// <param name="host">承载 ShowNotification 的宿主窗口；可为 null。</param>
                public WindowReporter(EditorWindow host)
                {
                    m_Host = host;
                }

                /// <summary>
                /// 通知 Batch 开始，记录名称与总步骤数，并打印 Debug 日志。
                /// </summary>
                /// <param name="batchName">Batch 名称。</param>
                /// <param name="totalSteps">步骤总数。</param>
                public void BeginBatch(string batchName, int totalSteps)
                {
                    m_BatchName = batchName;
                    m_TotalSteps = totalSteps;
                    Log.Debug(LogTag.Editor, "{0} Batch 开始：{1}（{2} 步）", c_LogPrefix, batchName, totalSteps);
                }

                /// <summary>
                /// 更新模态进度条，并返回用户是否点击了取消按钮。
                /// </summary>
                /// <param name="index">Step 索引（从 0 起）。</param>
                /// <param name="stepDisplayName">Step 展示名。</param>
                /// <param name="innerProgress">Step 内进度（0~1）。</param>
                /// <returns>true 表示用户点击了取消按钮；否则 false。</returns>
                public bool ReportStep(int index, string stepDisplayName, float innerProgress)
                {
                    float outer = m_TotalSteps == 0 ? 0f : (index + Mathf.Clamp01(innerProgress)) / m_TotalSteps;
                    string title = string.Format("{0} - {1}", m_BatchName, stepDisplayName);
                    string info = string.Format("第 {0}/{1} 步：{2}", index + 1, m_TotalSteps, stepDisplayName);
                    return EditorUtility.DisplayCancelableProgressBar(title, info, outer);
                }

                /// <summary>
                /// Step 结束回调：成功打 Debug 日志，失败打 Error 日志。
                /// </summary>
                /// <param name="index">Step 索引。</param>
                /// <param name="success">是否成功。</param>
                /// <param name="elapsed">耗时。</param>
                /// <param name="error">异常（失败时非 null）。</param>
                public void EndStep(int index, bool success, TimeSpan elapsed, Exception error)
                {
                    if (success)
                    {
                        Log.Debug(LogTag.Editor, "{0} 第 {1} 步完成（耗时 {2:0.00}s）", c_LogPrefix, index + 1, elapsed.TotalSeconds);
                    }
                    else
                    {
                        Log.Error(LogTag.Editor, "{0} 第 {1} 步失败：{2}", c_LogPrefix, index + 1, error);
                    }
                }

                /// <summary>
                /// Batch 结束回调：清除进度条，并打 Debug 或 Error 日志。
                /// </summary>
                /// <param name="success">整个 Batch 是否全部成功。</param>
                /// <param name="totalElapsed">总耗时。</param>
                public void EndBatch(bool success, TimeSpan totalElapsed)
                {
                    EditorUtility.ClearProgressBar();
                    if (success)
                    {
                        Log.Debug(LogTag.Editor, "{0} Batch 完成：{1}（总耗时 {2:0.00}s）", c_LogPrefix, m_BatchName, totalElapsed.TotalSeconds);
                    }
                    else
                    {
                        Log.Error(LogTag.Editor, "{0} Batch 失败：{1}（总耗时 {2:0.00}s）", c_LogPrefix, m_BatchName, totalElapsed.TotalSeconds);
                    }
                    if (m_Host == null)
                    {
                        return;
                    }
                    string toast = string.Format(success ? "{0}  ✓  {1:0.0}s" : "{0}  ✗  {1:0.0}s", m_BatchName, totalElapsed.TotalSeconds);
                    // EditorStyles.notificationText 是 ShowNotification 实际使用的单例 GUIStyle，
                    // 但访问修饰符是 internal，只能反射拿；每次显示前覆写其 textColor 让成功/失败显不同颜色。
                    GUIStyle notificationStyle = ResolveNotificationTextStyle();
                    if (notificationStyle != null)
                    {
                        notificationStyle.normal.textColor = success ? c_NotificationSuccessColor : c_NotificationFailureColor;
                    }
                    m_Host.ShowNotification(new GUIContent(toast));
                    m_Host.Repaint();
                }

                /// <summary>
                /// 反射解析 EditorStyles.notificationText（internal 属性）；首调缓存结果，失败返回 null 不再重试。
                /// </summary>
                /// <returns>NotificationText 样式实例；解析失败返回 null。</returns>
                private static GUIStyle ResolveNotificationTextStyle()
                {
                    if (s_NotificationStyleResolved)
                    {
                        return s_NotificationTextStyle;
                    }
                    s_NotificationStyleResolved = true;
                    System.Reflection.PropertyInfo prop = typeof(EditorStyles).GetProperty("notificationText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    if (prop != null)
                    {
                        s_NotificationTextStyle = prop.GetValue(null) as GUIStyle;
                    }
                    return s_NotificationTextStyle;
                }
            }
        }
    }
}
