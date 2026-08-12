/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIAPStoreModule.cs
 * author:    yingzheng
 * created:   2026/8/4
 * descrip:   IAP Demo 可选商店模块契约、上下文与运行时发现器
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEngine;

using FeedbackLevel = NovaFramework.Sdk.IAP.Samples.Runtime.BaseDemoView.FeedbackLevel;

namespace NovaFramework.Sdk.IAP.Samples.Runtime
{
    /// <summary>
    /// IAP Demo 支持的可选商店类型。
    /// </summary>
    internal enum DemoIAPStoreKind
    {
        /// <summary>
        /// 移动官方支付。
        /// </summary>
        Mobile = 0,

        /// <summary>
        /// 第三方支付。
        /// </summary>
        ThirdPay = 1,

        /// <summary>
        /// 金券支付。
        /// </summary>
        Voucher = 2,
    }

    /// <summary>
    /// 可选商店程序集向 Core Demo 暴露的最小生命周期契约。
    /// </summary>
    internal interface IDemoIAPStoreModule
    {
        /// <summary>
        /// 获取模块对应的商店类型。
        /// </summary>
        DemoIAPStoreKind Kind { get; }

        /// <summary>
        /// 注入 Core Bridge、对应 Panel 壳及当前已安装商店列表。
        /// </summary>
        /// <param name="context">商店模块初始化上下文。</param>
        void Initialize(DemoIAPStoreContext context);

        /// <summary>
        /// 创建当前商店的演示商品卡。
        /// </summary>
        void BuildProducts();

        /// <summary>
        /// 刷新当前商店状态。
        /// </summary>
        /// <returns>异步任务。</returns>
        UniTask RefreshAsync();

        /// <summary>
        /// 设置当前商店业务按钮是否可交互。
        /// </summary>
        /// <param name="interactable">是否允许交互。</param>
        void SetInteractable(bool interactable);

        /// <summary>
        /// 将当前商店 Panel 的滚动位置复位到顶部。
        /// </summary>
        void ResetScrollPosition();

        /// <summary>
        /// 清理当前商店运行时创建的内容和回调引用。
        /// </summary>
        void ClearRuntimeContent();
    }

    /// <summary>
    /// 可选商店进入当前 Tab 时接收通知的扩展生命周期契约。
    /// 未实现该接口的商店不会因 Tab 切换触发额外刷新。
    /// </summary>
    internal interface IDemoIAPStoreSelectionHandler
    {
        /// <summary>
        /// 当前商店 Tab 完成切换后执行按需刷新。
        /// </summary>
        /// <returns>刷新结束的异步任务。</returns>
        UniTask OnSelectedAsync();
    }

    /// <summary>
    /// Core Demo 传递给可选商店模块的只读初始化上下文。
    /// </summary>
    internal sealed class DemoIAPStoreContext
    {
        /// <summary>
        /// 创建商店模块上下文。
        /// </summary>
        /// <param name="bridge">Core IAP 桥接层。</param>
        /// <param name="panel">当前商店对应的 Core Panel 壳。</param>
        /// <param name="availableStores">本次发现到的商店模块列表。</param>
        /// <param name="feedback">底部反馈区回调。</param>
        internal DemoIAPStoreContext(DemoIAPBridge bridge, MonoBehaviour panel,
            IReadOnlyList<DemoIAPStoreKind> availableStores, Action<string, FeedbackLevel> feedback)
        {
            Bridge = bridge;
            Panel = panel;
            AvailableStores = availableStores;
            Feedback = feedback;
        }

        /// <summary>
        /// 获取 Core IAP 桥接层。
        /// </summary>
        internal DemoIAPBridge Bridge { get; }

        /// <summary>
        /// 获取当前商店对应的 Core Panel 壳。
        /// </summary>
        internal MonoBehaviour Panel { get; }

        /// <summary>
        /// 获取当前工程中成功发现的商店模块列表。
        /// </summary>
        internal IReadOnlyList<DemoIAPStoreKind> AvailableStores { get; }

        /// <summary>
        /// 获取底部反馈区回调。
        /// </summary>
        internal Action<string, FeedbackLevel> Feedback { get; }
    }

    /// <summary>
    /// 从已加载程序集发现可选商店模块；未安装的 package 不会产生对应程序集和模块类型。
    /// </summary>
    internal static class DemoIAPStoreModuleDiscovery
    {
        /// <summary>
        /// 实例化全部可用商店模块，并按 Mobile、ThirdPay、Voucher 顺序返回。
        /// </summary>
        /// <returns>当前运行环境可用的商店模块。</returns>
        internal static List<IDemoIAPStoreModule> CreateAvailableModules()
        {
            var modules = new List<IDemoIAPStoreModule>();
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
            {
                Type[] types = GetLoadableTypes(assemblies[assemblyIndex]);
                for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    Type type = types[typeIndex];
                    if (type == null || type.IsAbstract || type.IsInterface
                        || !typeof(IDemoIAPStoreModule).IsAssignableFrom(type))
                    {
                        continue;
                    }

                    try
                    {
                        if (Activator.CreateInstance(type, true) is IDemoIAPStoreModule module)
                        {
                            modules.Add(module);
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning("IAP Demo 商店模块创建失败：" + type.FullName + "，" + exception.Message);
                    }
                }
            }

            modules.Sort((left, right) => left.Kind.CompareTo(right.Kind));
            return modules;
        }

        /// <summary>
        /// 安全取得程序集中的可加载类型，容忍单个第三方类型加载失败。
        /// </summary>
        /// <param name="assembly">待扫描程序集。</param>
        /// <returns>可继续检查的类型数组。</returns>
        private static Type[] GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types;
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }
    }
}
