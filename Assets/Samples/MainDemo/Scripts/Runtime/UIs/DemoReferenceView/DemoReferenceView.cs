/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoReferenceView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Demo 1.4 — ReferencePool Get/Put 计数演示
 *            提供 Get×1 / Put×1 / Get×100 三个按钮，
 *            实时展示 DemoData 类型的 Using / Free 计数。
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Demo 1.4 ReferencePool Get/Put 计数演示 View。
    /// 演示 ReferencePool.Get / Put API，并实时打印 Using/Free 计数。
    /// </summary>
    public sealed class DemoReferenceView : BaseDemoView
    {
        /// <summary>
        /// 「Get×1」按钮，从池中获取一个 DemoData 实例。
        /// </summary>

        [SerializeField] private Button m_GetOneButton;

        /// <summary>
        /// 「Put×1」按钮，将最近获取的 DemoData 实例归还池中。
        /// </summary>

        [SerializeField] private Button m_PutOneButton;

        /// <summary>
        /// 「Get×100」按钮，批量获取 100 个 DemoData 实例。
        /// </summary>

        [SerializeField] private Button m_GetHundredButton;

        /// <summary>
        /// 当前持有的 DemoData 实例列表（未归还的）。
        /// </summary>
        private readonly List<DemoReferenceData> m_HeldRefs = new List<DemoReferenceData>();

        /// <summary>
        /// 初始化钩子，注册所有按钮事件。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            if (m_GetOneButton != null) m_GetOneButton.onClick.AddListener(OnGetOneClick);
            if (m_PutOneButton != null) m_PutOneButton.onClick.AddListener(OnPutOneClick);
            if (m_GetHundredButton != null) m_GetHundredButton.onClick.AddListener(OnGetHundredClick);
        }

        /// <summary>
        /// 打开钩子，设置标题与 API 副标题，并显示当前池状态。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            SetTitle("ReferencePool");
            SetButtonApiHint(m_GetOneButton, "ReferencePool.Get<T>()");
            SetButtonApiHint(m_PutOneButton, "ReferencePool.Put(reference)");
            SetButtonApiHint(m_GetHundredButton, "ReferencePool.Get<T>() ×100");
            LogPoolStatus("Init");
        }

        /// <summary>
        /// 关闭钩子，归还所有未放回的引用，防止泄漏。
        /// </summary>
        /// <param name="isShutdown">是否因视图管理器关闭而触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            for (int i = 0; i < m_HeldRefs.Count; i++)
            {
                ReferencePool.Put(m_HeldRefs[i]);
            }
            m_HeldRefs.Clear();
            base.OnClose(isShutdown, userData);
        }

        /// <summary>
        /// 「Get×1」点击回调。
        /// </summary>
        private void OnGetOneClick()
        {
            DemoReferenceData data = ReferencePool.Get<DemoReferenceData>();
            m_HeldRefs.Add(data);
            LogPoolStatus($"ReferencePool.Get<DemoReferenceData>()");
        }

        /// <summary>
        /// 「Put×1」点击回调，归还最后获取的实例。
        /// </summary>
        private void OnPutOneClick()
        {
            if (m_HeldRefs.Count == 0)
            {
                AppendFeedback("当前无持有实例，请先 Get", FeedbackLevel.Warn);
                return;
            }

            DemoReferenceData last = m_HeldRefs[m_HeldRefs.Count - 1];
            m_HeldRefs.RemoveAt(m_HeldRefs.Count - 1);
            ReferencePool.Put(last);
            LogPoolStatus("ReferencePool.Put(reference)");
        }

        /// <summary>
        /// 「Get×100」点击回调，批量获取 100 个实例。
        /// </summary>
        private void OnGetHundredClick()
        {
            for (int i = 0; i < 100; i++)
            {
                m_HeldRefs.Add(ReferencePool.Get<DemoReferenceData>());
            }
            LogPoolStatus("ReferencePool.Get<DemoReferenceData>() ×100");
        }

        /// <summary>
        /// 查询当前 DemoReferenceData 的池状态并输出到反馈区。
        /// </summary>
        /// <param name="action">触发动作描述，用于构成 API 前缀日志。</param>
        private void LogPoolStatus(string action)
        {
            int using_ = m_HeldRefs.Count;
            int free = 0;
            var infos = ReferencePool.GetAllReferencePoolInfos();
            if (infos != null)
            {
                for (int i = 0; i < infos.Count; i++)
                {
                    if (infos[i].Type == typeof(DemoReferenceData))
                    {
                        free = infos[i].UnusedReferenceCount;
                        using_ = infos[i].UsingReferenceCount;
                        break;
                    }
                }
            }
            AppendFeedback($"{action} -> Using={using_} Free={free}", FeedbackLevel.Success);
        }

        /// <summary>
        /// 演示用引用数据，实现 IReference 接口。
        /// </summary>
        public sealed class DemoReferenceData : IReference
        {
            /// <summary>
            /// 重置引用数据，归还池时由框架调用。
            /// </summary>
            public void Clear() { }
        }
    }
}
