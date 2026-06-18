/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.BundleBuilder.Methods.cs
 * author:    taoye
 * created:   2026/5/19
 * descrip:   EditorUtil.BundleBuilder 内部辅助方法
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using YooAsset;
using YooAsset.Editor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class BundleBuilder
        {
            /// <summary>
            /// 解析最终类名：参数为空时回退到默认实现的 FullName。
            /// </summary>
            /// <param name="className">外部传入的全类型名。</param>
            /// <param name="fallback">为空时使用的默认类型。</param>
            /// <returns>解析后的全类型名。</returns>
            private static string ResolveClassName(string className, Type fallback)
            {
                return string.IsNullOrEmpty(className) ? fallback.FullName : className;
            }

            /// <summary>
            /// 按全类型名实例化指定接口实现；找不到时记录 Warning 并返回 null。
            /// </summary>
            /// <typeparam name="T">服务接口类型。</typeparam>
            /// <param name="className">实现类的全类型名。</param>
            /// <returns>实例或 null。</returns>
            private static T CreateInstanceOrNull<T>(string className) where T : class
            {
                if (string.IsNullOrEmpty(className)) return null;
                System.Collections.Generic.List<Type> classTypes = EditorAssemblyUtility.GetAssignableTypes(typeof(T));
                Type classType = classTypes.Find(x => x.FullName.Equals(className));
                if (classType == null)
                {
                    Log.Warning(LogTag.Editor, "{0} 未找到 {1} 实现：{2}", c_LogPrefix, typeof(T).Name, className);
                    return null;
                }
                return (T)Activator.CreateInstance(classType);
            }

            /// <summary>
            /// 解析包裹内置着色器资源包名（与 ScriptableBuildPipelineViewer.GetBuiltinShaderBundleName 行为一致）。
            /// </summary>
            /// <param name="packageName">包裹名。</param>
            /// <returns>内置着色器资源包名。</returns>
            private static string ResolveBuiltinShaderBundleName(string packageName)
            {
                bool uniqueBundleName = BundleCollectorSettingData.Setting.UniqueBundleName;
                BundlePackRuleResult packRuleResult = DefaultBundlePackRule.CreateShadersPackRuleResult();
                return packRuleResult.GetBundleName(packageName, uniqueBundleName);
            }
        }
    }
}
