/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  GeneratedExportCompilationState.cs
 * author:    taoye
 * created:   2026/10/12
 * descrip:   生成导出收据对应的编译与脚本重载证据
 ***************************************************************/

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 用单次令牌关联导出后的真实编译周期；SessionState 仅跨脚本重载，Editor 重启后证据失效。
    /// </summary>
    [InitializeOnLoad]
    internal static class GeneratedExportCompilationState
    {
        private const string c_Key = "Nova.AgentActions.GeneratedExportCompilation.v2";
        private static int s_StableUpdates;
        private static readonly string s_DomainId = Guid.NewGuid().ToString("N");
        internal const string FirstReloadProtocol = "nova-generated-export-first-reload-v1";

        [Serializable]
        internal sealed class Record
        {
            public string token;
            public string payloadHash;
            public long createdUtcTicks;
            public bool requested;
            public bool cleanRequested;
            public bool started;
            public bool finished;
            public bool failed;
            public bool reloaded;
            public string firstReloadDomain;
            public string scheduledDomain;
            public string firstReloadProtocol;
            public string scheduleNonce;

            /// <summary>只登记真实完成记录第一次重载的当前域；JsonUtility将未签名null字符串往返为空串，两者都视为未签名，旧已重载记录和非空签名仍不能补签或更新。</summary>
            /// <param name="domain">本次InitializeOnLoad域nonce。</param>
            /// <returns>实际首次登记时为真。</returns>
            internal bool MarkFirstReload(string domain)
            {
                if (!requested || !started || !finished || failed || reloaded || !string.IsNullOrEmpty(firstReloadDomain) || assemblyCount <= 0 && notRequiredCount <= 0 || firstReloadProtocol != FirstReloadProtocol || !IsDomainNonce(scheduleNonce) || !IsDomainNonce(domain) || !IsDomainNonce(scheduledDomain) || scheduledDomain == domain) return false;
                firstReloadDomain = domain; reloaded = true; return true;
            }

            /// <summary>新的编译周期开始后只撤销新SVS首次域许可，保留旧导出Verify布尔语义。</summary>
            /// <returns>实际撤销新协议许可时为真；未首次重载的finished记录也永久撤销。</returns>
            internal bool InvalidateFirstReloadForNextCompilation()
            {
                if (firstReloadProtocol == null && scheduleNonce == null) return false;
                firstReloadProtocol = null; scheduleNonce = null; return true;
            }

            /// <summary>独立纯记录门，不调用Unity或改变状态；仅新SVS API消费，旧Verify语义不变。</summary>
            /// <param name="expectedToken">本次Receipt令牌。</param><param name="expectedHash">本次载荷SHA。</param><param name="domain">当前域nonce。</param><param name="nowTicks">当前UTC ticks。</param>
            /// <returns>完整成功、未过期、同令牌且位于第一次正式重载域才为真。</returns>
            internal bool IsCurrentFirstReload(string expectedToken, string expectedHash, string domain, long nowTicks)
            {
                return !string.IsNullOrEmpty(expectedToken) && !string.IsNullOrEmpty(expectedHash) && token == expectedToken && payloadHash == expectedHash && requested && started && finished && !failed && reloaded && (assemblyCount > 0 || notRequiredCount > 0) && firstReloadProtocol == FirstReloadProtocol && IsDomainNonce(scheduleNonce) && IsDomainNonce(scheduledDomain) && IsDomainNonce(domain) && scheduledDomain != domain && firstReloadDomain == domain && createdUtcTicks > 0 && nowTicks <= DateTime.MaxValue.Ticks && nowTicks >= createdUtcTicks && nowTicks - createdUtcTicks < TimeSpan.FromMinutes(5).Ticks;
            }
            /// <summary>只接受Schedule/实际域生成的规范N-GUID；反序列化旧字段不赋默认新stamp。</summary>
            /// <param name="value">原记录中的实际nonce。</param>
            /// <returns>恰为小写32位非空GUID时为真。</returns>
            private static bool IsDomainNonce(string value) => Guid.TryParseExact(value, "N", out Guid id) && id != Guid.Empty && id.ToString("N") == value;

            public int assemblyCount;
            public int notRequiredCount;
        }

        /// <summary>
        /// 重载后恢复结果，并重新订阅编译事件；不恢复或重放导出。
        /// </summary>
        static GeneratedExportCompilationState()
        {
            Record record = Read();
            if (record != null && record.finished && !record.failed && !record.reloaded)
            {
                // 新API只认可Schedule记录的原域；旧无stamp记录仍保留原reloaded布尔语义，不补签新证明。
                record.MarkFirstReload(s_DomainId); record.reloaded = true; Write(record);
            }
            CompilationPipeline.compilationStarted += OnStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyFinished;
            CompilationPipeline.assemblyCompilationNotRequired += OnAssemblyNotRequired;
            CompilationPipeline.compilationFinished += OnFinished;
            // update 订阅跨重载重建；导出回调后的 delayCall 在实际导入流程中可能丢失。
            EditorApplication.update += RequestCompilation;
        }

        /// <summary>
        /// 判断是否仍有未结束的导出编译请求，避免第二个导出覆盖第一份收据证据。
        /// </summary>
        internal static bool IsPending()
        {
            Record record = Read();
            return record != null && (!record.finished || (!record.failed && !record.cleanRequested && record.assemblyCount == 0 && record.notRequiredCount == 0)) &&
                DateTime.UtcNow.Ticks - record.createdUtcTicks < TimeSpan.FromMinutes(5).Ticks;
        }

        /// <summary>
        /// 正式输出完成后登记证据，接住后续自动编译；无实际程序集的空周期最多补一次清缓存编译。
        /// </summary>
        /// <param name="token">本次新正式输出的原Receipt令牌。</param>
        /// <param name="payloadHash">原完整输出材料绑定的载荷SHA。</param>
        internal static void Schedule(string token, string payloadHash)
        {
            if (IsPending()) throw new InvalidOperationException("已有导出编译尚未完成。");
            Write(new Record { token = token, payloadHash = payloadHash, createdUtcTicks = DateTime.UtcNow.Ticks, scheduledDomain = s_DomainId, firstReloadProtocol = FirstReloadProtocol, scheduleNonce = Guid.NewGuid().ToString("N") });
            s_StableUpdates = 0;
        }

        /// <summary>
        /// 只读核验令牌、载荷、本次编译流水线的程序集结果与重载；输出未变化结果不冒充物理重编译。
        /// </summary>
        internal static bool TryVerify(string token, string payloadHash, bool requireReload, out string error)
        {
            return Validate(Read(), token, payloadHash, requireReload, out error);
        }

        /// <summary>仅Generate SVS消费的首次正式重载域证明；旧TryVerify及其它导出保持原语义。</summary>
        /// <param name="token">原Receipt编译令牌。</param><param name="payloadHash">原载荷SHA。</param><param name="error">具体失败原因。</param>
        /// <returns>实际记录成功且当前正是首次重载域时为真。</returns>
        internal static bool TryVerifyInCurrentReloadDomain(string token, string payloadHash, out string error)
        {
            return TryCaptureCurrentReloadRecord(token, payloadHash, out _, out error);
        }

        /// <summary>本次只读核验时实际读取的完整新协议记录；DTO本身不能授予任何执行许可。</summary>
        [Serializable]
        internal sealed class ReloadDomainEvidence
        {
            public string currentDomain;
            public long observedUtcTicks;
            public Record record;
        }

        /// <summary>仅在实际首次域门全部通过时返回这次Read取得的记录，不改SessionState或旧Receipt。</summary>
        /// <param name="token">原正式Receipt令牌。</param><param name="payloadHash">原完整材料载荷SHA。</param><param name="evidence">本次真实只读记录与域。</param><param name="error">具体失败原因。</param>
        /// <returns>稳定、成功且当前正是首次不同域时为真。</returns>
        internal static bool TryCaptureCurrentReloadRecord(string token, string payloadHash, out ReloadDomainEvidence evidence, out string error)
        {
            evidence = null; Record record = Read(); long observed = DateTime.UtcNow.Ticks;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { error = "Generate SVS 当前Editor仍在编译或导入，不能消费首次重载许可。"; return false; }
            if (!Validate(record, token, payloadHash, true, out error)) return false;
            if (!record.IsCurrentFirstReload(token, payloadHash, s_DomainId, observed)) { error = "Generate SVS 需要本次未过期编译的首次重载域；旧记录缺stamp、后续重载或错误令牌均不能补签。"; return false; }
            evidence = new ReloadDomainEvidence { currentDomain = s_DomainId, observedUtcTicks = observed, record = record }; return true;
        }

        /// <summary>
        /// 校验独立编译记录，便于定向测试失败、旧令牌和未重载边界。
        /// </summary>
        internal static bool Validate(Record record, string token, string payloadHash, bool requireReload, out string error)
        {
            error = null;
            if (record == null || string.IsNullOrEmpty(token) || record.token != token || record.payloadHash != payloadHash)
                error = "本次导出编译证据不存在、已被后续导出替代或与 Receipt 不匹配。";
            else if (!record.requested || !record.started || !record.finished || (record.assemblyCount <= 0 && record.notRequiredCount <= 0))
                error = "本次导出的实际程序集编译尚未完成。";
            else if (record.failed || EditorUtility.scriptCompilationFailed)
                error = "本次 Unity 编译存在错误。";
            else if (requireReload && !record.reloaded)
                error = "代码导出后尚未完成脚本重载。";
            return error == null;
        }

        /// <summary>
        /// 等待两个稳定 update；未自动编译或自动周期未编译任何程序集时，同令牌最多请求一次清缓存编译。
        /// </summary>
        private static void RequestCompilation()
        {
            Record record = Read();
            if (record == null || record.failed || record.cleanRequested ||
                (record.requested && !record.finished) || (record.finished && (record.assemblyCount > 0 || record.notRequiredCount > 0)) ||
                DateTime.UtcNow.Ticks - record.createdUtcTicks >= TimeSpan.FromMinutes(5).Ticks) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                s_StableUpdates = 0;
                return;
            }
            if (++s_StableUpdates < 2) return;
            record.requested = true;
            record.cleanRequested = true;
            record.started = false;
            record.finished = false;
            record.reloaded = false;
            record.firstReloadDomain = null;
            record.assemblyCount = 0;
            record.notRequiredCount = 0;
            Write(record);
            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
        }

        /// <summary>
        /// 仅接受本次正式输出完成后开始的自动或显式编译周期，不借用已结束周期。
        /// </summary>
        /// <param name="_">Unity编译事件上下文；不读取或作为本次证明。</param>
        private static void OnStarted(object _)
        {
            Record record = Read();
            if (record == null) return;
            if (record.finished)
            {
                if (record.InvalidateFirstReloadForNextCompilation()) Write(record);
                return;
            }
            if (record.started) return;
            record.requested = true;
            record.started = true;
            Write(record);
        }

        /// <summary>
        /// 记录实际程序集完成回调与错误；独立于本次核验后输出未变化的回调数。
        /// </summary>
        private static void OnAssemblyFinished(string _, CompilerMessage[] messages)
        {
            Record record = Read();
            if (record == null || !record.started || record.finished) return;
            record.assemblyCount++;
            record.failed |= messages != null && messages.Any(message => message.type == CompilerMessageType.Error);
            Write(record);
        }

        /// <summary>
        /// 记录 Unity 本次编译输入核验后程序集输出未变化的真实事件；不将缓存命中冒称物理编译。
        /// </summary>
        private static void OnAssemblyNotRequired(string _)
        {
            Record record = Read();
            if (record == null || !record.started || record.finished) return;
            record.notRequiredCount++;
            Write(record);
        }

        /// <summary>
        /// 结束当前已登记的编译周期，保留失败结果供只读 Verify。
        /// </summary>
        private static void OnFinished(object _)
        {
            Record record = Read();
            if (record == null || !record.started || record.finished) return;
            record.finished = true;
            Write(record);
        }

        /// <summary>
        /// 读取当前 Editor 会话证据；损坏记录按无证据处理。
        /// </summary>
        private static Record Read()
        {
            string json = SessionState.GetString(c_Key, string.Empty);
            if (string.IsNullOrEmpty(json)) return null;
            try { return UnityEngine.JsonUtility.FromJson<Record>(json); }
            catch (ArgumentException) { return null; }
        }

        /// <summary>
        /// 保存当前请求证据，固定键仅保留最近一次导出，避免无界累积。
        /// </summary>
        private static void Write(Record record)
        {
            SessionState.SetString(c_Key, UnityEngine.JsonUtility.ToJson(record));
        }
    }
}
