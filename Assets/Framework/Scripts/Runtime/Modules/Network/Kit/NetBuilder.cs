/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetBuilder.cs
 * author:    taoye
 * created:   2026/5/26
 * descrip:   网络请求构建静态工具：Header 构建、Proto 序列化、AES 加密、Header JSON 构建
 ***************************************************************/

using System;
using System.ComponentModel;
using System.Text;
using Google.Protobuf;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 网络请求构建静态工具类。
    /// 承接所有「构建/加密」职责：Header 构建、Proto Body 序列化、AES 加密、Header JSON 构建。
    /// 所有字段在运行时自动从 Nova.Config.Common / Nova.SDK 读取，业务层无需手动注入配置。
    /// 渠道（Channel）由 Nova.Config.Channel 取得，在 BuildHeader() 内通过 InferChannel() 自动填充，业务 Service 无需感知。
    /// 仅供 Network 子包使用，业务侧请通过 Login 等业务 Service 接入。
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class NetBuilder
    {
        /// <summary>
        /// 构建标准请求 Header。
        /// AppId 来自 Nova.Config.Common.AppID（int.TryParse 失败时 Log.Warning + 回退 0）。
        /// DeviceId 来自 Nova.SDK.TryGet&lt;IDeviceIdProvider&gt;()（未注册时回退空串）。
        /// Uid 来自 NetService.Uid（登录后由 Login 写回）。
        /// Version / Language / Platform / Channel 在运行时自动内联取值。
        /// Channel 由 InferChannel() 从 Nova.Config.Channel 映射，业务 Service 无需传入。
        /// </summary>
        /// <returns>填充了全部公共字段的 PbNetReqHeader 实例。</returns>
        public static PbNetReqHeader BuildHeader()
        {
            int appId = 0;
            if (!int.TryParse(Nova.Config.Common.AppID, out appId))
            {
                Log.Warning(LogTag.Network, "NetBuilder.BuildHeader：Nova.Config.Common.AppID 无法解析为 int32，已回退为 0。请检查 ConfigMasterSO 中的 AppID 配置。");
            }

            string deviceId = string.Empty;
            if (Nova.SDK.TryGet<IDeviceIdProvider>(out IDeviceIdProvider deviceIdProvider))
            {
                deviceId = deviceIdProvider.GetDeviceID() ?? string.Empty;
            }

            return new PbNetReqHeader
            {
                AppId = appId,
                Version = Application.version,
                Language = LanguageMetadata.GetFlag(Nova.Localization.Language),
                DeviceId = deviceId,
                Platform = InferPlatform(),
                Channel = InferChannel(),
                Uid = NetService.Uid
            };
        }

        /// <summary>
        /// 将 Protobuf 消息 Body 序列化为字节数组。
        /// </summary>
        /// <typeparam name="T">请求 Proto 消息类型。</typeparam>
        /// <param name="body">请求 Proto 消息实例。</param>
        /// <returns>序列化后的字节数组。</returns>
        public static byte[] SerializeBody<T>(T body) where T : IMessage<T>
        {
            return body.ToByteArray();
        }

        /// <summary>
        /// 使用 AES-128-CBC + PKCS7 加密原始字节数据。
        /// 委托框架层 Util.Encrypt.AES 实现，本方法仅做职责归属封装。
        /// </summary>
        /// <param name="plainBytes">待加密的明文字节数组。</param>
        /// <param name="key">AES 密钥（16 字节 UTF-8 字符串）。</param>
        /// <param name="iv">AES 初始向量（16 字节 UTF-8 字符串）。</param>
        /// <returns>加密后的密文字节数组。</returns>
        public static byte[] Encrypt(byte[] plainBytes, string key, string iv)
        {
            return Util.Encrypt.AES.EncryptBytes(plainBytes, key, iv);
        }

        /// <summary>
        /// 构建正式环境请求 Header JSON 字符串。
        /// 格式：{"app_id":123,"Encoding-Aes":"Base64(iv)"}
        /// </summary>
        /// <param name="appId">应用 ID（int32，直接作为 JSON 数字字段输出）。</param>
        /// <param name="aesIV">AES 初始向量字符串，将被 UTF-8 编码后转 Base64。</param>
        /// <returns>正式环境请求 Header JSON 字符串。</returns>
        public static string BuildHeaderInfos(int appId, string aesIV)
        {
            string encodingAes = Convert.ToBase64String(Encoding.UTF8.GetBytes(aesIV));
            return $"{{\"app_id\":{appId},\"Encoding-Aes\":\"{encodingAes}\"}}";
        }

        /// <summary>
        /// 构建调试环境请求 Header JSON 字符串（跳过 AES 加密标记）。
        /// 格式：{"app_id":123,"X-Debug-Plain":"true"}
        /// </summary>
        /// <param name="appId">应用 ID（int32，直接作为 JSON 数字字段输出）。</param>
        /// <returns>调试用请求 Header JSON 字符串。</returns>
        public static string BuildDebugHeaderInfos(int appId)
        {
            return $"{{\"app_id\":{appId},\"X-Debug-Plain\":\"true\"}}";
        }

        /// <summary>
        /// 根据 Application.platform 推断对应的 PbNetPlatform 枚举值。
        /// 未能匹配的平台一律返回 Unspecified。
        /// </summary>
        /// <returns>推断得到的 PbNetPlatform 值。</returns>
        private static PbNetPlatform InferPlatform()
        {
            return Application.platform switch
            {
                RuntimePlatform.IPhonePlayer => PbNetPlatform.Ios,
                RuntimePlatform.Android => PbNetPlatform.Android,
                RuntimePlatform.WebGLPlayer => PbNetPlatform.Webgl,
                _ => PbNetPlatform.Unspecified,
            };
        }

        /// <summary>
        /// 将 Nova.Config.Channel（ChannelType）映射为 PbNetChannel 枚举值。
        /// 渠道来源为 Nova.Config.Channel，在 BuildHeader() 内自动调用，业务 Service 无需传入渠道参数。
        /// ChannelType.TikTok / Official / Alipay 等在 PbNetChannel 无对应值，统一返回 Unspecified。
        /// </summary>
        /// <returns>对应的 PbNetChannel 枚举值，无匹配时返回 Unspecified。</returns>
        private static PbNetChannel InferChannel()
        {
            return Nova.Config.Channel switch
            {
                ChannelType.Google => PbNetChannel.Google,
                ChannelType.Apple => PbNetChannel.Apple,
                ChannelType.WeChat => PbNetChannel.Wechat,
                _ => PbNetChannel.Unspecified,
            };
        }
    }
}
