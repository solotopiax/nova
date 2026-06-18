/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.AndroidResolver.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   Android 依赖解析工具 —— 公开接口
 ***************************************************************/

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// Android 依赖解析工具，封装对 EDM4U（External Dependency Manager for Unity）
        /// PlayServicesResolver 的反射调用。
        /// 由于 EDM4U 以裸 DLL 形式存在（无 asmdef），所有调用通过运行时反射完成，
        /// 避免在框架 asmdef 中硬依赖 Google.JarResolver.dll。
        /// </summary>
        public static partial class AndroidResolver
        {
            /// <summary>
            /// 触发一次同步 Android 依赖解析（等价于 EDM4U 菜单 Assets → External Dependency Manager → Android → Force Resolve）。
            /// 内部通过反射调用 GooglePlayServices.PlayServicesResolver.ResolveSync(true)，
            /// 强制重建 Assets/GeneratedLocalRepo/** 目录。
            /// 找不到 EDM4U 类型/方法或反射调用抛异常时，本方法直接抛 InvalidOperationException，
            /// 由调用方（Pipify Runner）捕获并标记 Step 失败；禁止静默跳过，避免下游 BuildPlayer
            /// 在 GeneratedLocalRepo 缺失时才崩。
            /// </summary>
            public static void Resolve()
            {
                ExecuteResolveSync();
            }
        }
    }
}
