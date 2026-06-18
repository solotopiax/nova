using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MoreMountains.FeedbacksForThirdParty
{
#if UNITY_EDITOR
    /// <summary>
    /// Automatically adds required scripting symbols after compilation.
    /// </summary>
    [InitializeOnLoad]
    public static class NiceVibrationsDefineSymbols
    {
        /// <summary>
        /// Symbols to add
        /// </summary>
        public static readonly string[] Symbols = new string[]
        {
            "MOREMOUNTAINS_NICEVIBRATIONS_INSTALLED"
        };

        static NiceVibrationsDefineSymbols()
        {
            // 取得当前 build target
            BuildTarget activeTarget = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup targetGroup = BuildPipeline.GetBuildTargetGroup(activeTarget);

            // 取得当前已有的 define 字符串
            string definesString = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);

            // 拆分成 List
            List<string> definesList = definesString.Split(';').ToList();

            // 添加缺失的 define
            foreach (var symbol in Symbols)
            {
                if (!definesList.Contains(symbol))
                {
                    definesList.Add(symbol);
                }
            }

            // 写回
            string newDefines = string.Join(";", definesList);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, newDefines);
        }
    }
#endif
}