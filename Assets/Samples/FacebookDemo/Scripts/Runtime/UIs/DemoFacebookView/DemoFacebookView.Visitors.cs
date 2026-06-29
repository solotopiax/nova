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
    }
}
