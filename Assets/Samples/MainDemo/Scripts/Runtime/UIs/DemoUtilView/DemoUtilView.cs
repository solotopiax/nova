/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoUtilView.cs
 * author:    taoye
 * created:   2026/05/23
 * descrip:   Demo 1.6 — Util 高频静态工具集合演示
 *            三子卡片：
 *            Json 互转（Util.Json.Serialize / Deserialize）
 *            AES 加密（Util.Encrypt.AES.EncryptString / DecryptString）
 *            MD5 计算（Util.MD5.GetHashFromBytes）
 ***************************************************************/

using System.Text;
using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// Demo 1.6 Util 工具集合演示 View。
    /// 三子卡片：Json 互转 / AES 加密 / MD5 计算，各含输入框与操作按钮。
    /// </summary>
    public sealed class DemoUtilView : BaseDemoView
    {
        /// <summary>
        /// Json 子卡片：输入框，内容将被序列化为 JSON 字符串。
        /// </summary>

        [SerializeField] private TMP_InputField m_JsonInput;

        /// <summary>
        /// Json 子卡片：「序列化」按钮。
        /// </summary>

        [SerializeField] private Button m_JsonSerializeButton;

        /// <summary>
        /// AES 子卡片：待加密/解密的明文输入框。
        /// </summary>

        [SerializeField] private TMP_InputField m_AesInput;

        /// <summary>
        /// AES 子卡片：「加密」按钮，使用默认 key 加密。
        /// </summary>

        [SerializeField] private Button m_AesEncryptButton;

        /// <summary>
        /// AES 子卡片：「解密」按钮，解密最近一次加密结果。
        /// </summary>

        [SerializeField] private Button m_AesDecryptButton;

        /// <summary>
        /// MD5 子卡片：待计算哈希的字符串输入框。
        /// </summary>

        [SerializeField] private TMP_InputField m_Md5Input;

        /// <summary>
        /// MD5 子卡片：「计算 MD5」按钮。
        /// </summary>

        [SerializeField] private Button m_Md5HashButton;

        /// <summary>
        /// 最近一次 AES 加密的密文，供解密演示使用。
        /// </summary>
        private string m_LastCipherText = string.Empty;

        /// <summary>
        /// 演示用 AES 密钥（32 字节 ASCII），不含敏感数据。
        /// </summary>
        private const string c_DemoAesKey = "NovaDemo1234567890ABCDEF12345678";

        /// <summary>
        /// 初始化钩子，注册所有按钮事件。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            if (m_JsonSerializeButton != null)
            {
                m_JsonSerializeButton.onClick.AddListener(OnJsonSerializeClick);
                SetButtonApiHint(m_JsonSerializeButton, "Util.Json.Serialize(obj)");
            }

            if (m_AesEncryptButton != null)
            {
                m_AesEncryptButton.onClick.AddListener(OnAesEncryptClick);
                SetButtonApiHint(m_AesEncryptButton, "Util.Encrypt.AES.EncryptString(text, key)");
            }

            if (m_AesDecryptButton != null)
            {
                m_AesDecryptButton.onClick.AddListener(OnAesDecryptClick);
                SetButtonApiHint(m_AesDecryptButton, "Util.Encrypt.AES.DecryptString(cipher, key)");
            }

            if (m_Md5HashButton != null)
            {
                m_Md5HashButton.onClick.AddListener(OnMd5HashClick);
                SetButtonApiHint(m_Md5HashButton, "Util.MD5.GetHashFromBytes(bytes)");
            }
        }

        /// <summary>
        /// 打开钩子，设置标题、API 副标题，并预填默认输入值。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            SetTitle("Util 工具集合");

            if (m_JsonInput != null && string.IsNullOrEmpty(m_JsonInput.text))
            {
                m_JsonInput.text = "Hello Nova";
            }
            if (m_AesInput != null && string.IsNullOrEmpty(m_AesInput.text))
            {
                m_AesInput.text = "hello";
            }
            if (m_Md5Input != null && string.IsNullOrEmpty(m_Md5Input.text))
            {
                m_Md5Input.text = "hello";
            }
        }

        /// <summary>
        /// Json 「序列化」点击回调，将输入文本包装成匿名对象并序列化。
        /// </summary>
        private void OnJsonSerializeClick()
        {
            string raw = m_JsonInput != null ? m_JsonInput.text : "hello";
            var obj = new { value = raw, timestamp = System.DateTime.UtcNow.Ticks };
            string json = Util.Json.Serialize(obj);
            AppendFeedback($"Util.Json.Serialize(obj) -> {json.Replace('\n', ' ')}", FeedbackLevel.Success);
        }

        /// <summary>
        /// AES 「加密」点击回调，使用 c_DemoAesKey 加密明文并保存密文供解密。
        /// </summary>
        private void OnAesEncryptClick()
        {
            string plain = m_AesInput != null ? m_AesInput.text : "hello";
            if (string.IsNullOrEmpty(plain))
            {
                AppendFeedback("输入不能为空", FeedbackLevel.Warn);
                return;
            }

            m_LastCipherText = Util.Encrypt.AES.EncryptString(plain, c_DemoAesKey);
            AppendFeedback($"Util.Encrypt.AES.EncryptString(\"{plain}\") -> {m_LastCipherText}", FeedbackLevel.Success);
        }

        /// <summary>
        /// AES 「解密」点击回调，解密最近一次加密的密文。
        /// </summary>
        private void OnAesDecryptClick()
        {
            if (string.IsNullOrEmpty(m_LastCipherText))
            {
                AppendFeedback("请先执行加密操作", FeedbackLevel.Warn);
                return;
            }

            string plain = Util.Encrypt.AES.DecryptString(m_LastCipherText, c_DemoAesKey);
            AppendFeedback($"Util.Encrypt.AES.DecryptString(cipher) -> \"{plain}\"", FeedbackLevel.Success);
        }

        /// <summary>
        /// MD5 「计算 MD5」点击回调，对输入字符串的 UTF8 字节计算哈希值。
        /// </summary>
        private void OnMd5HashClick()
        {
            string input = m_Md5Input != null ? m_Md5Input.text : "hello";
            if (string.IsNullOrEmpty(input))
            {
                AppendFeedback("输入不能为空", FeedbackLevel.Warn);
                return;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(input);
            string hash = Util.MD5.GetHashFromBytes(bytes);
            AppendFeedback($"Util.MD5.GetHashFromBytes(\"{input}\") -> {hash}", FeedbackLevel.Success);
        }
    }
}
