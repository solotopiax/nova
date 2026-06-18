/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  InfoBlock.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine.UI;

    public class InfoBlock : DebugMonoBehaviourEx
    {
        [RequiredField] public Text Content;

        [RequiredField] public Text Title;
    }
}
