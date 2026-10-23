/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPProductCatalogActionBridge.cs
 * author:    Codex
 * created:   2026/10/22
 * descrip:   受控 Action 复用的 IAP 商品目录冻结与脱敏一次上传桥
 ***************************************************************/

using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NovaFramework.Editor;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Editor
{
    /// <summary>
    /// 仅以固定签名提供显式导出物的目录准备、描述和提交，不注册任意类型或反射执行入口。
    /// </summary>
    public static class IAPProductCatalogActionBridge
    {
        /// <summary>
        /// 只驻留内存的精确准备类型；不反序列化，不向调用方暴露最终请求或密钥。
        /// </summary>
        private sealed class PreparedUpload
        {
            internal readonly IAPProductCatalogSnapshot Snapshot;
            internal readonly string RequestJson;
            internal readonly string RequestSha256;
            internal readonly string DescriptionJson;

            /// <summary>
            /// 原准备对象的原子消费标记；失败、取消或忙碌均不恢复，避免绕过上层计划的一次提交约束。
            /// </summary>
            internal int Consumed;

            /// <summary>
            /// 保存准备阶段已经冻结的快照、最终请求及脱敏描述，不保留可变 Config 引用。
            /// </summary>
            /// <param name="snapshot">地址、Head 和密文已经冻结的快照。</param>
            /// <param name="requestJson">唯一正式序列化入口生成的最终请求。</param>
            /// <param name="requestSha256">最终请求 UTF-8 字节的摘要。</param>
            /// <param name="descriptionJson">仅包含约定元数据的紧凑 JSON。</param>
            internal PreparedUpload(IAPProductCatalogSnapshot snapshot, string requestJson, string requestSha256, string descriptionJson)
            {
                Snapshot = snapshot;
                RequestJson = requestJson;
                RequestSha256 = requestSha256;
                DescriptionJson = descriptionJson;
            }
        }

        /// <summary>
        /// 从调用方已经锚定的显式导出物创建冻结目录；不读取全局当前配置、不保存或上传。
        /// </summary>
        /// <param name="runtime">由受控调用方核验身份及保存状态的 ConfigRuntimeSO。</param>
        /// <returns>仅本桥接受的私有准备对象，不能从 JSON 或任意对象替代构造。</returns>
        public static object Prepare(ConfigRuntimeSO runtime)
        {
            EditorUtil.Network.Http.RequestHeader head = EditorUtil.Network.Http.CreateDefaultHeader(runtime);
            return PrepareWithHeader(runtime, head);
        }

        /// <summary>
        /// 返回准备阶段的严格脱敏元数据，不重新读取配置，也不序列化请求 Body。
        /// </summary>
        /// <param name="prepared">同一已加载桥创建的精确私有准备对象。</param>
        /// <returns>schemaVersion 为 1 的固定十一字段 JSON。</returns>
        public static string Describe(object prepared)
        {
            return RequirePrepared(prepared).DescriptionJson;
        }

        /// <summary>
        /// 原子消费原准备对象后提交最终请求一次；即使失败或取消也不可复用该对象，仅返回脱敏 HTTP 事实。
        /// </summary>
        /// <param name="prepared">已经完成受控计划和身份核验的原准备对象。</param>
        /// <param name="cancellationToken">本次请求取消令牌，不触发重试或排队。</param>
        /// <returns>固定结果 JSON；Task 只用于跨程序集 Handler 边界，内部复用 UniTask 发送链。</returns>
        public static Task<string> SubmitAsync(object prepared, CancellationToken cancellationToken)
        {
            return SubmitCoreAsync(RequirePrepared(prepared), cancellationToken, IAPProductCatalogUploader.SendJsonAsync).AsTask();
        }

        /// <summary>
        /// 使用显式同源 Head 准备目录，供公开入口和无网络确定性测试复用同一实现。
        /// </summary>
        /// <param name="runtime">商品、模式与 App AES 凭据的唯一导出物。</param>
        /// <param name="head">由同一导出物构造的合法明文请求头。</param>
        /// <returns>不保留可变配置引用的私有准备对象。</returns>
        internal static object PrepareWithHeader(ConfigRuntimeSO runtime, EditorUtil.Network.Http.RequestHeader head)
        {
            IAPProductCatalogSnapshot snapshot = IAPProductCatalogProtocol.PrepareSnapshot(runtime, head, out string catalogHash, out int productCount);
            string json = IAPProductCatalogProtocol.Serialize(snapshot.Request);
            string requestSha256 = IAPProductCatalogCanonicalizer.ComputeHash(json);
            string description = new JObject
            {
                ["schemaVersion"] = 1,
                ["appId"] = head.AppId,
                ["platform"] = head.Platform,
                ["channel"] = head.Channel,
                ["developMode"] = head.DevelopMode,
                ["version"] = head.Version,
                ["uploadUri"] = snapshot.UploadUri.AbsoluteUri,
                ["catalogHash"] = catalogHash,
                ["productCount"] = productCount,
                ["requestSha256"] = requestSha256,
                ["requestByteCount"] = Encoding.UTF8.GetByteCount(json),
            }.ToString(Formatting.None);
            return new PreparedUpload(snapshot, json, requestSha256, description);
        }

        /// <summary>
        /// 以假发送器验证真实准备与结果适配路径；生产公开入口不接受委托参数。
        /// </summary>
        /// <param name="prepared">由同桥准备的精确对象。</param>
        /// <param name="cancellationToken">测试取消令牌。</param>
        /// <param name="sendJson">不会访问真实网络的测试发送器。</param>
        /// <returns>与公开 SubmitAsync 相同字段和脱敏规则的 JSON。</returns>
        internal static Task<string> SubmitWithSenderAsync(object prepared, CancellationToken cancellationToken, IAPProductCatalogUploader.SendJsonDelegate sendJson)
        {
            return SubmitCoreAsync(RequirePrepared(prepared), cancellationToken, sendJson).AsTask();
        }

        /// <summary>
        /// 只接收本桥准确类型，拒绝空值、匿名对象、JSON、跨加载域或其他类型的替代对象。
        /// </summary>
        /// <param name="prepared">调用方提供的不透明准备对象。</param>
        /// <returns>当前已加载桥拥有的精确准备对象。</returns>
        private static PreparedUpload RequirePrepared(object prepared)
        {
            if (prepared == null || prepared.GetType() != typeof(PreparedUpload))
            {
                throw new ArgumentException("prepared 必须是当前 IAP 商品目录桥创建的原准备对象。", nameof(prepared));
            }
            return (PreparedUpload)prepared;
        }

        /// <summary>
        /// 复用共享一次发送核心并只投影有限结果字段，原始响应与异常正文不进入日志或返回 JSON。
        /// </summary>
        /// <param name="prepared">保存原快照及最终 JSON 的私有对象。</param>
        /// <param name="cancellationToken">本次请求取消令牌。</param>
        /// <param name="sendJson">固定生产发送器或内部测试发送器。</param>
        /// <returns>只陈述一次请求真实结果的 schemaVersion 1 JSON。</returns>
        private static async UniTask<string> SubmitCoreAsync(PreparedUpload prepared, CancellationToken cancellationToken, IAPProductCatalogUploader.SendJsonDelegate sendJson)
        {
            IAPProductCatalogUploader.UploadAttempt attempt;
            if (Interlocked.CompareExchange(ref prepared.Consumed, 1, 0) != 0)
            {
                DateTime now = DateTime.UtcNow;
                attempt = new IAPProductCatalogUploader.UploadAttempt
                {
                    Outcome = "busy",
                    ExceptionKind = "busy",
                    StartedAtUtc = now,
                    CompletedAtUtc = now,
                };
            }
            else
            {
                attempt = await IAPProductCatalogUploader.UploadOnceAsync(prepared.Snapshot, prepared.RequestJson, cancellationToken, sendJson);
            }
            string responseBody = attempt.Response?.Body;
            return new JObject
            {
                ["schemaVersion"] = 1,
                ["requestSha256"] = prepared.RequestSha256,
                ["outcome"] = attempt.Outcome,
                ["dispatched"] = attempt.Dispatched,
                ["statusCode"] = attempt.Response?.StatusCode ?? 0,
                ["responseByteCount"] = responseBody == null ? 0 : Encoding.UTF8.GetByteCount(responseBody),
                ["responseSha256"] = responseBody == null ? string.Empty : IAPProductCatalogCanonicalizer.ComputeHash(responseBody),
                ["versionConflict"] = attempt.Outcome == "version-conflict",
                ["exceptionKind"] = attempt.ExceptionKind,
                ["startedAtUtc"] = attempt.StartedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                ["completedAtUtc"] = attempt.CompletedAtUtc.ToString("O", CultureInfo.InvariantCulture),
            }.ToString(Formatting.None);
        }
    }
}
