/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VersionTextBehaviour.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine.UI;

    public class VersionTextBehaviour : DebugMonoBehaviourEx
    {
        public string Format = "RuntimeDebugger {0}";

        [RequiredField] public Text Text;

        protected override void Start()
        {
            base.Start();

            Text.text = string.Format(Format, RuntimeDebugger.Version);
        }
    }
}
