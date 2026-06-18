/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaExtensionForFoundation.String.cs
 * author:    taoye
 * created:   2025/12/2
 * descrip:   框架对C#的扩展方法-String字符串
 ***************************************************************/
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 框架对 C#的扩展方法-String 字符串。
    /// 提供首字母大写、富文本长度计算、标题化、去除多余空格、
    /// 时间字符串转浮点、获取汉字数量等实用方法。
    /// </summary>
    public static partial class NovaExtensionForFoundation
    {
        /// <summary>
        /// 将字符串的首字母大写，如果字符串为空则返回空字符串。
        /// </summary>
        /// <param name="s">要处理的字符串。</param>
        /// <returns>首字母大写后的字符串。</returns>
        public static string UppercaseFirst(this string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            return char.ToUpper(s[0]) + s.Substring(1);
        }

        /// <summary>
        /// 获取富文本字符串的实际长度（剔除了所有 HTML/Unity 标签）。
        /// 例如："<color=red>Hi</color>" 的长度为 2。
        /// </summary>
        /// <param name="richText">富文本字符串。</param>
        /// <returns>剔除标签后的字符长度。</returns>
        public static int RichTextLength(this string richText)
        {
            int richTextLength = 0;
            bool insideTag = false;

            richText = richText.Replace("<br>", "-");

            foreach (char character in richText)
            {
                if (character == '<')
                {
                    insideTag = true;
                    continue;
                }
                else if (character == '>')
                {
                    insideTag = false;
                }
                else if (!insideTag)
                {
                    richTextLength++;
                }
            }

            return richTextLength;
        }

        /// <summary>
        /// 将字符串中的每个单词首字母大写，其余字母小写。
        /// 例如："hello world" -> "Hello World"。
        /// </summary>
        /// <param name="title">要处理的字符串。</param>
        /// <returns>标题化后的字符串。</returns>
        public static string ToTitleCase(this string title)
        {
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(title.ToLower());
        }

        /// <summary>
        /// 移除字符串中的额外空格，将多个连续空格替换为一个空格。
        /// </summary>
        /// <param name="s">要处理的字符串。</param>
        /// <returns>去除多余空格后的字符串。</returns>
        public static string RemoveExtraSpaces(this string s)
        {
            return Regex.Replace(s, @"\s+", " ");
        }

        /// <summary>
        /// 将 hh:mm:ss:SSS 格式的时间字符串转换为以秒为单位的浮点数。
        /// 例如："01:30:15:250" -> 5415.25f。
        /// </summary>
        /// <param name="timeInStringNotation">时间字符串，格式必须为 hh:mm:ss:SSS。</param>
        /// <returns>对应的浮点秒数。</returns>
        /// <exception cref="Exception">当时间字符串格式不正确时抛出异常。</exception>
        public static float TimeStringToFloat(this string timeInStringNotation)
        {
            if (timeInStringNotation.Length != 12)
            {
                throw new ArgumentException("参数 timeInStringNotation 必须是一个 hh:mm:ss:SSS 格式的字符串。", nameof(timeInStringNotation));
            }

            string[] timeStringArray = timeInStringNotation.Split(new string[] { ":" }, StringSplitOptions.None);

            float startTime = 0f;
            float result;
            if (float.TryParse(timeStringArray[0], out result))
            {
                startTime += result * 3600f;
            }
            if (float.TryParse(timeStringArray[1], out result))
            {
                startTime += result * 60f;
            }
            if (float.TryParse(timeStringArray[2], out result))
            {
                startTime += result;
            }
            if (float.TryParse(timeStringArray[3], out result))
            {
                startTime += result / 1000f;
            }

            return startTime;
        }

        /// <summary>
        /// 获取字符串中的中文字符数量。
        /// </summary>
        /// <param name="s">待检测的字符串。</param>
        /// <returns>中文字符数量。</returns>
        public static int GetChineseNum(this string s)
        {
            int count = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] >= '\u4E00' && s[i] <= '\u9FFF')
                {
                    count++;
                }
            }
            return count;
        }
    }
}
