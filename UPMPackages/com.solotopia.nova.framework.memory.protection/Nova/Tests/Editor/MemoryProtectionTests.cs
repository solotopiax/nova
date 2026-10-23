/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MemoryProtectionTests.cs
 * author:    taoye
 * created:   2026/10/21
 * descrip:   内存防护的可选插件、故障隔离和存档兼容契约测试
 ***************************************************************/

using System;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using NUnit.Framework;

namespace NovaFramework.Tests.Editor
{
    /// <summary>只创建隔离内存对象，不读取玩家存档、不修改场景、配置或真实埋点插件。</summary>
    public sealed class MemoryProtectionTests
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        /// <summary>每个用例开始前隔离静态生命周期。</summary>
        [SetUp]
        public void SetUp() => Reset();

        /// <summary>无论断言是否成功均释放字段、通知、事件队列和后端。</summary>
        [TearDown]
        public void TearDown() => Reset();

        /// <summary>没有可选插件时同一 API 可读写，普通数字 JSON 不引入运行时元数据。</summary>
        [Test]
        public void MissingProvider_PreservesNumericJsonAndReadWrite()
        {
            MemoryProtection.Initialize(new MemoryProtectionConfigs());
            using var gold = new ProtectedLong("wallet.gold", 1000);
            Assert.That(MemoryProtection.IsActive, Is.False);
            Assert.That(gold.IsProtected, Is.False);
            Assert.That(gold.TryWrite(900), Is.True);
            Assert.That(gold.Value, Is.EqualTo(900));
            Assert.That(JsonConvert.SerializeObject(gold), Is.EqualTo("900"));
            using var restored = JsonConvert.DeserializeObject<ProtectedLong>("900");
            Assert.That(restored.Value, Is.EqualTo(900));
            using var diamonds = JsonConvert.DeserializeObject<ProtectedInt>("2147483647");
            Assert.That(diamonds.Value, Is.EqualTo(int.MaxValue));
            Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<ProtectedInt>("2147483648"));
            Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<ProtectedLong>("1.5"));
        }

        /// <summary>配置显式关闭时不创建防护状态，轮换开关不单独启用加密。</summary>
        [Test]
        public void DetectionDisabled_LeavesBackendUnused()
        {
            RegisterActualProvider();
            MemoryProtection.Initialize(new MemoryProtectionConfigs { EnableMemoryTamperDetection = false, EnableMemoryKeyRotation = true });
            using var gold = new ProtectedLong("wallet.gold", 1000);
            Assert.That(gold.IsProtected, Is.False);
            Assert.That(gold.TryWrite(1200), Is.True);
            Assert.That(gold.Value, Is.EqualTo(1200));
        }

        /// <summary>初始化前创建的字段在配置加载时迁移，原有引用和普通数字格式不变。</summary>
        [Test]
        public void ConfigInitialization_ActivatesExistingFields()
        {
            using var gold = new ProtectedLong("wallet.gold", 1000);
            Assert.That(gold.IsProtected, Is.False);
            RegisterActualProvider();
            MemoryProtection.Initialize(new MemoryProtectionConfigs());
            Assert.That(gold.IsProtected, Is.True);
            Assert.That(gold.Value, Is.EqualTo(1000));
            Assert.That(JsonConvert.SerializeObject(gold), Is.EqualTo("1000"));
            using var restored = JsonConvert.DeserializeObject<ProtectedLong>("1000");
            Assert.That(restored.IsProtected, Is.True);
            Assert.That(restored.Value, Is.EqualTo(1000));
        }

        /// <summary>实际 ACTk 校验被破坏后，读、覆盖写、序列化均拒绝，通知只触发一次。</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void ActualTamper_QuarantinesBeforeReturningOrSaving(bool wholeStateZeroed)
        {
            RegisterActualProvider();
            MemoryProtection.Initialize(new MemoryProtectionConfigs());
            using var gold = new ProtectedLong("wallet.gold", 1000);
            int notices = 0;
            MemoryTamperInfo info = null;
            MemoryProtection.TamperDetected += value => { notices++; info = value; };
            object obscured = GetObscured(gold);
            if (wholeStateZeroed)
            {
                obscured.GetType().GetField("hiddenValue", Hidden).SetValue(obscured, 0L);
                obscured.GetType().GetField("currentCryptoKey", Hidden).SetValue(obscured, 0L);
                obscured.GetType().GetField("hash", Hidden).SetValue(obscured, 0);
            }
            else
            {
                FieldInfo hash = obscured.GetType().GetField("hash", Hidden);
                hash.SetValue(obscured, (int)hash.GetValue(obscured) ^ 1);
            }
            Assert.That(gold.TryRead(out long rejected), Is.False);
            Assert.That(rejected, Is.Zero);
            Assert.That(gold.TryWrite(1000), Is.False);
            Assert.That(gold.IsQuarantined, Is.True);
            Assert.Throws<JsonSerializationException>(() => JsonConvert.SerializeObject(gold));
            Assert.That(notices, Is.EqualTo(1));
            Assert.That(info.ProtectedFieldId, Is.EqualTo("wallet.gold"));
            Assert.That(info.DetectionReason, Is.EqualTo("integrity_mismatch"));
        }

        /// <summary>实际密钥轮换更新持有的原存储，数值不变；int 与 long 边界均可正常读写。</summary>
        [Test]
        public void ActualRotation_ChangesStoredKeyWithoutChangingValue()
        {
            RegisterActualProvider();
            MemoryProtection.Initialize(new MemoryProtectionConfigs());
            using var gold = new ProtectedLong("wallet.gold", long.MaxValue);
            object before = GetObscured(gold);
            object beforeKey = before.GetType().GetField("currentCryptoKey", Hidden).GetValue(before);
            object state = typeof(ProtectedLong).GetField("m_State", Hidden).GetValue(gold);
            Assert.That((bool)state.GetType().GetMethod("TryRotate", Hidden).Invoke(state, new object[] { "test-batch" }), Is.True);
            object after = GetObscured(gold);
            Assert.That(after.GetType().GetField("currentCryptoKey", Hidden).GetValue(after), Is.Not.EqualTo(beforeKey));
            Assert.That(gold.Value, Is.EqualTo(long.MaxValue));
            Assert.That(gold.TryWrite(long.MinValue), Is.True);
            Assert.That(gold.Value, Is.EqualTo(long.MinValue));
            using var diamonds = new ProtectedInt("wallet.diamonds", int.MinValue);
            Assert.That(diamonds.IsProtected, Is.True);
            Assert.That(diamonds.Value, Is.EqualTo(int.MinValue));
            Assert.That(diamonds.TryWrite(int.MaxValue), Is.True);
            Assert.That(diamonds.Value, Is.EqualTo(int.MaxValue));
        }

        /// <summary>框架重置后旧对象无法继续读写，下一生命周期不会继承旧值或事件。</summary>
        [Test]
        public void Reset_InvalidatesPreviousLifetime()
        {
            MemoryProtection.Initialize(new MemoryProtectionConfigs());
            using var gold = new ProtectedLong("wallet.gold", 1000);
            Reset();
            Assert.That(gold.TryRead(out _), Is.False);
            Assert.That(gold.TryWrite(1), Is.False);
        }

        /// <summary>旧资产缺少新字段时使用开启默认值；显式关闭值经维度复制及导出复制后仍为关闭。</summary>
        [Test]
        public void PrivacyFlags_PreserveDefaultsAndExplicitDisabledValues()
        {
            var legacy = UnityEngine.JsonUtility.FromJson<PrivacyConfigs>("{}");
            var defaults = MemoryProtectionConfigs.FromPrivacy(legacy);
            Assert.That(defaults.EnableMemoryTamperDetection, Is.True);
            Assert.That(defaults.EnableMemoryKeyRotation, Is.True);
            var disabled = new MemoryProtectionConfigs { EnableMemoryTamperDetection = false, EnableMemoryKeyRotation = false };
            var privacy = new PrivacyConfigs { ExtensionConfigsJson = disabled.WriteToPrivacyJson("{\"another.package\":{\"keep\":42}}") };
            var dimensions = typeof(NovaFramework.Editor.EditorUtil.Config.DimensionProjector);
            var dimensionCopy = (PrivacyConfigs)dimensions.GetMethod("DeepClonePrivacyConfigs", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { privacy });
            var exporter = typeof(NovaFramework.Editor.EditorUtil.Config.Exporter);
            var runtimeCopy = (PrivacyConfigs)exporter.GetMethod("ClonePrivacyConfigs", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { dimensionCopy });
            var result = MemoryProtectionConfigs.FromPrivacy(runtimeCopy);
            Assert.That(result.EnableMemoryTamperDetection, Is.False);
            Assert.That(result.EnableMemoryKeyRotation, Is.False);
            Assert.That(Newtonsoft.Json.Linq.JObject.Parse(runtimeCopy.ExtensionConfigsJson)["another.package"]["keep"].Value<int>(), Is.EqualTo(42));
        }

        /// <summary>扩展损坏时不把错误开关解释为开启，编辑写入也不覆盖其他扩展。</summary>
        [TestCase("{broken")]
        [TestCase("{\"com.solotopia.nova.framework.memory.protection\":false}")]
        [TestCase("{\"com.solotopia.nova.framework.memory.protection\":{\"EnableMemoryTamperDetection\":\"false\"}}")]
        public void InvalidPrivacyExtension_IsRejected(string json)
        {
            Assert.That(MemoryProtectionConfigs.TryRead(new PrivacyConfigs { ExtensionConfigsJson = json }, out _), Is.False);
        }

        /// <summary>包配置只拥有自己的字段，保存时保留其他包及未来字段，不重写不相关数据。</summary>
        [Test]
        public void PrivacyWrite_PreservesOtherExtensionsAndUnknownFields()
        {
            var root = new JObject
            {
                [MemoryProtectionConfigs.ExtensionKey] = new JObject { ["FutureOption"] = "preserved" },
                ["another.package"] = new JObject { ["Enabled"] = false }
            };
            string updated = new MemoryProtectionConfigs { EnableMemoryTamperDetection = false }.WriteToPrivacyJson(root.ToString());
            var result = JObject.Parse(updated);
            Assert.That(result[MemoryProtectionConfigs.ExtensionKey]["FutureOption"].Value<string>(), Is.EqualTo("preserved"));
            Assert.That(result["another.package"]["Enabled"].Value<bool>(), Is.False);
            Assert.Throws<JsonReaderException>(() => new MemoryProtectionConfigs().WriteToPrivacyJson("{broken"));
        }

        /// <summary>自动管理器在既有 SDK 之后轮询、之前关闭，关闭后不会因查询状态重新初始化。</summary>
        [Test]
        public void PackageLifecycle_UsesExistingManagerOrderingAndStopsAutomaticInitialization()
        {
            Type managerType = typeof(ProtectedLong).Assembly.GetType("NovaFramework.Runtime.MemoryProtectionManager", true);
            var manager = (FrameworkManager)Activator.CreateInstance(managerType, true);
            try
            {
                Assert.That(manager.Priority, Is.GreaterThan(16));
                MemoryProtection.Initialize(new MemoryProtectionConfigs());
                using var gold = new ProtectedLong("test.lifecycle.gold", 1000);
                manager.Shutdown();
                Assert.That(gold.TryRead(out _), Is.False);
                Assert.That(MemoryProtection.IsActive, Is.False);
            }
            finally
            {
                FrameworkManagersGroup.UnregisterManager(manager);
            }
        }

        /// <summary>只在测试中访问实际 vendor 状态以注入损坏，不输出任何字段值或密钥。</summary>
        private static object GetObscured(ProtectedLong value)
        {
            object state = typeof(ProtectedLong).GetField("m_State", Hidden).GetValue(value);
            object storage = state.GetType().GetField("Storage", Hidden).GetValue(state);
            return storage.GetType().GetField("m_Value", Hidden).GetValue(storage);
        }

        /// <summary>可选插件不存在时跳过插件专属测试，普通兼容测试仍照常运行。</summary>
        private static void RegisterActualProvider()
        {
            if (Type.GetType("CodeStage.AntiCheat.ObscuredTypes.ObscuredLong, ACTk.Runtime", false) == null)
                Assert.Ignore("当前工程未安装可选 Anti-Cheat Toolkit。");
            Type provider = Type.GetType("NovaFramework.Runtime.MemoryProtectionIntegration.ActkMemoryProtectionProvider, NovaFramework.MemoryProtection.Runtime", true);
            MemoryProtection.RegisterProvider((IMemoryProtectionProvider)Activator.CreateInstance(provider, true));
        }

        /// <summary>复用真实静态重置路径，不为生产 API 添加测试专用开关。</summary>
        private static void Reset() => typeof(MemoryProtection).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
    }
}
