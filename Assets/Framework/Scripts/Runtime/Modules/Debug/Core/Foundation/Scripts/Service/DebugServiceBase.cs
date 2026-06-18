/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugServiceBase.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    public abstract class DebugServiceBase<T> : DebugMonoBehaviourEx where T : class
    {
        protected override void Awake()
        {
            base.Awake();
            DebugServiceRegistry.RegisterService<T>(this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            DebugServiceRegistry.UnRegisterService<T>();
        }
    }
}
