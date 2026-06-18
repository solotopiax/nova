/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifyWindow.RightPanel.Execute.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   Pipify 窗口右侧面板底部执行区
 ***************************************************************/

using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class PipifyWindow : EditorWindow
    {
        /// <summary>
        /// 绘制底部执行区。
        /// <para>顶部分割线 + 右对齐「▶ 运行」按钮；</para>
        /// <para>禁用条件：当前 Batch 无 Item 或存在未保存改动（m_IsDirty）。</para>
        /// <para>点击后 fire-and-forget 调用 EditorUtil.Pipify.RunBatchAsync，
        /// 异常由 Runner 内部通过 WindowReporter 以模态对话框呈现，UI 不额外 try/catch。</para>
        /// </summary>
        private void DrawExecute()
        {
            if (m_Settings == null || m_SettingsSO == null) return;
            bool noBatch = m_SelectedBatchIndex < 0 || m_SelectedBatchIndex >= m_Settings.Batches.Count;
            if (noBatch) return;

            Batch batch = m_Settings.Batches[m_SelectedBatchIndex];

            EditorUtil.Draw.Space(4f);
            EditorUtil.Draw.Line();

            bool disabled = batch.Items.Count == 0 || m_IsDirty;
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.FlexibleSpace();
                EditorUtil.Draw.DisabledGroup(disabled, () =>
                {
                    EditorUtil.Draw.SuccessButton("▶ 运行", true, () => OnClickRunBatch(batch), GUILayout.Width(96f));
                });
            });
        }

        /// <summary>
        /// 点击「▶ 运行」后的处理器：fire-and-forget 执行当前 Batch。
        /// 用 EditorApplication.delayCall 推迟到 OnGUI 之外再启动，避免 DisplayCancelableProgressBar /
        /// LockReloadAssemblies 等 Editor 调用在 IMGUI Layout/Repaint 阶段打断 LayoutGroup 栈
        /// （表现为 "EndLayoutGroup: BeginLayoutGroup must be called first"）。
        /// 把当前窗口 this 作为宿主传给 Runner，Batch 结束后 WindowReporter 通过 this.ShowNotification 弹通知。
        /// </summary>
        /// <param name="batch">待执行的 Batch。</param>
        private void OnClickRunBatch(Batch batch)
        {
            EditorApplication.delayCall += () => EditorUtil.Pipify.RunBatchAsync(batch, this).Forget();
        }
    }
}
