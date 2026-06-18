/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugStringExtensions.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    public static class DebugStringExtensions
    {
#if UNITY_EDITOR
        [JetBrains.Annotations.StringFormatMethod("formatString")]
#endif
        public static string Fmt(this string formatString, params object[] args)
        {
            return string.Format(formatString, args);
        }
    }
}
