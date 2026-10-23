/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MemoryProtectionLegacyConfigMigrationTests.cs
 * author:    Codex
 * created:   2026/10/21
 * descrip:   旧隐私开关的只读解析、位置核对与内存序列化合并测试
 ***************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NovaFramework.Editor;
using NovaFramework.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Tests.Editor
{
    /// <summary>
    /// 仅传入合成 YAML 字符串并创建内存配置，不创建文件，不将此证据视为真实旧资产自动迁移验证。
    /// </summary>
    public sealed class MemoryProtectionLegacyConfigMigrationTests
    {
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        /// <summary>旧值均为关闭时，解析与 SerializedObject 合并保留关闭状态及其他扩展内容。</summary>
        [Test]
        public void RuntimeDisabledValues_SurviveJsonMergeAndSerializedObjectApply()
        {
            IList records = Parse("MonoBehaviour:\n  PrivacyConfigs:\n    EnableMemoryTamperDetection: 0\n    EnableMemoryKeyRotation: 0\n  Platform: 1\n", false);
            Assert.That(records.Count, Is.EqualTo(1));
            object record = records[0];
            Assert.That(Read<string>(record, "PropertyPath"), Is.EqualTo("PrivacyConfigs"));
            Assert.That(Read<int>(record, "FieldCount"), Is.EqualTo(2));
            Assert.That(Read<bool>(record, "HasCoordinates"), Is.False);

            ConfigRuntimeSO runtime = ScriptableObject.CreateInstance<ConfigRuntimeSO>();
            try
            {
                runtime.PrivacyConfigs = new PrivacyConfigs
                {
                    ExtensionConfigsJson = "{\"other.package\":{\"enabled\":true},\"" + MemoryProtectionConfigs.ExtensionKey + "\":{\"FutureField\":7}}",
                };
                MemoryProtectionConfigs config = new()
                {
                    EnableMemoryTamperDetection = Read<bool>(record, "Detection"),
                    EnableMemoryKeyRotation = Read<bool>(record, "Rotation"),
                };
                using (SerializedObject serialized = new(runtime))
                {
                    SerializedProperty extensions = serialized.FindProperty(Read<string>(record, "PropertyPath"))
                        .FindPropertyRelative("ExtensionConfigsJson");
                    extensions.stringValue = config.WriteToPrivacyJson(extensions.stringValue);
                    Assert.That(serialized.ApplyModifiedPropertiesWithoutUndo(), Is.True);
                }
                Assert.That(MemoryProtectionConfigs.TryRead(runtime.PrivacyConfigs, out MemoryProtectionConfigs restored), Is.True);
                Assert.That(restored.EnableMemoryTamperDetection, Is.False);
                Assert.That(restored.EnableMemoryKeyRotation, Is.False);
                JObject merged = JObject.Parse(runtime.PrivacyConfigs.ExtensionConfigsJson);
                Assert.That(merged["other.package"]?["enabled"]?.Value<bool>(), Is.True);
                Assert.That(merged[MemoryProtectionConfigs.ExtensionKey]?["FutureField"]?.Value<int>(), Is.EqualTo(7));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(runtime);
            }
        }

        /// <summary>旧资产只包含一项开关时，另一项保留原默认 true，不把缺失误读为 false。</summary>
        [Test]
        public void MissingLegacyField_PreservesTheOtherDefault()
        {
            object detectionOnly = Parse("MonoBehaviour:\n  PrivacyConfigs:\n    EnableMemoryTamperDetection: 0\n", false)[0];
            Assert.That(Read<bool>(detectionOnly, "Detection"), Is.False);
            Assert.That(Read<bool>(detectionOnly, "Rotation"), Is.True);
            Assert.That(Read<int>(detectionOnly, "FieldCount"), Is.EqualTo(1));

            object rotationOnly = Parse("MonoBehaviour:\n  PrivacyConfigs:\n    EnableMemoryKeyRotation: 0\n", false)[0];
            Assert.That(Read<bool>(rotationOnly, "Detection"), Is.True);
            Assert.That(Read<bool>(rotationOnly, "Rotation"), Is.False);
            Assert.That(Read<int>(rotationOnly, "FieldCount"), Is.EqualTo(1));
        }

        /// <summary>多坐标多模式记录保持物理顺序和完整路径，序列化坐标变化会被核对拒绝。</summary>
        [Test]
        public void MatrixRecords_MatchEverySerializedPathAndCoordinate()
        {
            const string yaml = @"MonoBehaviour:
  m_Entries:
  - Platform: 1
    Channel: 1
    PrivacyConfigsByMode:
    - Mode: 0
      Config:
        EnableMemoryTamperDetection: 0
        EnableMemoryKeyRotation: 1
    - Mode: 1
      Config:
        EnableMemoryTamperDetection: 1
        EnableMemoryKeyRotation: 0
    SDKConfigsByMode: []
  - Platform: 2
    Channel: 2
    PrivacyConfigsByMode:
    - Mode: 0
      Config:
        EnableMemoryTamperDetection: 1
        EnableMemoryKeyRotation: 1
    - Mode: 1
      Config:
        EnableMemoryTamperDetection: 0
        EnableMemoryKeyRotation: 0
  CurrentChannel: 1
";
            IList records = Parse(yaml, true);
            Assert.That(records.Count, Is.EqualTo(4));
            string[] expectedPaths =
            {
                "m_Entries.Array.data[0].PrivacyConfigsByMode.Array.data[0].Config",
                "m_Entries.Array.data[0].PrivacyConfigsByMode.Array.data[1].Config",
                "m_Entries.Array.data[1].PrivacyConfigsByMode.Array.data[0].Config",
                "m_Entries.Array.data[1].PrivacyConfigsByMode.Array.data[1].Config",
            };
            bool[] expectedDetection = { false, true, true, false };
            bool[] expectedRotation = { true, false, true, false };
            ConfigMasterSO master = ScriptableObject.CreateInstance<ConfigMasterSO>();
            try
            {
                master.EditorEntries.Clear();
                master.EditorEntries.Add(new PlatformChannelEntry { Platform = (PlatformType)1, Channel = (ChannelType)1 });
                master.EditorEntries.Add(new PlatformChannelEntry { Platform = (PlatformType)2, Channel = (ChannelType)2 });
                using SerializedObject serialized = new(master);
                List<string> actualPaths = (List<string>)Invoke("GetPrivacyPropertyPaths", serialized, true);
                Assert.That(actualPaths, Is.EqualTo(expectedPaths));
                for (int index = 0; index < records.Count; index++)
                {
                    object record = records[index];
                    Assert.That(Read<string>(record, "PropertyPath"), Is.EqualTo(expectedPaths[index]));
                    Assert.That(Read<int>(record, "Platform"), Is.EqualTo(index < 2 ? 1 : 2));
                    Assert.That(Read<int>(record, "Channel"), Is.EqualTo(index < 2 ? 1 : 2));
                    Assert.That(Read<int>(record, "Mode"), Is.EqualTo(index % 2));
                    Assert.That(Read<int>(record, "FieldCount"), Is.EqualTo(2));
                    Assert.That(Read<bool>(record, "Detection"), Is.EqualTo(expectedDetection[index]));
                    Assert.That(Read<bool>(record, "Rotation"), Is.EqualTo(expectedRotation[index]));
                    Assert.That(serialized.FindProperty(expectedPaths[index])?.FindPropertyRelative("ExtensionConfigsJson"), Is.Not.Null);
                    Assert.That((bool)Invoke("CoordinatesMatch", serialized, record), Is.True);
                }
                master.EditorEntries.Reverse();
                serialized.Update();
                Assert.That((bool)Invoke("CoordinatesMatch", serialized, records[0]), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(master);
            }
        }

        /// <summary>重复字段与无法识别的布尔值直接拒绝，不产生可被误用的部分解析结果。</summary>
        [Test]
        public void DuplicateOrInvalidLegacyBoolean_IsRejected()
        {
            string[] contents =
            {
                "    EnableMemoryTamperDetection: 0\n    EnableMemoryTamperDetection: 1\n",
                "    EnableMemoryKeyRotation: 1\n    EnableMemoryKeyRotation: 0\n",
                "    EnableMemoryTamperDetection: 2\n",
                "    EnableMemoryKeyRotation: enabled\n",
            };
            foreach (string content in contents)
            {
                TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
                    Parse("MonoBehaviour:\n  PrivacyConfigs:\n" + content, false));
                Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
            }
        }

        /// <summary>反射调用生产解析方法，输入只存在于内存，不通过文件系统制造旧资产。</summary>
        /// <param name="yaml">待验证的合成旧 YAML 字符串。</param>
        /// <param name="isMaster">是否采用设计态矩阵布局。</param>
        /// <returns>生产方法返回的旧隐私记录列表。</returns>
        private static IList Parse(string yaml, bool isMaster)
        {
            return (IList)Invoke("ParseLegacyPrivacy", yaml, isMaster);
        }

        /// <summary>读取生产私有记录中的字段，确保断言覆盖实际解析输出。</summary>
        /// <typeparam name="T">预期字段类型。</typeparam>
        /// <param name="record">生产解析记录实例。</param>
        /// <param name="name">待核对的真实字段名称。</param>
        /// <returns>记录的当前字段值。</returns>
        private static T Read<T>(object record, string name)
        {
            FieldInfo field = record.GetType().GetField(name, InstanceFields);
            Assert.That(field, Is.Not.Null, "旧隐私记录字段不存在：" + name);
            return (T)field.GetValue(record);
        }

        /// <summary>调用生产迁移器的私有纯解析或位置核对方法，不调用真实资产扫描和保存入口。</summary>
        /// <param name="name">待核对的生产私有静态方法名称。</param>
        /// <param name="arguments">该方法所需的确定输入。</param>
        /// <returns>生产方法返回值。</returns>
        private static object Invoke(string name, params object[] arguments)
        {
            MethodInfo method = typeof(MemoryProtectionLegacyConfigMigration).GetMethod(name, StaticPrivate);
            Assert.That(method, Is.Not.Null, "旧隐私迁移方法不存在：" + name);
            return method.Invoke(null, arguments);
        }
    }
}
