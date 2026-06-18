/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SetLayerFromSettings.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{

    public class SetLayerFromSettings : DebugMonoBehaviour
    {
        private void Start()
        {
            gameObject.SetLayerRecursive(Settings.Instance.DebugLayer);
        }
    }
}
