/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  GoogleSignInPluginBuildProcessor.cs
 * author:    Codex
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

        public override void OnPreprocessBuildOnAndroid(BuildReport report, NovaBuildContext context)
        {
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
