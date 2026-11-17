/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Config.PanelEditing.PrivacyInitialization.cs
 * author:    taoye
 * created:   2026/11/16
 * descrip:   Privacy 本地 AES 首次准备的逻辑组检查与受控字段请求
 ***************************************************************/

using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NovaFramework.Runtime;
using Coord = NovaFramework.Editor.EditorUtil.Config.DimensionProjector.Coord;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Config
        {
            internal static partial class PanelEditing
            {
                // 仅这两个已公开发布的脱敏占位允许被首次准备替换，不能把任意 YOUR_ 文本视为未使用旧值。
                private const string c_PublicPrivacyKey = "YOUR_PRIVACY_AES_KEY";
                private const string c_PublicPrivacyIv = "YOUR_PRIVACY_AES_IV";

                /// <summary>首次准备只接受一个完整 Privacy 坐标；不接受配置类型、维度调整或其它面板。</summary>
                internal static void ValidatePrivacyInitializationTarget(Target target)
                {
                    ValidateTarget(target);
                    if (target.panel != "PrivacyConfigs" || target.configType != null)
                        throw new InvalidOperationException("首次准备只允许 PrivacyConfigs，且不能包含 configType。");
                }

                /// <summary>只读核完整物理同组后创建内部字段请求；秘密只供当前 Plan，不能作为持久化恢复请求。</summary>
                /// <param name="master">未修改的真实 Master；此方法不补结构、不保存或导出。</param>
                /// <param name="target">三轴完整且不含类型的唯一 Privacy 面板目标。</param>
                internal static Change[] CreatePrivacyInitializationChanges(ConfigMasterSO master, Target target)
                {
                    ValidatePrivacyInitializationTarget(target);
                    if (master == null) throw new ArgumentNullException(nameof(master));
                    Coord coordinate = Coordinate(target);
                    PanelDimensionMask mask = Mask(master, EditablePanel.PrivacyConfigs, null);
                    Coord[] group = Coordinates(master).Where(item => SameGroup(mask, coordinate, item)).ToArray();
                    if (!group.Any(item => item.Platform == coordinate.Platform && item.Channel == coordinate.Channel && item.Mode == coordinate.Mode))
                        throw new InvalidOperationException("首次准备的 Privacy 坐标不存在。");
                    PrivacyConfigs first = ReadPrivacyInitializationSource(master, group[0]);
                    string key = first.AESKey ?? string.Empty;
                    string iv = first.AESIV ?? string.Empty;
                    // 任何同组差异都先阻断，不能利用保存归一把可能已使用的旧凭据覆盖成当前格。
                    foreach (Coord member in group)
                    {
                        PrivacyConfigs value = ReadPrivacyInitializationSource(master, member);
                        if ((value.AESKey ?? string.Empty) != key || (value.AESIV ?? string.Empty) != iv)
                            throw new InvalidOperationException("Privacy 同一逻辑组存在不同旧值，首次准备不能合并或覆盖；请核对来源。");
                    }
                    bool valid = Encoding.UTF8.GetByteCount(key) == 16 && Encoding.UTF8.GetByteCount(iv) == 16;
                    if (!valid)
                    {
                        if ((!string.IsNullOrEmpty(key) && key != c_PublicPrivacyKey) ||
                            (!string.IsNullOrEmpty(iv) && iv != c_PublicPrivacyIv))
                            throw new InvalidOperationException("Privacy 含非公开占位的旧值，首次准备不能更换；需要核对已有数据与维护契约。");
                        key = GenerateInitialPrivacySecret();
                        iv = GenerateInitialPrivacySecret();
                    }
                    // 已有效时仍走相同字段预演，既有完整归一、精确写集和有效旧值保护共同证明 no-op。
                    return new[] { new Change
                    {
                        target = CopyTarget(target, coordinate),
                        fields = new[]
                        {
                            new FieldEdit { path = "AESKey", valueJson = Util.Json.Serialize(key) },
                            new FieldEdit { path = "AESIV", valueJson = Util.Json.Serialize(iv) },
                        },
                    } };
                }

                /// <summary>只读定位已有唯一物理 Privacy 槽，拒绝空行、重复模式和缺失配置，不调用补结构 Getter。</summary>
                private static PrivacyConfigs ReadPrivacyInitializationSource(ConfigMasterSO master, Coord coordinate)
                {
                    if (!master.TryGetEntry(coordinate.Platform, coordinate.Channel, out PlatformChannelEntry row) || row.PrivacyConfigsByMode == null)
                        throw new InvalidOperationException("Privacy 物理配置行不存在。");
                    DevelopModePrivacyConfigsEntry[] entries = row.PrivacyConfigsByMode.Where(item => item != null && item.Mode == coordinate.Mode).ToArray();
                    if (entries.Length != 1 || entries[0].Config == null)
                        throw new InvalidOperationException("Privacy 物理配置槽缺失或不唯一。");
                    return entries[0].Config;
                }

                /// <summary>使用无偏密码学随机可打印 ASCII 适配既有 UTF-8 16 字节契约；不读取或复用应用协议密钥。</summary>
                private static string GenerateInitialPrivacySecret()
                {
                    byte[] raw = new byte[64];
                    char[] chars = new char[16];
                    using var random = RandomNumberGenerator.Create();
                    try
                    {
                        int count = 0;
                        while (count < chars.Length)
                        {
                            random.GetBytes(raw);
                            foreach (byte value in raw)
                            {
                                if (value >= 188) continue;
                                chars[count++] = (char)(33 + value % 94);
                                if (count == chars.Length) break;
                            }
                        }
                        return new string(chars);
                    }
                    finally { Array.Clear(raw, 0, raw.Length); Array.Clear(chars, 0, chars.Length); }
                }
            }
        }
    }
}
