/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Config.RuntimeProvider.cs
 * author:    taoye
 * created:   2026/4/30
 * descrip:   按需从 AssetDatabase 读取最新 ConfigRuntimeSO 的 Editor 端访问器
 ***************************************************************/

using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Config
        {
            /// <summary>
            /// 按需从 AssetDatabase 读取激活 ConfigMasterSO 配对的 ConfigRuntimeSO 的 Editor 端访问器。
            /// <para>通过 WorkspaceActive 锚点定位激活 master，根除多 sample 共存时 FindAssets 玄学命中问题。</para>
            /// </summary>
            public static class RuntimeProvider
            {
                /// <summary>
                /// 获取当前激活 ConfigMaster 配对的 ConfigRuntimeSO。
                /// <para>经 WorkspaceActive 锚定激活 master，按 ADR-033 布局约定（DemoRoot/Configs/ConfigRuntime.asset）定位其配对 ConfigRuntimeSO；</para>
                /// <para>无激活 master 或 ConfigRuntime 未导出时返回 null。</para>
                /// </summary>
                /// <returns>激活 master 配对的 ConfigRuntimeSO；无激活 master 或未导出时返回 null。</returns>
                public static ConfigRuntimeSO GetCurrent()
                {
                    return WorkspaceActive.GetActiveRuntime();
                }

                /// <summary>
                /// 获取当前运行时 Namespace。
                /// <para>直接读取激活 ConfigMasterSO 的 Namespace 源头真相（见 ADR-005 单一写入路径），</para>
                /// <para>经 WorkspaceActive.Get() 锚点定位激活 master（见 ADR-047），不再经 GetCurrent() 选派生的 ConfigRuntimeSO，</para>
                /// <para>从而避免多 sample 共存且三维相同时按 FindAssets 字典序选错副本；找不到激活 master 时返回 string.Empty。</para>
                /// </summary>
                /// <returns>Namespace 字符串；找不到激活 master 时返回 string.Empty。</returns>
                public static string GetNamespace()
                {
                    ConfigMasterSO master = WorkspaceActive.Get();
                    if (master == null) return string.Empty;
                    return master.Namespace ?? string.Empty;
                }

            }
        }
    }
}
