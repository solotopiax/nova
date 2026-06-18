/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetFxExtensions.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
#if NETFX_CORE

using System;
using System.Reflection;

namespace NovaFramework.Runtime
{

	public static class NetFxExtensions 
	{


		public static bool IsAssignableFrom(this Type @this, Type t) 
		{
			
			return @this.GetTypeInfo().IsAssignableFrom(t.GetTypeInfo());

		}

		public static bool IsInstanceOfType(this Type @this, object obj)
		{

			return @this.IsAssignableFrom(obj.GetType());

		}

	}

}

#endif
