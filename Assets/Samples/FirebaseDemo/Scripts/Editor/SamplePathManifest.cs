/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SamplePathManifest.cs
 * author:    taoye
 * created:   2026/5/21
 * descrip:   Sample 路径自适应清单
 *            发版脚本扫描 sample 内所有 .prefab/.asset/.unity，把含
 *            开发工程 sample 路径前缀的文件相对路径写入本 ScriptableObject。
 *            外部工程 import 后由 SamplePathRewriter 读取并执行字符串前缀替换。
 ***************************************************************/

using System.Collections.Generic;
using UnityEngine;

namespace NovaFramework.Sdk.Firebase.Samples.Editor
{
    /// <summary>
    /// Sample 路径自适应清单。声明开发工程下 sample 的根路径以及需要重写路径的资产文件相对路径列表。
    /// </summary>
    public sealed class SamplePathManifest : ScriptableObject
    {
        /// <summary>
        /// 开发工程下 sample 根路径（发版脚本注入），如 "Assets/Samples/FirebaseDemo"。
        /// 外部工程 import 后以此为字符串前缀，被替换为运行时定位到的真实根路径。
        /// </summary>
        [SerializeField]
        private string m_DevSampleRoot = "Assets/Samples/FirebaseDemo";

        /// <summary>
        /// 重写完成的标记文件名，写入 sample 根目录用于防重入。
        /// </summary>
        [SerializeField]
        private List<string> m_RewriteTargets = new List<string>();
        [SerializeField]
        private string m_RewrittenMarker = ".nova-path-rewritten";

        /// <summary>
        /// 开发工程下 sample 根路径。
        /// </summary>
        public string DevSampleRoot => m_DevSampleRoot;

        /// <summary>
        /// 需要重写路径前缀的资产相对路径列表。
        /// </summary>
        public IReadOnlyList<string> RewriteTargets => m_RewriteTargets;

        /// <summary>
        /// 重写完成的标记文件名。
        /// </summary>
        public string RewrittenMarker => m_RewrittenMarker;
    }
}
