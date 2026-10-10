/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * filename:  ResourceKeyBuildPreprocessor.cs
 * author:    taoye
 * created:   2026/10/9
 * descrip:   阻止使用未同步资源密钥构建客户端
 ***************************************************************/
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
namespace NovaFramework.Editor
{
    /// <summary>构建前只验证，不在 Player 构建中临时生成需要重新编译的代码。</summary>
    internal sealed class ResourceKeyBuildPreprocessor : IPreprocessBuildWithReport
    {
        /// <summary>在正式构建最早阶段阻止陈旧密钥。</summary>
        public int callbackOrder => int.MinValue + 50;
        /// <summary>确认本地密钥与已生成代码一致。</summary>
        public void OnPreprocessBuild(BuildReport report) => ResourceKeyCodeGenerator.ValidateGenerated();
    }
}
