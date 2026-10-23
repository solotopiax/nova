/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ActkMemoryProtectionProvider.cs
 * author:    taoye
 * created:   2026/10/21
 * descrip:   ACTk 公开 API 的可选适配，不引用或分发商业源码
 ***************************************************************/

using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Scripting;

namespace NovaFramework.Runtime.MemoryProtectionIntegration
{
    /// <summary>通过缓存的精确公开签名适配 ACTk，兼容官方 Assets 导入及内网 UPM 安装。</summary>
    [Preserve]
    internal sealed class ActkMemoryProtectionProvider : IMemoryProtectionProvider
    {
        private readonly IntegerApi m_Int;
        private readonly IntegerApi m_Long;
        /// <summary>来自实际 ACTk 常量的插件版本。</summary>
        public string Version { get; }

        /// <summary>验证所有必要 API；缺失插件时不注册，未安装消费者无需商业程序集。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            Type longType = Type.GetType("CodeStage.AntiCheat.ObscuredTypes.ObscuredLong, ACTk.Runtime", false);
            if (longType == null) return;
            try { MemoryProtection.RegisterProvider(new ActkMemoryProtectionProvider()); }
            catch (Exception) { Log.Warning(LogTag.Config, "Anti-Cheat Toolkit API 不兼容，内存防护未启用，请检查插件版本。"); }
        }

        /// <summary>一次性缓存两个具体数值类型和原厂校验方法，不使用动态代码生成。</summary>
        private ActkMemoryProtectionProvider()
        {
            m_Int = new IntegerApi("ObscuredInt", typeof(int));
            m_Long = new IntegerApi("ObscuredLong", typeof(long));
            Type common = Type.GetType("CodeStage.AntiCheat.Common.ACTk, ACTk.Runtime", true);
            Version = (string)common.GetField("Version", BindingFlags.Public | BindingFlags.Static).GetRawConstantValue();
        }

        /// <summary>创建只包含原厂 ObscuredInt 或 ObscuredLong 的引用存储。</summary>
        public IMemoryProtectionStorage Create(long value, bool useInt32) => new IntegerStorage(useInt32 ? m_Int : m_Long, value);

        /// <summary>原厂公开方法缓存；不复制原厂算法，不访问其 internal 字段。</summary>
        private sealed class IntegerApi
        {
            internal readonly bool UseInt32;
            internal readonly MethodInfo FromPlain;
            internal readonly MethodInfo GetEncrypted;
            internal readonly MethodInfo IsDefault;
            internal readonly PropertyInfo Hash;
            internal readonly Func<int, int, int> DecryptInt;
            internal readonly Func<long, long, long> DecryptLong;
            internal readonly Func<int, int, bool> ValidateInt;
            internal readonly Func<long, int, bool> ValidateLong;

            /// <summary>只接受已核实的 ACTk 整数公开 API；签名漂移时拒绝启用后端。</summary>
            internal IntegerApi(string typeName, Type primitive)
            {
                UseInt32 = primitive == typeof(int);
                Type type = Type.GetType("CodeStage.AntiCheat.ObscuredTypes." + typeName + ", ACTk.Runtime", true);
                Type hashUtils = Type.GetType("CodeStage.AntiCheat.Utils.HashUtils, ACTk.Runtime", true);
                FromPlain = Required(type, "op_Implicit", BindingFlags.Public | BindingFlags.Static, primitive);
                GetEncrypted = Required(type, "GetEncrypted", BindingFlags.Public | BindingFlags.Instance, primitive.MakeByRefType());
                IsDefault = Required(type, "IsDefault", BindingFlags.Public | BindingFlags.Instance);
                Hash = type.GetProperty("Hash", BindingFlags.Public | BindingFlags.Instance);
                if (Hash?.PropertyType != typeof(int)) throw new MissingMemberException();
                MethodInfo decrypt = Required(type, "Decrypt", BindingFlags.Public | BindingFlags.Static, primitive, primitive);
                MethodInfo validate = Required(hashUtils, "ValidateHash", BindingFlags.Public | BindingFlags.Static, primitive, typeof(int));
                if (UseInt32)
                {
                    DecryptInt = (Func<int, int, int>)Delegate.CreateDelegate(typeof(Func<int, int, int>), decrypt);
                    ValidateInt = (Func<int, int, bool>)Delegate.CreateDelegate(typeof(Func<int, int, bool>), validate);
                }
                else
                {
                    DecryptLong = (Func<long, long, long>)Delegate.CreateDelegate(typeof(Func<long, long, long>), decrypt);
                    ValidateLong = (Func<long, int, bool>)Delegate.CreateDelegate(typeof(Func<long, int, bool>), validate);
                }
            }

            /// <summary>精确匹配公开方法，禁止含糊选取 overload。</summary>
            private static MethodInfo Required(Type type, string name, BindingFlags flags, params Type[] parameters)
            {
                return type.GetMethod(name, flags, null, parameters, null) ?? throw new MissingMethodException(type.FullName, name);
            }

            /// <summary>以原厂隐式转换创建新的随机密钥及校验信息，参数数组不长期保存明文。</summary>
            internal object Create(long value)
            {
                object primitive = UseInt32 ? (object)checked((int)value) : value;
                return FromPlain.Invoke(null, new[] { primitive });
            }
        }

        /// <summary>引用拥有一个装箱后的原厂结构体，更新时替换实际存储，避免副本轮换。</summary>
        private sealed class IntegerStorage : IMemoryProtectionStorage
        {
            private readonly IntegerApi m_Api;
            private readonly object[] m_KeyArgument = new object[1];
            private object m_Value;

            /// <summary>创建实际加密状态，不在本封装中另存明文余额。</summary>
            internal IntegerStorage(IntegerApi api, long value)
            {
                m_Api = api;
                m_Value = api.Create(value);
            }

            /// <summary>调用原厂解码和 HashUtils 校验后才输出值；不触发会在 DEBUG 输出明文的 detector 路径。</summary>
            public bool TryRead(out long value)
            {
                value = 0;
                try
                {
                    // 本封装总由可信输入初始化；已建立的状态被整体清零不能视为合法默认值。
                    if ((bool)m_Api.IsDefault.Invoke(m_Value, null)) return false;
                    object encrypted = m_Api.GetEncrypted.Invoke(m_Value, m_KeyArgument);
                    int hash = (int)m_Api.Hash.GetValue(m_Value);
                    if (m_Api.UseInt32)
                    {
                        int candidate = m_Api.DecryptInt((int)encrypted, (int)m_KeyArgument[0]);
                        if (!m_Api.ValidateInt(candidate, hash)) return false;
                        value = candidate;
                    }
                    else
                    {
                        long candidate = m_Api.DecryptLong((long)encrypted, (long)m_KeyArgument[0]);
                        if (!m_Api.ValidateLong(candidate, hash)) return false;
                        value = candidate;
                    }
                    return true;
                }
                finally
                {
                    // out 参数不能让密钥滞留在长寿命参数数组中。
                    m_KeyArgument[0] = null;
                }
            }

            /// <summary>用可信输入建立新原厂状态；旧状态是否可写已由 Core 先验证。</summary>
            public void Write(long value) => m_Value = m_Api.Create(value);

            /// <summary>以 Core 刚验证的值重新调用原厂构造路径生成密钥，避免原厂解码诊断泄露异常值。</summary>
            public void RotateKey(long verifiedValue) => m_Value = m_Api.Create(verifiedValue);
        }
    }
}
