/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AgentActionLockManager.cs
 * author:    taoye
 * created:   2026/8/19
 * descrip:   Action 进程内资源锁；不使用 Unity 批锁 API
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;

namespace NovaFramework.Editor
{
    internal static class AgentActionLockManager
    {
        private static readonly object s_Gate = new object();
        private static readonly HashSet<string> s_HeldLocks = new HashSet<string>(StringComparer.Ordinal);

        public static bool TryAcquire(IEnumerable<string> resources, out IDisposable lease)
        {
            string[] normalized = (resources ?? Array.Empty<string>())
                .Where(resource => !string.IsNullOrWhiteSpace(resource))
                .Select(resource => resource.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(resource => resource, StringComparer.Ordinal)
                .ToArray();

            lock (s_Gate)
            {
                if (normalized.Any(resource => s_HeldLocks.Contains(resource)))
                {
                    lease = null;
                    return false;
                }

                foreach (string resource in normalized)
                {
                    s_HeldLocks.Add(resource);
                }
            }

            lease = new Lease(normalized);
            return true;
        }

        private sealed class Lease : IDisposable
        {
            private readonly string[] m_Resources;
            private bool m_Disposed;

            public Lease(string[] resources)
            {
                m_Resources = resources;
            }

            public void Dispose()
            {
                if (m_Disposed)
                {
                    return;
                }

                lock (s_Gate)
                {
                    foreach (string resource in m_Resources)
                    {
                        s_HeldLocks.Remove(resource);
                    }
                }

                m_Disposed = true;
            }
        }
    }
}
