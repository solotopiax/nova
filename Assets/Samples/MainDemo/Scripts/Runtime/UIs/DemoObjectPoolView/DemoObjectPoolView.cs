/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoObjectPoolView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Modules 2.11 — ObjectPool 模块演示视图（交互触发型）。
 *            演示对象池创建、Get/Put 操作及统计信息展示。
 *            API：Nova.ObjectPool.CreateSingleGettingObjectPool<T>() / GetObjectPool<T>()
 ***************************************************************/

using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// ObjectPool 模块演示视图，演示 DemoBullet 对象池的创建、Get/Put 及统计信息。
    /// 继承 BaseDemoView 三段式骨架，提供 Create/Spawn/Recycle/Destroy 四按钮。
    /// </summary>
    public sealed class DemoObjectPoolView : BaseDemoView
    {
        /// <summary>
        /// 创建对象池按钮。
        /// </summary>

        [SerializeField] private Button m_CreateButton;

        /// <summary>
        /// Spawn（Get）对象按钮。
        /// </summary>

        [SerializeField] private Button m_SpawnButton;

        /// <summary>
        /// Recycle（Put）对象按钮。
        /// </summary>

        [SerializeField] private Button m_RecycleButton;

        /// <summary>
        /// 销毁对象池按钮。
        /// </summary>

        [SerializeField] private Button m_DestroyButton;

        /// <summary>
        /// 统计信息文本（Count / CanReleaseCount / Capacity）。
        /// </summary>

        [SerializeField] private TextMeshProUGUI m_StatsText;

        /// <summary>
        /// 当前持有的 DemoBullet 实例（最近一次 Get），用于 Put 演示。
        /// </summary>
        private DemoBullet m_CurrentBullet;

        /// <summary>
        /// 已创建的 DemoBullet 对象池引用。
        /// </summary>
        private IObjectPool<DemoBullet> m_BulletPool;

        /// <summary>
        /// 视图初始化：注册按钮事件，设置标题与 API 副标题。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("ObjectPool 演示");

            if (m_CreateButton != null)
            {
                m_CreateButton.onClick.AddListener(OnCreateButtonClick);
                SetButtonApiHint(m_CreateButton, "Nova.ObjectPool.CreateSingleGettingObjectPool<T>()");
            }

            if (m_SpawnButton != null)
            {
                m_SpawnButton.onClick.AddListener(OnSpawnButtonClick);
                SetButtonApiHint(m_SpawnButton, "Nova.ObjectPool.GetObjectPool<T>().Get()");
            }

            if (m_RecycleButton != null)
            {
                m_RecycleButton.onClick.AddListener(OnRecycleButtonClick);
                SetButtonApiHint(m_RecycleButton, "Nova.ObjectPool.GetObjectPool<T>().Put()");
            }

            if (m_DestroyButton != null)
            {
                m_DestroyButton.onClick.AddListener(OnDestroyButtonClick);
                SetButtonApiHint(m_DestroyButton, "Nova.ObjectPool.DestroyObjectPool<T>()");
            }
        }

        /// <summary>
        /// 视图关闭：将未归还的 DemoBullet 实例放回对象池，防止泄漏。
        /// </summary>
        /// <param name="isShutdown">是否因视图管理器关闭而触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            if (m_CurrentBullet != null && m_BulletPool != null)
            {
                m_BulletPool.Put(m_CurrentBullet);
                m_CurrentBullet = null;
            }

            base.OnClose(isShutdown, userData);
        }

        /// <summary>
        /// 视图打开：尝试获取已有对象池并刷新统计。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            if (Nova.ObjectPool != null)
            {
                m_BulletPool = Nova.ObjectPool.GetObjectPool<DemoBullet>();
            }

            RefreshStats();
        }

        /// <summary>
        /// 创建按钮点击：创建 DemoBullet SingleGetting 对象池并注册一个初始对象。
        /// </summary>
        private void OnCreateButtonClick()
        {
            if (Nova.ObjectPool == null)
            {
                AppendFeedback("Nova.ObjectPool 不可用", FeedbackLevel.Error);
                return;
            }

            if (m_BulletPool != null)
            {
                AppendFeedback("Nova.ObjectPool -> DemoBullet 对象池已存在", FeedbackLevel.Warn);
                RefreshStats();
                return;
            }

            m_BulletPool = Nova.ObjectPool.CreateSingleGettingObjectPool<DemoBullet>();
            DemoBullet initialBullet = DemoBullet.Create();
            m_BulletPool.Register(initialBullet, false);

            AppendFeedback($"Nova.ObjectPool.CreateSingleGettingObjectPool<DemoBullet>() -> ok / Count={m_BulletPool.Count}", FeedbackLevel.Success);
            RefreshStats();
        }

        /// <summary>
        /// Spawn 按钮点击：从对象池 Get 一个 DemoBullet 对象。
        /// </summary>
        private void OnSpawnButtonClick()
        {
            if (m_BulletPool == null)
            {
                AppendFeedback("请先创建对象池", FeedbackLevel.Warn);
                return;
            }

            if (!m_BulletPool.CanGet())
            {
                DemoBullet newBullet = DemoBullet.Create();
                m_BulletPool.Register(newBullet, false);
            }

            m_CurrentBullet = m_BulletPool.Get();
            AppendFeedback($"Nova.ObjectPool.GetObjectPool<DemoBullet>().Get() -> Count={m_BulletPool.Count} CanRelease={m_BulletPool.CanReleaseCount}", FeedbackLevel.Success);
            RefreshStats();
        }

        /// <summary>
        /// Recycle 按钮点击：将当前 DemoBullet 对象 Put 回对象池。
        /// </summary>
        private void OnRecycleButtonClick()
        {
            if (m_BulletPool == null)
            {
                AppendFeedback("请先创建对象池", FeedbackLevel.Warn);
                return;
            }

            if (m_CurrentBullet == null)
            {
                AppendFeedback("无已 Spawn 的 DemoBullet 可回收，请先 Spawn", FeedbackLevel.Warn);
                return;
            }

            m_BulletPool.Put(m_CurrentBullet);
            m_CurrentBullet = null;

            AppendFeedback($"Nova.ObjectPool.GetObjectPool<DemoBullet>().Put() -> Count={m_BulletPool.Count} CanRelease={m_BulletPool.CanReleaseCount}", FeedbackLevel.Success);
            RefreshStats();
        }

        /// <summary>
        /// 销毁按钮点击：销毁 DemoBullet 对象池。
        /// </summary>
        private void OnDestroyButtonClick()
        {
            if (Nova.ObjectPool == null)
            {
                AppendFeedback("Nova.ObjectPool 不可用", FeedbackLevel.Error);
                return;
            }

            if (m_BulletPool == null)
            {
                AppendFeedback("DemoBullet 对象池不存在", FeedbackLevel.Warn);
                return;
            }

            m_CurrentBullet = null;
            Nova.ObjectPool.DestroyObjectPool<DemoBullet>();
            m_BulletPool = null;

            AppendFeedback("Nova.ObjectPool.DestroyObjectPool<DemoBullet>() -> ok", FeedbackLevel.Success);
            RefreshStats();
        }

        /// <summary>
        /// 刷新统计文本（Count / CanReleaseCount / Capacity）。
        /// </summary>
        private void RefreshStats()
        {
            if (m_StatsText == null)
            {
                return;
            }

            if (m_BulletPool == null)
            {
                m_StatsText.text = "对象池：未创建";
                return;
            }

            m_StatsText.text = $"Count={m_BulletPool.Count}  CanRelease={m_BulletPool.CanReleaseCount}  Capacity={m_BulletPool.Capacity}";
        }
    }
}
