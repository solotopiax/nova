/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Config.MemoryProtectionInstallation.cs
 * author:    Codex
 * created:   2026/10/21
 * descrip:   可选 Anti-Cheat Toolkit 的安装识别与购买引导元数据
 ***************************************************************/

using System;
using System.Reflection;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 只读识别已编译的 ACTk API；兼容官方 Assets 导入与内部 UPM，不检查购买凭证或阻断 Config。
    /// </summary>
    internal static class MemoryProtectionInstallation
    {
        internal const string PackageName = "com.codestage.anti.cheat.tookit";

        // 沿用 PlugPals 的商业库名称与购买地址模型，但不声明 Framework 的必需依赖。
        internal static readonly EditorUtil.PlugPals.RequiredLibraryGuide Guide = new()
        {
            displayName = "Anti-Cheat Toolkit",
            purchaseUrl = "https://marketplace.unity.com/packages/tools/utilities/anti-cheat-toolkit-202695",
        };

        private static bool? s_IsAvailable;

        /// <summary>
        /// 当前 Editor 是否已加载防护适配所需的 ACTk 类型；缓存随脚本域重载自然失效。
        /// </summary>
        internal static bool IsAvailable
        {
            get
            {
                s_IsAvailable ??= DetectAvailableTypes();
                return s_IsAvailable.Value;
            }
        }

        /// <summary>
        /// 用户导入插件后主动重新识别；仅刷新内存结果，不导入资产、不安装依赖、不改写配置。
        /// </summary>
        internal static void Refresh()
        {
            s_IsAvailable = DetectAvailableTypes();
        }

        /// <summary>
        /// 按 ACTk 的实际程序集与类型名识别已编译能力；加载异常按不可用处理，不影响配置编辑和导出。
        /// </summary>
        /// <returns>所需数值和校验类型均存在时返回 true。</returns>
        private static bool DetectAvailableTypes()
        {
            try
            {
                Type hashType = Type.GetType("CodeStage.AntiCheat.Utils.HashUtils, ACTk.Runtime", false);
                Type commonType = Type.GetType("CodeStage.AntiCheat.Common.ACTk, ACTk.Runtime", false);
                FieldInfo version = commonType?.GetField("Version", BindingFlags.Public | BindingFlags.Static);
                return version?.FieldType == typeof(string) && version.IsLiteral &&
                       version.GetRawConstantValue() is string && HasSupportedNumericApi(
                           Type.GetType("CodeStage.AntiCheat.ObscuredTypes.ObscuredInt, ACTk.Runtime", false),
                           typeof(int), hashType) &&
                       HasSupportedNumericApi(
                           Type.GetType("CodeStage.AntiCheat.ObscuredTypes.ObscuredLong, ACTk.Runtime", false),
                           typeof(long), hashType);
            }
            catch (Exception)
            {
                // 可选插件缺失、编译失败或程序集加载不完整时，配置窗口仍必须可用。
                return false;
            }
        }

        /// <summary>
        /// 检查数值适配实际使用的公开签名；旧版或不完整的导入也显示安装引导，不把类型存在误当成可用。
        /// </summary>
        /// <param name="obscuredType">待检测的 ACTk 受保护数值类型。</param>
        /// <param name="valueType">对应的 int 或 long 原始值类型。</param>
        /// <param name="hashType">ACTk 公开校验工具类型。</param>
        /// <returns>适配需要的公开成员均存在时返回 true。</returns>
        private static bool HasSupportedNumericApi(Type obscuredType, Type valueType, Type hashType)
        {
            return obscuredType != null && hashType != null &&
                   obscuredType.GetProperty("Hash")?.PropertyType == typeof(int) &&
                   obscuredType.GetMethod("op_Implicit", new[] { valueType }) != null &&
                   obscuredType.GetMethod("GetEncrypted", new[] { valueType.MakeByRefType() }) != null &&
                   obscuredType.GetMethod("Decrypt", new[] { valueType, valueType }) != null &&
                   obscuredType.GetMethod("IsDefault", Type.EmptyTypes)?.ReturnType == typeof(bool) &&
                   hashType.GetMethod("ValidateHash", new[] { valueType, typeof(int) }) != null;
        }
    }
}
