// modify: local fork - 普通浏览器 WebGL 不执行微信小游戏启动逻辑。
#if UNITY_WEBGL || WEIXINMINIGAME || UNITY_EDITOR
using System;
using UnityEngine;
using WeChatWASM;

internal class CheckFrame : MonoBehaviour
{
    private int frameCnt = 0;

    public void Update()
    {
        frameCnt++;
        if (frameCnt == 2)
        {
#if (UNITY_WEBGL || WEIXINMINIGAME) && !UNITY_EDITOR
            WXSDKManagerHandler.Instance.HideLoadingPage();
#endif
            Destroy(this);
        }
    }
}

internal class HideLoadingPage : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnGameLaunch()
    {
        if (!NovaWechatRuntimeEnvironment.IsMiniGame)
        {
            return;
        }

        var gameObject = new GameObject("HideLoadingPage");
        gameObject.AddComponent<CheckFrame>();
        DontDestroyOnLoad(gameObject);
    }
}
#endif
