/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoPrefabView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Modules 2.3 — Prefab 异步实例化与销毁演示 View（交互型）
 *            职责：演示 Nova.Prefab.InstantiateAsync 和 Destroy，
 *            在实例化容器中创建 DemoPrefabBlock 实例并计数，支持单路销毁。
 *            DemoPrefabBlock 是 UI Prefab（Image + Spinner），可挂在 Canvas 容器下直接显示。
 ***************************************************************/

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Modules 2.3 Prefab 演示 View（交互型）。
    /// 演示 Nova.Prefab.InstantiateAsync 异步实例化预制体，以及 Nova.Prefab.Destroy 单路销毁，
    /// 实时展示当前实例计数。
    /// </summary>
    public sealed class DemoPrefabView : BaseDemoView
    {
        /// <summary>
        /// 实例化按钮，触发 Nova.Prefab.InstantiateAsync。
        /// </summary>

        [SerializeField] private Button m_InstantiateButton;

        /// <summary>
        /// 销毁按钮，销毁最近一个实例。
        /// </summary>

        [SerializeField] private Button m_DestroyButton;

        /// <summary>
        /// 当前实例计数文本展示组件。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_CountText;

        /// <summary>
        /// 实例化容器 RectTransform，所有实例挂载到此节点下。
        /// </summary>

        [SerializeField] private RectTransform m_InstanceContainer;

        /// <summary>
        /// 演示预制体的 Asset 地址，Inspector 可调。
        /// </summary>

        [SerializeField] private string m_PrefabLocation = "DemoPrefabBlock";

        /// <summary>
        /// 当前已创建的所有预制体实例列表，LIFO 顺序管理销毁。
        /// </summary>
        private readonly List<GameObject> m_Instances = new List<GameObject>();

        /// <summary>
        /// 当前一次打开的取消令牌源；每个实例化任务捕获本次源身份，关闭或重开后不接管旧结果。
        /// </summary>
        private CancellationTokenSource m_Cts;

        /// <summary>
        /// 视图初始化钩子，注册按钮事件，设置标题与 API 副标题。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("Prefab");

            if (m_InstantiateButton != null)
            {
                m_InstantiateButton.onClick.AddListener(OnInstantiateButtonClick);
                SetButtonApiHint(m_InstantiateButton, "Nova.Prefab.InstantiateAsync(location)");
            }

            if (m_DestroyButton != null)
            {
                m_DestroyButton.onClick.AddListener(OnDestroyButtonClick);
                SetButtonApiHint(m_DestroyButton, "Nova.Prefab.Destroy(go)");
            }
        }

        /// <summary>
        /// 视图打开钩子，重置取消令牌源并刷新计数文本。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            CancelViewTasks();
            m_Cts = new CancellationTokenSource();

            RefreshCountText();
        }

        /// <summary>
        /// 视图关闭钩子，取消异步操作并销毁所有实例。
        /// </summary>
        /// <param name="isShutdown">是否因视图管理器关闭而触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            CancelViewTasks();

            for (int i = m_Instances.Count - 1; i >= 0; i--)
            {
                GameObject instance = m_Instances[i];
                m_Instances.RemoveAt(i);
                DestroyInstance(instance, Nova.Prefab);
            }

            base.OnClose(isShutdown, userData);
        }

        /// <summary>
        /// 先撤销当前打开的所有权，再取消并释放其令牌源；旧异步任务不会访问或释放新一次打开的源。
        /// </summary>
        private void CancelViewTasks()
        {
            CancellationTokenSource previous = m_Cts;
            m_Cts = null;
            if (previous == null) return;
            try
            {
                previous.Cancel();
            }
            finally
            {
                previous.Dispose();
            }
        }

        /// <summary>
        /// 实例化按钮点击回调，启动异步实例化流程。
        /// </summary>
        private void OnInstantiateButtonClick()
        {
            InstantiateAsync().Forget();
        }

        /// <summary>
        /// 销毁按钮点击回调，销毁最近一个实例并刷新计数。
        /// </summary>
        private void OnDestroyButtonClick()
        {
            if (m_Instances.Count == 0)
            {
                AppendFeedback("Destroy -> 无可销毁的实例", FeedbackLevel.Warn);
                return;
            }

            int lastIndex = m_Instances.Count - 1;
            GameObject last = m_Instances[lastIndex];
            m_Instances.RemoveAt(lastIndex);

            DestroyInstance(last, Nova.Prefab);

            RefreshCountText();
            AppendFeedback("Nova.Prefab.Destroy(go) -> " + m_Instances.Count + " instance(s) remaining", FeedbackLevel.Success);
        }

        /// <summary>
        /// 捕获本次打开的令牌与 Prefab 门面后实例化；只有同一次打开可接管结果，未接管实例在 finally 单路销毁。
        /// </summary>
        private async UniTaskVoid InstantiateAsync()
        {
            CancellationTokenSource viewCts = m_Cts;
            if (viewCts == null) return;
            CancellationToken viewToken = viewCts.Token;
            PrefabComponent prefabOwner = Nova.Prefab;
            if (prefabOwner == null)
            {
                AppendFeedback("Nova.Prefab.InstantiateAsync -> PrefabComponent 未初始化", FeedbackLevel.Error);
                return;
            }

            string location = m_PrefabLocation;
            Transform parent = m_InstanceContainer != null ? m_InstanceContainer : null;
            // 每个任务只释放自己的 linked 源；关闭取消 view 源后，任务源仍由本任务作用域负责 Dispose。
            using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(viewToken);
            GameObject instance = null;
            try
            {
                AppendFeedback("Nova.Prefab.InstantiateAsync(\"" + location + "\") -> 实例化中...");
                instance = await prefabOwner.InstantiateAsync(location, parent, operationCts.Token);
                if (this == null || operationCts.IsCancellationRequested || !ReferenceEquals(m_Cts, viewCts)) return;
                if (instance == null)
                {
                    AppendFeedback("Nova.Prefab.InstantiateAsync(\"" + location + "\") -> 返回 null，请确认资源存在", FeedbackLevel.Error);
                    return;
                }

                m_Instances.Add(instance);
                // 成功后实例所有权交给列表；按钮/关闭负责销毁，finally 只处理尚未接管的局部实例。
                instance = null;
                RefreshCountText();
                AppendFeedback("Nova.Prefab.InstantiateAsync(\"" + location + "\") -> " + m_Instances.Count + " instance(s)", FeedbackLevel.Success);
            }
            catch (System.OperationCanceledException)
            {
                // 关闭或重开取消当前任务；即使下层已经返回实例，也仍由 finally 回收。
            }
            finally
            {
                DestroyInstance(instance, prefabOwner);
            }
        }

        /// <summary>
        /// 通过创建任务捕获或当前可用的 Prefab 门面销毁；关停时门面已消失才使用原生销毁，句柄始终由实例 Tag 单路回收。
        /// </summary>
        /// <param name="instance">本调用唯一负责归还的实例；空或已经销毁时不处理。</param>
        /// <param name="prefabOwner">捕获的创建门面或当前门面；不得用新任务的令牌源判断旧实例归属。</param>
        private static void DestroyInstance(GameObject instance, PrefabComponent prefabOwner)
        {
            if (instance == null) return;
            if (prefabOwner != null)
            {
                prefabOwner.Destroy(instance);
            }
            else
            {
                // PrefabComponent.OnDestroy 已清空 Manager；原生销毁仍触发 PrefabInstanceTag，业务不再手动 Release。
                Object.Destroy(instance);
            }
        }

        /// <summary>
        /// 刷新当前实例计数文本。
        /// </summary>
        private void RefreshCountText()
        {
            if (m_CountText != null)
            {
                m_CountText.text = "实例数：" + m_Instances.Count;
            }
        }
    }
}
