/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TxtHelper.cs
 * author:    taoye
 * created:   2025/12/8
 * descrip:   ITxtHelper 接口在 Unity 平台的具体实现
 *            提供字符串格式化及前缀拼接缓存功能
 ***************************************************************/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Unity 平台文本工具实现。
    /// 提供字符串格式化及前缀拼接缓存功能，支持线程静态缓存。
    /// </summary>
    internal sealed class TxtHelper : ITxtHelper
    {
        /// <summary>
        /// 线程静态缓存 StringBuilder 避免频繁分配。
        /// 每个线程都有独立的 StringBuilder，不需要锁（lock）就可以安全地复用。
        /// </summary>
        [ThreadStatic]
        private static StringBuilder s_CachedStringBuilder;
        
        /// <summary>
        /// 静态缓存 StringBuilder 初始容量。
        /// </summary>
        private const int c_StringBuilderCapacity = 1024;

        /// <summary>
        /// 前缀缓存，避免重复拼接（线程安全）。
        /// 格式：<前缀拼接后字符串, 拼接后的字符串>。
        /// </summary>
        private static readonly ConcurrentDictionary<string, string> s_PrefixCache = new();

        /// <summary>
        /// 完整字符串缓存（线程安全）。
        /// 格式：<完整前缀, <关键字, 完整字符串>>。
        /// </summary>
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> s_FullPrefixToKeyWordsMap = new();

        /// <summary>
        /// 初始化。
        /// </summary>
        public void Initialize()
        {
            
        }
        
        /// <summary>
        /// 格式化文本（支持可变参数）。
        /// </summary>
        /// <param name="format">格式化字符串，例如 "Hello {0}"。</param>
        /// <param name="args">可变参数列表，用于替换格式化字符串占位符。</param>
        /// <returns>格式化后的字符串。</returns>
        public string Format(string format, params object[] args)
        {
            format = format ?? throw new ArgumentNullException(nameof(format), "格式无效。");

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, args);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串。
        /// </summary>
        /// <typeparam name="T">字符串参数的类型。</typeparam>
        /// <param name="format">字符串格式。</param>
        /// <param name="arg">字符串参数。</param>
        /// <returns>格式化后的字符串。</returns>
        public string Format<T>(string format, T arg)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            // 先调用 ToString() 得到 string（引用类型），避免值类型泛型参数装箱为 object
            s_CachedStringBuilder.AppendFormat(format, arg?.ToString() ?? string.Empty);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串。
        /// </summary>
        /// <typeparam name="T1">字符串参数 1 的类型。</typeparam>
        /// <typeparam name="T2">字符串参数 2 的类型。</typeparam>
        /// <param name="format">字符串格式。</param>
        /// <param name="arg1">字符串参数 1。</param>
        /// <param name="arg2">字符串参数 2。</param>
        /// <returns>格式化后的字符串。</returns>
        public string Format<T1, T2>(string format, T1 arg1, T2 arg2)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串。
        /// </summary>
        /// <typeparam name="T1">字符串参数 1 的类型。</typeparam>
        /// <typeparam name="T2">字符串参数 2 的类型。</typeparam>
        /// <typeparam name="T3">字符串参数 3 的类型。</typeparam>
        /// <param name="format">字符串格式。</param>
        /// <param name="arg1">字符串参数 1。</param>
        /// <param name="arg2">字符串参数 2。</param>
        /// <param name="arg3">字符串参数 3。</param>
        /// <returns>格式化后的字符串。</returns>
        public string Format<T1, T2, T3>(string format, T1 arg1, T2 arg2, T3 arg3)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串。
        /// </summary>
        /// <typeparam name="T1">字符串参数 1 的类型。</typeparam>
        /// <typeparam name="T2">字符串参数 2 的类型。</typeparam>
        /// <typeparam name="T3">字符串参数 3 的类型。</typeparam>
        /// <typeparam name="T4">字符串参数 4 的类型。</typeparam>
        /// <param name="format">字符串格式。</param>
        /// <param name="arg1">字符串参数 1。</param>
        /// <param name="arg2">字符串参数 2。</param>
        /// <param name="arg3">字符串参数 3。</param>
        /// <param name="arg4">字符串参数 4。</param>
        /// <returns>格式化后的字符串。</returns>
        public string Format<T1, T2, T3, T4>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3, arg4);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串。
        /// </summary>
        /// <typeparam name="T1">字符串参数 1 的类型。</typeparam>
        /// <typeparam name="T2">字符串参数 2 的类型。</typeparam>
        /// <typeparam name="T3">字符串参数 3 的类型。</typeparam>
        /// <typeparam name="T4">字符串参数 4 的类型。</typeparam>
        /// <typeparam name="T5">字符串参数 5 的类型。</typeparam>
        /// <param name="format">字符串格式。</param>
        /// <param name="arg1">字符串参数 1。</param>
        /// <param name="arg2">字符串参数 2。</param>
        /// <param name="arg3">字符串参数 3。</param>
        /// <param name="arg4">字符串参数 4。</param>
        /// <param name="arg5">字符串参数 5。</param>
        /// <returns>格式化后的字符串。</returns>
        public string Format<T1, T2, T3, T4, T5>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3, arg4, arg5);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串。
        /// </summary>
        /// <typeparam name="T1">字符串参数 1 的类型。</typeparam>
        /// <typeparam name="T2">字符串参数 2 的类型。</typeparam>
        /// <typeparam name="T3">字符串参数 3 的类型。</typeparam>
        /// <typeparam name="T4">字符串参数 4 的类型。</typeparam>
        /// <typeparam name="T5">字符串参数 5 的类型。</typeparam>
        /// <typeparam name="T6">字符串参数 6 的类型。</typeparam>
        /// <param name="format">字符串格式。</param>
        /// <param name="arg1">字符串参数 1。</param>
        /// <param name="arg2">字符串参数 2。</param>
        /// <param name="arg3">字符串参数 3。</param>
        /// <param name="arg4">字符串参数 4。</param>
        /// <param name="arg5">字符串参数 5。</param>
        /// <param name="arg6">字符串参数 6。</param>
        /// <returns>格式化后的字符串。</returns>
        public string Format<T1, T2, T3, T4, T5, T6>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3, arg4, arg5, arg6);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串。
        /// </summary>
        /// <typeparam name="T1">字符串参数 1 的类型。</typeparam>
        /// <typeparam name="T2">字符串参数 2 的类型。</typeparam>
        /// <typeparam name="T3">字符串参数 3 的类型。</typeparam>
        /// <typeparam name="T4">字符串参数 4 的类型。</typeparam>
        /// <typeparam name="T5">字符串参数 5 的类型。</typeparam>
        /// <typeparam name="T6">字符串参数 6 的类型。</typeparam>
        /// <typeparam name="T7">字符串参数 7 的类型。</typeparam>
        /// <param name="format">字符串格式。</param>
        /// <param name="arg1">字符串参数 1。</param>
        /// <param name="arg2">字符串参数 2。</param>
        /// <param name="arg3">字符串参数 3。</param>
        /// <param name="arg4">字符串参数 4。</param>
        /// <param name="arg5">字符串参数 5。</param>
        /// <param name="arg6">字符串参数 6。</param>
        /// <param name="arg7">字符串参数 7。</param>
        /// <returns>格式化后的字符串。</returns>
        public string Format<T1, T2, T3, T4, T5, T6, T7>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串。
        /// </summary>
        /// <typeparam name="T1">字符串参数 1 的类型。</typeparam>
        /// <typeparam name="T2">字符串参数 2 的类型。</typeparam>
        /// <typeparam name="T3">字符串参数 3 的类型。</typeparam>
        /// <typeparam name="T4">字符串参数 4 的类型。</typeparam>
        /// <typeparam name="T5">字符串参数 5 的类型。</typeparam>
        /// <typeparam name="T6">字符串参数 6 的类型。</typeparam>
        /// <typeparam name="T7">字符串参数 7 的类型。</typeparam>
        /// <typeparam name="T8">字符串参数 8 的类型。</typeparam>
        /// <param name="format">字符串格式。</param>
        /// <param name="arg1">字符串参数 1。</param>
        /// <param name="arg2">字符串参数 2。</param>
        /// <param name="arg3">字符串参数 3。</param>
        /// <param name="arg4">字符串参数 4。</param>
        /// <param name="arg5">字符串参数 5。</param>
        /// <param name="arg6">字符串参数 6。</param>
        /// <param name="arg7">字符串参数 7。</param>
        /// <param name="arg8">字符串参数 8。</param>
        /// <returns>格式化后的字符串。</returns>
        public string Format<T1, T2, T3, T4, T5, T6, T7, T8>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串。
        /// </summary>
        /// <typeparam name="T1">字符串参数 1 的类型。</typeparam>
        /// <typeparam name="T2">字符串参数 2 的类型。</typeparam>
        /// <typeparam name="T3">字符串参数 3 的类型。</typeparam>
        /// <typeparam name="T4">字符串参数 4 的类型。</typeparam>
        /// <typeparam name="T5">字符串参数 5 的类型。</typeparam>
        /// <typeparam name="T6">字符串参数 6 的类型。</typeparam>
        /// <typeparam name="T7">字符串参数 7 的类型。</typeparam>
        /// <typeparam name="T8">字符串参数 8 的类型。</typeparam>
        /// <typeparam name="T9">字符串参数 9 的类型。</typeparam>
        /// <param name="format">字符串格式。</param>
        /// <param name="arg1">字符串参数 1。</param>
        /// <param name="arg2">字符串参数 2。</param>
        /// <param name="arg3">字符串参数 3。</param>
        /// <param name="arg4">字符串参数 4。</param>
        /// <param name="arg5">字符串参数 5。</param>
        /// <param name="arg6">字符串参数 6。</param>
        /// <param name="arg7">字符串参数 7。</param>
        /// <param name="arg8">字符串参数 8。</param>
        /// <param name="arg9">字符串参数 9。</param>
        /// <returns>格式化后的字符串。</returns>
        public string Format<T1, T2, T3, T4, T5, T6, T7, T8, T9>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串。
        /// </summary>
        /// <typeparam name="T1">字符串参数 1 的类型。</typeparam>
        /// <typeparam name="T2">字符串参数 2 的类型。</typeparam>
        /// <typeparam name="T3">字符串参数 3 的类型。</typeparam>
        /// <typeparam name="T4">字符串参数 4 的类型。</typeparam>
        /// <typeparam name="T5">字符串参数 5 的类型。</typeparam>
        /// <typeparam name="T6">字符串参数 6 的类型。</typeparam>
        /// <typeparam name="T7">字符串参数 7 的类型。</typeparam>
        /// <typeparam name="T8">字符串参数 8 的类型。</typeparam>
        /// <typeparam name="T9">字符串参数 9 的类型。</typeparam>
        /// <typeparam name="T10">字符串参数 10 的类型。</typeparam>
        /// <param name="format">字符串格式。</param>
        /// <param name="arg1">字符串参数 1。</param>
        /// <param name="arg2">字符串参数 2。</param>
        /// <param name="arg3">字符串参数 3。</param>
        /// <param name="arg4">字符串参数 4。</param>
        /// <param name="arg5">字符串参数 5。</param>
        /// <param name="arg6">字符串参数 6。</param>
        /// <param name="arg7">字符串参数 7。</param>
        /// <param name="arg8">字符串参数 8。</param>
        /// <param name="arg9">字符串参数 9。</param>
        /// <param name="arg10">字符串参数 10。</param>
        /// <returns>格式化后的字符串。</returns>
        public string Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串。
        /// </summary>
        /// <typeparam name="T1">字符串参数 1 的类型。</typeparam>
        /// <typeparam name="T2">字符串参数 2 的类型。</typeparam>
        /// <typeparam name="T3">字符串参数 3 的类型。</typeparam>
        /// <typeparam name="T4">字符串参数 4 的类型。</typeparam>
        /// <typeparam name="T5">字符串参数 5 的类型。</typeparam>
        /// <typeparam name="T6">字符串参数 6 的类型。</typeparam>
        /// <typeparam name="T7">字符串参数 7 的类型。</typeparam>
        /// <typeparam name="T8">字符串参数 8 的类型。</typeparam>
        /// <typeparam name="T9">字符串参数 9 的类型。</typeparam>
        /// <typeparam name="T10">字符串参数 10 的类型。</typeparam>
        /// <typeparam name="T11">字符串参数 11 的类型。</typeparam>
        /// <param name="format">字符串格式。</param>
        /// <param name="arg1">字符串参数 1。</param>
        /// <param name="arg2">字符串参数 2。</param>
        /// <param name="arg3">字符串参数 3。</param>
        /// <param name="arg4">字符串参数 4。</param>
        /// <param name="arg5">字符串参数 5。</param>
        /// <param name="arg6">字符串参数 6。</param>
        /// <param name="arg7">字符串参数 7。</param>
        /// <param name="arg8">字符串参数 8。</param>
        /// <param name="arg9">字符串参数 9。</param>
        /// <param name="arg10">字符串参数 10。</param>
        /// <param name="arg11">字符串参数 11。</param>
        /// <returns>格式化后的字符串。</returns>
        public string Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串。
        /// </summary>
        /// <typeparam name="T1">字符串参数 1 的类型。</typeparam>
        /// <typeparam name="T2">字符串参数 2 的类型。</typeparam>
        /// <typeparam name="T3">字符串参数 3 的类型。</typeparam>
        /// <typeparam name="T4">字符串参数 4 的类型。</typeparam>
        /// <typeparam name="T5">字符串参数 5 的类型。</typeparam>
        /// <typeparam name="T6">字符串参数 6 的类型。</typeparam>
        /// <typeparam name="T7">字符串参数 7 的类型。</typeparam>
        /// <typeparam name="T8">字符串参数 8 的类型。</typeparam>
        /// <typeparam name="T9">字符串参数 9 的类型。</typeparam>
        /// <typeparam name="T10">字符串参数 10 的类型。</typeparam>
        /// <typeparam name="T11">字符串参数 11 的类型。</typeparam>
        /// <typeparam name="T12">字符串参数 12 的类型。</typeparam>
        /// <param name="format">字符串格式。</param>
        /// <param name="arg1">字符串参数 1。</param>
        /// <param name="arg2">字符串参数 2。</param>
        /// <param name="arg3">字符串参数 3。</param>
        /// <param name="arg4">字符串参数 4。</param>
        /// <param name="arg5">字符串参数 5。</param>
        /// <param name="arg6">字符串参数 6。</param>
        /// <param name="arg7">字符串参数 7。</param>
        /// <param name="arg8">字符串参数 8。</param>
        /// <param name="arg9">字符串参数 9。</param>
        /// <param name="arg10">字符串参数 10。</param>
        /// <param name="arg11">字符串参数 11。</param>
        /// <param name="arg12">字符串参数 12。</param>
        /// <returns>格式化后的字符串。</returns>
        public string Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串。
        /// </summary>
        /// <typeparam name="T1">字符串参数 1 的类型。</typeparam>
        /// <typeparam name="T2">字符串参数 2 的类型。</typeparam>
        /// <typeparam name="T3">字符串参数 3 的类型。</typeparam>
        /// <typeparam name="T4">字符串参数 4 的类型。</typeparam>
        /// <typeparam name="T5">字符串参数 5 的类型。</typeparam>
        /// <typeparam name="T6">字符串参数 6 的类型。</typeparam>
        /// <typeparam name="T7">字符串参数 7 的类型。</typeparam>
        /// <typeparam name="T8">字符串参数 8 的类型。</typeparam>
        /// <typeparam name="T9">字符串参数 9 的类型。</typeparam>
        /// <typeparam name="T10">字符串参数 10 的类型。</typeparam>
        /// <typeparam name="T11">字符串参数 11 的类型。</typeparam>
        /// <typeparam name="T12">字符串参数 12 的类型。</typeparam>
        /// <typeparam name="T13">字符串参数 13 的类型。</typeparam>
        /// <param name="format">字符串格式。</param>
        /// <param name="arg1">字符串参数 1。</param>
        /// <param name="arg2">字符串参数 2。</param>
        /// <param name="arg3">字符串参数 3。</param>
        /// <param name="arg4">字符串参数 4。</param>
        /// <param name="arg5">字符串参数 5。</param>
        /// <param name="arg6">字符串参数 6。</param>
        /// <param name="arg7">字符串参数 7。</param>
        /// <param name="arg8">字符串参数 8。</param>
        /// <param name="arg9">字符串参数 9。</param>
        /// <param name="arg10">字符串参数 10。</param>
        /// <param name="arg11">字符串参数 11。</param>
        /// <param name="arg12">字符串参数 12。</param>
        /// <param name="arg13">字符串参数 13。</param>
        /// <returns>格式化后的字符串。</returns>
        public string Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串。
        /// </summary>
        /// <typeparam name="T1">字符串参数 1 的类型。</typeparam>
        /// <typeparam name="T2">字符串参数 2 的类型。</typeparam>
        /// <typeparam name="T3">字符串参数 3 的类型。</typeparam>
        /// <typeparam name="T4">字符串参数 4 的类型。</typeparam>
        /// <typeparam name="T5">字符串参数 5 的类型。</typeparam>
        /// <typeparam name="T6">字符串参数 6 的类型。</typeparam>
        /// <typeparam name="T7">字符串参数 7 的类型。</typeparam>
        /// <typeparam name="T8">字符串参数 8 的类型。</typeparam>
        /// <typeparam name="T9">字符串参数 9 的类型。</typeparam>
        /// <typeparam name="T10">字符串参数 10 的类型。</typeparam>
        /// <typeparam name="T11">字符串参数 11 的类型。</typeparam>
        /// <typeparam name="T12">字符串参数 12 的类型。</typeparam>
        /// <typeparam name="T13">字符串参数 13 的类型。</typeparam>
        /// <typeparam name="T14">字符串参数 14 的类型。</typeparam>
        /// <param name="format">字符串格式。</param>
        /// <param name="arg1">字符串参数 1。</param>
        /// <param name="arg2">字符串参数 2。</param>
        /// <param name="arg3">字符串参数 3。</param>
        /// <param name="arg4">字符串参数 4。</param>
        /// <param name="arg5">字符串参数 5。</param>
        /// <param name="arg6">字符串参数 6。</param>
        /// <param name="arg7">字符串参数 7。</param>
        /// <param name="arg8">字符串参数 8。</param>
        /// <param name="arg9">字符串参数 9。</param>
        /// <param name="arg10">字符串参数 10。</param>
        /// <param name="arg11">字符串参数 11。</param>
        /// <param name="arg12">字符串参数 12。</param>
        /// <param name="arg13">字符串参数 13。</param>
        /// <param name="arg14">字符串参数 14。</param>
        /// <returns>格式化后的字符串。</returns>
        public string Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串。
        /// </summary>
        /// <typeparam name="T1">字符串参数 1 的类型。</typeparam>
        /// <typeparam name="T2">字符串参数 2 的类型。</typeparam>
        /// <typeparam name="T3">字符串参数 3 的类型。</typeparam>
        /// <typeparam name="T4">字符串参数 4 的类型。</typeparam>
        /// <typeparam name="T5">字符串参数 5 的类型。</typeparam>
        /// <typeparam name="T6">字符串参数 6 的类型。</typeparam>
        /// <typeparam name="T7">字符串参数 7 的类型。</typeparam>
        /// <typeparam name="T8">字符串参数 8 的类型。</typeparam>
        /// <typeparam name="T9">字符串参数 9 的类型。</typeparam>
        /// <typeparam name="T10">字符串参数 10 的类型。</typeparam>
        /// <typeparam name="T11">字符串参数 11 的类型。</typeparam>
        /// <typeparam name="T12">字符串参数 12 的类型。</typeparam>
        /// <typeparam name="T13">字符串参数 13 的类型。</typeparam>
        /// <typeparam name="T14">字符串参数 14 的类型。</typeparam>
        /// <typeparam name="T15">字符串参数 15 的类型。</typeparam>
        /// <param name="format">字符串格式。</param>
        /// <param name="arg1">字符串参数 1。</param>
        /// <param name="arg2">字符串参数 2。</param>
        /// <param name="arg3">字符串参数 3。</param>
        /// <param name="arg4">字符串参数 4。</param>
        /// <param name="arg5">字符串参数 5。</param>
        /// <param name="arg6">字符串参数 6。</param>
        /// <param name="arg7">字符串参数 7。</param>
        /// <param name="arg8">字符串参数 8。</param>
        /// <param name="arg9">字符串参数 9。</param>
        /// <param name="arg10">字符串参数 10。</param>
        /// <param name="arg11">字符串参数 11。</param>
        /// <param name="arg12">字符串参数 12。</param>
        /// <param name="arg13">字符串参数 13。</param>
        /// <param name="arg14">字符串参数 14。</param>
        /// <param name="arg15">字符串参数 15。</param>
        /// <returns>格式化后的字符串。</returns>
        public string Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 获取格式化字符串。
        /// </summary>
        /// <typeparam name="T1">字符串参数 1 的类型。</typeparam>
        /// <typeparam name="T2">字符串参数 2 的类型。</typeparam>
        /// <typeparam name="T3">字符串参数 3 的类型。</typeparam>
        /// <typeparam name="T4">字符串参数 4 的类型。</typeparam>
        /// <typeparam name="T5">字符串参数 5 的类型。</typeparam>
        /// <typeparam name="T6">字符串参数 6 的类型。</typeparam>
        /// <typeparam name="T7">字符串参数 7 的类型。</typeparam>
        /// <typeparam name="T8">字符串参数 8 的类型。</typeparam>
        /// <typeparam name="T9">字符串参数 9 的类型。</typeparam>
        /// <typeparam name="T10">字符串参数 10 的类型。</typeparam>
        /// <typeparam name="T11">字符串参数 11 的类型。</typeparam>
        /// <typeparam name="T12">字符串参数 12 的类型。</typeparam>
        /// <typeparam name="T13">字符串参数 13 的类型。</typeparam>
        /// <typeparam name="T14">字符串参数 14 的类型。</typeparam>
        /// <typeparam name="T15">字符串参数 15 的类型。</typeparam>
        /// <typeparam name="T16">字符串参数 16 的类型。</typeparam>
        /// <param name="format">字符串格式。</param>
        /// <param name="arg1">字符串参数 1。</param>
        /// <param name="arg2">字符串参数 2。</param>
        /// <param name="arg3">字符串参数 3。</param>
        /// <param name="arg4">字符串参数 4。</param>
        /// <param name="arg5">字符串参数 5。</param>
        /// <param name="arg6">字符串参数 6。</param>
        /// <param name="arg7">字符串参数 7。</param>
        /// <param name="arg8">字符串参数 8。</param>
        /// <param name="arg9">字符串参数 9。</param>
        /// <param name="arg10">字符串参数 10。</param>
        /// <param name="arg11">字符串参数 11。</param>
        /// <param name="arg12">字符串参数 12。</param>
        /// <param name="arg13">字符串参数 13。</param>
        /// <param name="arg14">字符串参数 14。</param>
        /// <param name="arg15">字符串参数 15。</param>
        /// <param name="arg16">字符串参数 16。</param>
        /// <returns>格式化后的字符串。</returns>
        public string Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15, T16 arg16)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            CheckCachedStringBuilder();
            s_CachedStringBuilder.Length = 0;
            s_CachedStringBuilder.AppendFormat(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16);
            return s_CachedStringBuilder.ToString();
        }

        /// <summary>
        /// 清除所有静态前缀缓存和全字符串缓存。
        /// 适用于 Domain Reload 后或需要强制重置缓存的场景。
        /// </summary>
        public static void ClearCache()
        {
            s_PrefixCache.Clear();
            s_FullPrefixToKeyWordsMap.Clear();
        }

        /// <summary>
        /// Domain Reload 时由 Unity 自动调用，重置所有静态缓存，防止跨域数据污染。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ClearCache();
        }

        /// <summary>
        /// 检查线程静态 StringBuilder 是否初始化。
        /// 若未初始化则创建一个具有初始容量的实例（一般为 1024）。
        /// </summary>
        private static void CheckCachedStringBuilder()
        {
            if (s_CachedStringBuilder == null)
            {
                s_CachedStringBuilder = new StringBuilder(c_StringBuilderCapacity);
            }
        }
        
        /// <summary>
        /// 获取完整字符串（可含多个前缀）。
        /// 使用缓存避免重复拼接，提高性能。
        /// </summary>
        /// <param name="keyWords">核心关键字，用于生成最终字符串。</param>
        /// <param name="prefixes">前缀数组，将依次拼接在关键字前。</param>
        /// <returns>完整拼接后的字符串。</returns>
        public string GetCachedFullString(string keyWords, params string[] prefixes)
        {
            string prefixKey = string.Concat(prefixes);
            string fullPrefix = s_PrefixCache.GetOrAdd(prefixKey, k => k);

            var keyToFullMap = s_FullPrefixToKeyWordsMap.GetOrAdd(fullPrefix, _ => new ConcurrentDictionary<string, string>());
            return keyToFullMap.GetOrAdd(keyWords, k => $"{fullPrefix}{k}");
        }
        
        /// <summary>
        /// 字符串间隙长度矫正。
        /// </summary>
        /// <param name="word">字符串内容。</param>
        /// <param name="length">长度。</param>
        /// <returns>矫正后的字符串。</returns>
        public string FillGap(string word, int length = 15)
        {
            if (word.Length >= length)
            {
                return word.Substring(0, length);
            }

            return word.PadRight(length);
        }
    }
}
