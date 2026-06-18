/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.cs
 * author:    taoye
 * created:   2026/1/13
 * descrip:   编辑器实用工具集
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 编辑器实用工具集。
    /// </summary>
    public static partial class EditorUtil
    {
        /// <summary>
        /// 初始化工具。
        /// </summary>
        [InitializeOnLoad]
        public static class Initializer
        {
            static Initializer()
            {
                // 初始化 Txt 助手
                var txtHelper = Util.TypeCreator.Create<ITxtHelper>("NovaFramework.Runtime.TxtHelper");
                txtHelper.Initialize();
                Txt.SetHelper(txtHelper);

                // 初始化 Log 助手
                ILogHelper logHelper = Util.TypeCreator.Create<ILogHelper>("NovaFramework.Runtime.LogHelper");
                logHelper.Initialize();
                Log.SetHelper(logHelper);
            }
        }
    }
}
