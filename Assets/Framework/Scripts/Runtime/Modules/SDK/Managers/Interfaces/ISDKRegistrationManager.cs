/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * filename: ISDKRegistrationManager.cs
 * author: Codex
 * created: 2026/10/08
 * descrip: SDK注册可选能力，不扩展既有ISDKManager实现要求
 ***************************************************************/
namespace NovaFramework.Runtime
{
    /// <summary>业务明确确认新用户时发送注册和一次登录。</summary>
    public interface ISDKRegistrationManager
    {
        /// <summary>每次有效调用均发送注册及一次登录，由调用方决定调用时机。</summary>
        void RegisterAndLogin(string userId);
    }
}
