/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SQLiteManagerConfig.cs
 * author:    taoye
 * created:   2026/3/18
 * descrip:   SQLite 持久化管理器配置 DTO
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// SQLite 持久化管理器配置。
    /// </summary>
    public class SQLiteManagerConfig : PersistManagerConfigBase
    {
        /// <summary>
        /// SQLite Cipher 数据库加密密码，为空时不启用数据库级加密。
        /// </summary>
        public string CipherPassword { get; set; }
    }
}
