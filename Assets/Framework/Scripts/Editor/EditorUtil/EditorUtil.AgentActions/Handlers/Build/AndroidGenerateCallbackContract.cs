/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidGenerateCallbackContract.cs
 * author:    taoye
 * created:   2026/10/20
 * descrip:   Android Generate准确回调全集与逐项合同挂接
 ***************************************************************/
using System;
using System.Linq;
using System.Threading;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>保留完整真实Player/Gradle/SDK集合；逐项闭合前不借WebGL合同或包名前缀放行。</summary>
    internal static class AndroidGenerateCallbackContract
    {
        /// <summary>完整身份与仍未闭合的准确回调；绑定项不能由请求或可变DTO授予许可。</summary>
        [Serializable] internal sealed class Snapshot
        {
            public BuildActionCommon.BuildCallbackSnapshot discovered;
            public string[] boundContracts;
            public string[] unsupported;
        }

        /// <summary>读取本次完整回调集合；仅原共享采集器已审的精确门禁沿用，其余逐项保留阻碍。</summary>
        /// <param name="token">读取当前模块与源码时的取消令牌。</param><returns>完整身份、已审精确门禁及剩余项。</returns>
        internal static Snapshot Capture(CancellationToken token)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) throw new InvalidOperationException("Android回调合同必须来自真实Android活动域。");
            BuildActionCommon.BuildCallbackSnapshot original = BuildActionCommon.CaptureBuildCallbacks(true, token);
            return new Snapshot { discovered = original, boundContracts = new[] { "BuildActionCommon.exact-read-only-guards" },
                unsupported = original.unsupported.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray() };
        }
    }
}
