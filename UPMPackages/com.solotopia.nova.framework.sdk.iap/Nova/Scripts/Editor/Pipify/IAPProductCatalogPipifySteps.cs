/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPProductCatalogPipifySteps.cs
 * author:    Codex
 * created:   2026/10/19
 * descrip:   IAP 商品目录 Pipify 上传步骤
 ***************************************************************/

using System;
using Cysharp.Threading.Tasks;
using NovaFramework.Editor;

namespace NovaFramework.SDK.IAP.Editor
{
    /// <summary>
    /// IAP 包内的商品目录 Pipify 步骤。
    /// </summary>
    internal static class IAPProductCatalogPipifySteps
    {
        /// <summary>
        /// 上传当前导出的 IAP 商品目录。
        /// </summary>
        [PipifyStep("iap.product_catalog.upload", "导出IAP数据到服务器", "IAP")]
        internal static async UniTask UploadAsync(PipifyContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            context.CancellationToken.ThrowIfCancellationRequested();
            await IAPProductCatalogUploader.UploadCurrentAsync(context.CancellationToken);
        }
    }
}
