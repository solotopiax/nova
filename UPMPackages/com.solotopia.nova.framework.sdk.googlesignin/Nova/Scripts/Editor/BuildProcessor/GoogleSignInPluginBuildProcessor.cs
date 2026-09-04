/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  GoogleSignInPluginBuildProcessor.cs
 * author:    yingzheng
 * created:   2026/6/25
 * descrip:   Google Sign-In build processor
 ***************************************************************/

using NovaFramework.Editor;
using NovaFramework.Runtime;
using UnityEditor.Build.Reporting;

namespace NovaFramework.SDK.GoogleSignIn.Editor
{
    public sealed class GoogleSignInPluginBuildProcessor : NovaSDKBuildProcessor
    {
        public override int PreprocessPriority => 620;

        public override int PostprocessPriority => 620;

        /// <summary>
        /// Google Sign-In 通过 Unity AndroidJavaClass 反射调用自定义 Java 桥接层。
        /// Android 开启 minify 时必须保留这些入口。
        /// </summary>
        private const string c_AndroidProguardRules =
            "-keep class com.solotopia.nova.googlesignin.** { *; }\n" +
            "-keepattributes Signature\n" +
            "-keepattributes *Annotation*\n" +
            "-dontwarn com.solotopia.nova.googlesignin.**";

        public override void OnPreprocessBuildOnAndroid(BuildReport report, NovaBuildContext context)
        {
            context.AddProguardRules("GoogleSignInPlugin", c_AndroidProguardRules);

            GoogleSignInPluginConfig config = GetSDKConfig<GoogleSignInPluginConfig>();
            if (config == null) return;

            if (string.IsNullOrEmpty(config.ClientId))
            {
                Log.Warning(LogTag.Editor, "[GoogleSignInPluginBuildProcessor] ClientId is empty; Android cannot request a Google ID Token.");
                return;
            }

            Log.Debug(LogTag.Editor, "[GoogleSignInPluginBuildProcessor] Android config validated.");
        }
    }
}
