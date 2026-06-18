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

        internal static IHttpTransport Create()
        {
            if (s_Factories.Count == 0)
            {
                return MissingHttpTransport.Instance;
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

            return selectedFactory.Create() ?? MissingHttpTransport.Instance;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetForSubsystemRegistration()
        {
            s_Factories.Clear();
        }
    }
}
