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
        /// <para>禁用条件：当前 Batch 无 Item、存在未保存改动（m_IsDirty）、包含未注册 Step，或 Google Play 输入未成对填写。</para>
        /// <para>缺失步骤在按钮下方提示；Google Play 输入错误只在对应字段旁提示，避免重复。</para>
        /// <para>点击后 fire-and-forget 调用 EditorUtil.Pipify.RunBatchAsync，
        /// 异常由 WindowReporter 写入 Console 并显示结果通知，UI 不额外 try/catch。</para>
        /// </summary>
        private void DrawExecute()
        {
            if (m_Settings == null || m_SettingsSO == null) return;
            bool noBatch = m_SelectedBatchIndex < 0 || m_SelectedBatchIndex >= m_Settings.Batches.Count;
            if (noBatch) return;

            Batch batch = m_Settings.Batches[m_SelectedBatchIndex];

            EditorUtil.Draw.Space(4f);
            EditorUtil.Draw.Line();

            string missingStepsReason = EditorUtil.Pipify.Registry.GetMissingStepsReason(batch);
            string googlePlayInputError = GetGooglePlayBatchInputPairError(batch);
            bool disabled = EditorUtil.Pipify.IsBatchRunning || batch.Items.Count == 0 || m_IsDirty || !string.IsNullOrEmpty(missingStepsReason) ||
                !string.IsNullOrEmpty(googlePlayInputError);
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.FlexibleSpace();
                EditorUtil.Draw.DisabledGroup(disabled, () =>
                {
                    EditorUtil.Draw.SuccessButton("▶ 运行", true, () => OnClickRunBatch(batch), GUILayout.Width(96f));
                });
            });

            if (!string.IsNullOrEmpty(missingStepsReason))
            {
                EditorUtil.Draw.ColoredMiniLabel($"运行已禁用：{missingStepsReason}", Color.red, false);
            }
        }

        /// <summary>
        /// 检查当前 Batch 的 Google Play 成对输入；折叠步骤也检查，避免保存后绕过运行门禁。
        /// </summary>
        /// <param name="batch">当前选中 Batch。</param>
        /// <returns>首个未成对填写步骤的位置与补填提示；全部有效时返回 null。</returns>
        private string GetGooglePlayBatchInputPairError(Batch batch)
        {
            for (int i = 0; i < batch.Items.Count; i++)
            {
                BatchItem item = batch.Items[i];
                PipifyStepInfo info = EditorUtil.Pipify.Registry.FindById(item.StepId);
                if (info?.ParamsType != typeof(PipifySteps.GooglePlayUploadParams)) continue;
                var p = EnsureParamsInstance(i, item, info) as PipifySteps.GooglePlayUploadParams;
                string error = PipifySteps.GetGooglePlayInputPairError(p);
                if (!string.IsNullOrEmpty(error)) return $"第 {i + 1} 个步骤：{error}";
            }
            return null;
        }

        /// <summary>
        /// 点击「▶ 运行」后的处理器：fire-and-forget 执行当前 Batch。
        /// 用 EditorApplication.delayCall 推迟到 OnGUI 之外再启动，避免 DisplayCancelableProgressBar /
        /// 等 Editor 调用在 IMGUI Layout/Repaint 阶段打断 LayoutGroup 栈
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
