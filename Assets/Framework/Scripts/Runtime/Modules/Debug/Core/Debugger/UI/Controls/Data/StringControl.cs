/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  StringControl.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using System;
    using UnityEngine.UI;

    public class StringControl : DataBoundControl
    {
        [RequiredField] public InputField InputField;

        [RequiredField] public Text Title;

        protected override void Start()
        {
            base.Start();
#if UNITY_4_6 || UNITY_4_7 || UNITY_5_0 || UNITY_5_1 || UNITY_5_2
            InputField.onValueChange.AddListener(OnValueChanged);
#else
            InputField.onValueChanged.AddListener(OnValueChanged);
#endif
        }

        private void OnValueChanged(string newValue)
        {
            UpdateValue(newValue);
        }

        protected override void OnBind(string propertyName, Type t)
        {
            base.OnBind(propertyName, t);
            Title.text = propertyName;
            InputField.text = "";
            InputField.interactable = !IsReadOnly;
        }

        protected override void OnValueUpdated(object newValue)
        {
            var value = newValue == null ? "" : (string) newValue;
            InputField.text = value;
        }

        public override bool CanBind(Type type, bool isReadOnly)
        {
            return type == typeof (string) && !isReadOnly;
        }
    }
}
