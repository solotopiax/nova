/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ResourceRecordType.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   DNS资源记录类型
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// DNS 资源记录类型枚举。
    /// </summary>
    public enum ResourceRecordType : Int16
    {
        /// <summary>IPv4 地址记录。</summary>
        A = 1,
        /// <summary>IPv6 地址记录。</summary>
        AAAA = 28,
        /// <summary>IPv6 地址记录（旧版）。</summary>
        A6 = 38,
        /// <summary>AFS 数据库位置记录。</summary>
        AFSDB = 18,
        /// <summary>别名规范名称记录。</summary>
        CNAME = 5,
        /// <summary>授权委派名称记录。</summary>
        DNAME = 39,
        /// <summary>DNS 公钥记录。</summary>
        DNSKEY = 48,
        /// <summary>委派签名记录。</summary>
        DS = 43,
        /// <summary>MAC 地址（EUI-48）记录。</summary>
        EUI48 = 108,
        /// <summary>MAC 地址（EUI-64）记录。</summary>
        EUI64 = 109,
        /// <summary>主机信息记录。</summary>
        HINFO = 13,
        /// <summary>ISDN 地址记录。</summary>
        ISDN = 20,
        /// <summary>公钥记录（旧版）。</summary>
        KEY = 25,
        /// <summary>地理位置记录。</summary>
        LOC = 29,
        /// <summary>邮件交换记录。</summary>
        MX = 15,
        /// <summary>命名权限指针记录。</summary>
        NAPTR = 35,
        /// <summary>名称服务器记录。</summary>
        NS = 2,
        /// <summary>下一安全记录。</summary>
        NSEC = 47,
        /// <summary>下一域名记录（旧版）。</summary>
        NXT = 30,
        /// <summary>指针记录（反向 DNS）。</summary>
        PTR = 12,
        /// <summary>负责人记录。</summary>
        RP = 17,
        /// <summary>DNSSEC 资源记录签名。</summary>
        RRSIG = 46,
        /// <summary>路由穿越记录。</summary>
        RT = 21,
        /// <summary>签名记录（旧版）。</summary>
        SIG = 24,
        /// <summary>权威记录起始。</summary>
        SOA = 6,
        /// <summary>发件人策略框架记录。</summary>
        SPF = 99,
        /// <summary>服务定位记录。</summary>
        SRV = 33,
        /// <summary>文本记录。</summary>
        TXT = 16,
        /// <summary>统一资源标识符记录。</summary>
        URI = 256,
        /// <summary>众所周知服务记录。</summary>
        WKS = 11,
        /// <summary>X.25 地址记录。</summary>
        X25 = 19,
        /// <summary>通配查询所有记录类型。</summary>
        ALL = 255
    }
}
