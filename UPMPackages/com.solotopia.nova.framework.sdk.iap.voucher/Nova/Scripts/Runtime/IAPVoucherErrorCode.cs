/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPVoucherErrorCode.cs
 * author:    yingzheng
 * created:   2026/6/5
 * descrip:   Voucher store 专属错误码，从 0 起编
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    public enum IAPVoucherErrorCode
    {
        None = 0,
        NetworkError = 1,
        ServerValidationFailed = 2,
        InsufficientBalance = 3,
        CodeInvalid = 4,
    }
}
