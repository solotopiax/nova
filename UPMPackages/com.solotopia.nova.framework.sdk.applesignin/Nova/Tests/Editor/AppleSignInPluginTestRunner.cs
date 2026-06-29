using System;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.SDK.AppleSignIn.Tests.Editor
{
    /// <summary>
    /// Apple 登录插件命令行测试运行器。
    /// </summary>
    public static class AppleSignInPluginTestRunner
    {
        /// <summary>
        /// 执行 Apple 登录插件断言。
        /// </summary>
        public static void Run()
        {
            AssertConfigDefaults();
            AssertUserDataMapsUserId();
            AssertUserDataFailsWhenUserIdMissing();
            AssertLoginBeforeInitializeReturnsFailure();
            Debug.Log("AppleSignInPluginTestRunner OK");
        }

        /// <summary>
        /// 校验默认配置。
        /// </summary>
        private static void AssertConfigDefaults()
        {
            var config = new AppleSignInPluginConfig();

            AssertTrue(config.RequestFullName, nameof(config.RequestFullName));
            AssertEqual("Apple", config.DisplayName, nameof(config.DisplayName));
        }

        /// <summary>
        /// 校验 Apple 用户 ID 映射。
        /// </summary>
        private static void AssertUserDataMapsUserId()
        {
            var data = new AppleSignInUserData("apple-user", "User");

            AuthResult result = data.ToAuthResult("Apple");

            AssertTrue(result.Success, nameof(result.Success));
            AssertEqual("apple-user", result.UserId, nameof(result.UserId));
            AssertNull(result.Token, nameof(result.Token));
            AssertEqual("Apple", result.Provider, nameof(result.Provider));
            AssertNull(result.ErrorMessage, nameof(result.ErrorMessage));
        }

        /// <summary>
        /// 校验缺字段失败。
        /// </summary>
        private static void AssertUserDataFailsWhenUserIdMissing()
        {
            var data = new AppleSignInUserData(string.Empty, "User");

            AuthResult result = data.ToAuthResult("Apple");

            AssertFalse(result.Success, nameof(result.Success));
            AssertEqual("Apple", result.Provider, nameof(result.Provider));
            AssertNotEmpty(result.ErrorMessage, nameof(result.ErrorMessage));
        }

        /// <summary>
        /// 校验未初始化登录失败。
        /// </summary>
        private static void AssertLoginBeforeInitializeReturnsFailure()
        {
            var plugin = new AppleSignInPlugin();

            AuthResult result = plugin.LoginAsync("Apple").GetAwaiter().GetResult();

            AssertFalse(result.Success, nameof(result.Success));
            AssertEqual("Apple", result.Provider, nameof(result.Provider));
            AssertNotEmpty(result.ErrorMessage, nameof(result.ErrorMessage));
        }

        /// <summary>
        /// 断言为真。
        /// </summary>
        /// <param name="value">实际值。</param>
        /// <param name="name">断言名。</param>
        private static void AssertTrue(bool value, string name)
        {
            if (!value) throw new InvalidOperationException($"{name} expected true.");
        }

        /// <summary>
        /// 断言为假。
        /// </summary>
        /// <param name="value">实际值。</param>
        /// <param name="name">断言名。</param>
        private static void AssertFalse(bool value, string name)
        {
            if (value) throw new InvalidOperationException($"{name} expected false.");
        }

        /// <summary>
        /// 断言相等。
        /// </summary>
        /// <param name="expected">期望值。</param>
        /// <param name="actual">实际值。</param>
        /// <param name="name">断言名。</param>
        private static void AssertEqual(string expected, string actual, string name)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"{name} expected '{expected}', got '{actual}'.");
            }
        }

        /// <summary>
        /// 断言为空。
        /// </summary>
        /// <param name="value">实际值。</param>
        /// <param name="name">断言名。</param>
        private static void AssertNull(string value, string name)
        {
            if (value != null) throw new InvalidOperationException($"{name} expected null.");
        }

        /// <summary>
        /// 断言非空。
        /// </summary>
        /// <param name="value">实际值。</param>
        /// <param name="name">断言名。</param>
        private static void AssertNotEmpty(string value, string name)
        {
            if (string.IsNullOrEmpty(value)) throw new InvalidOperationException($"{name} expected not empty.");
        }
    }
}
