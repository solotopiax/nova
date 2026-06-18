/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Build.Definitions.cs
 * author:    taoye
 * created:   2026/5/21
 * descrip:   EditorUtil.Build 嵌套类型定义
 ***************************************************************/

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Build
        {
            /// <summary>
            /// 打包方式，对应 Unity Build Profiles 的 Build 按钮三种触发形态。
            /// 三档互斥（Unity BuildOptions 层面 CleanBuildCache 与 BuildScriptsOnly 互斥）；与 DevelopmentBuild 正交，可叠加。
            /// </summary>
            public enum BuildMode
            {
                /// <summary>
                /// 直接点 Build 按钮：不附加任何额外 BuildOptions flag，走 Unity 默认增量构建。
                /// </summary>
                Build = 0,

                /// <summary>
                /// Build 按钮下拉「Clean Build…」：附加 BuildOptions.CleanBuildCache，强制全量重建。
                /// </summary>
                CleanBuild = 1,

                /// <summary>
                /// Build 按钮下拉「Force skip data build」：附加 BuildOptions.BuildScriptsOnly，仅重编脚本 DLL，跳过资源/Data 构建。
                /// </summary>
                ForceSkipDataBuild = 2,
            }
        }
    }
}
