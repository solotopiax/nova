using System.IO;
using NovaFramework.SDK.AppleSignIn;
using NUnit.Framework;

namespace NovaFramework.SDK.AppleSignIn.Tests.Editor
{
    /// <summary>
    /// Apple 登录插件编辑器测试。
    /// </summary>
    public sealed class AppleSignInPluginTests
    {
        /// <summary>
        /// 验证默认配置只请求姓名。
        /// </summary>
        [Test]
        public void Config_DefaultsRequestFullNameOnly()
        {
            var config = new AppleSignInPluginConfig();

            Assert.IsTrue(config.RequestFullName);
            Assert.AreEqual("Apple", config.DisplayName);
        }

        /// <summary>
        /// 验证 Apple 用户 ID 映射到 Nova 登录结果。
        /// </summary>
        [Test]
        public void UserData_ToAuthResultMapsUserId()
        {
            var data = new AppleSignInUserData("apple-user", "User");

            var result = data.ToAuthResult("Apple");

            Assert.IsTrue(result.Success);
            Assert.AreEqual("apple-user", result.UserId);
            Assert.IsNull(result.Token);
            Assert.AreEqual("Apple", result.Provider);
            Assert.IsNull(result.ErrorMessage);
        }

        /// <summary>
        /// 验证用户标识缺失时返回失败。
        /// </summary>
        [Test]
        public void UserData_ToAuthResultFailsWhenUserIdMissing()
        {
            var data = new AppleSignInUserData(string.Empty, "User");

            var result = data.ToAuthResult("Apple");

            Assert.IsFalse(result.Success);
            Assert.AreEqual("Apple", result.Provider);
            Assert.IsNotEmpty(result.ErrorMessage);
        }

        /// <summary>
        /// 验证未初始化时登录返回失败。
        /// </summary>
        [Test]
        public void LoginAsync_BeforeInitializeReturnsFailure()
        {
            var plugin = new AppleSignInPlugin();

            var result = plugin.LoginAsync("Apple").GetAwaiter().GetResult();

            Assert.IsFalse(result.Success);
            Assert.AreEqual("Apple", result.Provider);
            Assert.IsNotEmpty(result.ErrorMessage);
        }

        [Test]
        public void PluginPartialFiles_FollowStorageRules()
        {
            string mainSource = ReadPackageFile("UPMPackages/com.solotopia.nova.framework.sdk.applesignin/Nova/Scripts/Runtime/AppleSignInPlugin.cs");
            string methodsSource = ReadPackageFile("UPMPackages/com.solotopia.nova.framework.sdk.applesignin/Nova/Scripts/Runtime/AppleSignInPlugin.Methods.cs");
            string visitorsSource = ReadPackageFile("UPMPackages/com.solotopia.nova.framework.sdk.applesignin/Nova/Scripts/Runtime/AppleSignInPlugin.Visitors.cs");

            StringAssert.Contains("public async UniTask<AuthResult> LoginAsync", mainSource);
            StringAssert.Contains("public UniTask LogoutAsync", mainSource);
            StringAssert.DoesNotContain("protected override", mainSource);
            StringAssert.DoesNotContain("public override string Name", mainSource);
            StringAssert.DoesNotContain("public bool IsLoggedIn", mainSource);

            StringAssert.Contains("protected override UniTask OnInitializeAsync", methodsSource);
            StringAssert.Contains("protected override UniTask OnDisposeAsync", methodsSource);
            StringAssert.Contains("private void SetLoginState", methodsSource);
            StringAssert.DoesNotContain("RestoreAsync", methodsSource);

            StringAssert.Contains("private AppleSignInUserData m_CurrentUserData", visitorsSource);
            StringAssert.Contains("public AppleSignInUserData CurrentUserData", visitorsSource);
            StringAssert.DoesNotContain("public string CurrentUserId", visitorsSource);
            StringAssert.DoesNotContain("public string CurrentIdentityToken", visitorsSource);
            StringAssert.DoesNotContain("public string CurrentAuthorizationCode", visitorsSource);
            StringAssert.DoesNotContain("public string CurrentEmail", visitorsSource);
            StringAssert.DoesNotContain("public string CurrentFullName", visitorsSource);
            StringAssert.DoesNotContain("public string CurrentRealUserStatus", visitorsSource);
            StringAssert.DoesNotContain("private string m_CurrentUserId", visitorsSource);
            StringAssert.DoesNotContain("private string m_CurrentIdentityToken", visitorsSource);
            StringAssert.DoesNotContain("private bool m_IsLoggedIn", visitorsSource);
        }

        [Test]
        public void SampleView_ExposesLoginLogoutAndCurrentUserButtons()
        {
            string view = ReadPackageFile("Assets/Samples/AppleSigninDemo/Scripts/Runtime/UIs/DemoAppleSigninView/DemoAppleSigninView.cs");
            string methods = ReadPackageFile("Assets/Samples/AppleSigninDemo/Scripts/Runtime/UIs/DemoAppleSigninView/DemoAppleSigninView.Methods.cs");
            string visitors = ReadPackageFile("Assets/Samples/AppleSigninDemo/Scripts/Runtime/UIs/DemoAppleSigninView/DemoAppleSigninView.Visitors.cs");
            string builder = ReadPackageFile("Assets/Samples/AppleSigninDemo/Scripts/Editor/DemoAppleSigninViewPrefabBuilder.cs");

            StringAssert.Contains("m_LoginButton", visitors);
            StringAssert.Contains("m_LogoutButton", visitors);
            StringAssert.Contains("m_CurrentUserButton", visitors);
            StringAssert.Contains("private AppleSignInPlugin m_Plugin", visitors);
            StringAssert.DoesNotContain("m_SampleButton", visitors);

            StringAssert.Contains("OnLoginButtonClick", view);
            StringAssert.Contains("OnLogoutButtonClick", view);
            StringAssert.Contains("OnCurrentUserButtonClick", view);
            StringAssert.Contains("AppleSignInPlugin.LoginAsync(\"Apple\")", view);
            StringAssert.Contains("AppleSignInPlugin.LogoutAsync()", view);
            StringAssert.Contains("AppleSignInPlugin.CurrentUserData", view);

            StringAssert.Contains("plugin.LoginAsync(\"Apple", methods);
            StringAssert.Contains("plugin.LogoutAsync", methods);
            StringAssert.Contains("plugin.CurrentUserData", methods);
            StringAssert.Contains("TryGetAppleSignInPlugin(out AppleSignInPlugin plugin)", methods);
            StringAssert.Contains("Nova.SDK.TryGet(out plugin)", methods);
            StringAssert.DoesNotContain("new AppleSignInPluginConfig()", methods);
            StringAssert.DoesNotContain("new AppleSignInPlugin()", methods);
            StringAssert.DoesNotContain("DisposeAsync(CancellationToken.None)", methods);
            StringAssert.DoesNotContain("CurrentUserId", methods);
            StringAssert.DoesNotContain("CurrentEmail", methods);
            StringAssert.DoesNotContain("userData.Email", methods);
            StringAssert.DoesNotContain("RealUserStatus", methods);

            StringAssert.Contains("m_LoginButton", builder);
            StringAssert.Contains("m_LogoutButton", builder);
            StringAssert.Contains("m_CurrentUserButton", builder);
            StringAssert.DoesNotContain("m_SampleButton", builder);
        }

        private static string ReadPackageFile(string relativePath)
        {
            string directory = TestContext.CurrentContext.TestDirectory;
            while (!string.IsNullOrEmpty(directory))
            {
                string candidate = Path.Combine(directory, relativePath);
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }

                directory = Directory.GetParent(directory)?.FullName;
            }

            return File.ReadAllText(relativePath);
        }
    }
}
