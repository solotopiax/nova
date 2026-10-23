/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPProductCatalogProtocol.cs
 * author:    Codex
 * created:   2026/10/19
 * descrip:   从当前导出 Config 构造 IAP 商品目录快照
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using NovaFramework.Editor;
using NovaFramework.Runtime;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Editor
{
    /// <summary>
    /// Editor-only 的 IAP 商品目录协议入口，供后续 Pipify 上传步骤复用。
    /// </summary>
    public static class IAPProductCatalogProtocol
    {
        /// <summary>
        /// 开发者模式使用的测试服上传地址。
        /// </summary>
        public static readonly Uri DevelopmentUploadUri = new Uri("https://t-tools-admin.ahameet.com/api/admin/v1/iap/product-catalogs/upload");

        /// <summary>
        /// Release 模式使用的正式服上传地址。
        /// </summary>
        public static readonly Uri ProductionUploadUri = new Uri("https://tools-admin.ahameet.com/api/admin/v1/iap/product-catalogs/upload");

        /// <summary>
        /// 根据是否为开发者模式选择商品目录上传地址。
        /// </summary>
        /// <param name="developMode">当前导出 ConfigRuntimeSO 的开发模式。</param>
        /// <returns>Debug 对应测试地址，Release 对应正式地址。</returns>
        public static Uri GetUploadUri(DevelopMode developMode)
        {
            return developMode switch
            {
                DevelopMode.Debug => DevelopmentUploadUri,
                DevelopMode.Release => ProductionUploadUri,
                _ => throw new ArgumentOutOfRangeException(nameof(developMode), developMode, "developMode 必须是 Debug 或 Release。")
            };
        }

        /// <summary>
        /// 从当前激活的同一份 ConfigRuntime 构造默认 Head 与商品目录快照。
        /// </summary>
        /// <returns>包含最终上传地址和加密请求的冻结快照。</returns>
        public static IAPProductCatalogSnapshot CreateFromCurrentConfig()
        {
            ConfigRuntimeSO runtime = EditorUtil.Config.RuntimeProvider.GetCurrent();
            EditorUtil.Network.Http.RequestHeader head = EditorUtil.Network.Http.CreateDefaultHeader(runtime);
            return Create(runtime, head);
        }

        /// <summary>
        /// 把冻结请求序列化为紧凑 JSON。
        /// </summary>
        /// <param name="request">包含明文 Head 和加密 Body 的冻结请求。</param>
        /// <returns>最终发送的紧凑 JSON。</returns>
        public static string Serialize(IAPProductCatalogRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return JsonConvert.SerializeObject(request, Formatting.None);
        }

        /// <summary>
        /// 从指定导出物构造确定性快照，供同源编排与测试使用。
        /// </summary>
        /// <param name="runtime">商品、模式和 App AES 凭据的同源导出物。</param>
        /// <param name="head">从同一导出物构造的明文请求头。</param>
        /// <returns>包含最终上传地址和加密请求的冻结快照。</returns>
        internal static IAPProductCatalogSnapshot Create(ConfigRuntimeSO runtime, EditorUtil.Network.Http.RequestHeader head)
        {
            return CreateCore(runtime, head, out _, out _);
        }

        /// <summary>
        /// 复用同一投影与加密算法生成快照，并返回受控上传计划需要的完整目录身份。
        /// </summary>
        /// <param name="runtime">已经由调用方锚定的同源运行时导出物。</param>
        /// <param name="head">由同一导出物构造的明文请求头。</param>
        /// <param name="catalogHash">完整规范商品数组的小写 SHA-256。</param>
        /// <param name="productCount">经过校验并冻结的全量商品数量。</param>
        /// <returns>与既有 Create 相同字节协议的只读快照。</returns>
        internal static IAPProductCatalogSnapshot PrepareSnapshot(ConfigRuntimeSO runtime, EditorUtil.Network.Http.RequestHeader head, out string catalogHash, out int productCount)
        {
            return CreateCore(runtime, head, out catalogHash, out productCount);
        }

        /// <summary>
        /// 统一校验、排序、规范化、哈希和加密商品目录，避免计划与实际发送使用不同算法。
        /// </summary>
        /// <param name="runtime">商品、模式及 App AES 凭据的唯一导出物。</param>
        /// <param name="head">同源明文请求头；调用方负责其与导出物一致。</param>
        /// <param name="catalogHash">完整商品数组的规范化哈希。</param>
        /// <param name="productCount">完整商品数组数量。</param>
        /// <returns>冻结的上传地址、Head 和密文 Body。</returns>
        private static IAPProductCatalogSnapshot CreateCore(ConfigRuntimeSO runtime, EditorUtil.Network.Http.RequestHeader head, out string catalogHash, out int productCount)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (head == null) throw new ArgumentNullException(nameof(head));
            if (runtime.AppConfigs == null)
            {
                throw new InvalidOperationException("当前 ConfigRuntime.AppConfigs 不存在，无法加密 IAP 商品目录 Body。");
            }
            ValidateAppAesCredential(runtime.AppConfigs.AppAesKey, nameof(runtime.AppConfigs.AppAesKey));
            ValidateAppAesCredential(runtime.AppConfigs.AppAesIV, nameof(runtime.AppConfigs.AppAesIV));
            if (!Enum.IsDefined(typeof(DevelopMode), runtime.DevelopMode))
            {
                throw new InvalidOperationException($"ConfigRuntime.DevelopMode={runtime.DevelopMode} 未定义。");
            }

            IAPPluginConfig iap = runtime.GetSDKPluginConfig<IAPPluginConfig>();
            if (iap == null)
            {
                throw new InvalidOperationException("当前 ConfigRuntime 未导出已启用的 IAPPluginConfig。");
            }

            IReadOnlyList<IAPProductEntry> source = iap.Products;
            if (source == null || source.Count == 0)
            {
                throw new InvalidOperationException("IAPPluginConfig.Products 不能为空。");
            }

            var ids = new HashSet<long>();
            var projected = new List<(long TableId, IAPProductCatalogItem Item)>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                IAPProductEntry entry = source[i];
                if (entry == null)
                {
                    throw new InvalidOperationException($"IAPPluginConfig.Products[{i}] 不能为 null。");
                }
                if (entry.TableId <= 0)
                {
                    throw new InvalidOperationException($"商品 TableId 必须为正整数，当前值={entry.TableId}。");
                }
                if (!ids.Add(entry.TableId))
                {
                    throw new InvalidOperationException($"商品 TableId={entry.TableId} 重复。");
                }
                if (!Enum.IsDefined(typeof(IAPProductType), entry.ProductType))
                {
                    throw new InvalidOperationException($"商品 TableId={entry.TableId} 的 ProductType 未定义。");
                }

                projected.Add((entry.TableId, new IAPProductCatalogItem(entry.TableId.ToString(CultureInfo.InvariantCulture), entry.Name, entry.ProductID, entry.ThirdProductID, entry.ProductType.ToString(), entry.SubGroupID, entry.Price, entry.Currency)));
            }

            IAPProductCatalogItem[] products = projected
                .OrderBy(pair => pair.TableId)
                .Select(pair => pair.Item)
                .ToArray();
            string canonical = IAPProductCatalogCanonicalizer.Canonicalize(products);
            catalogHash = IAPProductCatalogCanonicalizer.ComputeHash(canonical);
            productCount = products.Length;
            var body = new IAPProductCatalogBody(catalogHash, products);
            string bodyJson = JsonConvert.SerializeObject(body, Formatting.None);
            string encryptedBody = Util.Encrypt.AES.EncryptString(bodyJson, runtime.AppConfigs.AppAesKey, runtime.AppConfigs.AppAesIV);
            if (string.IsNullOrEmpty(encryptedBody))
            {
                throw new InvalidOperationException("IAP 商品目录 Body AES 加密失败。");
            }

            var request = new IAPProductCatalogRequest(head, encryptedBody);
            return new IAPProductCatalogSnapshot(GetUploadUri(runtime.DevelopMode), request);
        }

        /// <summary>
        /// 校验服务端协议使用的 App AES 凭据，禁止回退到 Privacy AES 默认值。
        /// </summary>
        /// <param name="value">App AES Key 或 IV。</param>
        /// <param name="fieldName">用于错误信息的配置字段名。</param>
        private static void ValidateAppAesCredential(string value, string fieldName)
        {
            if (string.IsNullOrEmpty(value) || Encoding.UTF8.GetByteCount(value) != 16)
            {
                throw new InvalidOperationException($"ConfigRuntime.AppConfigs.{fieldName} 按 UTF-8 编码后必须为 16 字节，请在 Nova/Open Config → 通用配置中修复并重新导出 ConfigRuntimeSO。");
            }
        }
    }
}
