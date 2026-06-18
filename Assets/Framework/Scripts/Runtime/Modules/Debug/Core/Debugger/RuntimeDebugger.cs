/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  RuntimeDebugger.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR || UNITY_STANDALONE
#define COPY_TO_CLIPBOARD_SUPPORTED
#endif

using System;
using System.Reflection;
using UnityEngine;

namespace NovaFramework.Runtime
{
public static class RuntimeDebugger
{
    public const string Version = VersionInfo.Version;

    public static bool IsInitialized { get; private set; }

    /// <summary>
    /// 日志 Tag 常量宿主类型。必须是含 public static const string 字段的静态类，
    /// 字段值形如 "[Nova][SDK][Module]"。TagFilterPanelControl 通过反射枚举这些常量构建过滤树。
    /// 必须在首次打开 Console Tab 前通过 RuntimeDebugger.LogTagType = typeof(YourLogTag) 注入。
    /// </summary>
    /// <summary>
    /// RuntimeDebugger 初始化选项，通过 Init(options) 一次性传入所有配置。
    /// </summary>
    public class InitOptions
    {
        /// <summary>
        /// 日志 Tag 常量宿主类型，含 public static const string 字段。
        /// </summary>
        public Type LogTagType { get; set; }

        /// <summary>
        /// 从 FieldInfo 解析标签描述文字的委托，返回 null 或空字符串表示无描述。
        /// </summary>
        public Func<FieldInfo, string> LogTagDescriptionResolver { get; set; }

        /// <summary>
        /// 控制台最大保留条目数，0 表示使用默认值。
        /// </summary>
        public int MaximumConsoleEntries { get; set; }

        /// <summary>
        /// 点击上传日志按钮时触发，为 null 时按钮不显示。
        /// </summary>
        public Action<string> UploadLogCallback { get; set; }
    }

    public static Type LogTagType { get; private set; }

    /// <summary>
    /// 从 FieldInfo 解析标签描述文字的委托。
    /// 返回 null 或空字符串表示该字段无描述。
    /// </summary>
    public static Func<FieldInfo, string> LogTagDescriptionResolver { get; private set; }

    public static IDebugService Instance
    {
        get { return DebugServiceRegistry.GetService<IDebugService>(); }
    }

    /// <summary>
    /// Action to be invoked whenever the user selects "copy" in the console window.
    /// If null, copy/paste will not be available.
    /// </summary>
    public static Action<ConsoleEntry> CopyConsoleItemCallback = GetDefaultCopyConsoleItemCallback();

    /// <summary>
    /// 点击上传日志按钮时触发，由外部（如 GameInit）赋值实现上传逻辑。
    /// 为 null 时按钮不显示。
    /// </summary>
    public static Action<string> UploadLogCallback;

    /// <summary>
    /// 携带初始化选项启动 RuntimeDebugger，所有配置通过 options 一次性传入。
    /// </summary>
    /// <param name="options">初始化选项，为 null 时使用全部默认值。</param>
    public static void Init(InitOptions options = null)
    {
        if (options != null)
        {
            LogTagType = options.LogTagType;
            LogTagDescriptionResolver = options.LogTagDescriptionResolver;
            if (options.MaximumConsoleEntries > 0)
                NovaFramework.Runtime.Settings.Instance.MaximumConsoleEntries = options.MaximumConsoleEntries;
            if (options.UploadLogCallback != null)
                UploadLogCallback = options.UploadLogCallback;
        }

        IsInitialized = true;

        DebugServiceRegistry.RegisterAssembly<IDebugService>();

        // Initialize console if it hasn't already initialized.
        DebugServiceRegistry.GetService<IConsoleService>();

        // Load the debug service
        DebugServiceRegistry.GetService<IDebugService>();

#if UNITY_EDITOR
        RuntimeScriptRecompileHelper.SetHasInitialized();
#endif
    }

    public static void SetMaximumConsoleEntries(int count)
    {
        Settings.Instance.MaximumConsoleEntries = count;
    }

    public static Action<ConsoleEntry> GetDefaultCopyConsoleItemCallback()
    {
#if COPY_TO_CLIPBOARD_SUPPORTED
        return entry =>
        {
            GUIUtility.systemCopyBuffer =
                string.Format("{0}: {1}\n\r\n\r{2}", entry.LogType, entry.Message, entry.StackTrace);
        };
#else
        return null;
#endif
    }
}
}
