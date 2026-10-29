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

namespace NovaFramework.Sdk.Datamaster.Samples.Editor
{
    /// <summary>
    /// Sample 导入清单。声明开发态根路径、待重写资产和允许在导入后准备的生成目录。
    /// </summary>
    public sealed class SamplePathManifest : ScriptableObject
    {
        /// <summary>
        /// 开发工程下 sample 根路径（发版脚本注入），如 "Assets/Samples/DataMasterDemo"。
        /// 外部工程 import 后以此为字符串前缀，被替换为运行时定位到的真实根路径。
        /// </summary>
        [SerializeField]
        private string m_DevSampleRoot = "Assets/Samples/DataMasterDemo";

        /// <summary>
        /// 需要重写开发态路径前缀的资产文件相对路径。
        /// </summary>
        [SerializeField]
        private List<string> m_RewriteTargets = new List<string>();

        /// <summary>
        /// 重写完成的标记文件名，写入 sample 根目录用于防重入。
        /// </summary>
        [SerializeField]
        private string m_RewrittenMarker = ".nova-path-rewritten";

        /// <summary>
        /// 发版描述符明确声明的生成目录，相对当前 Sample 根目录；旧清单默认不创建目录。
        /// 这里只准备文件夹，不代表 DLL 或其他构建产物已经生成。
        /// </summary>
        [SerializeField]
        private List<string> m_GeneratedDirectories = new List<string>();

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

        /// <summary>
        /// 允许幂等准备的生成目录，只读接口不复制底层列表。
        /// </summary>
        public IReadOnlyList<string> GeneratedDirectories => m_GeneratedDirectories;
    }
}
