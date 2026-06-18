/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TypeNamePair.cs
 * author:    taoye
 * created:   2025/12/10
 * descrip:   类型和名称的组合值
 ***************************************************************/

using System;
using System.Runtime.InteropServices;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 类型和名称的组合值。
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal struct TypeNamePair : IEquatable<TypeNamePair>
    {
        /// <summary>
        /// 类型。
        /// </summary>
        private readonly Type m_Type;
        public Type Type => m_Type;
        
        /// <summary>
        /// 名称。
        /// </summary>
        private readonly string m_Name;
        public string Name => m_Name;

        /// <summary>
        /// 初始化类型和名称的组合值的新实例。
        /// </summary>
        /// <param name="type">类型。</param>
        public TypeNamePair(Type type):this(type, string.Empty)
        {
        }

        /// <summary>
        /// 初始化类型和名称的组合值的新实例。
        /// </summary>
        /// <param name="type">类型。</param>
        /// <param name="name">名称。</param>
        public TypeNamePair(Type type, string name)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type), "Type 无效。");
            }

            m_Type = type;
            m_Name = name ?? string.Empty;
        }

        /// <summary>
        /// 获取类型和名称的组合值字符串。
        /// </summary>
        /// <returns>类型和名称的组合值字符串。</returns>
        public override string ToString()
        {
            string typeName = m_Type.FullName;
            return string.IsNullOrEmpty(m_Name) ? typeName : Txt.Format("{0}.{1}", typeName, m_Name);
        }

        /// <summary>
        /// 获取对象的哈希值，使用质数乘法减少碰撞概率。
        /// </summary>
        /// <returns>对象的哈希值。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return (m_Type.GetHashCode() * 397) ^ m_Name.GetHashCode();
            }
        }

        /// <summary>
        /// 比较对象是否与自身相等，使用模式匹配替代强转，避免无效装箱。
        /// </summary>
        /// <param name="obj">要比较的对象。</param>
        /// <returns>被比较的对象是否与自身相等。</returns>
        public override bool Equals(object obj)
        {
            return obj is TypeNamePair other && Equals(other);
        }

        /// <summary>
        /// 比较对象是否与自身相等。
        /// </summary>
        /// <param name="value">要比较的对象。</param>
        /// <returns>被比较的对象是否与自身相等。</returns>
        public bool Equals(TypeNamePair value)
        {
            return m_Type == value.m_Type && m_Name == value.m_Name;
        }

        /// <summary>
        /// 判断两个对象是否相等。
        /// </summary>
        /// <param name="a">值 a。</param>
        /// <param name="b">值 b。</param>
        /// <returns>两个对象是否相等。</returns>
        public static bool operator ==(TypeNamePair a, TypeNamePair b)
        {
            return a.Equals(b);
        }

        /// <summary>
        /// 判断两个对象是否不相等。
        /// </summary>
        /// <param name="a">值 a。</param>
        /// <param name="b">值 b。</param>
        /// <returns>两个对象是否不相等。</returns>
        public static bool operator !=(TypeNamePair a, TypeNamePair b)
        {
            return !(a == b);
        }
    }
}
