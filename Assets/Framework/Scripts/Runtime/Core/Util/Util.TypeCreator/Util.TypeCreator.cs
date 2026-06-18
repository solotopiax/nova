/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Util.TypeCreator.cs
 * author:    taoye
 * created:   2026/1/15
 * descrip:   类型生成器相关的实用函数
 ***************************************************************/
using System;
using UnityEngine;

namespace NovaFramework.Runtime
{
    public static partial class Util
    {
        public static class TypeCreator
        {
            /// <summary>
            /// 创建指定类型的实例（无参数构造函数）。
            /// </summary>
            public static T Create<T>(string typeName) where T : class
            {
                return Create<T>(typeName, null);
            }

            /// <summary>
            /// 创建指定类型的实例，并传递构造函数参数。
            /// </summary>
            /// <typeparam name="T">期望的类型。</typeparam>
            /// <param name="typeName">类型全名。</param>
            /// <param name="args">构造函数参数。</param>
            /// <returns>实例对象。</returns>
            public static T Create<T>(string typeName, params object[] args) where T : class
            {
                if (string.IsNullOrEmpty(typeName))
                {
                    throw new ArgumentException("typeName 无效。");
                }

                Type type = Util.Assembly.GetType(typeName);
                if (type == null)
                {
                    throw new ArgumentException(Txt.Format("类型{0}获取失败。", typeName), nameof(typeName));
                }

                if (!typeof(T).IsAssignableFrom(type))
                {
                    throw new ArgumentException(Txt.Format("类型{0}并未继承实现{1}。", typeName, typeof(T).Name), nameof(typeName));
                }

                T instance = null;
                try
                {
                    if (typeof(MonoBehaviour).IsAssignableFrom(type))
                    {
                        var go = new GameObject(typeName);
                        instance = go.AddComponent(type) as T;
                    }
                    else
                    {
                        instance = Activator.CreateInstance(type, args) as T;
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(Txt.Format("创建实例{0}失败，原因: {1}。", typeName, ex.Message), ex);
                }

                if (instance == null)
                {
                    throw new InvalidOperationException($"创建实例{typeName}失败。");
                }

                return instance;
            }
        }   
    }
}


