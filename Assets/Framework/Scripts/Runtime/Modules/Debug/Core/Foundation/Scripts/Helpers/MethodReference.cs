/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MethodReference.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
using System;

namespace NovaFramework.Runtime
{
    using System.Reflection;

    public sealed class MethodReference
    {
        private readonly Func<object[], object> _method;

        public MethodReference(object target, MethodInfo method)
        {
            DebugAssert.AssertNotNull(target);

            _method = o => method.Invoke(target, o);
        }

        public MethodReference(Func<object[], object> method)
        {
            _method = method;
        }

        public object Invoke(object[] parameters)
        {
            return _method.Invoke(parameters);
        }

        public static implicit operator MethodReference(Action action)
        {
            return new MethodReference(args =>
            {
                action();
                return null;
            });
        }
    }
}
