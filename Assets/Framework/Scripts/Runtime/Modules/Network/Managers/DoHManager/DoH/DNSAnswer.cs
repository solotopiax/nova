/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DNSAnswer.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   DNS应答
 ***************************************************************/

using System;
using Newtonsoft.Json.Linq;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 单条 DNS 应答记录。
    /// </summary>
    public class DNSAnswer
    {
        /// <summary>
        /// 应答名称（域名）。
        /// </summary>
        public string Name;

        /// <summary>
        /// 有效期（秒）。
        /// </summary>
        public int TTL;

        /// <summary>
        /// 数据内容（IP 地址字符串 / CNAME 域名等）。
        /// </summary>
        public string Data;

        /// <summary>
        /// 资源记录类型。
        /// </summary>
        public ResourceRecordType RecordType;

        /// <summary>
        /// 从 JSON 节点解析出一条 DNSAnswer 实例。
        /// </summary>
        /// <param name="jsonAnswer">包含 name/type/TTL/data 字段的 JSON 对象。</param>
        /// <returns>解析出的 DNSAnswer 实例。</returns>
        internal static DNSAnswer FromJSON(JObject jsonAnswer)
        {
            string name = jsonAnswer["name"].ToString();
            ResourceRecordType recordType = (ResourceRecordType)Convert.ToInt32(jsonAnswer["type"].ToString());
            int ttl = Convert.ToInt32(jsonAnswer["TTL"].ToString());
            string data = jsonAnswer["data"].ToString();
            while (data.EndsWith("."))
            {
                data = data.Substring(0, data.Length - 1);
            }

            return new DNSAnswer { Name = name, RecordType = recordType, Data = data, TTL = ttl };
        }

        /// <summary>
        /// 返回可读字符串。
        /// </summary>
        /// <returns>格式为 "name:xxx,type:xxx,ttl:xxx,data:xxx" 的字符串。</returns>
        public override string ToString()
        {
            return $"name:{Name},type:{RecordType},ttl:{TTL},data:{Data}";
        }
    }
}
