/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  OffsetBundleDecryptor.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   YooAsset 偏移量解密器（占位骨架）
 ***************************************************************/

using YooAsset;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// YooAsset 偏移量解密器。
    /// </summary>
    /// <remarks>
    /// 当前为占位骨架，待加密管线对接后填充偏移量逻辑；
    /// AssetManager 工厂遇到 AssetDecryptorType.OffsetBundleDecryptor 时实例化此类。
    /// </remarks>
    public sealed class OffsetBundleDecryptor : IBundleOffsetDecryptor
    {
        /// <summary>
        /// 当前 AssetBundle 文件的解密前缀偏移量（字节）。
        /// </summary>
        /// <param name="args">YooAsset 解密参数。</param>
        /// <returns>需跳过的字节数；占位实现返回 0。</returns>
        public long GetFileOffset(BundleDecryptArgs args)
        {
            return 0L;
        }
    }
}
