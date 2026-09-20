// modify: local fork - 普通浏览器 WebGL 保持项目原有键盘输入设置。
#if UNITY_WEBGL || WEIXINMINIGAME || UNITY_EDITOR
using System;
using UnityEngine;
using WeChatWASM;


internal class DisableKeyboardInput : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnGameLaunch()
    {
        if (!NovaWechatRuntimeEnvironment.IsMiniGame)
        {
            return;
        }

#if !UNITY_EDITOR
#if PLATFORM_PLAYABLEADS
        PlayableAdsInput.mobileKeyboardSupport = false;
#elif PLATFORM_WEIXINMINIGAME
        WeixinMiniGameInput.mobileKeyboardSupport = false;
#elif PLATFORM_WEBGL
#if UNITY_2022_1_OR_NEWER
        WebGLInput.mobileKeyboardSupport = false;
#endif
#endif
#endif
    }
}
#endif
