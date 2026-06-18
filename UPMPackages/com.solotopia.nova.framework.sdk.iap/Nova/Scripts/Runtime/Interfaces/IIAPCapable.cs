/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IIAPCapable.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   IAP store 扩展能力基接口，所有 capability 接口均继承此接口
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAP store 扩展能力标记基接口。
    /// 所有 store 特有能力接口（IIAPQueryCapable、IIAPSubscriptionCapable 等）均继承此接口，
    /// 配合 IAPPlugin.TryGetCapability&lt;T&gt; 实现按功能发现能力，避免在 IAPPlugin 上堆积转发方法。
    /// </summary>
    public interface IIAPCapable
    {
    }
}
