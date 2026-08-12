/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIAPAssemblyInfo.cs
 * author:    yingzheng
 * created:   2026/8/4
 * descrip:   IAP Demo Core 向可选商店适配程序集开放内部契约
 ***************************************************************/

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("NovaFramework.Sdk.IAP.Samples.Mobile.Runtime")]
[assembly: InternalsVisibleTo("NovaFramework.Sdk.IAP.Samples.ThirdPay.Runtime")]
[assembly: InternalsVisibleTo("NovaFramework.Sdk.IAP.Samples.Voucher.Runtime")]
