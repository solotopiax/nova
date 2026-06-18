/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ReadOnlyControl.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using System;
    using UnityEngine.UI;

    public class ReadOnlyControl : DataBoundControl
    {
        [RequiredField]
        public Text ValueText;

        [RequiredField]
        public Text Title;

        protected override void Start()
        {
            base.Start();
        }

        protected override void OnBind(string propertyName, Type t)
        {
            base.OnBind(propertyName, t);
            Title.text = propertyName;
        }

        protected override void OnValueUpdated(object newValue)
        {
            ValueText.text = Convert.ToString(newValue);
        }

        public override bool CanBind(Type type, bool isReadOnly)
        {
            return type == typeof(string) && isReadOnly;
        }
    }
}
