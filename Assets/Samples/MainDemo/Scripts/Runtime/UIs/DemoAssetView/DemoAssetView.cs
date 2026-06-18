/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoAssetView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Modules 2.2 — Asset 同步/异步/取消三态演示 View（交互型）
 *            职责：演示 Nova.Asset.LoadAsync<Sprite> 和 Nova.Asset.Release，
 *            支持 Sync/Async/Cancel 三种加载模式，结果用 Image 展示；
 *            同时演示运行时增量下载（RefreshManifest + tag 切片）API 调用姿势。
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
    /// 演示 Nova.Asset.LoadAsync（Sprite）的异步加载、取消，以及 Release 资源释放。
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
        /// 释放资源按钮，调用 Nova.Asset.Release。
        /// </summary>

        [SerializeField] private Button m_ReleaseButton;

        /// <summary>
        /// 加载结果展示图片组件。
        /// </summary>

        [SerializeField] private Image m_ResultImage;

        /// <summary>
        /// 检查并下载 tag 增量按钮，触发 RefreshManifest + CreateDownloaderByTags + RunAsync 三步流程。
        /// </summary>

        [SerializeField] private Button m_CheckPatchButton;

        /// <summary>
        /// 演示无效 Asset 地址跳过按钮，触发 CreateDownloaderByLocations Warning 跳过行为。
        /// </summary>

        [SerializeField] private Button m_InvalidLocationButton;

        /// <summary>
        /// 运行时增量下载任务的取消令牌源，视图关闭时取消。
        /// </summary>
        private CancellationTokenSource m_PatchCts;

        /// <summary>
        /// 当前已加载的资源句柄，用于 Release。
        /// </summary>
        private NovaFramework.Runtime.IAssetHandle<Sprite> m_CurrentHandle;

        /// <summary>
        /// 当前异步加载任务的取消令牌源。
        /// </summary>
        private CancellationTokenSource m_LoadCts;

        /// <summary>
        /// 视图初始化钩子，注册按钮事件，设置标题与 API 副标题。
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

            if (m_CheckPatchButton != null)
            {
                m_CheckPatchButton.onClick.AddListener(OnCheckPatchButtonClick);
                SetButtonApiHint(m_CheckPatchButton, "Nova.Asset.RefreshManifestAsync() -> CreateDownloaderByTags(tags) -> RunAsync(ct)");
            }

            if (m_InvalidLocationButton != null)
            {
                m_InvalidLocationButton.onClick.AddListener(OnInvalidLocationButtonClick);
                SetButtonApiHint(m_InvalidLocationButton, "Nova.Asset.CreateDownloaderByLocations(locations) -> Scope/IsEmpty");
            }
        }

        /// <summary>
        /// 视图打开钩子，初始化输入框默认值并清空图片展示区。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

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
        /// 视图关闭钩子，取消进行中的加载并释放已加载资源。
        /// </summary>
        /// <param name="isShutdown">是否因视图管理器关闭而触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            CancelLoad();
            ReleaseCurrentAsset();
            CancelPatch();
            base.OnClose(isShutdown, userData);
        }
    }
}
