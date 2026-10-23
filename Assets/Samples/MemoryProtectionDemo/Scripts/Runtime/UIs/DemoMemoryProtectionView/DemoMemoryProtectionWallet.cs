/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoMemoryProtectionWallet.cs
 * author:    Codex
 * created:   2026/10/21
 * descrip:   演示钱包：普通存档与受保护运行时数值之间的转换
 ***************************************************************/

using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;

namespace NovaFramework.Memory.Protection.Samples.Runtime
{
    /// <summary>只操作独立演示分类；不初始化存储、不修改隐私配置，也不接管真实玩家钱包。</summary>
    public sealed class DemoMemoryProtectionWallet : IDisposable
    {
        /// <summary>固定演示分类与条目，不使用玩家标识，也不与原 PlayerPrefs 示例共用数据。</summary>
        public const string SaveClassify = "MemoryProtectionDemo";
        public const string SaveItem = "wallet";
        public const string GoldFieldId = "memory_protection_demo.wallet.gold";
        public const string DiamondsFieldId = "memory_protection_demo.wallet.diamonds";

        private readonly IFileFragmentManager m_Store;
        /// <summary>只在内存持有的长期金币字段。</summary>
        public ProtectedLong Gold { get; private set; }
        /// <summary>只在内存持有的长期钻石字段。</summary>
        public ProtectedInt Diamonds { get; private set; }

        /// <summary>绑定已由 Nova 启动链加载的存储；调用方拥有存储生命周期。</summary>
        /// <param name="store">已就绪的 FileFragment 接口；演示仅写固定独立分类。</param>
        public DemoMemoryProtectionWallet(IFileFragmentManager store)
        {
            m_Store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>加载普通整数存档后建立内存基线；缺档只创建默认内存值，不自动保存。</summary>
        /// <returns>找到并读取演示存档时为 true；缺档时为 false，坏结构则抛错而不覆盖存档。</returns>
        public bool Load()
        {
            Dispose();
            if (!m_Store.HasItem(SaveClassify, SaveItem))
            {
                CreateNew();
                return false;
            }
            JObject data = JObject.Parse(m_Store.GetString(SaveClassify, SaveItem));
            if (data.Count != 3 || data["SchemaVersion"]?.Type != JTokenType.Integer ||
                data["SchemaVersion"].Value<int>() != 1 || data["Gold"]?.Type != JTokenType.Integer ||
                data["Diamonds"]?.Type != JTokenType.Integer)
                throw new InvalidDataException("演示钱包存档结构无效。");
            long gold = data["Gold"].Value<long>();
            int diamonds = data["Diamonds"].Value<int>();
            if (gold < 0 || diamonds < 0) throw new InvalidDataException("演示钱包数值范围无效。");
            Apply(gold, diamonds);
            return true;
        }

        /// <summary>用户显式新建演示钱包，只重建本页的固定初始值，不自动写盘。</summary>
        public void CreateNew() => Apply(1000, 100);

        /// <summary>两项均通过校验才输出完整快照；失败输出不作为合法余额使用。</summary>
        public bool TryRead(out long gold, out int diamonds)
        {
            gold = 0;
            diamonds = 0;
            if (Gold == null || Diamonds == null || !Gold.TryRead(out long checkedGold) ||
                !Diamonds.TryRead(out int checkedDiamonds)) return false;
            gold = checkedGold;
            diamonds = checkedDiamonds;
            return true;
        }

        /// <summary>先校验钱包，再修改金币；余额不足、溢出或隔离时拒绝变更。</summary>
        public bool TryChangeGold(long delta)
        {
            if (!TryRead(out long gold, out _)) return false;
            try
            {
                long next = checked(gold + delta);
                return next >= 0 && Gold.TryWrite(next);
            }
            catch (OverflowException) { return false; }
        }

        /// <summary>先校验钱包，再修改钻石；不把异常字段的输出零值用于结算。</summary>
        public bool TryChangeDiamonds(int delta)
        {
            if (!TryRead(out _, out int diamonds)) return false;
            try
            {
                int next = checked(diamonds + delta);
                return next >= 0 && Diamonds.TryWrite(next);
            }
            catch (OverflowException) { return false; }
        }

        /// <summary>仅校验通过后才更新普通 DTO 并请求保存；不持久化内存密钥、校验信息或插件对象。</summary>
        /// <returns>校验失败时不调用存储；否则沿用 FileFragment.Save 的返回值，不保证设备耐久性。</returns>
        public bool TrySave()
        {
            if (!TryRead(out long gold, out int diamonds)) return false;
            string json = JsonConvert.SerializeObject(new SaveData { SchemaVersion = 1, Gold = gold, Diamonds = diamonds });
            m_Store.SetString(SaveClassify, SaveItem, json);
            return m_Store.Save(SaveClassify);
        }

        /// <summary>释放旧字段后使用已验证的普通值创建本页运行时字段。</summary>
        private void Apply(long gold, int diamonds)
        {
            Dispose();
            Gold = new ProtectedLong(GoldFieldId, gold);
            Diamonds = new ProtectedInt(DiamondsFieldId, diamonds);
        }

        /// <summary>页面关闭或重载时释放旧字段；不关闭调用方的存储。</summary>
        public void Dispose()
        {
            Gold?.Dispose();
            Diamonds?.Dispose();
            Gold = null;
            Diamonds = null;
        }

        /// <summary>演示存档只保存这三个普通整数字段。</summary>
        private sealed class SaveData
        {
            public int SchemaVersion;
            public long Gold;
            public int Diamonds;
        }
    }
}
