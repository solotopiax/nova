/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidResolvePeripheralRefreshContracts.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   Android 精确模板域中包与引擎外围回调的自然支路合同提案
 ***************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEditor.Presets;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>只证明已绑定回调的自然分支；不调用回调、构造工厂、加载资产或改变设置。</summary>
    internal static class AndroidResolvePeripheralRefreshContracts
    {
        private const BindingFlags DeclaredFields = BindingFlags.Static | BindingFlags.Instance |
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static readonly Dictionary<int, string> s_Names = new Dictionary<int, string>
        {
            { 9, "UnityEditor.Timeline.SampleDependencyImporter+SamplePostprocessor" },
            { 10, "TMPro.EditorUtilities.TMPro_TexturePostProcessor" },
            { 11, "UnityEditor.Rendering.BuiltIn.MaterialPostprocessor" },
            { 12, "UnityEditor.ShaderGraph.NodeClassCache+PostProcessor" },
            { 13, "UnityEditor.ShaderGraph.ShaderGraphAssetPostProcessor" },
            { 14, "UnityEngine.InputSystem.Editor.ProjectWideActionsAsset+ProjectSettingsProjectWideActionsAssetConverter+ProjectSettingsPostprocessor" },
            { 15, "UnityEngine.InputSystem.Editor.InputActionImporter+InputActionJsonNameModifierAssetProcessor" },
            { 16, "UnityEngine.InputSystem.Editor.InputActionAssetEditor+InputActionAssetPostprocessor" },
            { 17, "Unity.Cinemachine.Editor.SampleDependencyImporter+SamplePostprocessor" },
            { 18, "Unity.Cinemachine.Editor.SampleDependencyImporter+DomainReloadProcessor" },
            { 19, "Unity.Cinemachine.Editor.CinemachineSettings" },
            { 20, "UnityEditor.Tilemaps.GridPaletteBrushes+AssetProcessor" },
            { 21, "UnityEditor.Tilemaps.GridPaintPaletteClipboard+AssetProcessor" },
            { 22, "UnityEditor.Tilemaps.GridPalettes+AssetProcessor" },
            { 23, "UnityEditor.U2D.SpriteShapeAssetPostProcessor" },
            { 24, "UnityEditor.U2D.Sprites.SpriteEditorTexturePostprocessor" },
            { 25, "UnityEditor.U2D.PSD.PSDImporterAssetPostProcessor" },
            { 26, "UnityEditor.U2D.PSD.PSDImportPostProcessor" },
            { 27, "UnityEditor.U2D.Animation.SpriteLibraryEditor.SpriteLibraryAssetPostprocessor" },
            { 28, "UnityEditor.U2D.Animation.SpritePostProcess" },
            { 29, "DA_Assets.DM.DependencyManager+AssetWatcher" },
            { 30, "UnityEditor.RetainedMode+RetainedModeAssetPostprocessor" },
            { 31, "Unity.UI.Builder.BuilderAssetPostprocessor" },
            { 32, "UnityEditor.TextCore.Text.TextAssetPostProcessor" },
            { 34, "UnityEditor.TerrainTools.TerrainToolbarOverlayPostProcessor" },
            { 36, "Unity.PlayMode.Editor.PlayModeScenarioUtils+ConfigAssetsTracker" },
            { 37, "Unity.Multiplayer.PlayMode.Editor.ScenarioRunner+ScenarioDriftAssetsTracker" },
            { 38, "Unity.Multiplayer.PlayMode.Editor.AssetDatabaseCallbacks" },
            { 39, "Unity.GraphToolkit.Editor.WindowAssetPostprocessingWatcher" },
            { 40, "UnityEditor.AssetEvents" },
            { 41, "UnityEditor.Presets.PresetManagerPostProcessor" },
            { 42, "UnityEditor.AudioContainerWindow+AudioContainerPostProcessor" },
            { 43, "UnityEditor.PlayerSettingsSplashScreenEditor+CancelSplashScreenOnAssetChange" },
            { 44, "UnityEditor.Rendering.RenderPipelineGlobalSettingsPostprocessor" },
            { 45, "UnityEditor.AudioMixerWindow+AudioMixerPostprocessor" },
            { 46, "UnityEditor.ModelImporterRigEditor+ReferencedClipsPostProcessor" },
            { 47, "UnityEditor.MPE.DataService+AssetEvents" },
            { 49, "UnityEditor.ArtifactBrowserPostProcessor" },
            { 50, "UnityEditor.PackageManager.UI.Internal.AssetDatabaseProxy+AssetPostprocessor" },
            { 51, "UnityEditor.Experimental.EditorResources+StyleCatalogPostProcessor" },
            { 52, "UnityEditor.SpeedTreePostProcessor" },
        };
        private static readonly Dictionary<string, string[]> s_Modules = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "Unity.Timeline.Editor.dll", new[] { "06207e491fde41d6348bf400dbfecc889fd48d6d7dd7ab0cbbb623af92219ffc", "5fcf00c2-be8c-4b4a-9e7d-45bdcbac5861" } },
            { "Unity.TextMeshPro.Editor.dll", new[] { "25384482a1f8c12f38ca475d59c9b0eb7eef8d7d48b2d8a51f0e3c63d6c6f3c9", "6cc3a159-b8f4-44bd-a27b-1abb18b89c13" } },
            { "Unity.ShaderGraph.Editor.dll", new[] { "0ae624838ae217a0a8cf24e88273c070d9c67c63cc55f2a8b5beb3e079096448", "4cf747a4-8249-4b1a-ab13-0e353d8e2706" } },
            { "Unity.InputSystem.dll", new[] { "7f255bd4a489c9188dcc11b3089efd084fed86e4aa77f1c2a6f0bb9652aa6817", "3d4dabe8-0d94-486c-be32-6c4cdbcf664b" } },
            { "Unity.Cinemachine.Editor.dll", new[] { "1c542cfc4c5e4606d914c9477ec7d92538724e3acdb18441f223de7ce1e2477e", "24c33adf-41d7-41de-8efb-ee04c90b791c" } },
            { "Unity.2D.Tilemap.Editor.dll", new[] { "fa153a745d260c6ad2ee51dfc7e8de0db3a522037576cb210ea44d0b71920741", "0463dda6-b969-4326-9533-be27a1a86375" } },
            { "Unity.2D.SpriteShape.Editor.dll", new[] { "7bb139ef38e254a9c723eeb31468407d5ad36b09d1bfc11ad52ac4e08937d9f3", "a2e981e0-e622-416b-9b00-2fafc115be7b" } },
            { "Unity.2D.Sprite.Editor.dll", new[] { "454461173bc76dabc86e5e080d62b5ec09354766d4f9bbdf358be20fc56dfa73", "fa0aab0e-90a0-4fde-a3bb-17b84de8cc6f" } },
            { "Unity.2D.Psdimporter.Editor.dll", new[] { "01868ff68015d822b4a85091cc5f901a05987e7ebc6e08fa241bb5d2a745c7ae", "6c8f3c84-5a7f-4493-8341-153aff5f36b4" } },
            { "Unity.2D.Animation.Editor.dll", new[] { "e13301a8f8e89c401657147e4093c497637f5ed9db38aab60d73b1a44ec1dc60", "4ee3f94f-93e0-40dc-a96a-1a39bb6e26f2" } },
            { "DA_Assets.DM.dll", new[] { "c845543e1f8ce98854671ed57a61c4b693198240c7a517ed72b5ad6e5db9af8a", "12c350c2-9bf8-4673-b1e4-0ab28cff2db8" } },
            { "UnityEditor.UIElementsModule.dll", new[] { "9bb68962a197695dc9cf12c37c38b3f5679304c6496e4655d6287319bd8b8b10", "d572b22f-1d90-4477-9407-b36592117da9" } },
            { "UnityEditor.UIBuilderModule.dll", new[] { "e0217163f769359511b9e738e055a6d0634d8f62859233f36355b81f71abce13", "81f60ad4-dc3c-4e56-8651-c0c682addd9d" } },
            { "UnityEditor.TextCoreTextEngineModule.dll", new[] { "5ccf7a5a43c7b0dc5e1280f9b38142115c8d2ced8ee5518806fffbcb0eb3ef70", "7c6bb697-5409-4585-8b24-d9510bb2bad4" } },
            { "UnityEditor.TerrainModule.dll", new[] { "2fd66c8f03b5dc44cf3ba26440b412124d2140bb20ae6ae56650054dfcd8eddc", "0753dcb3-3f74-471d-ada8-ab0c97ef658d" } },
            { "UnityEditor.PlayModeModule.dll", new[] { "e79ffcdc7cd6316a18745f92495cc86040c0b25d94a031932e3d85b07f15c5b6", "f97cba61-85b3-49d3-bf47-b28da0807459" } },
            { "UnityEditor.MultiplayerModule.dll", new[] { "d88ea26d238062106c0afc145109c0f42ff291dc931ce28cb95f7f6def7914fe", "fe5e1b6f-1d28-4908-9514-1197f4df0b92" } },
            { "UnityEditor.GraphToolkitModule.dll", new[] { "a6ab78cb6935564242fedc8c32e97ca301678594283684b00d659a766a1c42d6", "278bf535-ccd3-4fe5-a5d5-3d10d1431c76" } },
            { "UnityEditor.CoreModule.dll", new[] { "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", "11f76f7e-1291-4a87-8174-9af3fa5b621a" } },
            { "Unity.2D.SpriteShape.Runtime.dll", new[] { "3f5a9b0cfd5bd4a9de99e5f91dcba02dd7c4d2cfbe8df90ddb206cc41995d09e", "5ad586ac-e82a-4f83-8098-0619e0ca8f32" } },
            { "UnityEngine.CoreModule.dll", new[] { "25281ab07e11130a45ede1e02cf3003666f9616cca68ae8ab9637f0ccffa33ad", "e816434e-022e-42a6-bec8-8f93bd4bf5e5" } },
            { "UnityEditor.SceneViewModule.dll", new[] { "2d040f01b4c99da98e80664f10178c90bbe8613857a3671ff3ce428f977943bd", "21baec1f-5f0c-448b-ae43-a99daca1271f" } },
        };
        private static readonly string[][] s_ExtraSources =
        {
            new[] { "com.unity.inputsystem", "InputSystem/Editor/ProjectWideActions/ProjectWideActionsBuildProvider.cs", "3cebfc6059805c45876329c7b043efcbe254f338d8402d198bc06aa515caeaee" },
            new[] { "com.unity.2d.tilemap", "Editor/GridPaintingState.cs", "2154c4a632f40cd50c20135ca8df568deb2babec1c1a0c1f201ddfc397afb469" },
            new[] { "com.unity.2d.sprite", "Editor/SpriteEditor/SpriteEditorWindow.cs", "89153821c318caa736f79211a8e88903be3b33c136453b254839d554c8bfed22" },
            new[] { "com.unity.2d.sprite", "Editor/SpriteEditorModule/TextureImporterDataProvider.cs", "d4def2a01482618c13e948f254d50077d9add82c5f64632d6be7e23b02be2f81" },
        };
        private static readonly string s_DomainIdentity = Guid.NewGuid().ToString("N");
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, WeakIdentity> s_WeakIdentities =
            new System.Runtime.CompilerServices.ConditionalWeakTable<object, WeakIdentity>();

        /// <summary>只保存在当前域内的随机不透明身份；不持有目标强引用，也不执行目标哈希或getter。</summary>
        private sealed class WeakIdentity
        {
            internal readonly string Value = Guid.NewGuid().ToString("N");

            /// <summary>创建本合同私有身份缓存值，不构造或修改任何Unity/vendor对象。</summary>
            internal WeakIdentity() { }
        }

        private static readonly string[] s_ClipboardColorKeys =
        {
            "2D/Tile Palette Grid", "2D/Tile Palette Background", "2D/Tile Palette Background Edit"
        };

        private static readonly string[] s_ApprovedPendingColorKeys =
        {
            "Trail Renderer/Bounds", "Trail Renderer/Shape Gizmos", "Cloth/Brush Color 2",
            "Cloth/Self or Inter Collision Particle Color 2", "Cloth/Unselected Self or Inter Collision Particle Color 2",
            "Cloth/Selected Self or Inter Collision Particle Color 2"
        };
        private static readonly string[] s_ColorMaterializationKeys = s_ClipboardColorKeys.Concat(s_ApprovedPendingColorKeys).ToArray();
        private static readonly string[] s_LightingColorKeys =
        {
            "Scene/Material Validator Value Too Low", "Scene/Material Validator Value Too High",
            "Scene/Material Validator Pure Metal", "Scene/Back-Facing Geometry",
            "Scene/Receive GI Only (Light Probes)", "Scene/Receive + Contribute GI (Lightmaps)",
            "Scene/Receive + Contribute GI (Light Probes)"
        };
        private static readonly string[] s_LightingShaderKeys =
        {
            "unity_MaterialValidateLowColor", "unity_MaterialValidateHighColor",
            "unity_MaterialValidatePureMetalColor", "unity_BackfaceHighlightsColor"
        };

        /// <summary>
        /// 父合同已绑定该回调整个DLL、MVID与原始源码，并保证同域、无脚本变更的精确模板刷新后调用。
        /// exactPaths须包含导入、删除、移动两端、父目录、meta与新增路径；null表示已证明自然支路，字符串说明真实阻断。
        /// 允许的派生内存及编辑器UI初始化须与DeclaredEffects一起冻结；不得将null解释成所有内存逐字节不变。
        /// </summary>
        /// <param name="id">完整源码和模块绑定表中的精确回调编号。</param>
        /// <param name="type">已加载且需要核对的实际类型元数据。</param>
        /// <param name="exactPaths">本轮完整精确路径域，包含主资产、meta 和可能触及的父目录。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>自然分支已证明时返回 null；否则返回精确阻断原因。</returns>
        internal static string Check(int id, Type type, string[] exactPaths, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!s_Names.TryGetValue(id, out string name) || type == null || type.FullName != name)
                return "不属于本外围合同的精确回调身份";
            if (exactPaths == null || exactPaths.Length == 0 || exactPaths.Any(string.IsNullOrEmpty))
                return "缺少完整精确路径集合";
            try
            {
                VerifyModule(type, token);
                foreach (string path in exactPaths)
                {
                    token.ThrowIfCancellationRequested();
                    if (path.IndexOf('\\') >= 0 || path.Split('/').Any(segment => segment == "." || segment == ".."))
                        return "路径没有完成规范化";
                }
                switch (id)
                {
                    case 9: case 17:
                        return ReadStatic(type, "AssetImported") == null ? null : "Sample导入事件有实际订阅";
                    case 10: case 32:
                        return CheckImportedMainTypes(exactPaths, token);
                    case 11:
                        return HasSuffix(exactPaths, ".mat") ? "Material回调命中mat路径，包括同名父目录" : null;
                    case 12:
                        return CheckSubGraphCache(type, exactPaths, token);
                    case 13:
                        if (HasSuffix(exactPaths, "shadergraph", "shadersubgraph")) return "命中ShaderGraph导入/删除语义";
                        return NoLoaded(Owner("UnityEditor.ShaderGraph.Drawing.MaterialGraphEditWindow", token)) ? null :
                            "ShaderGraph现存窗口可能因缺失主类型而安排子图重载";
                    case 14:
                        return CheckInputActions(exactPaths, token);
                    case 15: case 16:
                        return HasSuffix(exactPaths, ".inputactions") ? "InputActions回调命中输入资产" : null;
                    case 18:
                        return null; // 已绑定源码先判didDomainReload；父同域合同为false时不读EditorPrefs或处理Sample。
                    case 19:
                        return null; // 父合同保证本次刷新didDomainReload=false；cctor只准备静态UI缓存与委托。
                    case 20:
                        return CheckBrushes(token);
                    case 21:
                        return CheckClipboard(token);
                    case 22:
                        return CheckPalettes(token);
                    case 23:
                        return NoLoaded(Owner("UnityEngine.U2D.SpriteShapeController", token)) ? null :
                            "SpriteShape现存组件会被本次刷新重建";
                    case 24:
                        return NoLoaded(Owner("UnityEditor.U2D.Sprites.SpriteEditorWindow", token)) ? null :
                            "存在SpriteEditor窗口，会进入窗口/Inspector刷新链";
                    case 25:
                        return ReadStatic(type, "s_AssetImporter") == null ? null : "PSD pending importer列表非null";
                    case 26:
                        object applying = ReadStatic(type, "s_CurrentApplyAssetPath");
                        return applying == null || applying is string path && !exactPaths.Contains(path, StringComparer.Ordinal) ?
                            null : "PSD正在应用的路径与本次集合相交或字段类型漂移";
                    case 27:
                        return HasSuffix(exactPaths, ".spriteLib") ? "SpriteLibrary回调命中spriteLib路径" : null;
                    case 28:
                        return CheckSpriteFactoryMetadata(exactPaths, token);
                    case 29:
                        object scheduled = ReadStatic(Owner("DA_Assets.DM.DependencyManager", token), "_runScheduled");
                        if (!(scheduled is bool queued)) return "DA DependencyManager现有调度字段签名未知";
                        if (queued) return "DA DependencyManager已有正常delayCall待执行；等待其自然收尾后重新Plan，不消费或清理该任务";
                        return HasSuffix(exactPaths, ".cs", ".asmdef", ".dll", ".asset") ?
                            "DA DependencyManager路径可能触发新的依赖检查" : null;
                    case 30: case 31:
                        return HasSuffix(exactPaths, ".uxml", ".uss", ".uss.asset", ".tss") ?
                            "UIElements/Builder路径过滤命中" : null;
                    case 34:
                        return null; // 同域模板刷新不设didDomainReload，不读取可能未初始化的Overlay静态字段。
                    case 36:
                        return ReadStatic(Owner("Unity.PlayMode.Editor.PlayModeScenarioUtils", token), "AssetsChanged") == null ?
                            null : "PlayMode AssetsChanged有实际订阅";
                    case 37: case 38:
                        return CheckMultiplayer(token);
                    case 39:
                        return NoLoaded(Owner("Unity.GraphToolkit.Editor.GraphViewEditorWindow", token)) ? null :
                            "GraphToolkit存在窗口；不能只用路径过滤排除图状态更新";
                    case 40:
                        return CheckAssetEvents(type, token);
                    case 41:
                        return CheckPresets(exactPaths, token);
                    case 42:
                        return ReadStatic(Owner("UnityEditor.AudioContainerWindow", token), "<Instance>k__BackingField") == null ?
                            null : "AudioContainer已有窗口backing";
                    case 43:
                        VerifyModule(typeof(UnityEngine.Rendering.SplashScreen), token);
                        return UnityEngine.Rendering.SplashScreen.isFinished ? null : "SplashScreen尚未完成，Stop不是幂等支路";
                    case 44:
                        VerifyModule(typeof(UnityEngine.Rendering.GraphicsSettings), token);
                        return UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null ? null :
                            "当前RenderPipeline非null，可能重建GlobalSettings";
                    case 45:
                        return HasSuffix(exactPaths, ".mixer") ? "AudioMixer回调命中mixer路径" : null;
                    case 46:
                        return ReadStatic(type, "ReferencedClipsChanged") == null ? null :
                            "ReferencedClipsChanged有实际订阅，即使没有@路径仍会Invoke空List";
                    case 47:
                        return ReadStatic(Owner("UnityEditor.MPE.DataService", token), "s_ImportRefreshEnabled") is bool enabled && !enabled ?
                            null : "MPE刷新已启用，会安排EmitRefresh";
                    case 49:
                        return NoLoaded(Owner("UnityEditor.ImportActivityWindow", token)) ? null : "ImportActivity窗口会安排delayCall";
                    case 50:
                        return CheckPackageProxy(exactPaths, token);
                    case 51:
                        Type resources = Owner("UnityEditor.Experimental.EditorResources", token);
                        return ReadStatic(resources, "s_StyleCatalog") != null &&
                            ReadStatic(resources, "s_RefreshGlobalStyleCatalog") is bool refresh && !refresh &&
                            !HasSuffix(exactPaths, ".uss") ? null : "EditorResources需要构建/刷新StyleCatalog";
                    case 52:
                        // 整Core DLL中该老SpeedTree回调只遍历imported并判.st，再读取SpeedTreeImporter；本域不含此后缀。
                        return HasSuffix(exactPaths, ".st") ? "旧SpeedTree回调命中st输入，可能修补纹理sRGB" : null;
                    default:
                        return "该回调没有本外围合同";
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) { return "外围合同读取失败：" + exception.GetType().Name + "：" + exception.Message; }
        }

        /// <summary>明确列出正常回调可以产生的派生内存效应，供父合同冻结、展示与执行后核对。</summary>
        /// <param name="id">完整源码和模块绑定表中的精确回调编号。</param>
        /// <returns>该固定回调允许的正常派生缓存效果描述。</returns>
        internal static string DeclaredEffects(int id)
        {
            switch (id)
            {
                case 11: return "首次Material类型初始化仅创建两个空路径缓存和空upgrader数组";
                case 16: return "首次InputActionAssetPostprocessor初始化三个空路径List";
                case 19: return "首次CinemachineSettings初始化Logo/滚动坐标及固定绘制委托；同域回调不注册Hierarchy处理器";
                case 20: return "GridPaletteBrushes.s_RefreshCache=true；可创建无FilePath的空Brush/State单例；空brush列表清空并置State刷新flag；State首次OnEnable只注册四个固定UI事件";
                case 21: return "可初始化L10n字符串缓存、空clipboard List及三个PrefColor注册（callback自身不调用Load）；后续自然Load仅复制现有cache或对固定九key（本三key+当前六个已审pending）执行已冻结同字节SetString；RectTransform与Overlay订阅由实际key过滤返回；现存Lighting内容仅以七个已加载PrefColor刷新四个固定Shader全局色、Handles GI三色与m_LastKnownColorSpace，不命中色块UI；完整已审pending可物化/清空；无现存clipboard，不触其后续预览重置";
                case 22: return "GridPalettes派生缓存=null；可创建无FilePath的空单例；不发palettesChanged事件";
                case 28: return "工厂缓存可在主域或ImportWorker从null自然初始化；唯一无cctor/空ctor工厂生成四项表并缓存两类attribute方法元数据，绝不调用provider方法";
                case 30: case 31: return "首次编译器lambda/Builder初始化只创建空集合或固定委托缓存";
                case 36: return "PlayMode s_AllConfigs=null；AssetsChanged无订阅";
                case 39: return "GraphViewEditorWindow首次cctor创建空窗口List/反射UI缓存，并注册唯一固定Console双击闭包；不自动触发双击委托";
                case 40: return "每个精确SceneViewStageHandling.AssetsChangedOnHDD只将自身m_BreadcrumbInitialized=false，保持SceneView/StateCache/BreadcrumbBar/Stage对象与事件身份不变";
                case 45: return "首次AudioMixerWindow仅缓存两个常量key，相关lambda仅缓存固定委托";
                case 47: return "MPE首次初始化false标记与空字符串数组；不安排EmitRefresh";
                case 49: return "ImportActivity首次仅初始化静态UI常量；无窗口时不安排delayCall";
                case 50: return "PageRefreshHandler只更新RefreshOptions[64]的内存timestamp；AssetStore imported cache与packages保持不变，不执行全量RefreshImportedAssets";
                default: return "已审路径/类型/空事件自然支路，无持久资产或设置写入";
            }
        }

        /// <summary>域内稳定状态快照；摘要只供收据展示，验证同时比较不透明对象的真实引用身份。</summary>
        [Serializable]
        internal sealed class StableSnapshot
        {
            public string Digest;
            public string[] ReferenceIdentities;
            public string ClipboardPendingFingerprint;
            public string[] ClipboardPreferenceEffects;
            public string[] LightingExpectedEffects;
            public string[] LightingObservedEffects;

            /// <summary>供Receipt序列化器创建快照；空快照不能用于通过验证。</summary>
            public StableSnapshot() { }

            /// <summary>冻结规范化摘要与同域WeakGUID身份，不保存原始Unity/vendor引用到Receipt。</summary>
            /// <param name="digest">成功时的稳定 SHA 摘要，不包含原始用户内容。</param>
            /// <param name="referenceIdentities">同域弱表身份序列；不能跨域继承。</param>
            internal StableSnapshot(string digest, string[] referenceIdentities)
            { Digest = digest; ReferenceIdentities = referenceIdentities; }
        }

        /// <summary>
        /// 仅在checkedIds对应的全部Check通过后，捕获本次实际参与回调的稳定状态。
        /// 明确排除允许变化的刷新flag、空单例身份、工厂缓存物化、Graph UI新增和UPM timestamp64。
        /// </summary>
        /// <param name="checkedIds">已实际参与且对应 Check 通过的外围回调编号。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>包含稳定语义和同域弱身份的可序列化快照。</returns>
        internal static StableSnapshot CaptureStableSnapshot(int[] checkedIds, CancellationToken token)
        {
            if (checkedIds == null) throw new ArgumentNullException(nameof(checkedIds));
            var rows = new List<string>();
            var references = new List<object> { AppDomain.CurrentDomain };
            var lightingExpected = new List<string>();
            var lightingObserved = new List<string>();
            Action<string, object> add = (name, value) => rows.Add(name + "=" + SnapshotScalar(value, references));
            foreach (int id in checkedIds.Distinct().OrderBy(value => value))
            {
                token.ThrowIfCancellationRequested();
                if (!s_Names.TryGetValue(id, out string name)) throw new InvalidOperationException("未知快照ID");
                Type type = Owner(name, token);
                add("callback:" + id, type);
                switch (id)
                {
                    case 9: case 17:
                        add("sample:" + id, ReadStatic(type, "AssetImported")); break;
                    case 12:
                        CaptureMap(rows, "subgraphs", ReadStatic(Owner("UnityEditor.ShaderGraph.NodeClassCache", token),
                            "m_KnownSubGraphLookupTable"), references, -1); break;
                    case 14:
                        object manager = ReadStatic(Owner("UnityEngine.InputSystem.InputSystem", token), "s_Manager");
                        add("input:manager", manager);
                        add("input:actions", manager == null ? null : ReadInstance(manager, "m_Actions")); break;
                    case 20: case 22:
                        object state = Singleton(Owner("UnityEditor.Tilemaps.GridPaintingState", token), token);
                        foreach (string field in new[] { "m_Brush", "m_Palette", "m_ScenePaintTarget", "m_EditModeScenePaintTarget",
                            "m_CachedEditor", "m_ActiveGrid", "m_LastActiveGrid", "m_ActivePickPalette", "m_CachedPaintTargets",
                            "m_PreviousToolActivatedEditor", "m_PaintableSceneViewGrid" })
                            add("grid:" + field, UnityNull(state) ? null : ReadInstance(state, field));
                        foreach (string field in new[] { "m_SavingPalette", "m_IsEditing", "m_HasActivePick", "m_DrawGizmos", "m_ExitingEditMode" })
                            add("grid:" + field, UnityNull(state) ? false : ReadInstance(state, field));
                        add("grid:m_DrawGridGizmo", UnityNull(state) ? true : ReadInstance(state, "m_DrawGridGizmo"));
                        object brushes = Singleton(Owner("UnityEditor.Tilemaps.GridPaletteBrushes", token), token);
                        foreach (string field in new[] { "m_BrushNames", "m_BrushTooltips", "m_BrushIcons" })
                            add("brush:" + field, UnityNull(brushes) ? null : ReadInstance(brushes, field));
                        add("palette:event", ReadStatic(Owner("UnityEditor.Tilemaps.GridPalettes", token), "palettesChanged"));
                        break;
                    case 21:
                        Type prefs = Owner("UnityEditor.PrefSettings", token);
                        IDictionary values = SortedList(ReadStatic(prefs, "m_Prefs"));
                        var otherValues = new SortedList<string, object>(StringComparer.Ordinal);
                        foreach (DictionaryEntry entry in values)
                            if (!(entry.Key is string key) || !s_ColorMaterializationKeys.Contains(key, StringComparer.Ordinal))
                                otherValues.Add((string)entry.Key, entry.Value);
                        CaptureMap(rows, "prefs:otherValues", otherValues, references, -1);
                        foreach (DictionaryEntry entry in (IDictionary)otherValues)
                            if (entry.Value != null && entry.Value.GetType() == Owner("UnityEditor.PrefColor", token))
                                foreach (string field in new[] { "m_Name", "m_Color", "m_DefaultColor", "m_SeparateColors",
                                    "m_OptionalDarkColor", "m_OptionalDarkDefaultColor", "m_Loaded" })
                                    add("prefs:" + SnapshotScalar(entry.Key, references) + ":" + field, ReadInstance(entry.Value, field));
                        CaptureClipboardSemanticRows(rows, values, references);
                        add("prefs:settingChanged", ReadStatic(prefs, "settingChanged"));
                        add("prefs:settingsReverted", ReadStatic(prefs, "settingsReverted"));
                        CapturePreferenceConsumerRows(rows, prefs, references, lightingExpected, lightingObserved, token);
                        break;
                    case 25: add("psd:pending", ReadStatic(type, "s_AssetImporter")); break;
                    case 26: add("psd:applying", ReadStatic(type, "s_CurrentApplyAssetPath")); break;
                    case 27:
                        foreach (string field in new[] { "OnImported", "OnDeleted", "OnMovedAssetFromTo" })
                            add("spriteLibrary:" + field, ReadStatic(type, field)); break;
                    case 29: add("da:scheduled", ReadStatic(Owner("DA_Assets.DM.DependencyManager", token), "_runScheduled")); break;
                    case 36: add("playmode:event", ReadStatic(Owner("Unity.PlayMode.Editor.PlayModeScenarioUtils", token), "AssetsChanged")); break;
                    case 40:
                        object eventsValue = ReadStatic(type, "assetsChangedOnHDD");
                        add("assets:event", eventsValue);
                        if (eventsValue is Delegate events)
                        foreach (Delegate handler in events.GetInvocationList())
                        {
                            string targetKey = "assets:target:" + WeakIdentityKey(handler.Target) + ":";
                            foreach (string field in new[] { "m_SceneView", "m_StateCache", "m_BreadcrumbBar", "m_StageClickedFromBreadcrumb" })
                                add(targetKey + field, ReadInstance(handler.Target, field));
                        }
                        break;
                    case 46: add("clips:event", ReadStatic(type, "ReferencedClipsChanged")); break;
                    case 50:
                        object container = Singleton(Owner("UnityEditor.PackageManager.UI.Internal.ServicesContainer", token), token);
                        add("upm:container", container);
                        object registry = ReadInstance(container, "m_RegisteredServices");
                        CaptureMap(rows, "upm:services", registry, references, -1);
                        object proxy = FindTypeValue(Dictionary(registry), Owner("UnityEditor.PackageManager.UI.Internal.AssetDatabaseProxy", token));
                        add("upm:proxy", proxy);
                        Delegate callbacks = ReadInstance(proxy, "onPostprocessAllAssets") as Delegate;
                        add("upm:callbacks", callbacks);
                        if (callbacks == null) break;
                        foreach (Delegate handler in callbacks.GetInvocationList())
                        {
                            if (handler.Method.DeclaringType.FullName != "UnityEditor.PackageManager.UI.Internal.PageRefreshHandler") continue;
                            object page = handler.Target;
                            add("upm:page", page);
                            CaptureMap(rows, "upm:timestamps", ReadInstance(page, "m_RefreshTimestamps"), references, 64);
                            object client = ReadInstance(page, "m_AssetStoreClient");
                            object cache = ReadInstance(client, "m_AssetStoreCache");
                            add("upm:client", client); add("upm:cache", cache);
                            CaptureMap(rows, "upm:importedAssets", ReadInstance(cache, "m_ImportedAssets"), references, -1);
                            CaptureMap(rows, "upm:importedPackages", ReadInstance(cache, "m_ImportedPackages"), references, -1);
                            foreach (DictionaryEntry entry in Dictionary(ReadInstance(cache, "m_ImportedAssets")))
                            {
                                string prefix = "upm:asset:" + SnapshotScalar(entry.Key, references) + ":";
                                object asset = entry.Value;
                                foreach (string field in new[] { "guid", "importedPath" }) add(prefix + field, ReadInstance(asset, field));
                                object origin = ReadInstance(asset, "origin");
                                add(prefix + "origin", origin);
                                if (origin != null)
                                    foreach (string field in new[] { "productId", "packageVersion", "packageName", "assetPath", "uploadId" })
                                        add(prefix + field, ReadInstance(origin, field));
                            }
                        }
                        break;
                }
            }
            rows.Sort(StringComparer.Ordinal);
            using (var sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", rows)));
                var snapshot = new StableSnapshot(BitConverter.ToString(digest).Replace("-", "").ToLowerInvariant(), references.Select(WeakIdentityKey).ToArray());
                snapshot.LightingExpectedEffects = lightingExpected.OrderBy(row => row, StringComparer.Ordinal).ToArray();
                snapshot.LightingObservedEffects = lightingObserved.OrderBy(row => row, StringComparer.Ordinal).ToArray();
                snapshot.ClipboardPendingFingerprint = checkedIds.Contains(21) ? CaptureClipboardPendingFingerprint(token) : null;
                snapshot.ClipboardPreferenceEffects = checkedIds.Contains(21) ? CaptureClipboardPreferenceEffects(token) : Array.Empty<string>();
                return snapshot;
            }
        }

        /// <summary>稳定摘要/身份须一致；Lighting可完全保持原状态，或在自然Load发生后成为完整已声明Legend结果。</summary>
        /// <param name="before">同域执行前冻结的完整状态。</param>
        /// <param name="after">当前重新捕获的执行后状态。</param>
        /// <returns>稳定语义和同域身份一致时为 null，否则为具体错误。</returns>
        internal static string VerifyStableSnapshot(StableSnapshot before, StableSnapshot after)
        {
            string error = CompareStableSnapshotState(before, after);
            if (error != null) return error;
            string[] observed = after.LightingObservedEffects ?? Array.Empty<string>();
            return (before.LightingExpectedEffects ?? Array.Empty<string>()).SequenceEqual(observed) ||
                (before.LightingObservedEffects ?? Array.Empty<string>()).SequenceEqual(observed) ? null :
                "Lighting固定Shader颜色或颜色空间缓存既非原状态，也非Plan声明的完整Legend期望";
        }

        /// <summary>只比较稳定输入与同域WeakGUID；不在执行前要求已声明的UI输出已发生。</summary>
        /// <param name="before">同域执行前冻结的完整状态。</param>
        /// <param name="after">当前重新捕获的执行后状态。</param>
        /// <returns>完整稳定语义与同域身份相同时为 null，否则为精确错误。</returns>
        private static string CompareStableSnapshotState(StableSnapshot before, StableSnapshot after)
        {
            if (before == null || after == null || string.IsNullOrEmpty(before.Digest) || before.Digest != after.Digest ||
                before.ReferenceIdentities == null || after.ReferenceIdentities == null ||
                before.ReferenceIdentities.Length != after.ReferenceIdentities.Length || before.ReferenceIdentities.Length == 0)
                return "外围非目标稳定状态摘要变化";
            for (int i = 0; i < before.ReferenceIdentities.Length; i++)
                if (before.ReferenceIdentities[i] != after.ReferenceIdentities[i] ||
                    !before.ReferenceIdentities[i].StartsWith(s_DomainIdentity + "|", StringComparison.Ordinal))
                    return "外围非目标对象/委托WeakGUID或域身份变化";
            return null;
        }

        /// <summary>执行前除稳定状态外还须逐字匹配完整pending输入队列；执行后改用VerifyStableSnapshot允许已声明缓存物化。</summary>
        /// <param name="planned">Plan 阶段冻结的完整稳定输入快照。</param>
        /// <param name="current">当前重新捕获的完整状态。</param>
        /// <returns>完整稳定输入、原值与 pending 队列不变时为 null，否则为具体错误。</returns>
        internal static string VerifyStableSnapshotInputs(StableSnapshot planned, StableSnapshot current)
        {
            string error = CompareStableSnapshotState(planned, current);
            if (error != null) return error;
            return planned.ClipboardPendingFingerprint == current.ClipboardPendingFingerprint &&
                (planned.ClipboardPreferenceEffects ?? Array.Empty<string>()).SequenceEqual(current.ClipboardPreferenceEffects ?? Array.Empty<string>()) &&
                (planned.LightingExpectedEffects ?? Array.Empty<string>()).SequenceEqual(current.LightingExpectedEffects ?? Array.Empty<string>()) &&
                (planned.LightingObservedEffects ?? Array.Empty<string>()).SequenceEqual(current.LightingObservedEffects ?? Array.Empty<string>()) ? null :
                "PrefSettings完整pending队列、九key精确原/预期值或消费者身份在执行前改变";
        }

        /// <summary>仅枚举精确BCL映射；UPM可跳过已声明timestamp64，其余键和值逐项稳定捕获。</summary>
        /// <param name="rows">当前捕获生成的规范化元数据行。</param>
        /// <param name="prefix">当前记录项的规范化前缀。</param>
        /// <param name="value">需要检查或规范化的当前值；不执行其自定义方法。</param>
        /// <param name="references">当前遍历中用于建立不透明身份的对象序列。</param>
        /// <param name="excludedEnum">明确允许变化的唯一数值枚举键；小于零不排除任何键，其他项完整冻结。</param>
        private static void CaptureMap(List<string> rows, string prefix, object value, List<object> references, int excludedEnum)
        {
            if (value == null) { rows.Add(prefix + "=null"); return; }
            Type definition = value.GetType().IsGenericType ? value.GetType().GetGenericTypeDefinition() : null;
            IDictionary map = definition == typeof(Dictionary<,>) ? Dictionary(value) : SortedList(value);
            var entries = new List<DictionaryEntry>();
            foreach (DictionaryEntry entry in map)
            {
                if (excludedEnum >= 0 && entry.Key is Enum && Convert.ToInt32(entry.Key) == excludedEnum) continue;
                entries.Add(entry);
            }
            foreach (DictionaryEntry entry in entries.OrderBy(entry => SnapshotScalar(entry.Key, references), StringComparer.Ordinal))
                rows.Add(prefix + ":" + SnapshotScalar(entry.Key, references) + "=" + SnapshotScalar(entry.Value, references));
            rows.Add(prefix + ":count=" + entries.Count);
        }

        /// <summary>字符串与数值规范化；任意对象只保存域内真实引用，不执行业务ToString/GetHashCode/getter。</summary>
        /// <param name="value">需要检查或规范化的当前值；不执行其自定义方法。</param>
        /// <param name="references">当前遍历中用于建立不透明身份的对象序列。</param>
        /// <returns>当前 BCL 标量的规范表示或现存对象的不透明身份。</returns>
        private static string SnapshotScalar(object value, List<object> references)
        {
            if (value == null) return "null";
            if (value is string text) return "s:" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text));
            if (value is bool boolean) return boolean ? "b:1" : "b:0";
            if (value is int integer) return "i:" + integer;
            if (value is long ticks) return "l:" + ticks;
            if (value is Type type) return "t:" + type.Module.ModuleVersionId + ":" + type.FullName;
            if (value is Enum flag) return "e:" + flag.GetType().FullName + ":" + Convert.ToInt64(flag);
            if (value is Color color) return "color:" + BitConverter.ToString(BitConverter.GetBytes(color.r)) + ":" +
                BitConverter.ToString(BitConverter.GetBytes(color.g)) + ":" + BitConverter.ToString(BitConverter.GetBytes(color.b)) + ":" +
                BitConverter.ToString(BitConverter.GetBytes(color.a));
            if (!references.Any(reference => ReferenceEquals(reference, value))) references.Add(value);
            return "ref:" + WeakIdentityKey(value);
        }

        /// <summary>为已存在对象分配同域WeakGUID，不调用Unity/vendor属性、哈希或构造器。</summary>
        /// <param name="target">已经存在的实际目标对象，不创建实例。</param>
        /// <returns>绑定当前域与现存对象的弱身份字符串。</returns>
        private static string WeakIdentityKey(object target)
        {
            return s_DomainIdentity + "|" + (target == null ? "static" :
                s_WeakIdentities.GetValue(target, ignored => new WeakIdentity()).Value);
        }

        /// <summary>生成可写入Receipt的Graph处理器身份，首项固定域标记使空事件也不能跨重载重用。</summary>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>包含域标记和有序委托身份的序列化证据。</returns>
        internal static string[] CaptureGraphUiHandlerKeys(CancellationToken token)
        {
            return new[] { s_DomainIdentity + "|graph-snapshot" }.Concat(CaptureGraphUiHandlers(token)
                .Select(GraphHandlerKey)).ToArray();
        }

        /// <summary>保留旧身份顺序与同域约束，只允许至多一个已审Graph首次UI注册新增。</summary>
        /// <param name="before">同域执行前冻结的完整状态。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>原订阅有序保留且仅发生允许的固定新增时为 null，否则为错误。</returns>
        internal static string VerifyGraphUiHandlerKeys(string[] before, CancellationToken token)
        {
            if (before == null || before.Length == 0 || before[0] != s_DomainIdentity + "|graph-snapshot")
                return "Graph UI快照缺失或已跨域";
            Delegate[] after = CaptureGraphUiHandlers(token);
            int oldIndex = 1;
            int additions = 0;
            foreach (Delegate handler in after)
            {
                string key = GraphHandlerKey(handler);
                if (oldIndex < before.Length && key == before[oldIndex]) { oldIndex++; continue; }
                if (++additions > 1 || !IsExactGraphUiHandler(handler, token)) return "Graph UI处理器有未声明新增";
            }
            return oldIndex == before.Length ? null : "Graph UI原有处理器被移除或重排";
        }

        /// <summary>用完整模块MVID、方法token及同域target WeakGUID表达委托，不序列化委托或对象引用。</summary>
        /// <param name="handler">当前已订阅的单一实际委托；只读身份，不调用它。</param>
        /// <returns>绑定 MVID、方法 token 与同域目标身份的字符串。</returns>
        private static string GraphHandlerKey(Delegate handler)
        {
            return handler.Method.Module.ModuleVersionId + "|" + handler.Method.MetadataToken + "|" + WeakIdentityKey(handler.Target);
        }

        /// <summary>捕获Console双击处理器身份供执行后复核；其cctor已逐条确认为空引用、false与UI常量赋值。</summary>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>当前有序实际委托数组；不调用处理器。</returns>
        internal static Delegate[] CaptureGraphUiHandlers(CancellationToken token)
        {
            Type console = Owner("UnityEditor.ConsoleWindow", token);
            object value = ReadStatic(console, "entryWithManagedCallbackDoubleClicked");
            return value == null ? Array.Empty<Delegate>() : ((Delegate)value).GetInvocationList();
        }

        /// <summary>保留原有顺序与target身份，只允许正常Graph首次初始化增加至多一个已审固定闭包，不调用它。</summary>
        /// <param name="before">同域执行前冻结的完整状态。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>当前有序订阅满足固定新增合同则为 null，否则为错误。</returns>
        internal static string VerifyGraphUiHandlers(Delegate[] before, CancellationToken token)
        {
            if (before == null) return "缺少Graph UI处理器执行前快照";
            Delegate[] after = CaptureGraphUiHandlers(token);
            int oldIndex = 0;
            int additions = 0;
            foreach (Delegate handler in after)
            {
                if (oldIndex < before.Length && handler.Method == before[oldIndex].Method &&
                    ReferenceEquals(handler.Target, before[oldIndex].Target))
                { oldIndex++; continue; }
                if (++additions > 1 || !IsExactGraphUiHandler(handler, token))
                    return "Console双击订阅出现未声明变化";
            }
            return oldIndex == before.Length ? null : "原有Console双击订阅被移除或重排";
        }

        /// <summary>穿透已审EditorBridge闭包的delegate字段，比对实际Graph lambda方法与target的完整模块身份。</summary>
        /// <param name="handler">当前已订阅的单一实际委托；只读身份，不调用它。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        private static bool IsExactGraphUiHandler(Delegate handler, CancellationToken token)
        {
            VerifyModule(handler.Method.DeclaringType, token);
            object bridge = handler.Target;
            if (bridge == null || bridge.GetType().FullName !=
                    "Unity.GraphToolsAuthoringFramework.InternalEditorBridge.EditorBridge+<>c__DisplayClass2_0" ||
                handler.Method.DeclaringType != bridge.GetType() || handler.Method.Name !=
                    "<SetEntryDoubleClickedDelegate>g__CallEntryDoubleClickedCallback|0") return false;
            object value = ReadInstance(bridge, "doubleClickedCallback");
            if (!(value is Delegate callback) || callback.GetInvocationList().Length != 1) return false;
            VerifyModule(callback.Method.DeclaringType, token);
            return callback.Target != null && callback.Target.GetType() == callback.Method.DeclaringType &&
                callback.Method.DeclaringType.FullName == "Unity.GraphToolkit.Editor.GraphViewEditorWindow+<>c" &&
                callback.Method.Name == "<SetupLogStickyCallback>b__119_0";
        }

        /// <summary>只读取导入后主类型；已存在字体/纹理会拒绝，缺失路径不触发资产加载或字体atlas getter。</summary>
        /// <param name="paths">本次冻结的精确路径集合，不授予递归写权限。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>已审自然分支满足时为 null，否则为精确阻断原因。</returns>
        private static string CheckImportedMainTypes(string[] paths, CancellationToken token)
        {
            VerifyModule(typeof(AssetDatabase), token);
            foreach (string path in paths)
            {
                token.ThrowIfCancellationRequested();
                Type main = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (main == typeof(Texture2D) || main == typeof(Font) || main?.FullName == "TMPro.TMP_FontAsset" ||
                    main?.FullName == "UnityEngine.TextCore.Text.FontAsset") return "TMP/TextCore会处理本次主类型：" + path;
            }
            return null;
        }

        /// <summary>NodeClassCache已标InitializeOnLoad；只枚举真实Dictionary，不调用缓存getter，不清删除GUID。</summary>
        /// <param name="callback">已绑定的实际回调类型。</param>
        /// <param name="paths">本次冻结的精确路径集合，不授予递归写权限。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>已审自然分支满足时为 null，否则为精确阻断原因。</returns>
        private static string CheckSubGraphCache(Type callback, string[] paths, CancellationToken token)
        {
            if (HasSuffix(paths, "shadersubgraph")) return "本次路径命中SubGraph入口";
            Type owner = Owner("UnityEditor.ShaderGraph.NodeClassCache", token);
            object cache = ReadStatic(owner, "m_KnownSubGraphLookupTable");
            if (cache == null) return null;
            IDictionary dictionary = Dictionary(cache);
            foreach (string path in paths)
                if (ContainsOrdinalKey(dictionary, AssetDatabase.AssetPathToGUID(path))) return "删除/移动路径命中SubGraph缓存";
            return null;
        }

        /// <summary>额外绑定PWA getter源；没有配置key时自然返回null，否则仅接受现有manager actions避免setter。</summary>
        /// <param name="paths">本次冻结的精确路径集合，不授予递归写权限。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>已审自然分支满足时为 null，否则为精确阻断原因。</returns>
        private static string CheckInputActions(string[] paths, CancellationToken token)
        {
            if (paths.Contains("ProjectSettings/InputManager.asset", StringComparer.Ordinal)) return "输入迁移路径命中";
            VerifyExtraSource("com.unity.inputsystem", "InputSystem/Editor/ProjectWideActions/ProjectWideActionsBuildProvider.cs", token);
            VerifyModule(typeof(EditorBuildSettings), token);
            if (!EditorBuildSettings.GetConfigObjectNames().Contains("com.unity.input.settings.actions", StringComparer.Ordinal)) return null;
            Type owner = Owner("UnityEngine.InputSystem.InputSystem", token);
            object manager = ReadStatic(owner, "s_Manager");
            return manager != null && manager.GetType() == Owner("UnityEngine.InputSystem.InputManager", token) &&
                !UnityNull(ReadInstance(manager, "m_Actions")) ? null : "PWA配置存在且manager actions为空，正常回调可能赋值";
        }

        /// <summary>只读ScriptableSingleton backing；首次自然创建仅能发生在已审无FilePath的空类型上。</summary>
        /// <param name="owner">字段或单例 backing 的已核声明类型。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>现存 backing 单例或 null，不调用 instance getter。</returns>
        private static object Singleton(Type owner, CancellationToken token)
        {
            Type parent = owner.BaseType;
            if (parent == null || !parent.IsGenericType || parent.GetGenericTypeDefinition().FullName != "UnityEditor.ScriptableSingleton`1")
                throw new InvalidOperationException("单例继承关系漂移");
            VerifyModule(parent, token);
            object instance = ReadStatic(parent, "s_Instance");
            if (!UnityNull(instance) && instance.GetType() != owner) throw new InvalidOperationException("单例backing不是精确owner类型");
            return instance;
        }

        /// <summary>Brush列表不为null时FlushCache必达State；即使空列表，也核m_CachedEditor防止DestroyImmediate。</summary>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>已审自然分支满足时为 null，否则为精确阻断原因。</returns>
        private static string CheckBrushes(CancellationToken token)
        {
            VerifyExtraSource("com.unity.2d.tilemap", "Editor/GridPaintingState.cs", token);
            Type stateType = Owner("UnityEditor.Tilemaps.GridPaintingState", token);
            object state = Singleton(stateType, token);
            if (!UnityNull(state) && ReadInstance(state, "m_SavingPalette") is bool saving && saving) return null;
            object brushes = Singleton(Owner("UnityEditor.Tilemaps.GridPaletteBrushes", token), token);
            if (UnityNull(brushes)) return null;
            object list = ReadInstance(brushes, "m_Brushes");
            if (list == null) return null;
            if (!EmptyConcreteList(list)) return "Brush列表含对象，FlushCache可能DestroyImmediate";
            return UnityNull(state) || UnityNull(ReadInstance(state, "m_CachedEditor")) ? null :
                "空Brush列表仍会FlushCache并销毁现存绘图Editor";
        }

        /// <summary>只在完整pending队列均为已审PrefColor时接纳cache或九key原字符串精确同值支路；不调用Load/Set。</summary>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>已审自然分支满足时为 null，否则为精确阻断原因。</returns>
        private static string CheckClipboard(CancellationToken token)
        {
            Type clipboard = Owner("UnityEditor.Tilemaps.GridPaintPaletteClipboard", token);
            if (!NoLoaded(clipboard)) return "存在clipboard；延后重置会改变现有编辑状态";
            Type prefs = Owner("UnityEditor.PrefSettings", token);
            Type colorType = Owner("UnityEditor.PrefColor", token);
            IDictionary values = SortedList(ReadStatic(prefs, "m_Prefs"));
            object pending = ReadStatic(prefs, "m_AddedPrefs");
            if (pending == null || !pending.GetType().IsGenericType || pending.GetType().GetGenericTypeDefinition() != typeof(List<>))
                return "PrefSettings pending不是精确List";
            bool maySetSameValue = false;
            foreach (object value in (IEnumerable)pending)
            {
                if (value == null || value.GetType() != colorType) return "PrefSettings pending含未知IPrefType消费者";
                if (!(ReadInstance(value, "m_Loaded") is bool loaded) || !(ReadInstance(value, "m_Name") is string key))
                    return "PrefColor pending字段shape漂移";
                if (loaded) continue;
                if (!s_ColorMaterializationKeys.Contains(key, StringComparer.Ordinal)) return "pending含本九key外未加载颜色：" + key;
                string reason = CheckClipboardPreferenceKey(values, key, colorType, token, out bool writes);
                if (reason != null) return reason;
                maySetSameValue |= writes;
            }
            foreach (string key in s_ColorMaterializationKeys)
            {
                string reason = CheckClipboardPreferenceKey(values, key, colorType, token, out bool writes);
                if (reason != null) return reason;
                maySetSameValue |= writes;
            }
            return maySetSameValue ? CheckPreferenceConsumers(prefs, s_ColorMaterializationKeys.Where(key =>
                FindOrdinalValue(values, key) == null).ToArray(), token) : null;
        }

        /// <summary>只接纳已审三类真实订阅，并按实际key与target字段证明全部消费者自然返回，不调用它们。</summary>
        /// <param name="prefs">已绑定的 PrefSettings 声明类型，不通过 getter 初始化。</param>
        /// <param name="changedKeys">本轮已冻结且可能自然同值 SetString 的精确键集合。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>已审自然分支满足时为 null，否则为精确阻断原因。</returns>
        private static string CheckPreferenceConsumers(Type prefs, string[] changedKeys, CancellationToken token)
        {
            object value = ReadStatic(prefs, "settingChanged");
            if (value == null) return null;
            if (!(value is Delegate handlers)) return "settingChanged backing不是Delegate";
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                MethodInfo method = handler.Method;
                VerifyModule(method.DeclaringType, token);
                if (method.ReturnType != typeof(void) || method.GetParameters().Length != 2 ||
                    method.GetParameters()[0].ParameterType != typeof(string) || method.GetParameters()[1].ParameterType != typeof(Type))
                    return "settingChanged订阅签名漂移";
                if (method.DeclaringType.FullName == "UnityEditor.RectTransformEditorUtility" && method.Name == "OnSettingChanged" &&
                    method.MetadataToken == 100686179 && method.IsStatic && handler.Target == null)
                {
                    if (changedKeys.Contains("Scene/RectTransform Wire", StringComparer.Ordinal)) return "触发RectTransform颜色更新";
                    continue;
                }
                if (method.DeclaringType.FullName == "UnityEditor.SceneViewLightingColors" && method.Name == "UpdatePrefColors" &&
                    method.MetadataToken == 100663428 && !method.IsStatic && handler.Target != null && handler.Target.GetType() == method.DeclaringType)
                {
                    if (ReadInstance(handler.Target, "m_ContentRoot") != null)
                    {
                        string lightingReason = ReadLightingColors(handler.Target, changedKeys, token, out Color[] ignored);
                        if (lightingReason != null) return lightingReason;
                    }
                    continue;
                }
                if (method.DeclaringType.FullName == "UnityEditor.Overlays.OverlayPrefs" && method.Name == "OnSettingsChanged" &&
                    method.MetadataToken == 100711870 && !method.IsStatic && handler.Target != null && handler.Target.GetType() == method.DeclaringType)
                {
                    object keys = ReadInstance(handler.Target, "m_ColorPrefKeys");
                    if (keys == null || keys.GetType() != typeof(HashSet<string>)) return "Overlay颜色key集合类型漂移";
                    if (((HashSet<string>)keys).Any(key => changedKeys.Contains(key, StringComparer.Ordinal)))
                        return "同值颜色Set命中Overlay颜色key，会安排样式重建";
                    continue;
                }
                return "settingChanged存在未审方法/target消费者：" + method.DeclaringType.FullName + "." + method.Name;
            }
            return null;
        }

        /// <summary>捕获已审消费者的自然返回字段；保持SceneView内容/场景身份与Overlay key集合、dirty和样式引用稳定。</summary>
        /// <param name="rows">当前捕获生成的规范化元数据行。</param>
        /// <param name="prefs">已绑定的 PrefSettings 声明类型，不通过 getter 初始化。</param>
        /// <param name="references">当前遍历中用于建立不透明身份的对象序列。</param>
        /// <param name="lightingExpected">本轮固定 Lighting 消费者允许的精确目标状态行。</param>
        /// <param name="lightingObserved">当前只读得到的固定 Lighting 原生颜色/颜色空间状态行。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        private static void CapturePreferenceConsumerRows(List<string> rows, Type prefs, List<object> references,
            List<string> lightingExpected, List<string> lightingObserved, CancellationToken token)
        {
            object value = ReadStatic(prefs, "settingChanged");
            if (!(value is Delegate handlers)) return;
            IDictionary prefValues = SortedList(ReadStatic(prefs, "m_Prefs"));
            bool willNotify = s_ColorMaterializationKeys.Any(key => FindOrdinalValue(prefValues, key) == null);
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                token.ThrowIfCancellationRequested();
                object target = handler.Target;
                if (target == null) continue;
                string prefix = "prefs:consumer:" + WeakIdentityKey(target) + ":";
                if (target.GetType().FullName == "UnityEditor.SceneViewLightingColors")
                {
                    foreach (string field in new[] { "m_ContentRoot", "m_PrefColorSwatches", "m_SceneView", "m_ShouldDisplay",
                        "m_BackfaceHighlightsContent", "m_AlbedoContent", "m_GlobalIlluminationContent", "m_TexelValidityContent", "m_UVOverlapContent" })
                        rows.Add(prefix + field + "=" + SnapshotScalar(ReadInstance(target, field), references));
                    if (ReadInstance(target, "m_ContentRoot") != null)
                    {
                        string reason = ReadLightingColors(target, s_ColorMaterializationKeys, token, out Color[] colors);
                        if (reason != null) throw new InvalidOperationException(reason);
                        CaptureMap(rows, prefix + "swatches", ReadInstance(target, "m_PrefColorSwatches"), references, -1);
                        ColorSpace colorSpace = PlayerSettings.colorSpace;
                        rows.Add(prefix + "expectedColorSpace=" + SnapshotScalar(colorSpace, references));
                        for (int i = 0; i < colors.Length; i++)
                            rows.Add(prefix + "legendInput:" + s_LightingColorKeys[i] + "=" + SnapshotScalar(colors[i], references));
                        for (int i = 0; i < s_LightingShaderKeys.Length; i++)
                        {
                            Color expected = colorSpace == ColorSpace.Linear ? colors[i].linear : colors[i];
                            string key = "shader:" + s_LightingShaderKeys[i] + "=";
                            Color observed = Shader.GetGlobalColor(s_LightingShaderKeys[i]);
                            lightingExpected.Add(key + SnapshotScalar(willNotify ? expected : observed, references));
                            lightingObserved.Add(key + SnapshotScalar(observed, references));
                        }
                        string lastKey = prefix + "m_LastKnownColorSpace=";
                        object lastKnown = ReadInstance(target, "m_LastKnownColorSpace");
                        lightingExpected.Add(lastKey + SnapshotScalar(willNotify ? (object)colorSpace : lastKnown, references));
                        lightingObserved.Add(lastKey + SnapshotScalar(lastKnown, references));
                    }
                }
                if (target.GetType().FullName == "UnityEditor.Overlays.OverlayPrefs")
                {
                    foreach (string field in new[] { "m_StyleSheetDirty", "styleSheet", "m_Windows" })
                        rows.Add(prefix + field + "=" + SnapshotScalar(ReadInstance(target, field), references));
                    object keys = ReadInstance(target, "m_ColorPrefKeys");
                    if (keys == null || keys.GetType() != typeof(HashSet<string>)) throw new InvalidOperationException("Overlay key集合漂移");
                    foreach (string key in ((HashSet<string>)keys).OrderBy(key => key, StringComparer.Ordinal))
                        rows.Add(prefix + "key=" + SnapshotScalar(key, references));
                }
            }
        }

        /// <summary>从已创建swatch字典读取静态源对象的已加载字段，避开Lighting cctor及PrefColor getter；只接纳固定七个单色对象。</summary>
        /// <param name="target">已经存在的实际目标对象，不创建实例。</param>
        /// <param name="changedKeys">本轮已冻结且可能自然同值 SetString 的精确键集合。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <param name="colors">成功时读取的七个已加载 PrefColor 当前颜色，不调用 Color getter。</param>
        /// <returns>七个实际颜色对象及消费者分支完整满足时为 null，否则为具体错误。</returns>
        private static string ReadLightingColors(object target, string[] changedKeys, CancellationToken token, out Color[] colors)
        {
            colors = new Color[s_LightingColorKeys.Length];
            VerifyModule(typeof(Shader), token);
            VerifyModule(typeof(PlayerSettings), token);
            Type colorType = Owner("UnityEditor.PrefColor", token);
            object value = ReadInstance(target, "m_PrefColorSwatches");
            if (value == null || !value.GetType().IsGenericType || value.GetType().GetGenericTypeDefinition() != typeof(Dictionary<,>) ||
                value.GetType().GetGenericArguments()[0] != colorType || value.GetType().GetGenericArguments()[1].FullName != "UnityEngine.UIElements.VisualElement")
                return "Lighting swatch字典shape漂移";
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in (IDictionary)value)
            {
                if (entry.Key == null || entry.Key.GetType() != colorType || !(ReadInstance(entry.Key, "m_Name") is string name))
                    return "Lighting swatch含未知PrefColor";
                int index = Array.IndexOf(s_LightingColorKeys, name);
                if (index < 0 || !seen.Add(name) || changedKeys.Contains(name, StringComparer.Ordinal))
                    return "Lighting swatch包含非固定/重复/本次命中的key：" + name;
                if (!(ReadInstance(entry.Key, "m_Loaded") is bool loaded) || !loaded)
                    return "Lighting正常Color getter尚会加载额外偏好：" + name;
                if (!(ReadInstance(entry.Key, "m_SeparateColors") is bool separate) || separate)
                    return "Lighting颜色启用双主题，需另证现有EditorGUIUtility生命周期：" + name;
                if (!(ReadInstance(entry.Key, "m_Color") is Color color)) return "Lighting颜色字段shape漂移";
                colors[index] = color;
            }
            return seen.Count == s_LightingColorKeys.Length ? null : "Lighting swatch缺少固定七个已加载颜色";
        }

        /// <summary>已有cache只读；无cache时只接纳已有EditorPrefs字符串经原厂格式完整roundtrip同字节，缺key不制造默认值。</summary>
        /// <param name="values">已核具体 BCL 集合或当前字典，不调用自定义容器。</param>
        /// <param name="key">当前精确键或依赖名称。</param>
        /// <param name="colorType">完整模块绑定后的精确 PrefColor 类型。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <param name="maySetSameValue">成功时指明正常 Load 是否会对该已有键执行同值 SetString。</param>
        /// <returns>已审自然分支满足时为 null，否则为精确阻断原因。</returns>
        private static string CheckClipboardPreferenceKey(IDictionary values, string key, Type colorType,
            CancellationToken token, out bool maySetSameValue)
        {
            maySetSameValue = false;
            object cached = FindOrdinalValue(values, key);
            if (cached != null) return cached.GetType() == colorType ? null : "颜色cache类型不是精确PrefColor：" + key;
            VerifyModule(typeof(EditorPrefs), token);
            if (!EditorPrefs.HasKey(key)) return "颜色偏好key不存在，正常Load会新建持久设置，非同值支路：" + key;
            string original = EditorPrefs.GetString(key);
            if (!TryGetClipboardColorRoundTrip(key, original, out string expected, out string error)) return error;
            if (!string.Equals(original, expected, StringComparison.Ordinal)) return "正常Load会重排/规范化颜色字符串，非同值支路：" + key;
            maySetSameValue = true;
            return null;
        }

        /// <summary>逐条复现PrefColor的5/9段格式、逗号替换与Float(167)解析，再以当前Culture按原厂G格式写出预期字符串。</summary>
        /// <param name="key">当前精确键或依赖名称。</param>
        /// <param name="original">已存在且只读取得的偏好原始字符串。</param>
        /// <param name="expected">按原厂格式与当前 Culture 推导的完整偏好目标字符串。</param>
        /// <param name="error">失败时的精确原因；成功时为 null。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        internal static bool TryGetClipboardColorRoundTrip(string key, string original, out string expected, out string error)
        {
            expected = null; error = null;
            if (!s_ColorMaterializationKeys.Contains(key, StringComparer.Ordinal) || string.IsNullOrEmpty(original))
            { error = "颜色偏好key或原字符串为空/不在固定九key内"; return false; }
            string[] parts = original.Split(';');
            if ((parts.Length != 5 && parts.Length != 9) || parts[0] != key)
            { error = "颜色偏好原字符串不符合固定key的5/9段格式：" + key; return false; }
            var outputs = new string[parts.Length]; outputs[0] = key;
            for (int i = 1; i < parts.Length; i++)
            {
                if (!float.TryParse(parts[i].Replace(',', '.'), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float value) || float.IsNaN(value) || float.IsInfinity(value))
                { error = "颜色偏好含无法自然完整解析的通道：" + key; return false; }
                outputs[i] = value.ToString(null, System.Globalization.CultureInfo.CurrentCulture);
            }
            expected = string.Join(";", outputs);
            return true;
        }

        /// <summary>对九项颜色的稳定语义统一成现cache或原字符串预期通道，使正常首次cache物化前后可比。</summary>
        /// <param name="rows">当前捕获生成的规范化元数据行。</param>
        /// <param name="values">已核具体 BCL 集合或当前字典，不调用自定义容器。</param>
        /// <param name="references">当前遍历中用于建立不透明身份的对象序列。</param>
        private static void CaptureClipboardSemanticRows(List<string> rows, IDictionary values, List<object> references)
        {
            foreach (string key in s_ColorMaterializationKeys)
            {
                string prefix = "prefs:clipboard:" + SnapshotScalar(key, references) + ":";
                bool exists = EditorPrefs.HasKey(key);
                string original = exists ? EditorPrefs.GetString(key) : null;
                rows.Add(prefix + "raw=" + SnapshotScalar(original, references));
                object cached = FindOrdinalValue(values, key);
                if (cached != null)
                {
                    foreach (string field in new[] { "m_Name", "m_Color", "m_SeparateColors", "m_OptionalDarkColor" })
                        rows.Add(prefix + field + "=" + SnapshotScalar(ReadInstance(cached, field), references));
                    continue;
                }
                if (!TryGetClipboardColorRoundTrip(key, original, out string ignored, out string error))
                    throw new InvalidOperationException(error);
                string[] parts = original.Split(';');
                float[] channels = parts.Skip(1).Select(part => float.Parse(part.Replace(',', '.'),
                    System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                rows.Add(prefix + "m_Name=" + SnapshotScalar(key, references));
                rows.Add(prefix + "m_Color=" + SnapshotScalar(new Color(channels[0], channels[1], channels[2], channels[3]), references));
                rows.Add(prefix + "m_SeparateColors=" + SnapshotScalar(parts.Length == 9, references));
                rows.Add(prefix + "m_OptionalDarkColor=" + SnapshotScalar(parts.Length == 9 ?
                    new Color(channels[4], channels[5], channels[6], channels[7]) : new Color(0, 0, 0, 0), references));
            }
        }

        /// <summary>为Plan与Receipt列出固定九key的精确原/预期同值与cache-only分支，不写偏好或构造目标对象。</summary>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>固定九键的原值、预期同值与 cache-only 效果列表。</returns>
        internal static string[] CaptureClipboardPreferenceEffects(CancellationToken token)
        {
            Type prefs = Owner("UnityEditor.PrefSettings", token);
            IDictionary values = SortedList(ReadStatic(prefs, "m_Prefs"));
            var rows = new List<string>();
            foreach (string key in s_ColorMaterializationKeys)
            {
                bool exists = EditorPrefs.HasKey(key);
                string original = exists ? EditorPrefs.GetString(key) : null;
                if (FindOrdinalValue(values, key) != null)
                { rows.Add(key + "|cache-only|original=" + (original ?? "<missing>") + "|expected=" + (original ?? "<missing>")); continue; }
                if (!TryGetClipboardColorRoundTrip(key, original, out string expected, out string error) || original != expected)
                    throw new InvalidOperationException(error ?? "颜色偏好不是同字节支路");
                rows.Add(key + "|idempotent-SetString|original=" + original + "|expected=" + expected);
            }
            return rows.ToArray();
        }

        /// <summary>冻结完整pending顺序/对象WeakGUID/精确字段与事件身份；仅供Plan到Execute前比对，不调用消费者。</summary>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>完整已审 pending 顺序、对象、字段和消费者的同域 SHA。</returns>
        internal static string CaptureClipboardPendingFingerprint(CancellationToken token)
        {
            Type prefs = Owner("UnityEditor.PrefSettings", token);
            Type colorType = Owner("UnityEditor.PrefColor", token);
            var rows = new List<string> { s_DomainIdentity };
            var references = new List<object>();
            object pending = ReadStatic(prefs, "m_AddedPrefs");
            if (pending == null || !pending.GetType().IsGenericType || pending.GetType().GetGenericTypeDefinition() != typeof(List<>))
                throw new InvalidOperationException("PrefSettings pending类型漂移");
            int index = 0;
            foreach (object value in (IEnumerable)pending)
            {
                if (value == null || value.GetType() != colorType) throw new InvalidOperationException("pending含未知IPrefType消费者");
                string prefix = "pending:" + index++ + ":" + WeakIdentityKey(value) + ":";
                foreach (string field in new[] { "m_Name", "m_Color", "m_DefaultColor", "m_SeparateColors",
                    "m_OptionalDarkColor", "m_OptionalDarkDefaultColor", "m_Loaded" })
                    rows.Add(prefix + field + "=" + SnapshotScalar(ReadInstance(value, field), references));
            }
            foreach (string field in new[] { "settingChanged", "settingsReverted" })
                rows.Add(field + "=" + SnapshotScalar(ReadStatic(prefs, field), references));
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(
                System.Text.Encoding.UTF8.GetBytes(string.Join("\n", rows)))).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>GridPalettes只清派生缓存；无订阅时允许其已审空单例的自然初始化。</summary>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>已审自然分支满足时为 null，否则为精确阻断原因。</returns>
        private static string CheckPalettes(CancellationToken token)
        {
            VerifyExtraSource("com.unity.2d.tilemap", "Editor/GridPaintingState.cs", token);
            object state = Singleton(Owner("UnityEditor.Tilemaps.GridPaintingState", token), token);
            if (!UnityNull(state) && ReadInstance(state, "m_SavingPalette") is bool saving && saving) return null;
            return ReadStatic(Owner("UnityEditor.Tilemaps.GridPalettes", token), "palettesChanged") == null ? null :
                "palettesChanged有实际订阅";
        }

        /// <summary>每次比较完整TypeCache工厂元数据；空缓存与ImportWorker首次初始化均只构造唯一已审空工厂。</summary>
        /// <param name="paths">本次冻结的精确路径集合，不授予递归写权限。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>已审自然分支满足时为 null，否则为精确阻断原因。</returns>
        private static string CheckSpriteFactoryMetadata(string[] paths, CancellationToken token)
        {
            Type factories = Owner("UnityEditor.U2D.Sprites.SpriteDataProviderFactories", token);
            Type contract = Owner("UnityEditor.U2D.Sprites.ISpriteDataProviderFactory`1", token);
            Type expected = Owner("UnityEditor.U2D.Sprites.TextureImporterDataProviderFactory", token);
            VerifyExtraSource("com.unity.2d.sprite", "Editor/SpriteEditor/SpriteEditorWindow.cs", token);
            VerifyExtraSource("com.unity.2d.sprite", "Editor/SpriteEditorModule/TextureImporterDataProvider.cs", token);
            Type[] actual = TypeCache.GetTypesDerivedFrom(contract).ToArray();
            if (actual.Length != 1 || actual[0] != expected || expected.TypeInitializer != null)
                return "Sprite factory完整roster不等于唯一已审无cctor工厂";
            Type[] arguments = expected.GetInterfaces().Where(type => type.IsGenericType &&
                type.GetGenericTypeDefinition() == contract).Select(type => type.GetGenericArguments()[0]).ToArray();
            string[] allowed = { "UnityEngine.Texture2D", "UnityEngine.Sprite", "UnityEditor.TextureImporter", "UnityEngine.GameObject" };
            if (arguments.Length != 4 || arguments.Select(type => type.FullName).OrderBy(x => x, StringComparer.Ordinal)
                .SequenceEqual(allowed.OrderBy(x => x, StringComparer.Ordinal)) == false)
                return "Sprite factory接口集合漂移";
            object cached = ReadStatic(factories, "s_Factories");
            if (cached != null)
            {
                if (!(cached is Array entries) || entries.Length != 4) return "已初始化Sprite工厂表不等于四项";
                var observed = new List<string>();
                foreach (object entry in entries)
                {
                    if (entry == null || entry.GetType().FullName !=
                        "UnityEditor.U2D.Sprites.SpriteDataProviderFactories+SpriteDataProviderFactory")
                        return "Sprite缓存entry类型漂移";
                    Type match = ReadInstance(entry, "methodType") as Type;
                    MethodInfo method = ReadInstance(entry, "method") as MethodInfo;
                    object instance = ReadInstance(entry, "instance");
                    if (match == null || !allowed.Contains(match.FullName, StringComparer.Ordinal) || instance == null ||
                        instance.GetType() != expected || method == null || method.Name != "CreateDataProvider" ||
                        method.IsStatic || !method.DeclaringType.IsGenericType ||
                        method.DeclaringType.GetGenericTypeDefinition() != contract ||
                        method.DeclaringType.GetGenericArguments()[0] != match || method.GetParameters().Length != 1 ||
                        method.GetParameters()[0].ParameterType != match)
                        return "Sprite缓存methodType/接口方法/instance与已审四接口不符";
                    observed.Add(match.FullName);
                }
                if (!observed.OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(allowed.OrderBy(value => value, StringComparer.Ordinal)))
                    return "Sprite缓存存在重复或缺少匹配项";
            }
            // 精确写权限与实际资产角色分开：ProjectSettings状态XML不派发Importer；meta只对应其主资产一次。
            if (!AndroidResolveCodeEditorTailContract.TryGetPotentialDispatchPaths(paths, out string[] importerPaths, out string roleError))
                return roleError;
            foreach (string path in importerPaths)
            {
                token.ThrowIfCancellationRequested();
                Type overrideType = ReadImporterOverride(path, token);
                if (overrideType != null && overrideType != typeof(PluginImporter) && overrideType != typeof(AssetImporter))
                    return "模板存在ScriptedImporter或非原生override：" + path;
                Type[] available = ReadAvailableImporters(path, token);
                if (available != null && available.Any(type => type != typeof(PluginImporter) && type != typeof(AssetImporter)))
                    return "模板可选Importer包含ScriptedImporter或未审类型：" + path;
                AssetImporter importer = AssetImporter.GetAtPath(path);
                if (importer != null && importer.GetType() != typeof(PluginImporter) && importer.GetType() != typeof(AssetImporter))
                    return "模板存在非原生PluginImporter/AssetImporter：" + path;
            }
            // 无论主域缓存已存在还是Worker从null初始化，均不执行Init、Activator或provider方法。
            return null;
        }

        /// <summary>精确复现已审disabled表达式；不调用EnsureInitialized，也不把未初始化false当作已禁用。</summary>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>已审自然分支满足时为 null，否则为精确阻断原因。</returns>
        private static string CheckMultiplayer(CancellationToken token)
        {
            Type owner = Owner("Unity.Multiplayer.PlayMode.Editor.MigrationUtility", token);
            return ReadStatic(owner, "s_Initialized") is bool initialized && initialized &&
                ReadStatic(owner, "s_TestPackageInstalled") is bool tests && !tests &&
                ReadStatic(owner, "s_IsMppmPackageInstalled") is bool package &&
                ReadStatic(owner, "s_IsVirtualProjectsInPackage") is bool virtualProjects && (!package || virtualProjects) ? null :
                "Multiplayer初始化/禁用表达式未满足";
        }

        /// <summary>使用实际已有原生importer的公开PresetType(Object)，不构造Importer、不使用internal Type构造器。</summary>
        /// <param name="paths">本次冻结的精确路径集合，不授予递归写权限。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>已审自然分支满足时为 null，否则为精确阻断原因。</returns>
        private static string CheckPresets(string[] paths, CancellationToken token)
        {
            VerifyModule(typeof(Preset), token);
            var representatives = new List<AssetImporter>();
            foreach (string path in paths)
            {
                token.ThrowIfCancellationRequested();
                AssetImporter importer = AssetImporter.GetAtPath(path);
                if (importer != null) representatives.Add(importer);
            }
            representatives.AddRange(Resources.FindObjectsOfTypeAll(typeof(AssetImporter)).OfType<AssetImporter>());
            foreach (Type type in new[] { typeof(PluginImporter), typeof(AssetImporter) })
            {
                AssetImporter representative = representatives.FirstOrDefault(value => value.GetType() == type);
                if (representative == null) return "缺少已导入原生Importer供公开PresetType(Object)读取：" + type.FullName;
                if (Preset.GetDefaultPresetsForType(new PresetType(representative)).Length != 0)
                    return "原生Importer存在默认Preset：" + type.FullName;
            }
            return null;
        }

        /// <summary>实际SceneView订阅已由当前完整Core DLL证明只重置派生breadcrumb flag，不调用场景/窗口getter。</summary>
        /// <param name="type">已加载且需要核对的实际类型元数据。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>已审自然分支满足时为 null，否则为精确阻断原因。</returns>
        private static string CheckAssetEvents(Type type, CancellationToken token)
        {
            object value = ReadStatic(type, "assetsChangedOnHDD");
            if (value == null) return null;
            if (!(value is Delegate callbacks)) return "AssetEvents backing不是Delegate";
            Type expected = Owner("UnityEditor.SceneViewStageHandling", token);
            foreach (Delegate handler in callbacks.GetInvocationList())
                if (handler.Target == null || handler.Target.GetType() != expected || handler.Method.DeclaringType != expected ||
                    handler.Method.Name != "AssetsChangedOnHDD" || handler.Method.MetadataToken != 100693590 ||
                    handler.Method.IsStatic || handler.Method.ReturnType != typeof(void) ||
                    handler.Method.GetParameters().Length != 4 || handler.Method.GetParameters().Any(parameter => parameter.ParameterType != typeof(string[])))
                    return "AssetEvents存在非已审SceneViewStageHandling handler/target";
            return null;
        }

        /// <summary>不Resolve/启用service；只接纳当前精确proxy、已启用、已审两个handler及timestamp非零的增量分支。</summary>
        /// <param name="paths">本次冻结的精确路径集合，不授予递归写权限。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>已审自然分支满足时为 null，否则为精确阻断原因。</returns>
        private static string CheckPackageProxy(string[] paths, CancellationToken token)
        {
            Type owner = Owner("UnityEditor.PackageManager.UI.Internal.ServicesContainer", token);
            object container = Singleton(owner, token);
            if (UnityNull(container)) return "ServicesContainer尚未存在，正常回调会初始化service图";
            IDictionary services = Dictionary(ReadInstance(container, "m_RegisteredServices"));
            Type proxyType = Owner("UnityEditor.PackageManager.UI.Internal.AssetDatabaseProxy", token);
            object proxy = FindTypeValue(services, proxyType);
            if (proxy == null || proxy.GetType() != proxyType || !ReadInheritedBoolean(proxy, "m_Enabled"))
                return "实际AssetDatabaseProxy缺失/未启用，Resolve会改变service生命周期";
            if (!ReferenceEquals(FindTypeValue(services, Owner("UnityEditor.PackageManager.UI.Internal.IAssetDatabaseProxy", token)), proxy))
                return "UPM具体/接口服务alias不是同一实际proxy";
            var callbacks = ReadInstance(proxy, "onPostprocessAllAssets") as Delegate;
            if (callbacks == null) return null;
            Delegate[] handlers = callbacks.GetInvocationList();
            if (handlers.Length != 2) return "UPM proxy handler集合不等于ret lambda+PageRefreshHandler";
            object page = null;
            foreach (Delegate handler in handlers)
            {
                MethodInfo method = handler.Method;
                VerifyModule(method.DeclaringType, token);
                if (method.ReturnType != typeof(void) || method.GetParameters().Length != 4 ||
                    method.GetParameters().Any(parameter => parameter.ParameterType != typeof(string[])))
                    return "UPM proxy handler签名漂移";
                if (method.DeclaringType.FullName == "UnityEditor.PackageManager.UI.Internal.AssetDatabaseProxy+<>c" &&
                    method.Name == "<.ctor>b__15_0" && method.MetadataToken == 100705068 && !method.IsStatic &&
                    handler.Target != null && handler.Target.GetType() == method.DeclaringType) continue;
                if (method.DeclaringType.FullName != "UnityEditor.PackageManager.UI.Internal.PageRefreshHandler" ||
                    method.Name != "OnPostprocessAllAssets" || method.MetadataToken != 100704558 || method.IsStatic || handler.Target == null ||
                    handler.Target.GetType() != method.DeclaringType || page != null)
                    return "UPM proxy包含未审handler或target";
                page = handler.Target;
            }
            if (page == null || !ReferenceEquals(ReadInstance(page, "m_AssetDatabase"), proxy))
                return "PageRefreshHandler未绑定同一实际proxy";
            IDictionary timestamps = Dictionary(ReadInstance(page, "m_RefreshTimestamps"));
            object timestamp = null;
            foreach (DictionaryEntry entry in timestamps)
                if (entry.Key.GetType().FullName == "UnityEditor.PackageManager.UI.Internal.RefreshOptions" &&
                    Convert.ToInt32(entry.Key) == 64) timestamp = entry.Value;
            if (!(timestamp is long ticks) || ticks == 0) return "UPM timestamp[64]缺失/为0，会执行全量RefreshImportedAssets";
            object client = ReadInstance(page, "m_AssetStoreClient");
            Type clientType = Owner("UnityEditor.PackageManager.UI.Internal.AssetStoreClientV2", token);
            if (client == null || client.GetType() != clientType || !ReferenceEquals(ReadInstance(client, "m_AssetDatabase"), proxy))
                return "UPM实际client/proxy不是已审AssetStoreClientV2";
            object cache = ReadInstance(client, "m_AssetStoreCache");
            if (cache == null || cache.GetType() != Owner("UnityEditor.PackageManager.UI.Internal.AssetStoreCache", token))
                return "UPM imported cache类型不是已审实现";
            IDictionary imported = Dictionary(ReadInstance(cache, "m_ImportedAssets"));
            foreach (string path in paths)
            {
                token.ThrowIfCancellationRequested();
                if (ContainsOrdinalKey(imported, path)) return "精确刷新路径存在AssetStore imported cache，会移除/更新package";
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid)) continue;
                object origin = ReadAssetOrigin(guid, token);
                if (origin != null && ReadInstance(origin, "productId") is int productId && productId > 0)
                    return "导入路径已有有效AssetStore origin，会更新imported cache";
            }
            return null;
        }

        /// <summary>读取已绑定CoreModule原生Importer候选集合，避免已有路径之外的新增目标选择未审ScriptedImporter。</summary>
        /// <param name="path">当前检查的精确路径，不授予目录通配权限。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>当前精确路径的实际可用 importer 类型列表。</returns>
        private static Type[] ReadAvailableImporters(string path, CancellationToken token)
        {
            VerifyModule(typeof(AssetDatabase), token);
            MethodInfo method = typeof(AssetDatabase).GetMethod("GetAvailableImporters", BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
            if (method == null || method.ReturnType != typeof(Type[])) throw new MissingMethodException("GetAvailableImporters(string)");
            return (Type[])method.Invoke(null, new object[] { path });
        }

        /// <summary>读取已绑定CoreModule的原生Importer override，不调用ScriptedImporter或改变默认导入器。</summary>
        /// <param name="path">当前检查的精确路径，不授予目录通配权限。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>当前精确路径已有 importer override 类型，或 null。</returns>
        private static Type ReadImporterOverride(string path, CancellationToken token)
        {
            VerifyModule(typeof(AssetDatabase), token);
            MethodInfo method = typeof(AssetDatabase).GetMethod("GetImporterOverride", BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
            if (method == null || method.ReturnType != typeof(Type)) throw new MissingMethodException("GetImporterOverride(string)");
            return (Type)method.Invoke(null, new object[] { path });
        }

        /// <summary>只调用已绑定CoreModule中精确签名的原生只读origin查询；不执行AssetStore或vendor方法。</summary>
        /// <param name="guid">当前注册资产的精确 GUID。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>当前 GUID 已有来源信息，不创建或修改资产。</returns>
        private static object ReadAssetOrigin(string guid, CancellationToken token)
        {
            VerifyModule(typeof(AssetDatabase), token);
            MethodInfo method = typeof(AssetDatabase).GetMethod("GetAssetOrigin", BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
            Type origin = Owner("UnityEditor.AssetOrigin", token);
            if (method == null || method.ReturnType != origin) throw new MissingMethodException("GetAssetOrigin(string)");
            return method.Invoke(null, new object[] { guid });
        }

        /// <summary>只读取已加载且身份明确的owner模块；相同全名多份加载立即拒绝，不加载任何程序集。</summary>
        /// <param name="name">固定完整类型名或已审字段名，不接受业务输入作为反射指令。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>唯一已加载且完整模块已核对的类型。</returns>
        private static Type Owner(string name, CancellationToken token)
        {
            Type[] matches = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name, false))
                .Where(type => type != null).Distinct().ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("owner缺失或同名冲突：" + name);
            VerifyModule(matches[0], token);
            return matches[0];
        }

        /// <summary>额外owner一律冻结整个DLL及实际MVID；不能仅凭与某回调同名/同程序集名称接纳实现。</summary>
        /// <param name="type">已加载且需要核对的实际类型元数据。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        private static void VerifyModule(Type type, CancellationToken token)
        {
            string filename = Path.GetFileName(type.Assembly.Location);
            if (!s_Modules.TryGetValue(filename, out string[] expected) ||
                type.Module.ModuleVersionId.ToString() != expected[1] || FileHash(type.Assembly.Location, token) != expected[0])
                throw new InvalidOperationException("owner DLL/MVID未绑定或已漂移：" + type.FullName);
        }

        /// <summary>新增下游源独立冻结PackageInfo实际解析位置与完整源摘要，不修改包或使用猜测PackageCache路径。</summary>
        /// <param name="packageName">已审源码归属的精确包名。</param>
        /// <param name="source">包内已审完整源文件相对路径。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        private static void VerifyExtraSource(string packageName, string source, CancellationToken token)
        {
            string[] expected = s_ExtraSources.Single(row => row[0] == packageName && row[1] == source);
            PackageInfo[] packages = PackageInfo.GetAllRegisteredPackages();
            PackageInfo package = packages.SingleOrDefault(value => value.name == packageName);
            if (package == null || FileHash(Path.Combine(package.resolvedPath, source), token) != expected[2])
                throw new InvalidOperationException("新增下游源码漂移：" + packageName + "/" + source);
        }

        /// <summary>分块只读摘要并响应取消；每次执行重新计算，不复用可能过期的磁盘摘要。</summary>
        /// <param name="path">当前检查的精确路径，不授予目录通配权限。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>当前完整文件内容的 SHA，读取失败抛出异常。</returns>
        private static string FileHash(string path, CancellationToken token)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                var buffer = new byte[65536];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) != 0)
                { token.ThrowIfCancellationRequested(); sha.TransformBlock(buffer, 0, read, buffer, 0); }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>静态字段只有无cctor、已InitializeOnLoad或已逐条精读的纯常量/集合初始化才准读取。</summary>
        /// <param name="owner">字段或单例 backing 的已核声明类型。</param>
        /// <param name="name">固定完整类型名或已审字段名，不接受业务输入作为反射指令。</param>
        /// <returns>固定静态字段当前值；缺失时拒绝。</returns>
        private static object ReadStatic(Type owner, string name)
        {
            bool initializedOnLoad = CustomAttributeData.GetCustomAttributes(owner).Any(attribute =>
                attribute.AttributeType == typeof(InitializeOnLoadAttribute));
            bool auditedCacheInitializer = owner.IsGenericType &&
                owner.GetGenericTypeDefinition().FullName == "UnityEditor.ScriptableSingleton`1" ||
                owner.FullName == "UnityEditor.PrefSettings" || owner.FullName == "UnityEditor.MPE.DataService" ||
                owner.FullName == "UnityEditor.ConsoleWindow";
            if (owner.TypeInitializer != null && !initializedOnLoad && !auditedCacheInitializer)
                throw new InvalidOperationException("禁止Plan触发未证明的cctor：" + owner.FullName);
            FieldInfo field = owner.GetField(name, DeclaredFields);
            if (field == null || !field.IsStatic) throw new MissingFieldException(owner.FullName, name);
            return field.GetValue(null);
        }

        /// <summary>只读取实例的声明字段，不调用属性、虚方法或构造器；需先由调用点确认实例精确类型。</summary>
        /// <param name="instance">已经存在的目标实例，不初始化单例。</param>
        /// <param name="name">固定完整类型名或已审字段名，不接受业务输入作为反射指令。</param>
        /// <returns>固定实例字段当前值；缺失时拒绝。</returns>
        private static object ReadInstance(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(name, DeclaredFields);
            if (field == null || field.IsStatic) throw new MissingFieldException(instance.GetType().FullName, name);
            return field.GetValue(instance);
        }

        /// <summary>沿已绑定BaseService继承链读取bool backing，不执行enabled getter或EnableService。</summary>
        /// <param name="instance">已经存在的目标实例，不初始化单例。</param>
        /// <param name="name">固定完整类型名或已审字段名，不接受业务输入作为反射指令。</param>
        /// <returns>现存实例继承链中固定布尔字段的值。</returns>
        private static bool ReadInheritedBoolean(object instance, string name)
        {
            for (Type owner = instance.GetType(); owner != null; owner = owner.BaseType)
            {
                FieldInfo field = owner.GetField(name, DeclaredFields);
                if (field == null) continue;
                if (field.IsStatic || field.FieldType != typeof(bool)) throw new InvalidOperationException("service backing漂移");
                return (bool)field.GetValue(instance);
            }
            throw new MissingFieldException(instance.GetType().FullName, name);
        }

        /// <summary>只接受BCL真正Dictionary，不运行业务自定义IDictionary实现或比较器。</summary>
        /// <param name="value">需要检查或规范化的当前值；不执行其自定义方法。</param>
        /// <returns>已核具体 BCL Dictionary 的非泛型只读视图。</returns>
        private static IDictionary Dictionary(object value)
        {
            if (value == null || !value.GetType().IsGenericType || value.GetType().GetGenericTypeDefinition() != typeof(Dictionary<,>))
                throw new InvalidOperationException("字段不是精确Dictionary");
            return (IDictionary)value;
        }

        /// <summary>PrefSettings只接纳精确BCL SortedList，枚举既有值不会执行IPrefType.Load。</summary>
        /// <param name="value">需要检查或规范化的当前值；不执行其自定义方法。</param>
        /// <returns>已核具体 BCL SortedList 的非泛型只读视图。</returns>
        private static IDictionary SortedList(object value)
        {
            if (value == null || !value.GetType().IsGenericType || value.GetType().GetGenericTypeDefinition() != typeof(SortedList<,>))
                throw new InvalidOperationException("PrefSettings字段不是精确SortedList");
            return (IDictionary)value;
        }

        /// <summary>通过逐项字符串比较查询，避免Dictionary自定义comparer参与判定。</summary>
        /// <param name="values">已核具体 BCL 集合或当前字典，不调用自定义容器。</param>
        /// <param name="key">当前精确键或依赖名称。</param>
        /// <returns>Ordinal 精确键对应的现有值，未命中为 null。</returns>
        private static object FindOrdinalValue(IDictionary values, string key)
        {
            foreach (DictionaryEntry entry in values)
                if (entry.Key is string text && string.Equals(text, key, StringComparison.Ordinal)) return entry.Value;
            return null;
        }

        /// <summary>只做精确路径/Guid成员判断；不把空GUID、集合数量或近似名称当作同一项。</summary>
        /// <param name="values">已核具体 BCL 集合或当前字典，不调用自定义容器。</param>
        /// <param name="key">当前精确键或依赖名称。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        private static bool ContainsOrdinalKey(IDictionary values, string key)
        {
            foreach (DictionaryEntry entry in values)
                if (entry.Key is string text && string.Equals(text, key, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>服务解析仅匹配Dictionary中的精确Type键，不通过接口名称猜测或启用service。</summary>
        /// <param name="values">已核具体 BCL 集合或当前字典，不调用自定义容器。</param>
        /// <param name="key">当前精确键或依赖名称。</param>
        /// <returns>与实际类型精确相同的唯一现有服务对象。</returns>
        private static object FindTypeValue(IDictionary values, Type key)
        {
            foreach (DictionaryEntry entry in values) if (ReferenceEquals(entry.Key, key)) return entry.Value;
            return null;
        }

        /// <summary>读取精确BCL List.Count；自定义集合和非空List均拒绝，不执行元素回调。</summary>
        /// <param name="value">需要检查或规范化的当前值；不执行其自定义方法。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        private static bool EmptyConcreteList(object value)
        {
            return value != null && value.GetType().IsGenericType && value.GetType().GetGenericTypeDefinition() == typeof(List<>) &&
                ((ICollection)value).Count == 0;
        }

        /// <summary>仅枚举已存在对象，不打开窗口、不加载资产、不触发目标Type的cctor。</summary>
        /// <param name="type">已加载且需要核对的实际类型元数据。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        private static bool NoLoaded(Type type)
        {
            return Resources.FindObjectsOfTypeAll(type).All(value => value == null);
        }

        /// <summary>保留Unity已销毁对象的原生null语义，但不调用任意业务Equals或getter。</summary>
        /// <param name="value">需要检查或规范化的当前值；不执行其自定义方法。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        private static bool UnityNull(object value)
        {
            return value == null || value is UnityEngine.Object unityObject && unityObject == null;
        }

        /// <summary>每个精确路径含父目录都单独筛后缀；meta同时检查其真实资产路径。</summary>
        /// <param name="paths">本次冻结的精确路径集合，不授予递归写权限。</param>
        /// <param name="suffixes">已审需要排除的精确后缀列表。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        private static bool HasSuffix(string[] paths, params string[] suffixes)
        {
            foreach (string path in paths)
            {
                string asset = path.EndsWith(".meta", StringComparison.Ordinal) ? path.Substring(0, path.Length - 5) : path;
                if (suffixes.Any(suffix => asset.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))) return true;
            }
            return false;
        }
    }
}
