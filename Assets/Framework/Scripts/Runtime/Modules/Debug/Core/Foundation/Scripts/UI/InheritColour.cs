/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  InheritColour.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;
    using UnityEngine.UI;

    [RequireComponent(typeof (Graphic))]
    [ExecuteInEditMode]
    [AddComponentMenu(ComponentMenuPaths.InheritColour)]
    public class InheritColour : DebugMonoBehaviour
    {
        private Graphic _graphic;
        public Graphic From;

        private Graphic Graphic
        {
            get
            {
                if (_graphic == null)
                {
                    _graphic = GetComponent<Graphic>();
                }

                return _graphic;
            }
        }

        private void Refresh()
        {
            if (From == null)
            {
                return;
            }

            Graphic.color = From.canvasRenderer.GetColor();
        }

        private void Update()
        {
            Refresh();
        }

        private void Start()
        {
            Refresh();
        }
    }
}
