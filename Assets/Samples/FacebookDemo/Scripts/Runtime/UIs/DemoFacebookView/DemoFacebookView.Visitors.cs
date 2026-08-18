using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Sdk.Facebook.Samples.Runtime
{
    public sealed partial class DemoFacebookView
    {
        /// <summary>
        /// 用户登录按钮。
        /// </summary>
        [SerializeField] private Button m_LoginButton;

        /// <summary>
        /// Facebook 绑定按钮。
        /// </summary>
        [SerializeField] private Button m_BindButton;

        /// <summary>
        /// Facebook 解绑按钮。
        /// </summary>
        [SerializeField] private Button m_UnbindButton;

        /// <summary>
        /// Facebook 分享按钮。
        /// </summary>
        [SerializeField] private Button m_ShareButton;

        /// <summary>
        /// 好友列表读取按钮。
        /// </summary>
        [SerializeField] private Button m_FriendsButton;

        /// <summary>
        /// 待发送到 Facebook 的打点事件名输入框。
        /// </summary>
        [SerializeField] private TMP_InputField m_EventNameInput;

        /// <summary>
        /// 待添加打点参数的 key 输入框。
        /// </summary>
        [SerializeField] private TMP_InputField m_EventParamKeyInput;

        /// <summary>
        /// 待添加打点参数的 value 输入框。
        /// </summary>
        [SerializeField] private TMP_InputField m_EventParamValueInput;

        /// <summary>
        /// 点击后将当前 key/value 输入加入待发送打点参数。
        /// </summary>
        [SerializeField] private Button m_AddEventParamButton;

        /// <summary>
        /// 点击后清空当前待发送打点参数。
        /// </summary>
        [SerializeField] private Button m_ClearEventParamsButton;

        /// <summary>
        /// 点击后将事件名和当前参数发送到 Facebook。
        /// </summary>
        [SerializeField] private Button m_SendEventButton;

        /// <summary>
        /// 当前待发送打点参数的预览文本。
        /// </summary>
        [SerializeField] private TextMeshProUGUI m_EventParamsPreviewText;

        /// <summary>
        /// Facebook 昵称展示文本。
        /// </summary>
        [SerializeField] private TextMeshProUGUI m_NameValueText;

        /// <summary>
        /// Facebook 用户 ID 展示文本。
        /// </summary>
        [SerializeField] private TextMeshProUGUI m_FacebookIdValueText;

        /// <summary>
        /// Facebook 头像展示图片。
        /// </summary>
        [SerializeField] private RawImage m_AvatarImage;

        /// <summary>
        /// Facebook 好友列表展示文本。
        /// </summary>
        [SerializeField] private TextMeshProUGUI m_FriendsListText;

        /// <summary>
        /// 当前头像运行时纹理，加载新头像前需要先销毁旧纹理。
        /// </summary>
        private Texture2D m_AvatarTexture;

        /// <summary>
        /// 最近一次 Facebook 登录成功后返回的用户 ID，绑定流程直接消费该值，不再重复拉起 Facebook 登录。
        /// </summary>
        private string m_CurrentFacebookId;

        /// <summary>
        /// 当前待发送到 Facebook 的示例打点参数。
        /// </summary>
        private readonly Dictionary<string, object> m_EventParams = new Dictionary<string, object>();
    }
}
