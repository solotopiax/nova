/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoAssetView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Modules 2.2 — Asset 同步/异步/取消三态演示 View（交互型）
 *            职责：演示 Nova.Asset.LoadAsync<Sprite> 和 IAssetHandle.Release，
 *            支持 Async/Cancel/Release，并通过动态控件演示指定包磁盘缓存清理与取消等待。
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Modules 2.2 Asset 演示 View（交互型）。
    /// 演示资源加载/释放及既有 Package 的公开缓存清理；清理返回不等同磁盘删除验收。
    /// </summary>
    public sealed partial class DemoAssetView : BaseDemoView
    {
        /// <summary>
        /// Asset 地址输入框，默认值 sprite_icon_tree。
        /// </summary>

        [SerializeField] private TMP_InputField m_LocationInput;

        /// <summary>
        /// 异步加载按钮，调用 Nova.Asset.LoadAsync。
        /// </summary>

        [SerializeField] private Button m_AsyncButton;

        /// <summary>
        /// 取消加载按钮，取消当前进行中的异步加载。
        /// </summary>

        [SerializeField] private Button m_CancelButton;

        /// <summary>
        /// 释放资源按钮，调用当前 IAssetHandle 的 Release。
        /// </summary>

        [SerializeField] private Button m_ReleaseButton;

        /// <summary>
        /// 加载结果展示图片组件。
        /// </summary>

        [SerializeField] private Image m_ResultImage;

        /// <summary>
        /// 当前已加载的资源句柄，用于 Release。
        /// </summary>
        private NovaFramework.Runtime.IAssetHandle<Sprite> m_CurrentHandle;

        /// <summary>
        /// 当前异步加载任务的取消令牌源。
        /// </summary>
        private CancellationTokenSource m_LoadCts;

        /// <summary>每次取消或替换加载递增，旧任务不能接管新任务的句柄与 UI。</summary>
        private int m_LoadGeneration;

        /// <summary>视图关闭后只回收异步结果，不再更新 UI。</summary>
        private bool m_IsClosed = true;

        /// <summary>
        /// 视图初始化钩子，注册原加载按钮并创建本View拥有的清理控件。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("Asset");

            if (m_AsyncButton != null)
            {
                m_AsyncButton.onClick.AddListener(OnAsyncButtonClick);
                SetButtonApiHint(m_AsyncButton, "Nova.Asset.LoadAsync<Sprite>(location, ct)");
            }

            if (m_CancelButton != null)
            {
                m_CancelButton.onClick.AddListener(OnCancelButtonClick);
                SetButtonApiHint(m_CancelButton, "CancellationTokenSource.Cancel()");
            }

            if (m_ReleaseButton != null)
            {
                m_ReleaseButton.onClick.AddListener(OnReleaseButtonClick);
                SetButtonApiHint(m_ReleaseButton, "IAssetHandle.Release()");
            }

            InitializeCacheClearControls();
        }

        /// <summary>
        /// 视图打开后允许本次加载/清理等待更新UI，刷新控件和原加载展示区。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            m_IsClosed = false;
            RefreshCacheClearControls();

            if (m_LocationInput != null && string.IsNullOrEmpty(m_LocationInput.text))
            {
                m_LocationInput.text = "sprite_icon_tree";
            }

            if (m_ResultImage != null)
            {
                m_ResultImage.sprite = null;
                m_ResultImage.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 关闭时取消本页清理等待和加载，再释放当前资源；实际清理请求继续保持占用。
        /// </summary>
        /// <param name="isShutdown">是否因视图管理器关闭而触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            m_IsClosed = true;
            CancelCacheWait();
            CancelLoad();
            ReleaseCurrentAsset();
            base.OnClose(isShutdown, userData);
        }
    }
}
