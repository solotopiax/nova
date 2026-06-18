/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaExtensionForUnity.Animator.cs
 * author:    taoye
 * created:   2025/12/2
 * descrip:   框架对Unity的扩展方法-Animator
 *            提供Animator参数检测、更新以及安全更新的辅助方法
 ***************************************************************/

using System.Collections.Generic;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 框架对 Unity Animator 的扩展方法。
    /// </summary>
    public static partial class NovaExtensionForUnity
    {
        /// <summary>
        /// 检查 Animator 中是否包含指定名称和类型的参数。
        /// </summary>
        /// <param name="animator">Animator 对象。</param>
        /// <param name="name">参数名称。</param>
        /// <param name="type">参数类型。</param>
        /// <returns>如果存在返回 true，否则返回 false。</returns>
        public static bool HasParameterOfType(this Animator animator, string name, AnimatorControllerParameterType type)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (var param in animator.parameters)
            {
                if (param.type == type && param.name == name)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 向整数参数列表中添加 Animator 参数的 Hash 值，如果该参数存在。
        /// </summary>
        /// <param name="animator">Animator 对象。</param>
        /// <param name="parameterName">参数名称。</param>
        /// <param name="parameter">输出参数 Hash 值。</param>
        /// <param name="type">参数类型。</param>
        /// <param name="parameterList">HashSet 列表，用于存储有效参数 Hash 值。</param>
        public static void AddAnimatorParameterIfExists(this Animator animator, string parameterName, out int parameter, AnimatorControllerParameterType type, HashSet<int> parameterList)
        {
            if (string.IsNullOrEmpty(parameterName))
            {
                parameter = -1;
                return;
            }

            parameter = Animator.StringToHash(parameterName);
            if (animator.HasParameterOfType(parameterName, type))
            {
                parameterList.Add(parameter);
            }
        }

        /// <summary>
        /// 向字符串参数列表中添加 Animator 参数名称，如果该参数存在。
        /// </summary>
        /// <param name="animator">Animator 对象。</param>
        /// <param name="parameterName">参数名称。</param>
        /// <param name="type">参数类型。</param>
        /// <param name="parameterList">HashSet 列表，用于存储有效参数名称。</param>
        public static void AddAnimatorParameterIfExists(this Animator animator, string parameterName, AnimatorControllerParameterType type, HashSet<string> parameterList)
        {
            if (animator.HasParameterOfType(parameterName, type))
            {
                parameterList.Add(parameterName);
            }
        }

        /// <summary>
        /// 设置 Animator 的 Boolean 参数值。
        /// </summary>
        /// <param name="animator">Animator 对象。</param>
        /// <param name="parameterName">参数名称。</param>
        /// <param name="value">Boolean 值。</param>
        public static void UpdateAnimatorBool(this Animator animator, string parameterName, bool value)
        {
            animator.SetBool(parameterName, value);
        }

        /// <summary>
        /// 设置 Animator 的 Integer 参数值。
        /// </summary>
        /// <param name="animator">Animator 对象。</param>
        /// <param name="parameterName">参数名称。</param>
        /// <param name="value">Integer 值。</param>
        public static void UpdateAnimatorInteger(this Animator animator, string parameterName, int value)
        {
            animator.SetInteger(parameterName, value);
        }

        /// <summary>
        /// 设置 Animator 的 Float 参数值。
        /// </summary>
        /// <param name="animator">Animator 对象。</param>
        /// <param name="parameterName">参数名称。</param>
        /// <param name="value">Float 值。</param>
        /// <param name="performSanityCheck">是否执行安全检查（未使用参数，可保留用于统一接口）。</param>
        public static void UpdateAnimatorFloat(this Animator animator, string parameterName, float value, bool performSanityCheck = true)
        {
            animator.SetFloat(parameterName, value);
        }

        /// <summary>
        /// 使用参数 Hash 安全更新 Boolean 值。
        /// </summary>
        /// <param name="animator">Animator 对象。</param>
        /// <param name="parameter">参数 Hash 值。</param>
        /// <param name="value">Boolean 值。</param>
        /// <param name="parameterList">有效参数 Hash 列表。</param>
        /// <param name="performSanityCheck">是否进行安全检查。</param>
        /// <returns>是否成功更新。</returns>
        public static bool UpdateAnimatorBool(this Animator animator, int parameter, bool value, HashSet<int> parameterList, bool performSanityCheck = true)
        {
            if (performSanityCheck && !parameterList.Contains(parameter)) return false;
            animator.SetBool(parameter, value);
            return true;
        }

        /// <summary>
        /// 使用参数 Hash 安全触发 Trigger。
        /// </summary>
        /// <param name="animator">Animator 对象。</param>
        /// <param name="parameter">参数 Hash 值。</param>
        /// <param name="parameterList">有效参数 Hash 列表。</param>
        /// <param name="performSanityCheck">是否进行安全检查。</param>
        /// <returns>是否成功触发。</returns>
        public static bool UpdateAnimatorTrigger(this Animator animator, int parameter, HashSet<int> parameterList, bool performSanityCheck = true)
        {
            if (performSanityCheck && !parameterList.Contains(parameter)) return false;
            animator.SetTrigger(parameter);
            return true;
        }

        /// <summary>
        /// 使用参数 Hash 安全更新 Float 值。
        /// </summary>
        /// <param name="animator">Animator 对象。</param>
        /// <param name="parameter">参数 Hash 值。</param>
        /// <param name="value">Float 值。</param>
        /// <param name="parameterList">有效参数 Hash 列表。</param>
        /// <param name="performSanityCheck">是否进行安全检查。</param>
        /// <returns>是否成功更新。</returns>
        public static bool UpdateAnimatorFloat(this Animator animator, int parameter, float value, HashSet<int> parameterList, bool performSanityCheck = true)
        {
            if (performSanityCheck && !parameterList.Contains(parameter)) return false;
            animator.SetFloat(parameter, value);
            return true;
        }

        /// <summary>
        /// 使用参数 Hash 安全更新 Integer 值。
        /// </summary>
        /// <param name="animator">Animator 对象。</param>
        /// <param name="parameter">参数 Hash 值。</param>
        /// <param name="value">Integer 值。</param>
        /// <param name="parameterList">有效参数 Hash 列表。</param>
        /// <param name="performSanityCheck">是否进行安全检查。</param>
        /// <returns>是否成功更新。</returns>
        public static bool UpdateAnimatorInteger(this Animator animator, int parameter, int value, HashSet<int> parameterList, bool performSanityCheck = true)
        {
            if (performSanityCheck && !parameterList.Contains(parameter)) return false;
            animator.SetInteger(parameter, value);
            return true;
        }

        /// <summary>
        /// 使用参数名称安全更新 Boolean 值。
        /// </summary>
        /// <param name="animator">Animator 对象。</param>
        /// <param name="parameterName">参数名称。</param>
        /// <param name="value">Boolean 值。</param>
        /// <param name="parameterList">有效参数名称列表。</param>
        /// <param name="performSanityCheck">是否进行安全检查。</param>
        public static void UpdateAnimatorBool(this Animator animator, string parameterName, bool value, HashSet<string> parameterList, bool performSanityCheck = true)
        {
            if (performSanityCheck && !parameterList.Contains(parameterName)) return;
            animator.SetBool(parameterName, value);
        }

        /// <summary>
        /// 使用参数名称安全触发 Trigger。
        /// </summary>
        /// <param name="animator">Animator 对象。</param>
        /// <param name="parameterName">参数名称。</param>
        /// <param name="parameterList">有效参数名称列表。</param>
        /// <param name="performSanityCheck">是否进行安全检查。</param>
        public static void UpdateAnimatorTrigger(this Animator animator, string parameterName, HashSet<string> parameterList, bool performSanityCheck = true)
        {
            if (performSanityCheck && !parameterList.Contains(parameterName)) return;
            animator.SetTrigger(parameterName);
        }

        /// <summary>
        /// 使用参数名称安全更新 Float 值。
        /// </summary>
        /// <param name="animator">Animator 对象。</param>
        /// <param name="parameterName">参数名称。</param>
        /// <param name="value">Float 值。</param>
        /// <param name="parameterList">有效参数名称列表。</param>
        /// <param name="performSanityCheck">是否进行安全检查。</param>
        public static void UpdateAnimatorFloat(this Animator animator, string parameterName, float value, HashSet<string> parameterList, bool performSanityCheck = true)
        {
            if (performSanityCheck && !parameterList.Contains(parameterName)) return;
            animator.SetFloat(parameterName, value);
        }

        /// <summary>
        /// 使用参数名称安全更新 Integer 值。
        /// </summary>
        /// <param name="animator">Animator 对象。</param>
        /// <param name="parameterName">参数名称。</param>
        /// <param name="value">Integer 值。</param>
        /// <param name="parameterList">有效参数名称列表。</param>
        /// <param name="performSanityCheck">是否进行安全检查。</param>
        public static void UpdateAnimatorInteger(this Animator animator, string parameterName, int value, HashSet<string> parameterList, bool performSanityCheck = true)
        {
            if (performSanityCheck && !parameterList.Contains(parameterName)) return;
            animator.SetInteger(parameterName, value);
        }

        /// <summary>
        /// 如果 Boolean 参数存在则更新。
        /// </summary>
        /// <param name="animator">Animator 对象。</param>
        /// <param name="parameterName">参数名称。</param>
        /// <param name="value">Boolean 值。</param>
        /// <param name="performSanityCheck">是否进行安全检查。</param>
        public static void UpdateAnimatorBoolIfExists(this Animator animator, string parameterName, bool value, bool performSanityCheck = true)
        {
            if (animator.HasParameterOfType(parameterName, AnimatorControllerParameterType.Bool))
                animator.SetBool(parameterName, value);
        }

        /// <summary>
        /// 如果 Trigger 参数存在则触发。
        /// </summary>
        /// <param name="animator">Animator 对象。</param>
        /// <param name="parameterName">参数名称。</param>
        /// <param name="performSanityCheck">是否进行安全检查。</param>
        public static void UpdateAnimatorTriggerIfExists(this Animator animator, string parameterName, bool performSanityCheck = true)
        {
            if (animator.HasParameterOfType(parameterName, AnimatorControllerParameterType.Trigger))
                animator.SetTrigger(parameterName);
        }

        /// <summary>
        /// 如果 Float 参数存在则更新。
        /// </summary>
        /// <param name="animator">Animator 对象。</param>
        /// <param name="parameterName">参数名称。</param>
        /// <param name="value">Float 值。</param>
        /// <param name="performSanityCheck">是否进行安全检查。</param>
        public static void UpdateAnimatorFloatIfExists(this Animator animator, string parameterName, float value, bool performSanityCheck = true)
        {
            if (animator.HasParameterOfType(parameterName, AnimatorControllerParameterType.Float))
                animator.SetFloat(parameterName, value);
        }

        /// <summary>
        /// 如果 Integer 参数存在则更新。
        /// </summary>
        /// <param name="animator">Animator 对象。</param>
        /// <param name="parameterName">参数名称。</param>
        /// <param name="value">Integer 值。</param>
        /// <param name="performSanityCheck">是否进行安全检查。</param>
        public static void UpdateAnimatorIntegerIfExists(this Animator animator, string parameterName, int value, bool performSanityCheck = true)
        {
            if (animator.HasParameterOfType(parameterName, AnimatorControllerParameterType.Int))
                animator.SetInteger(parameterName, value);
        }
    }
}
