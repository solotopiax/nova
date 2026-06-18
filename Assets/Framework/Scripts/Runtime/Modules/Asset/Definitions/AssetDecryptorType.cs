/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetDecryptorType.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   YooAsset 解密器类型
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// YooAsset 解密器类型。
    /// </summary>
    /// <remarks>
    /// 由启动期从 bootconfig.json 读取，决定 AssetManager 注入哪种 IBundleDecryptor 派生实现。
    /// </remarks>
    public enum AssetDecryptorType : byte
    {
        /// <summary>
        /// 不启用解密。
        /// </summary>
        None = 0,

        /// <summary>
        /// 偏移量解密器（IBundleOffsetDecryptor）。
        /// </summary>
        OffsetBundleDecryptor = 2,
    }
}
