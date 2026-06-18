/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  BestHttpTransportRegistration.cs
 * author:    taoye
 * created:   2026/6/15
 * descrip:   BestHTTP transport registration
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.BestHTTP.Runtime
{
    internal static class BestHttpTransportRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RegisterBestHttpTransport()
        {
            HttpTransportRegistry.Register(new BestHttpTransportFactory());
        }

        private sealed class BestHttpTransportFactory : IHttpTransportFactory
        {
            public int Priority => 100;

            public IHttpTransport Create()
            {
                return new BestHttpTransport();
            }
        }
    }
}
