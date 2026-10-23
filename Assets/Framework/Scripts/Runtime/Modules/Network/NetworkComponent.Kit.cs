/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetworkComponent.Kit.cs
 * author:    taoye
 * created:   2026/5/26
 * descrip:   NetworkComponent Kit Service 惰性单例容器（partial）
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Google.Protobuf;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// NetworkComponent Kit 扩展分部。
    /// 提供 Kit Service 惰性单例容器，子包（Login/Account 等）的 Service 类通过此入口获取实例。
    /// </summary>
    public sealed partial class NetworkComponent : FrameworkComponent
    {
        /// <summary>
        /// 获取或惰性创建指定 Kit Service 的单例实例。
        /// 子包（Login/Account/...）的 Service 类必须满足 class + 无参构造约束。
        /// 不持有 Kit 层 asmdef 引用，不形成主框架 → Kit 的反向依赖。
        /// </summary>
        /// <typeparam name="T">Kit Service 类型，必须为 class 且具备无参构造器。</typeparam>
        /// <returns>对应 Service 的单例实例。</returns>
        public T Kit<T>() where T : class, new()
        {
            Type t = typeof(T);
            if (!m_KitInstances.TryGetValue(t, out object inst))
            {
                inst = new T();
                m_KitInstances[t] = inst;
            }
            return (T)inst;
        }

        /// <summary>
        /// 从项目自有业务 Service 发送标准 Protobuf + AES 请求，复用既有主备、重试与响应解析链。
        /// 调用前须完成 Nova 的 Start 绑定、Config 加载与 Network 路由加载；不自动初始化或加载。
        /// </summary>
        /// <typeparam name="TReq">项目正式生成的请求消息类型。</typeparam>
        /// <typeparam name="TResp">项目正式生成的响应消息类型。</typeparam>
        /// <param name="cmdRow">已确认协议对应的 HTTP_POST NetCmd 行；不能为 null，不接受调用方自定主备 URL。</param>
        /// <param name="requestFactory">
        /// 仅调用一次的同步请求工厂；将传入的标准 Header 显式绑定到协议规定的 Head 字段，并填写业务字段。
        /// 工厂不得发送请求、修改 Header 身份或包含其他业务副作用；返回消息不能为 null。
        /// </param>
        /// <param name="parser">正式响应消息的 Parser；不能为 null。</param>
        /// <param name="cancellationToken">默认不主动取消；取消会终止后续主备尝试，不能撤销已到达服务端的业务。</param>
        /// <returns>
        /// 既有 NetResponse；URL、AES、HTTP、协议和业务错误沿用 NetService 的错误码与响应数据语义。
        /// 不更新 UID/OpenID、不登录、不重发业务错误、不返回需调用方归还的 HttpResponse。
        /// </returns>
        /// <exception cref="ArgumentNullException">cmdRow、requestFactory 或 parser 为 null。</exception>
        /// <exception cref="ArgumentException">指令行的 Way 不是 HTTP_POST。</exception>
        /// <exception cref="InvalidOperationException">运行入口、路由或配置尚未就绪，HTTP 管理器不支持业务主备链，或工厂返回 null。</exception>
        /// <exception cref="OperationCanceledException">委托既有请求链前发现令牌已取消。</exception>
        public UniTask<NetResponse<TResp>> SendProtoAsync<TReq, TResp>(
            INetworkCmdRow cmdRow,
            Func<PbNetReqHeader, TReq> requestFactory,
            MessageParser<TResp> parser,
            CancellationToken cancellationToken = default)
            where TReq : class, IMessage<TReq>
            where TResp : class, IMessage<TResp>
        {
            if (cmdRow == null)
            {
                throw new ArgumentNullException(nameof(cmdRow));
            }
            if (requestFactory == null)
            {
                throw new ArgumentNullException(nameof(requestFactory));
            }
            if (parser == null)
            {
                throw new ArgumentNullException(nameof(parser));
            }
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.Equals(cmdRow.Way, "HTTP_POST", StringComparison.Ordinal))
            {
                throw new ArgumentException("标准 Protobuf 请求必须使用 HTTP_POST 指令行。", nameof(cmdRow));
            }

            if (!IsLoadOver || m_NetworkManager == null || !(m_HttpManager is IBusinessHttpManager))
            {
                throw new InvalidOperationException("请先完成 Network 路由加载，并使用支持业务主备链的 HTTP 管理器。");
            }

            IConfigManager configManager = FrameworkManagersGroup.GetManager<IConfigManager>();
            if (configManager == null || !configManager.IsLoadOver || configManager.AppConfigs == null ||
                Nova.Network != this || Nova.Config == null || Nova.SDK == null || Nova.Localization == null)
            {
                throw new InvalidOperationException("请先完成 Nova 的 Start 绑定与 Config 加载，再发送标准 Protobuf 请求。");
            }

            // Header 由原构建器取得当前确认身份；业务只绑定正式消息，不反射写 Head 或重建协议算法。
            PbNetReqHeader header = NetBuilder.BuildHeader();
            cancellationToken.ThrowIfCancellationRequested();
            TReq request = requestFactory(header);
            if (request == null)
            {
                throw new InvalidOperationException("标准 Protobuf 请求工厂不能返回 null。");
            }
            cancellationToken.ThrowIfCancellationRequested();

            return NetService.SendAsync(cmdRow, request, parser, cancellationToken);
        }
    }
}
