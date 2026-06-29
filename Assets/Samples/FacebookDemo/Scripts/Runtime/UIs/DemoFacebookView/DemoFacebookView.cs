namespace NovaFramework.Sdk.Facebook.Samples.Runtime
{
    /// <summary>
    /// Facebook SDK 示例界面入口，负责连接预制体控件并展示主要 SDK 流程。
    /// </summary>
    public sealed partial class DemoFacebookView : BaseDemoView
    {
        /// <summary>
        /// 初始化界面静态状态，并一次性绑定所有按钮回调。
        /// </summary>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            SetTitle("Facebook 演示");
            RegisterButtonHandlers();
            RefreshProfile(null);
        }

        /// <summary>
        /// 界面打开时输出当前示例支持的 Facebook 操作说明。
        /// </summary>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            AppendFeedback("Facebook 演示已打开，可登录、绑定、解绑、分享并读取好友列表。");
        }

        /// <summary>
        /// 关闭界面时不主动退出 Facebook 会话，解绑按钮单独负责登出。
        /// </summary>
        public override void OnClose(bool isShutdown, object userData)
        {
            base.OnClose(isShutdown, userData);
        }
    }
}
