namespace NovaFramework.Runtime
{
    /// <summary>
    /// HTTP 后端工厂。可选传输程序集通过 HttpTransportRegistry.Register 注册工厂。
    /// </summary>
    public interface IHttpTransportFactory
    {
        /// <summary>
        /// 后端优先级，数值越大越优先。
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// 创建一个 HTTP 后端实例。
        /// </summary>
        IHttpTransport Create();
    }
}
