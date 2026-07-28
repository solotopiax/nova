using System;
using System.Collections.Generic;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// HTTP 后端注册表。供可选传输程序集注册后端工厂。
    /// </summary>
    public static class HttpTransportRegistry
    {
        private static readonly List<IHttpTransportFactory> s_Factories = new List<IHttpTransportFactory>();

        /// <summary>
        /// 注册 HTTP 后端工厂。同类型工厂重复注册时会被忽略。
        /// </summary>
        /// <param name="factory">HTTP 后端工厂。</param>
        public static void Register(IHttpTransportFactory factory)
        {
            if (factory == null)
            {
                return;
            }

            Type factoryType = factory.GetType();
            for (int i = 0; i < s_Factories.Count; i++)
            {
                IHttpTransportFactory registeredFactory = s_Factories[i];
                if (ReferenceEquals(registeredFactory, factory) || registeredFactory.GetType() == factoryType)
                {
                    return;
                }
            }

            s_Factories.Add(factory);
        }

        /// <summary>
        /// 创建最高优先级的已注册后端；没有可选后端或工厂创建失败时使用内置 UnityWebRequest。
        /// </summary>
        /// <returns>可用的 HTTP 传输实例。</returns>
        internal static IHttpTransport Create()
        {
            if (s_Factories.Count == 0)
            {
                return new UnityWebRequestTransport();
            }

            IHttpTransportFactory selectedFactory = s_Factories[0];
            for (int i = 1; i < s_Factories.Count; i++)
            {
                IHttpTransportFactory factory = s_Factories[i];
                if (factory.Priority > selectedFactory.Priority)
                {
                    selectedFactory = factory;
                }
            }

            return selectedFactory.Create() ?? new UnityWebRequestTransport();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetForSubsystemRegistration()
        {
            s_Factories.Clear();
        }
    }
}
