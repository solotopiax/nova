/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoAssetView.Cache.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   Sample 包缓存清理与界面等待生命周期
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>只消费现存公开 Asset 门面；不初始化资源框架、不直连 YooAsset 或操作磁盘。</summary>
    public sealed partial class DemoAssetView
    {
        private TMP_InputField m_CachePackageInput;
        private Button m_ClearCacheButton;
        private Button m_CancelCacheWaitButton;
        private CancellationTokenSource m_CacheWaitCts;
        private int m_CacheWaitGeneration;

        // 占用属于实际请求而非UIView；关闭/重开或换一页不能重放同一公开Owner的同包清理。
        private static readonly ConditionalWeakTable<object, HashSet<string>> s_CacheClearOwners =
            new ConditionalWeakTable<object, HashSet<string>>();

        /// <summary>仅创建本View拥有的动态控件，复用现有滚动交互区、输入行及按钮样式，不修改Prefab。</summary>
        private void InitializeCacheClearControls()
        {
            if (m_InteractionRoot == null || m_LocationInput == null || m_LocationInput.transform.parent == null ||
                m_ReleaseButton == null || m_ClearCacheButton != null) return;
            GameObject row = Instantiate(m_LocationInput.transform.parent.gameObject, m_InteractionRoot, false);
            row.name = "CachePackageRow";
            m_CachePackageInput = row.GetComponentInChildren<TMP_InputField>(true);
            TMP_Text label = row.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (label != null) label.text = "Package";
            if (m_CachePackageInput != null)
            {
                m_CachePackageInput.onValueChanged = new TMP_InputField.OnChangeEvent();
                m_CachePackageInput.onEndEdit = new TMP_InputField.SubmitEvent();
                m_CachePackageInput.text = "Default"; // MainDemo当前明确包；执行时仍以用户可见的精确输入冻结。
                if (m_CachePackageInput.placeholder is TMP_Text placeholder) placeholder.text = "已加载清单的精确Package";
            }
            m_ClearCacheButton = CreateCacheButton("ClearUnusedCacheButton", "清理未引用缓存", OnClearCacheButtonClick,
                "Nova.Asset.ClearUnusedCacheAsync(package)");
            m_CancelCacheWaitButton = CreateCacheButton("CancelCacheWaitButton", "取消清理等待", OnCancelCacheWaitButtonClick,
                "仅取消界面等待；后台删除可能继续");
            RefreshCacheClearControls();
        }

        /// <summary>克隆已有按钮外观，但使用全新事件对象，不继承加载/释放或持久回调。</summary>
        /// <param name="objectName">本View拥有的固定节点名。</param>
        /// <param name="label">业务操作显示名。</param>
        /// <param name="callback">该按钮唯一的本View点击处理器。</param>
        /// <param name="hint">显示的公开API或取消语义。</param>
        /// <returns>仅存在于当前View实例中的动态按钮。</returns>
        private Button CreateCacheButton(string objectName, string label, UnityEngine.Events.UnityAction callback, string hint)
        {
            Button button = Instantiate(m_ReleaseButton, m_InteractionRoot, false);
            button.gameObject.name = objectName;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(callback);
            TMP_Text text = button.transform.Find("Text")?.GetComponent<TMP_Text>();
            if (text != null) text.text = label;
            SetButtonApiHint(button, hint);
            return button;
        }

        /// <summary>仅提交当前页面指定包的一次公开清理请求，不自动切清单或重试。</summary>
        private void OnClearCacheButtonClick()
        {
            ClearPackageCacheAsync().Forget();
        }

        /// <summary>取消本页等待，不撤销磁盘操作；实际请求的同包占用保持到正常终态。</summary>
        private void OnCancelCacheWaitButtonClick()
        {
            if (m_CacheWaitCts == null) return;
            CancelCacheWait();
            AppendFeedback("清理界面等待已取消；底层可能继续，本包完成前不会重复提交。", FeedbackLevel.Warn);
            RefreshCacheClearControls();
        }

        /// <summary>冻结可见包名和当前版本后复用既有门面；结果只说明操作返回，不冒称已删除字节。</summary>
        /// <returns>仅本View的界面等待任务；后台清理拥有独立终态和占用。</returns>
        private async UniTaskVoid ClearPackageCacheAsync()
        {
            if (this == null || m_IsClosed || m_CacheWaitCts != null) return;
            string package = m_CachePackageInput != null ? m_CachePackageInput.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(package))
            { AppendFeedback("清缓存需要明确Package，不能用空值猜默认包。", FeedbackLevel.Warn); return; }
            AssetComponent asset = Nova.Asset;
            if (asset == null)
            { AppendFeedback("清缓存 -> 现有Asset未初始化，不自动Bootstrap。", FeedbackLevel.Error); return; }
            string version;
            try { version = asset.GetCurrentPackageVersion(package); }
            catch (Exception)
            { AppendFeedback("清缓存 -> 指定Package不可用，未提交操作。", FeedbackLevel.Error); return; }
            if (string.IsNullOrEmpty(version))
            { AppendFeedback("清缓存 -> 指定Package尚无激活清单，未提交操作。", FeedbackLevel.Warn); return; }
            if (m_LoadCts != null)
            { AppendFeedback("清缓存 -> 本页仍在加载资源，请先等待加载完成或取消加载。", FeedbackLevel.Warn); return; }

            var waitCts = new CancellationTokenSource();
            int generation = ++m_CacheWaitGeneration;
            if (!TryOwnCacheClear(asset, package, waitCts.Token))
            {
                waitCts.Dispose();
                AppendFeedback("清缓存 -> 同包已有清理或结果未确认；本次未重复提交。", FeedbackLevel.Warn);
                return;
            }
            m_CacheWaitCts = waitCts;
            RefreshCacheClearControls();
            try
            {
                AppendFeedback("清缓存 -> Package=" + package + ", 当前版本=" + version + "；仅移除当前清单未引用的磁盘Bundle。");
                // 保存实际底层终态；取消只附加到UI等待，不能提前释放同包占用。
                Task<CacheClearOutcome> operation = RunOwnedCacheClearAsync(asset, package, version).AsTask();
                CacheClearOutcome outcome = await operation.AsUniTask().AttachExternalCancellation(waitCts.Token);
                if (this == null || m_IsClosed || generation != m_CacheWaitGeneration) return;
                AppendFeedback(outcome.Message, outcome.Completed && outcome.VersionUnchanged ? FeedbackLevel.Success : FeedbackLevel.Warn);
            }
            catch (OperationCanceledException)
            {
                if (this != null && !m_IsClosed && generation == m_CacheWaitGeneration)
                    AppendFeedback("清缓存等待已取消；后台可能继续，不代表停止或回滚。", FeedbackLevel.Warn);
            }
            catch (Exception)
            {
                if (this != null && !m_IsClosed && generation == m_CacheWaitGeneration)
                    AppendFeedback("清缓存界面结果未完成；不自动重放，实际请求占用保持到已知正常终态。", FeedbackLevel.Warn);
            }
            finally
            {
                if (ReferenceEquals(m_CacheWaitCts, waitCts)) m_CacheWaitCts = null;
                waitCts.Dispose();
                if (this != null && !m_IsClosed && generation == m_CacheWaitGeneration) RefreshCacheClearControls();
            }
        }

        /// <summary>实际公开清理独立等待终态；UI取消后仍观察最终完成，异常则保留不确定占用而不自动重放。</summary>
        /// <param name="asset">当前已有的公开资源Owner，不创建或初始化Manager/Component。</param>
        /// <param name="package">已向用户展示并冻结的精确包名。</param>
        /// <param name="version">提交时实际已激活的清单版本。</param>
        /// <returns>固定结果说明；不包含磁盘删除或释放空间的未经验证断言。</returns>
        private static async UniTask<CacheClearOutcome> RunOwnedCacheClearAsync(AssetComponent asset, string package, string version)
        {
            bool returned = false;
            try
            {
                await asset.ClearUnusedCacheAsync(package, CancellationToken.None);
                returned = true;
                string currentVersion = asset.GetCurrentPackageVersion(package);
                bool unchanged = currentVersion == version;
                return new CacheClearOutcome
                {
                    Completed = true, VersionUnchanged = unchanged,
                    Message = "清缓存操作已返回 -> Package=" + package + ", 当前版本=" + currentVersion +
                        (unchanged ? "；仍需核对实际磁盘与资源加载结果，EditorSimulate可能无磁盘删除。" : "；清单版本已变化，本次结果未验收，不自动重放。"),
                };
            }
            catch (Exception)
            {
                return new CacheClearOutcome
                {
                    Message = returned ? "清缓存已返回，但当前清单取证失败；未验收，不自动重放。" :
                        "清缓存失败或终态不明；本会话保留同包占用，不重复提交。",
                };
            }
            finally
            {
                if (returned) ReleaseCacheClearOwner(asset, package);
            }
        }

        /// <summary>调用前取消不取得占用；同一Owner同包在实际请求结束前仅允许一次提交。</summary>
        /// <param name="owner">现有公开Owner对象身份；只作为弱表键，不调用其Equals或GetHashCode。</param>
        /// <param name="package">非空精确包名，Ordinal区分。</param>
        /// <param name="token">提交前的界面取消令牌，已取消时先退出。</param>
        /// <returns>首次取得该Owner同包占用为true，重复请求为false。</returns>
        private static bool TryOwnCacheClear(object owner, string package, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (owner == null || string.IsNullOrWhiteSpace(package)) throw new ArgumentException("Cache owner and package must be explicit.");
            return s_CacheClearOwners.GetValue(owner, ignored => new HashSet<string>(StringComparer.Ordinal)).Add(package);
        }

        /// <summary>仅实际正常返回后释放自己的精确包占用；UI取消、关闭或异常不能调用它来造完成。</summary>
        /// <param name="owner">发起该实际请求的同一公开Owner引用。</param>
        /// <param name="package">该请求取得的精确包名。</param>
        private static void ReleaseCacheClearOwner(object owner, string package)
        {
            if (owner != null && s_CacheClearOwners.TryGetValue(owner, out HashSet<string> packages)) packages.Remove(package);
        }

        /// <summary>只使当前UI等待失效并取消其CTS；实际请求及同包占用不受影响。</summary>
        private void CancelCacheWait()
        {
            m_CacheWaitGeneration++;
            CancellationTokenSource pending = m_CacheWaitCts;
            m_CacheWaitCts = null;
            pending?.Cancel();
        }

        /// <summary>只反映本页等待状态；取消后再点击会由同包占用明确拒绝，而不是重放底层操作。</summary>
        private void RefreshCacheClearControls()
        {
            bool waiting = m_CacheWaitCts != null;
            if (m_ClearCacheButton != null) m_ClearCacheButton.interactable = !waiting;
            if (m_CancelCacheWaitButton != null) m_CancelCacheWaitButton.interactable = waiting;
            if (m_CachePackageInput != null) m_CachePackageInput.interactable = !waiting;
        }

        /// <summary>只保存一次公开调用的终态说明，不保存真实Bundle、文件路径或资源句柄。</summary>
        private sealed class CacheClearOutcome
        {
            internal bool Completed;
            internal bool VersionUnchanged;
            internal string Message;
        }
    }
}
