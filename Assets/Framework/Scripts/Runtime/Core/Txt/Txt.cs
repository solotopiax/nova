/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Txt.cs 
 * author:    taoye
 * created:   2025/12/8 
 * descrip:   格式化文本工具：门面类，提供统一静态入口 
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 文本工具集：门面层（Facade）。
    /// 提供静态统一入口，内部委托给平台实现的 ITxtHelper（例如 Unity 平台的 TxtHelper）。
    /// </summary>
    public static class Txt
    {
        /// <summary>
        /// 文本工具帮助对象（平台实现），默认为 <see cref="TxtHelper"/>。
        /// 如果需要在运行时替换实现，可在未来提供替换方法（当前为 readonly 静态字段）。
        /// </summary>
        private static ITxtHelper s_TxtHelper = null;

        /// <summary>
        /// 设置框架格式化文本辅助器。
        /// </summary>
        /// <param name="helper">要设置的框架格式化文本辅助器。</param>
        public static void SetHelper(ITxtHelper helper)
        {
            s_TxtHelper = helper;
        }

        /// <summary>
        /// 使用单个类型参数对 <paramref name="format"/> 进行格式化并返回结果。
        /// </summary>
        /// <typeparam name="T">参数的类型。</typeparam>
        /// <param name="format">格式化字符串，不能为空。</param>
        /// <param name="arg">用于替换格式化字符串中的占位符的参数，可为 null（参数值可为 null）。</param>
        /// <returns>格式化后的字符串。</returns>
        public static string Format<T>(string format, T arg)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            if (s_TxtHelper == null)
            {
                return string.Format(format, arg);
            }

            return s_TxtHelper.Format<T>(format, arg);
        }

        /// <summary>
        /// 使用两个类型参数对 <paramref name="format"/> 进行格式化并返回结果。
        /// </summary>
        /// <typeparam name="T1">参数 1 的类型。</typeparam>
        /// <typeparam name="T2">参数 2 的类型。</typeparam>
        /// <param name="format">格式化字符串，不能为空。</param>
        /// <param name="arg1">参数 1 的值，可为 null（值可为 null）。</param>
        /// <param name="arg2">参数 2 的值，可为 null（值可为 null）。</param>
        /// <returns>格式化后的字符串。</returns>
        public static string Format<T1, T2>(string format, T1 arg1, T2 arg2)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            if (s_TxtHelper == null)
            {
                return string.Format(format, arg1, arg2);
            }

            return s_TxtHelper.Format<T1, T2>(format, arg1, arg2);
        }

        /// <summary>
        /// 使用三个类型参数对 <paramref name="format"/> 进行格式化并返回结果。
        /// </summary>
        /// <typeparam name="T1">参数 1 的类型。</typeparam>
        /// <typeparam name="T2">参数 2 的类型。</typeparam>
        /// <typeparam name="T3">参数 3 的类型。</typeparam>
        /// <param name="format">格式化字符串，不能为空。</param>
        /// <param name="arg1">参数 1 的值，可为 null。</param>
        /// <param name="arg2">参数 2 的值，可为 null。</param>
        /// <param name="arg3">参数 3 的值，可为 null。</param>
        /// <returns>格式化后的字符串。</returns>
        public static string Format<T1, T2, T3>(string format, T1 arg1, T2 arg2, T3 arg3)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            if (s_TxtHelper == null)
            {
                return string.Format(format, arg1, arg2, arg3);
            }

            return s_TxtHelper.Format<T1, T2, T3>(format, arg1, arg2, arg3);
        }

        /// <summary>
        /// 使用四个类型参数对 <paramref name="format"/> 进行格式化并返回结果。
        /// </summary>
        /// <typeparam name="T1">参数 1 的类型。</typeparam>
        /// <typeparam name="T2">参数 2 的类型。</typeparam>
        /// <typeparam name="T3">参数 3 的类型。</typeparam>
        /// <typeparam name="T4">参数 4 的类型。</typeparam>
        /// <param name="format">格式化字符串，不能为空。</param>
        /// <param name="arg1">参数 1 的值，可为 null。</param>
        /// <param name="arg2">参数 2 的值，可为 null。</param>
        /// <param name="arg3">参数 3 的值，可为 null。</param>
        /// <param name="arg4">参数 4 的值，可为 null。</param>
        /// <returns>格式化后的字符串。</returns>
        public static string Format<T1, T2, T3, T4>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            if (s_TxtHelper == null)
            {
                return string.Format(format, arg1, arg2, arg3, arg4);
            }

            return s_TxtHelper.Format<T1, T2, T3, T4>(format, arg1, arg2, arg3, arg4);
        }

        /// <summary>
        /// 使用五个类型参数对 <paramref name="format"/> 进行格式化并返回结果。
        /// </summary>
        /// <typeparam name="T1">参数 1 的类型。</typeparam>
        /// <typeparam name="T2">参数 2 的类型。</typeparam>
        /// <typeparam name="T3">参数 3 的类型。</typeparam>
        /// <typeparam name="T4">参数 4 的类型。</typeparam>
        /// <typeparam name="T5">参数 5 的类型。</typeparam>
        /// <param name="format">格式化字符串，不能为空。</param>
        /// <param name="arg1">参数 1 的值，可为 null。</param>
        /// <param name="arg2">参数 2 的值，可为 null。</param>
        /// <param name="arg3">参数 3 的值，可为 null。</param>
        /// <param name="arg4">参数 4 的值，可为 null。</param>
        /// <param name="arg5">参数 5 的值，可为 null。</param>
        /// <returns>格式化后的字符串。</returns>
        public static string Format<T1, T2, T3, T4, T5>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            if (s_TxtHelper == null)
            {
                return string.Format(format, arg1, arg2, arg3, arg4, arg5);
            }

            return s_TxtHelper.Format<T1, T2, T3, T4, T5>(format, arg1, arg2, arg3, arg4, arg5);
        }

        /// <summary>
        /// 使用六个类型参数对 <paramref name="format"/> 进行格式化并返回结果。
        /// </summary>
        /// <typeparam name="T1">参数 1 的类型。</typeparam>
        /// <typeparam name="T2">参数 2 的类型。</typeparam>
        /// <typeparam name="T3">参数 3 的类型。</typeparam>
        /// <typeparam name="T4">参数 4 的类型。</typeparam>
        /// <typeparam name="T5">参数 5 的类型。</typeparam>
        /// <typeparam name="T6">参数 6 的类型。</typeparam>
        /// <param name="format">格式化字符串，不能为空。</param>
        /// <param name="arg1">参数 1 的值，可为 null。</param>
        /// <param name="arg2">参数 2 的值，可为 null。</param>
        /// <param name="arg3">参数 3 的值，可为 null。</param>
        /// <param name="arg4">参数 4 的值，可为 null。</param>
        /// <param name="arg5">参数 5 的值，可为 null。</param>
        /// <param name="arg6">参数 6 的值，可为 null。</param>
        /// <returns>格式化后的字符串。</returns>
        public static string Format<T1, T2, T3, T4, T5, T6>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            if (s_TxtHelper == null)
            {
                return string.Format(format, arg1, arg2, arg3, arg4, arg5, arg6);
            }

            return s_TxtHelper.Format<T1, T2, T3, T4, T5, T6>(format, arg1, arg2, arg3, arg4, arg5, arg6);
        }

        /// <summary>
        /// 使用七个类型参数对 <paramref name="format"/> 进行格式化并返回结果。
        /// </summary>
        /// <typeparam name="T1">参数 1 的类型。</typeparam>
        /// <typeparam name="T2">参数 2 的类型。</typeparam>
        /// <typeparam name="T3">参数 3 的类型。</typeparam>
        /// <typeparam name="T4">参数 4 的类型。</typeparam>
        /// <typeparam name="T5">参数 5 的类型。</typeparam>
        /// <typeparam name="T6">参数 6 的类型。</typeparam>
        /// <typeparam name="T7">参数 7 的类型。</typeparam>
        /// <param name="format">格式化字符串，不能为空。</param>
        /// <param name="arg1">参数 1 的值，可为 null。</param>
        /// <param name="arg2">参数 2 的值，可为 null。</param>
        /// <param name="arg3">参数 3 的值，可为 null。</param>
        /// <param name="arg4">参数 4 的值，可为 null。</param>
        /// <param name="arg5">参数 5 的值，可为 null。</param>
        /// <param name="arg6">参数 6 的值，可为 null。</param>
        /// <param name="arg7">参数 7 的值，可为 null。</param>
        /// <returns>格式化后的字符串。</returns>
        public static string Format<T1, T2, T3, T4, T5, T6, T7>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            if (s_TxtHelper == null)
            {
                return string.Format(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
            }

            return s_TxtHelper.Format<T1, T2, T3, T4, T5, T6, T7>(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
        }

        /// <summary>
        /// 使用八个类型参数对 <paramref name="format"/> 进行格式化并返回结果。
        /// </summary>
        /// <typeparam name="T1">参数 1 的类型。</typeparam>
        /// <typeparam name="T2">参数 2 的类型。</typeparam>
        /// <typeparam name="T3">参数 3 的类型。</typeparam>
        /// <typeparam name="T4">参数 4 的类型。</typeparam>
        /// <typeparam name="T5">参数 5 的类型。</typeparam>
        /// <typeparam name="T6">参数 6 的类型。</typeparam>
        /// <typeparam name="T7">参数 7 的类型。</typeparam>
        /// <typeparam name="T8">参数 8 的类型。</typeparam>
        /// <param name="format">格式化字符串，不能为空。</param>
        /// <param name="arg1">参数 1 的值，可为 null。</param>
        /// <param name="arg2">参数 2 的值，可为 null。</param>
        /// <param name="arg3">参数 3 的值，可为 null。</param>
        /// <param name="arg4">参数 4 的值，可为 null。</param>
        /// <param name="arg5">参数 5 的值，可为 null。</param>
        /// <param name="arg6">参数 6 的值，可为 null。</param>
        /// <param name="arg7">参数 7 的值，可为 null。</param>
        /// <param name="arg8">参数 8 的值，可为 null。</param>
        /// <returns>格式化后的字符串。</returns>
        public static string Format<T1, T2, T3, T4, T5, T6, T7, T8>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            if (s_TxtHelper == null)
            {
                return string.Format(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
            }

            return s_TxtHelper.Format<T1, T2, T3, T4, T5, T6, T7, T8>(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        }

        /// <summary>
        /// 使用九个类型参数对 <paramref name="format"/> 进行格式化并返回结果。
        /// </summary>
        /// <typeparam name="T1">参数 1 的类型。</typeparam>
        /// <typeparam name="T2">参数 2 的类型。</typeparam>
        /// <typeparam name="T3">参数 3 的类型。</typeparam>
        /// <typeparam name="T4">参数 4 的类型。</typeparam>
        /// <typeparam name="T5">参数 5 的类型。</typeparam>
        /// <typeparam name="T6">参数 6 的类型。</typeparam>
        /// <typeparam name="T7">参数 7 的类型。</typeparam>
        /// <typeparam name="T8">参数 8 的类型。</typeparam>
        /// <typeparam name="T9">参数 9 的类型。</typeparam>
        /// <param name="format">格式化字符串，不能为空。</param>
        /// <param name="arg1">参数 1 的值，可为 null。</param>
        /// <param name="arg2">参数 2 的值，可为 null。</param>
        /// <param name="arg3">参数 3 的值，可为 null。</param>
        /// <param name="arg4">参数 4 的值，可为 null。</param>
        /// <param name="arg5">参数 5 的值，可为 null。</param>
        /// <param name="arg6">参数 6 的值，可为 null。</param>
        /// <param name="arg7">参数 7 的值，可为 null。</param>
        /// <param name="arg8">参数 8 的值，可为 null。</param>
        /// <param name="arg9">参数 9 的值，可为 null。</param>
        /// <returns>格式化后的字符串。</returns>
        public static string Format<T1, T2, T3, T4, T5, T6, T7, T8, T9>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            if (s_TxtHelper == null)
            {
                return string.Format(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
            }

            return s_TxtHelper.Format<T1, T2, T3, T4, T5, T6, T7, T8, T9>(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        }

        /// <summary>
        /// 使用十个类型参数对 <paramref name="format"/> 进行格式化并返回结果。
        /// </summary>
        /// <typeparam name="T1">参数 1 的类型。</typeparam>
        /// <typeparam name="T2">参数 2 的类型。</typeparam>
        /// <typeparam name="T3">参数 3 的类型。</typeparam>
        /// <typeparam name="T4">参数 4 的类型。</typeparam>
        /// <typeparam name="T5">参数 5 的类型。</typeparam>
        /// <typeparam name="T6">参数 6 的类型。</typeparam>
        /// <typeparam name="T7">参数 7 的类型。</typeparam>
        /// <typeparam name="T8">参数 8 的类型。</typeparam>
        /// <typeparam name="T9">参数 9 的类型。</typeparam>
        /// <typeparam name="T10">参数 10 的类型。</typeparam>
        /// <param name="format">格式化字符串，不能为空。</param>
        /// <param name="arg1">参数 1 的值，可为 null。</param>
        /// <param name="arg2">参数 2 的值，可为 null。</param>
        /// <param name="arg3">参数 3 的值，可为 null。</param>
        /// <param name="arg4">参数 4 的值，可为 null。</param>
        /// <param name="arg5">参数 5 的值，可为 null。</param>
        /// <param name="arg6">参数 6 的值，可为 null。</param>
        /// <param name="arg7">参数 7 的值，可为 null。</param>
        /// <param name="arg8">参数 8 的值，可为 null。</param>
        /// <param name="arg9">参数 9 的值，可为 null。</param>
        /// <param name="arg10">参数 10 的值，可为 null。</param>
        /// <returns>格式化后的字符串。</returns>
        public static string Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            if (s_TxtHelper == null)
            {
                return string.Format(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
            }

            return s_TxtHelper.Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
        }
        
        /// <summary>
        /// 使用十一个类型参数对 <paramref name="format"/> 进行格式化并返回结果。
        /// </summary>
        /// <typeparam name="T1">参数 1 的类型。</typeparam>
        /// <typeparam name="T2">参数 2 的类型。</typeparam>
        /// <typeparam name="T3">参数 3 的类型。</typeparam>
        /// <typeparam name="T4">参数 4 的类型。</typeparam>
        /// <typeparam name="T5">参数 5 的类型。</typeparam>
        /// <typeparam name="T6">参数 6 的类型。</typeparam>
        /// <typeparam name="T7">参数 7 的类型。</typeparam>
        /// <typeparam name="T8">参数 8 的类型。</typeparam>
        /// <typeparam name="T9">参数 9 的类型。</typeparam>
        /// <typeparam name="T10">参数 10 的类型。</typeparam>
        /// <typeparam name="T11">参数 11 的类型。</typeparam>
        /// <param name="format">格式化字符串，不能为空。</param>
        /// <param name="arg1">参数 1 的值，可为 null。</param>
        /// <param name="arg2">参数 2 的值，可为 null。</param>
        /// <param name="arg3">参数 3 的值，可为 null。</param>
        /// <param name="arg4">参数 4 的值，可为 null。</param>
        /// <param name="arg5">参数 5 的值，可为 null。</param>
        /// <param name="arg6">参数 6 的值，可为 null。</param>
        /// <param name="arg7">参数 7 的值，可为 null。</param>
        /// <param name="arg8">参数 8 的值，可为 null。</param>
        /// <param name="arg9">参数 9 的值，可为 null。</param>
        /// <param name="arg10">参数 10 的值，可为 null。</param>
        /// <param name="arg11">参数 11 的值，可为 null。</param>
        /// <returns>格式化后的字符串。</returns>
        public static string Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            if (s_TxtHelper == null)
            {
                return string.Format(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11);
            }

            return s_TxtHelper.Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11);
        }
        
        /// <summary>
        /// 使用十二个类型参数对 <paramref name="format"/> 进行格式化并返回结果。
        /// </summary>
        /// <typeparam name="T1">参数 1 的类型。</typeparam>
        /// <typeparam name="T2">参数 2 的类型。</typeparam>
        /// <typeparam name="T3">参数 3 的类型。</typeparam>
        /// <typeparam name="T4">参数 4 的类型。</typeparam>
        /// <typeparam name="T5">参数 5 的类型。</typeparam>
        /// <typeparam name="T6">参数 6 的类型。</typeparam>
        /// <typeparam name="T7">参数 7 的类型。</typeparam>
        /// <typeparam name="T8">参数 8 的类型。</typeparam>
        /// <typeparam name="T9">参数 9 的类型。</typeparam>
        /// <typeparam name="T10">参数 10 的类型。</typeparam>
        /// <typeparam name="T11">参数 11 的类型。</typeparam>
        /// <typeparam name="T12">参数 12 的类型。</typeparam>
        /// <param name="format">格式化字符串，不能为空。</param>
        /// <param name="arg1">参数 1 的值，可为 null。</param>
        /// <param name="arg2">参数 2 的值，可为 null。</param>
        /// <param name="arg3">参数 3 的值，可为 null。</param>
        /// <param name="arg4">参数 4 的值，可为 null。</param>
        /// <param name="arg5">参数 5 的值，可为 null。</param>
        /// <param name="arg6">参数 6 的值，可为 null。</param>
        /// <param name="arg7">参数 7 的值，可为 null。</param>
        /// <param name="arg8">参数 8 的值，可为 null。</param>
        /// <param name="arg9">参数 9 的值，可为 null。</param>
        /// <param name="arg10">参数 10 的值，可为 null。</param>
        /// <param name="arg11">参数 11 的值，可为 null。</param>
        /// <param name="arg12">参数 12 的值，可为 null。</param>
        /// <returns>格式化后的字符串。</returns>
        public static string Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            if (s_TxtHelper == null)
            {
                return string.Format(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12);
            }

            return s_TxtHelper.Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12);
        }
        
        /// <summary>
        /// 使用十三个类型参数对 <paramref name="format"/> 进行格式化并返回结果。
        /// </summary>
        /// <typeparam name="T1">参数 1 的类型。</typeparam>
        /// <typeparam name="T2">参数 2 的类型。</typeparam>
        /// <typeparam name="T3">参数 3 的类型。</typeparam>
        /// <typeparam name="T4">参数 4 的类型。</typeparam>
        /// <typeparam name="T5">参数 5 的类型。</typeparam>
        /// <typeparam name="T6">参数 6 的类型。</typeparam>
        /// <typeparam name="T7">参数 7 的类型。</typeparam>
        /// <typeparam name="T8">参数 8 的类型。</typeparam>
        /// <typeparam name="T9">参数 9 的类型。</typeparam>
        /// <typeparam name="T10">参数 10 的类型。</typeparam>
        /// <typeparam name="T11">参数 11 的类型。</typeparam>
        /// <typeparam name="T12">参数 12 的类型。</typeparam>
        /// <typeparam name="T13">参数 13 的类型。</typeparam>
        /// <param name="format">格式化字符串，不能为空。</param>
        /// <param name="arg1">参数 1 的值，可为 null。</param>
        /// <param name="arg2">参数 2 的值，可为 null。</param>
        /// <param name="arg3">参数 3 的值，可为 null。</param>
        /// <param name="arg4">参数 4 的值，可为 null。</param>
        /// <param name="arg5">参数 5 的值，可为 null。</param>
        /// <param name="arg6">参数 6 的值，可为 null。</param>
        /// <param name="arg7">参数 7 的值，可为 null。</param>
        /// <param name="arg8">参数 8 的值，可为 null。</param>
        /// <param name="arg9">参数 9 的值，可为 null。</param>
        /// <param name="arg10">参数 10 的值，可为 null。</param>
        /// <param name="arg11">参数 11 的值，可为 null。</param>
        /// <param name="arg12">参数 12 的值，可为 null。</param>
        /// <param name="arg13">参数 13 的值，可为 null。</param>
        /// <returns>格式化后的字符串。</returns>
        public static string Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            if (s_TxtHelper == null)
            {
                return string.Format(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13);
            }

            return s_TxtHelper.Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13);
        }
        
        /// <summary>
        /// 使用十四个类型参数对 <paramref name="format"/> 进行格式化并返回结果。
        /// </summary>
        /// <typeparam name="T1">参数 1 的类型。</typeparam>
        /// <typeparam name="T2">参数 2 的类型。</typeparam>
        /// <typeparam name="T3">参数 3 的类型。</typeparam>
        /// <typeparam name="T4">参数 4 的类型。</typeparam>
        /// <typeparam name="T5">参数 5 的类型。</typeparam>
        /// <typeparam name="T6">参数 6 的类型。</typeparam>
        /// <typeparam name="T7">参数 7 的类型。</typeparam>
        /// <typeparam name="T8">参数 8 的类型。</typeparam>
        /// <typeparam name="T9">参数 9 的类型。</typeparam>
        /// <typeparam name="T10">参数 10 的类型。</typeparam>
        /// <typeparam name="T11">参数 11 的类型。</typeparam>
        /// <typeparam name="T12">参数 12 的类型。</typeparam>
        /// <typeparam name="T13">参数 13 的类型。</typeparam>
        /// <typeparam name="T14">参数 14 的类型。</typeparam>
        /// <param name="format">格式化字符串，不能为空。</param>
        /// <param name="arg1">参数 1 的值，可为 null。</param>
        /// <param name="arg2">参数 2 的值，可为 null。</param>
        /// <param name="arg3">参数 3 的值，可为 null。</param>
        /// <param name="arg4">参数 4 的值，可为 null。</param>
        /// <param name="arg5">参数 5 的值，可为 null。</param>
        /// <param name="arg6">参数 6 的值，可为 null。</param>
        /// <param name="arg7">参数 7 的值，可为 null。</param>
        /// <param name="arg8">参数 8 的值，可为 null。</param>
        /// <param name="arg9">参数 9 的值，可为 null。</param>
        /// <param name="arg10">参数 10 的值，可为 null。</param>
        /// <param name="arg11">参数 11 的值，可为 null。</param>
        /// <param name="arg12">参数 12 的值，可为 null。</param>
        /// <param name="arg13">参数 13 的值，可为 null。</param>
        /// <param name="arg14">参数 14 的值，可为 null。</param>
        /// <returns>格式化后的字符串。</returns>
        public static string Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            if (s_TxtHelper == null)
            {
                return string.Format(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14);
            }

            return s_TxtHelper.Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14);
        }
        
        /// <summary>
        /// 使用十五个类型参数对 <paramref name="format"/> 进行格式化并返回结果。
        /// </summary>
        /// <typeparam name="T1">参数 1 的类型。</typeparam>
        /// <typeparam name="T2">参数 2 的类型。</typeparam>
        /// <typeparam name="T3">参数 3 的类型。</typeparam>
        /// <typeparam name="T4">参数 4 的类型。</typeparam>
        /// <typeparam name="T5">参数 5 的类型。</typeparam>
        /// <typeparam name="T6">参数 6 的类型。</typeparam>
        /// <typeparam name="T7">参数 7 的类型。</typeparam>
        /// <typeparam name="T8">参数 8 的类型。</typeparam>
        /// <typeparam name="T9">参数 9 的类型。</typeparam>
        /// <typeparam name="T10">参数 10 的类型。</typeparam>
        /// <typeparam name="T11">参数 11 的类型。</typeparam>
        /// <typeparam name="T12">参数 12 的类型。</typeparam>
        /// <typeparam name="T13">参数 13 的类型。</typeparam>
        /// <typeparam name="T14">参数 14 的类型。</typeparam>
        /// <typeparam name="T15">参数 15 的类型。</typeparam>
        /// <param name="format">格式化字符串，不能为空。</param>
        /// <param name="arg1">参数 1 的值，可为 null。</param>
        /// <param name="arg2">参数 2 的值，可为 null。</param>
        /// <param name="arg3">参数 3 的值，可为 null。</param>
        /// <param name="arg4">参数 4 的值，可为 null。</param>
        /// <param name="arg5">参数 5 的值，可为 null。</param>
        /// <param name="arg6">参数 6 的值，可为 null。</param>
        /// <param name="arg7">参数 7 的值，可为 null。</param>
        /// <param name="arg8">参数 8 的值，可为 null。</param>
        /// <param name="arg9">参数 9 的值，可为 null。</param>
        /// <param name="arg10">参数 10 的值，可为 null。</param>
        /// <param name="arg11">参数 11 的值，可为 null。</param>
        /// <param name="arg12">参数 12 的值，可为 null。</param>
        /// <param name="arg13">参数 13 的值，可为 null。</param>
        /// <param name="arg14">参数 14 的值，可为 null。</param>
        /// <param name="arg15">参数 15 的值，可为 null。</param>
        /// <returns>格式化后的字符串。</returns>
        public static string Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            if (s_TxtHelper == null)
            {
                return string.Format(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15);
            }

            return s_TxtHelper.Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15);
        }
        
        /// <summary>
        /// 使用十六个类型参数对 <paramref name="format"/> 进行格式化并返回结果。
        /// </summary>
        /// <typeparam name="T1">参数 1 的类型。</typeparam>
        /// <typeparam name="T2">参数 2 的类型。</typeparam>
        /// <typeparam name="T3">参数 3 的类型。</typeparam>
        /// <typeparam name="T4">参数 4 的类型。</typeparam>
        /// <typeparam name="T5">参数 5 的类型。</typeparam>
        /// <typeparam name="T6">参数 6 的类型。</typeparam>
        /// <typeparam name="T7">参数 7 的类型。</typeparam>
        /// <typeparam name="T8">参数 8 的类型。</typeparam>
        /// <typeparam name="T9">参数 9 的类型。</typeparam>
        /// <typeparam name="T10">参数 10 的类型。</typeparam>
        /// <typeparam name="T11">参数 11 的类型。</typeparam>
        /// <typeparam name="T12">参数 12 的类型。</typeparam>
        /// <typeparam name="T13">参数 13 的类型。</typeparam>
        /// <typeparam name="T14">参数 14 的类型。</typeparam>
        /// <typeparam name="T15">参数 15 的类型。</typeparam>
        /// <typeparam name="T16">参数 16 的类型。</typeparam>
        /// <param name="format">格式化字符串，不能为空。</param>
        /// <param name="arg1">参数 1 的值，可为 null。</param>
        /// <param name="arg2">参数 2 的值，可为 null。</param>
        /// <param name="arg3">参数 3 的值，可为 null。</param>
        /// <param name="arg4">参数 4 的值，可为 null。</param>
        /// <param name="arg5">参数 5 的值，可为 null。</param>
        /// <param name="arg6">参数 6 的值，可为 null。</param>
        /// <param name="arg7">参数 7 的值，可为 null。</param>
        /// <param name="arg8">参数 8 的值，可为 null。</param>
        /// <param name="arg9">参数 9 的值，可为 null。</param>
        /// <param name="arg10">参数 10 的值，可为 null。</param>
        /// <param name="arg11">参数 11 的值，可为 null。</param>
        /// <param name="arg12">参数 12 的值，可为 null。</param>
        /// <param name="arg13">参数 13 的值，可为 null。</param>
        /// <param name="arg14">参数 14 的值，可为 null。</param>
        /// <param name="arg15">参数 15 的值，可为 null。</param>
        /// <param name="arg16">参数 16 的值，可为 null。</param>
        /// <returns>格式化后的字符串。</returns>
        public static string Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(string format, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, T11 arg11, T12 arg12, T13 arg13, T14 arg14, T15 arg15, T16 arg16)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format), "格式异常。");
            }

            if (s_TxtHelper == null)
            {
                return string.Format(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16);
            }

            return s_TxtHelper.Format<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(format, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16);
        }
        
        /// <summary>
        /// 根据给定的关键字与可选前缀（按顺序）拼接并返回完整字符串，方法会对前缀数组与关键字进行合法性检查。
        /// 该方法会委托到底层实现并利用缓存以避免重复拼接带来的性能开销。
        /// </summary>
        /// <param name="keyWords">核心关键字字符串，不能为空。</param>
        /// <param name="prefixes">按顺序要拼接在关键字前的前缀数组，不能为空（但可以是空数组）。</param>
        /// <returns>拼接后的完整字符串（前缀 + keyWords）。</returns>
        public static string GetCachedFullString(string keyWords, params string[] prefixes)
        {
            if (string.IsNullOrEmpty(keyWords))
            {
                throw new ArgumentException("参数 keyWords 无效。", nameof(keyWords));
            }

            if (s_TxtHelper == null)
            {
                return string.Concat(prefixes) + keyWords;
            }

            return s_TxtHelper.GetCachedFullString(keyWords, prefixes);
        }

        /// <summary>
        /// 字符串间隙长度矫正。
        /// </summary>
        /// <param name="word">字符串内容。</param>
        /// <param name="length">长度。</param>
        /// <returns>矫正后的字符串。</returns>
        public static string FillGap(string word, int length = 15)
        {
            if (word == null)
            {
                throw new ArgumentNullException(nameof(word), "参数 word 无效。");
            }

            if (s_TxtHelper == null)
            {
                return word.Length >= length ? word.Substring(0, length) : word.PadRight(length);
            }

            return s_TxtHelper.FillGap(word, length);
        }
        
    }
}
