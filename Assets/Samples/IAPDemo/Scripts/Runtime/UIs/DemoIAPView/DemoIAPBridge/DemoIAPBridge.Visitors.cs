/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIAPBridge.Visitors.cs
 * author:    nova-create-sample
 * created:   2026/06/05
 * descrip:   DemoIAPView IAP 调度桥接层 - 字段与类型
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using NovaFramework.SDK.IAP.Runtime;

using FeedbackLevel = NovaFramework.Sdk.IAP.Samples.Runtime.BaseDemoView.FeedbackLevel;

namespace NovaFramework.Sdk.IAP.Samples.Runtime
{
    internal sealed partial class DemoIAPBridge
    {
        private const string c_SceneName = "DemoIAPView";

        private readonly Action<string, FeedbackLevel> m_Feedback;
        private readonly Action<long, string> m_ProductTextChanged;
        private readonly Action<bool> m_PayInteractableChanged;
        private readonly Dictionary<long, ProductInfo> m_MobileProductInfos = new Dictionary<long, ProductInfo>();
        private readonly CancellationTokenSource m_Cancellation = new CancellationTokenSource();

        private IAPPlugin m_IAP;
        private bool m_EventsSubscribed;
        private bool m_Disposed;

        [Serializable]
        private sealed class PayPayload
        {
            public long TableId;
            public string Scene;
        }
    }
}
