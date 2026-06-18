/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPThirdPayErrorCode.cs
 * author:    yingzheng
 * created:   2026/6/5
 * descrip:   ThirdPay store 专属错误码，从 0 起编
 ***************************************************************/

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    public enum IAPThirdPayErrorCode
    {
        None = 0,
        StoreInitFailed = 1,
        UserCancelled = 2,
        NetworkError = 3,
        ServerValidationFailed = 4,
        StoreNotAvailable = 5,
        WebViewClosed = 6,
        BillingNotReady = 7,
        ManualDelivery = 8,
    }
}
