/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IMemoryProtectionProvider.cs
 * author:    taoye
 * created:   2026/10/21
 * descrip:   可选内存防护后端契约，不依赖商业插件程序集
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>由可选适配程序集提供数值存储；仅允许在 Unity 主线程访问。</summary>
    public interface IMemoryProtectionProvider
    {
        /// <summary>当前实际适配的插件版本。</summary>
        string Version { get; }

        /// <summary>从可信业务输入创建新的受保护存储，useInt32 区分 int 与 long。</summary>
        IMemoryProtectionStorage Create(long value, bool useInt32);
    }

    /// <summary>只保存单个数值的内存状态；不允许持久化此对象、密钥或校验信息。</summary>
    public interface IMemoryProtectionStorage
    {
        /// <summary>校验当前状态并读取；异常时返回 false，不将可疑值交给调用方。</summary>
        bool TryRead(out long value);

        /// <summary>用已验证的业务输入重建存储及运行时密钥。</summary>
        void Write(long value);

        /// <summary>从 Core 刚校验通过的数值重建随机密钥；调用后再复核，禁止重新解码可疑状态。</summary>
        void RotateKey(long verifiedValue);
    }
}
