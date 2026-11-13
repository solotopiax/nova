/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SpriteBindingLifetime.cs
 * author:    taoye
 * created:   2026/11/12
 * descrip:   SpriteRenderer 所在对象的原生销毁令牌宿主
 ***************************************************************/

using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 仅在显式创建 SpriteRenderer 绑定时挂到同一个 GameObject，为助手提供原生 destroyCancellationToken。
    /// 不参与 Update、禁用或资源加载，也不持有任何其他组件或 Manager。
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    internal sealed class SpriteBindingLifetime : MonoBehaviour { }
}
