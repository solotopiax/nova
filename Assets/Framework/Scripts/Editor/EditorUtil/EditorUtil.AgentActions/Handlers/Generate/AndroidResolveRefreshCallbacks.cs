/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidResolveRefreshCallbacks.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   Android 模板刷新实际派发回调的身份与自然支路合同
 ***************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>只核对本次模板域会触发的 post-all / generic preprocess，不调用任何 vendor 回调。</summary>
    internal static class AndroidResolveRefreshCallbacks
    {
        /// <summary>固定已审类型与来源角色；预编译插件核发布 DLL，源码包核完整编译输入和真实所有者。</summary>
        private sealed class Binding
        {
            internal int Id;
            internal string Type;
            internal string Package;
            internal string Source;
            internal string SourceHash;
            internal string AssemblyHash;
            internal string Mvid;
        }

        private const int LegacyMemoryCallbackId = 53;
        private const string LegacyMemoryOwner = "NovaFramework.Editor.MemoryProtectionLegacyConfigMigration";

        private static readonly Binding[] s_Bindings =
        {
            new Binding { Id = 0, Type = "Firebase.Editor.GenerateXmlFromGoogleServicesJson", AssemblyHash = "b50eefa93d23c673bc8a97266e3d36dcfba0e9d3e49f5e7cc7850d595c12f1bf", Mvid = "c3292f5b-6663-4089-885f-13602a971adf" },
            new Binding { Id = 1, Type = "Firebase.Editor.SettingsWindowUpdater", AssemblyHash = "b50eefa93d23c673bc8a97266e3d36dcfba0e9d3e49f5e7cc7850d595c12f1bf", Mvid = "c3292f5b-6663-4089-885f-13602a971adf" },
            new Binding { Id = 2, Type = "Firebase.Editor.XcodeProjectPatcher", AssemblyHash = "b50eefa93d23c673bc8a97266e3d36dcfba0e9d3e49f5e7cc7850d595c12f1bf", Mvid = "c3292f5b-6663-4089-885f-13602a971adf" },
            new Binding { Id = 3, Type = "Google.PackageManagerResolver" },
            new Binding { Id = 4, Type = "Firebase.Messaging.Editor.AndroidManifestPatcher", AssemblyHash = "decbd91fb4c7a257882a2d07235fe825ddf33c62d6af935a4e15868f4b8a147d", Mvid = "671306aa-fb2c-42c3-883e-4387e6f3456e" },
            new Binding { Id = 5, Type = "Google.VersionHandlerImpl" },
            new Binding { Id = 6, Type = "GooglePlayServices.PlayServicesResolver" },
            new Binding { Id = 7, Type = "Google.IOSResolver" },
            new Binding { Id = 8, Type = "Facebook.Unity.Editor.FacebookImporter", AssemblyHash = "5645b83e43ed8179e72d9f0cacc267280df27e6cd2da00ec71ad17f6a18a80e1", Mvid = "948cdfa7-bc07-4962-9e42-d34623513f9a" },
            new Binding { Id = 9, Type = "UnityEditor.Timeline.SampleDependencyImporter+SamplePostprocessor", Package = "com.unity.timeline", Source = "Editor/Samples/SampleDependencyImporter.cs" },
            new Binding { Id = 10, Type = "TMPro.EditorUtilities.TMPro_TexturePostProcessor", Package = "com.unity.ugui", Source = "Editor/TMP/TMPro_TexturePostProcessor.cs" },
            new Binding { Id = 11, Type = "UnityEditor.Rendering.BuiltIn.MaterialPostprocessor", Package = "com.unity.shadergraph", Source = "Editor/Generation/Targets/BuiltIn/Editor/AssetPostProcessors/MaterialPostprocessor.cs" },
            new Binding { Id = 12, Type = "UnityEditor.ShaderGraph.NodeClassCache+PostProcessor", Package = "com.unity.shadergraph", Source = "Editor/Data/Nodes/NodeClassCache.cs" },
            new Binding { Id = 13, Type = "UnityEditor.ShaderGraph.ShaderGraphAssetPostProcessor", Package = "com.unity.shadergraph", Source = "Editor/Importers/ShaderGraphAssetPostProcessor.cs" },
            new Binding { Id = 14, Type = "UnityEngine.InputSystem.Editor.ProjectWideActionsAsset+ProjectSettingsProjectWideActionsAssetConverter+ProjectSettingsPostprocessor", Package = "com.unity.inputsystem", Source = "InputSystem/Editor/ProjectWideActions/ProjectWideActionsAsset.cs" },
            new Binding { Id = 15, Type = "UnityEngine.InputSystem.Editor.InputActionImporter+InputActionJsonNameModifierAssetProcessor", Package = "com.unity.inputsystem", Source = "InputSystem/Editor/AssetImporter/InputActionImporter.cs" },
            new Binding { Id = 16, Type = "UnityEngine.InputSystem.Editor.InputActionAssetEditor+InputActionAssetPostprocessor", Package = "com.unity.inputsystem", Source = "InputSystem/Editor/AssetImporter/InputActionAssetEditor.cs" },
            new Binding { Id = 17, Type = "Unity.Cinemachine.Editor.SampleDependencyImporter+SamplePostprocessor", AssemblyHash = "1c542cfc4c5e4606d914c9477ec7d92538724e3acdb18441f223de7ce1e2477e", Mvid = "24c33adf-41d7-41de-8efb-ee04c90b791c", Package = "com.unity.cinemachine", Source = "Editor/Samples/SampleDependencyImporter.cs", SourceHash = "43be14e6b1a73c7f2661975e83b85d0be2ecf7519f69d501cdd88c0f10ddf66f" },
            new Binding { Id = 18, Type = "Unity.Cinemachine.Editor.SampleDependencyImporter+DomainReloadProcessor", AssemblyHash = "1c542cfc4c5e4606d914c9477ec7d92538724e3acdb18441f223de7ce1e2477e", Mvid = "24c33adf-41d7-41de-8efb-ee04c90b791c", Package = "com.unity.cinemachine", Source = "Editor/Samples/SampleDependencyImporter.cs", SourceHash = "43be14e6b1a73c7f2661975e83b85d0be2ecf7519f69d501cdd88c0f10ddf66f" },
            new Binding { Id = 19, Type = "Unity.Cinemachine.Editor.CinemachineSettings", AssemblyHash = "1c542cfc4c5e4606d914c9477ec7d92538724e3acdb18441f223de7ce1e2477e", Mvid = "24c33adf-41d7-41de-8efb-ee04c90b791c", Package = "com.unity.cinemachine", Source = "Editor/Windows/CinemachineSettings.cs", SourceHash = "9b360bfa0c57d7a26678b694ad9aa5e54acd7baffef60e74e7cd7ed7ccefb709" },
            new Binding { Id = 20, Type = "UnityEditor.Tilemaps.GridPaletteBrushes+AssetProcessor", Package = "com.unity.2d.tilemap", Source = "Editor/GridPaletteBrushes.cs" },
            new Binding { Id = 21, Type = "UnityEditor.Tilemaps.GridPaintPaletteClipboard+AssetProcessor", Package = "com.unity.2d.tilemap", Source = "Editor/GridPaintPaletteClipboard.cs" },
            new Binding { Id = 22, Type = "UnityEditor.Tilemaps.GridPalettes+AssetProcessor", Package = "com.unity.2d.tilemap", Source = "Editor/GridPalettes.cs" },
            new Binding { Id = 23, Type = "UnityEditor.U2D.SpriteShapeAssetPostProcessor", Package = "com.unity.2d.spriteshape", Source = "Editor/SpriteShapeAssetPostProcessor.cs" },
            new Binding { Id = 24, Type = "UnityEditor.U2D.Sprites.SpriteEditorTexturePostprocessor", Package = "com.unity.2d.sprite", Source = "Editor/SpriteEditor/SpriteEditorWindow.cs" },
            new Binding { Id = 25, Type = "UnityEditor.U2D.PSD.PSDImporterAssetPostProcessor", Package = "com.unity.2d.psdimporter", Source = "Editor/PSDImporterAssetPostProcessor.cs" },
            new Binding { Id = 26, Type = "UnityEditor.U2D.PSD.PSDImportPostProcessor", Package = "com.unity.2d.psdimporter", Source = "Editor/PSDImportPostProcessor.cs" },
            new Binding { Id = 27, Type = "UnityEditor.U2D.Animation.SpriteLibraryEditor.SpriteLibraryAssetPostprocessor", Package = "com.unity.2d.animation", Source = "Editor/SpriteLib/SpriteLibraryEditor/SpriteLibraryAssetPostprocessor.cs" },
            new Binding { Id = 28, Type = "UnityEditor.U2D.Animation.SpritePostProcess", Package = "com.unity.2d.animation", Source = "Editor/SpritePostProcess.cs" },
            new Binding { Id = 29, Type = "DA_Assets.DM.DependencyManager+AssetWatcher", AssemblyHash = "c845543e1f8ce98854671ed57a61c4b693198240c7a517ed72b5ad6e5db9af8a", Mvid = "12c350c2-9bf8-4673-b1e4-0ab28cff2db8", Package = "com.da-assets.figma-converter", Source = "Core/DA-Assets/Dependency-Manager/Editor/DependencyManager.cs", SourceHash = "331acb068d20e8d1b0b2539482277e9018ffd0665280373886a6fa4996ac880e" },
            new Binding { Id = 30, Type = "UnityEditor.RetainedMode+RetainedModeAssetPostprocessor", AssemblyHash = "9bb68962a197695dc9cf12c37c38b3f5679304c6496e4655d6287319bd8b8b10", Mvid = "d572b22f-1d90-4477-9407-b36592117da9" },
            new Binding { Id = 31, Type = "Unity.UI.Builder.BuilderAssetPostprocessor", AssemblyHash = "e0217163f769359511b9e738e055a6d0634d8f62859233f36355b81f71abce13", Mvid = "81f60ad4-dc3c-4e56-8651-c0c682addd9d" },
            new Binding { Id = 32, Type = "UnityEditor.TextCore.Text.TextAssetPostProcessor", AssemblyHash = "5ccf7a5a43c7b0dc5e1280f9b38142115c8d2ced8ee5518806fffbcb0eb3ef70", Mvid = "7c6bb697-5409-4585-8b24-d9510bb2bad4" },
            new Binding { Id = 33, Type = "UnityEditor.TreeAOImporter", AssemblyHash = "2fd66c8f03b5dc44cf3ba26440b412124d2140bb20ae6ae56650054dfcd8eddc", Mvid = "0753dcb3-3f74-471d-ada8-ab0c97ef658d" },
            new Binding { Id = 34, Type = "UnityEditor.TerrainTools.TerrainToolbarOverlayPostProcessor", AssemblyHash = "2fd66c8f03b5dc44cf3ba26440b412124d2140bb20ae6ae56650054dfcd8eddc", Mvid = "0753dcb3-3f74-471d-ada8-ab0c97ef658d" },
            new Binding { Id = 35, Type = "UnityEditor.Search.AssetChangedListener", AssemblyHash = "0ca16197f4fcc755460371942d380ca092534d5fca9882caf032eb3837cd9b74", Mvid = "bf8d264b-6e3a-4341-a376-382a20f39d5a" },
            new Binding { Id = 36, Type = "Unity.PlayMode.Editor.PlayModeScenarioUtils+ConfigAssetsTracker", AssemblyHash = "e79ffcdc7cd6316a18745f92495cc86040c0b25d94a031932e3d85b07f15c5b6", Mvid = "f97cba61-85b3-49d3-bf47-b28da0807459" },
            new Binding { Id = 37, Type = "Unity.Multiplayer.PlayMode.Editor.ScenarioRunner+ScenarioDriftAssetsTracker", AssemblyHash = "d88ea26d238062106c0afc145109c0f42ff291dc931ce28cb95f7f6def7914fe", Mvid = "fe5e1b6f-1d28-4908-9514-1197f4df0b92" },
            new Binding { Id = 38, Type = "Unity.Multiplayer.PlayMode.Editor.AssetDatabaseCallbacks", AssemblyHash = "d88ea26d238062106c0afc145109c0f42ff291dc931ce28cb95f7f6def7914fe", Mvid = "fe5e1b6f-1d28-4908-9514-1197f4df0b92" },
            new Binding { Id = 39, Type = "Unity.GraphToolkit.Editor.WindowAssetPostprocessingWatcher", AssemblyHash = "a6ab78cb6935564242fedc8c32e97ca301678594283684b00d659a766a1c42d6", Mvid = "278bf535-ccd3-4fe5-a5d5-3d10d1431c76" },
            new Binding { Id = 40, Type = "UnityEditor.AssetEvents", AssemblyHash = "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", Mvid = "11f76f7e-1291-4a87-8174-9af3fa5b621a" },
            new Binding { Id = 41, Type = "UnityEditor.Presets.PresetManagerPostProcessor", AssemblyHash = "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", Mvid = "11f76f7e-1291-4a87-8174-9af3fa5b621a" },
            new Binding { Id = 42, Type = "UnityEditor.AudioContainerWindow+AudioContainerPostProcessor", AssemblyHash = "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", Mvid = "11f76f7e-1291-4a87-8174-9af3fa5b621a" },
            new Binding { Id = 43, Type = "UnityEditor.PlayerSettingsSplashScreenEditor+CancelSplashScreenOnAssetChange", AssemblyHash = "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", Mvid = "11f76f7e-1291-4a87-8174-9af3fa5b621a" },
            new Binding { Id = 44, Type = "UnityEditor.Rendering.RenderPipelineGlobalSettingsPostprocessor", AssemblyHash = "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", Mvid = "11f76f7e-1291-4a87-8174-9af3fa5b621a" },
            new Binding { Id = 45, Type = "UnityEditor.AudioMixerWindow+AudioMixerPostprocessor", AssemblyHash = "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", Mvid = "11f76f7e-1291-4a87-8174-9af3fa5b621a" },
            new Binding { Id = 46, Type = "UnityEditor.ModelImporterRigEditor+ReferencedClipsPostProcessor", AssemblyHash = "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", Mvid = "11f76f7e-1291-4a87-8174-9af3fa5b621a" },
            new Binding { Id = 47, Type = "UnityEditor.MPE.DataService+AssetEvents", AssemblyHash = "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", Mvid = "11f76f7e-1291-4a87-8174-9af3fa5b621a" },
            new Binding { Id = 48, Type = "UnityEditor.SpeedTree.Importer.SpeedTree9Postprocessor", AssemblyHash = "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", Mvid = "11f76f7e-1291-4a87-8174-9af3fa5b621a" },
            new Binding { Id = 49, Type = "UnityEditor.ArtifactBrowserPostProcessor", AssemblyHash = "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", Mvid = "11f76f7e-1291-4a87-8174-9af3fa5b621a" },
            new Binding { Id = 50, Type = "UnityEditor.PackageManager.UI.Internal.AssetDatabaseProxy+AssetPostprocessor", AssemblyHash = "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", Mvid = "11f76f7e-1291-4a87-8174-9af3fa5b621a" },
            new Binding { Id = 51, Type = "UnityEditor.Experimental.EditorResources+StyleCatalogPostProcessor", AssemblyHash = "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", Mvid = "11f76f7e-1291-4a87-8174-9af3fa5b621a" },
            new Binding { Id = 52, Type = "UnityEditor.SpeedTreePostProcessor", AssemblyHash = "14a25302095cb3c648b4b83b9936ba2df10454ae254da6440a1a968395f2d581", Mvid = "11f76f7e-1291-4a87-8174-9af3fa5b621a" },
            new Binding { Id = LegacyMemoryCallbackId, Type = "NovaFramework.Editor.MemoryProtectionLegacyConfigPostprocessor", Package = "com.solotopia.nova.framework.memory.protection", Source = "Nova/Editor/MemoryProtectionLegacyConfigMigration.cs" },
            new Binding { Id = 54, Type = "Unity.AI.Assistant.Editor.GraphRefreshPostprocessor", Package = "com.unity.ai.assistant", Source = "Editor/Assistant/GraphRefreshPostprocessor.cs" },
            new Binding { Id = 55, Type = "Unity.AI.Assistant.Editor.SkillsProjectAssetPostprocessor", Package = "com.unity.ai.assistant", Source = "Editor/Assistant/Skills/SkillsRegistryInitializer.cs" },
            new Binding { Id = 56, Type = "UnityEditor.Rendering.Universal.MaterialPostprocessor", Package = "com.unity.render-pipelines.universal", Source = "Editor/AssetPostProcessors/MaterialPostprocessor.cs" },
            new Binding { Id = 57, Type = "UnityEditor.Rendering.Universal.UniversalRenderPipelineGlobalSettingsPostprocessor", Package = "com.unity.render-pipelines.universal", Source = "Editor/AssetPostProcessors/UniversalRenderPipelineGlobalSettingsPostprocessor.cs" },
            new Binding { Id = 58, Type = "Unity.AI.Animate.Components.AnimateLoop+AssetReferenceReimportMonitor", Package = "com.unity.ai.assistant", Source = "Modules/Unity.AI.Animate/Components/AnimateLoop/AnimateLoop.cs" },
            new Binding { Id = 59, Type = "Unity.AI.Animate.Services.Stores.Actions.AssetReferenceReimportMonitor", Package = "com.unity.ai.assistant", Source = "Modules/Unity.AI.Animate/Services/Stores/Actions/AppActions.cs" },
            new Binding { Id = 60, Type = "Unity.AI.Assistant.Editor.TrackSelectedAssetsProcessor", Package = "com.unity.ai.assistant", Source = "Editor/Assistant/TrackSelectedAssetsProcessor.cs" },
            new Binding { Id = 61, Type = "Unity.AI.Generators.UI.ScaleToFitObjectFieldImage+AssetReferenceReimportMonitor", Package = "com.unity.ai.assistant", Source = "Modules/Unity.AI.Generators.UI/Manipulators/ScaleToFitObjectFieldImage.cs" },
            new Binding { Id = 62, Type = "Unity.AI.Image.Services.Stores.Actions.AssetReferenceReimportMonitor", Package = "com.unity.ai.assistant", Source = "Modules/Unity.AI.Image/Services/Stores/Actions/AppActions.cs" },
            new Binding { Id = 63, Type = "Unity.AI.Mesh.Services.Stores.Actions.AssetReferenceReimportMonitor", Package = "com.unity.ai.assistant", Source = "Modules/Unity.AI.Mesh/Services/Stores/Actions/AppActions.cs" },
            new Binding { Id = 64, Type = "Unity.AI.Pbr.Services.Stores.Actions.AssetReferenceReimportMonitor", Package = "com.unity.ai.assistant", Source = "Modules/Unity.AI.Pbr/Services/Stores/Actions/AppActions.cs" },
            new Binding { Id = 65, Type = "Unity.AI.Search.Editor.Knowledge.AssetChangeDetector", Package = "com.unity.ai.assistant", Source = "Editor/Unity.AI.Search.Editor/Services/Knowledge/AssetChangeDetector.cs" },
            new Binding { Id = 66, Type = "Unity.AI.Sound.Components.SoundEnvelope+AssetReferenceReimportMonitor", Package = "com.unity.ai.assistant", Source = "Modules/Unity.AI.Sound/Components/SoundEnvelope/SoundEnvelope.cs" },
            new Binding { Id = 67, Type = "Unity.AI.Sound.Services.Stores.Actions.AssetReferenceReimportMonitor", Package = "com.unity.ai.assistant", Source = "Modules/Unity.AI.Sound/Services/Stores/Actions/AppActions.cs" },
            new Binding { Id = 68, Type = "Unity.InferenceEngine.Editor.Visualizer.Editor.ModelAssetProcessor", Package = "com.unity.ai.inference", Source = "Editor/Visualizer/Editor/ModelAssetProcessor.cs" },
            new Binding { Id = 69, Type = "Unity.PlasticSCM.Editor.AssetUtils.Processor.UVCSAssetPostprocessor", Package = "com.unity.collab-proxy", Source = "Editor/AssetsUtils/Processor/UVCSAssetPostprocessor.cs" },
        };

        /// <summary>post-all 使用相同 56 flags，四参数优先于五参数；保留通用 preprocess，模型/纹理专用入口不扩大到模板域。</summary>
        /// <param name="type">已加载且需要核对的实际类型元数据。</param>
        /// <returns>Unity 当前实际方法绑定规则选中的相关方法列表，不调用这些方法。</returns>
        internal static MethodInfo[] GetDispatchMethods(Type type)
        {
            const BindingFlags post = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var methods = new List<MethodInfo>();
            Type[] four = { typeof(string[]), typeof(string[]), typeof(string[]), typeof(string[]) };
            MethodInfo all = type.GetMethod("OnPostprocessAllAssets", post, null, four, null) ??
                type.GetMethod("OnPostprocessAllAssets", post, null, four.Concat(new[] { typeof(bool) }).ToArray(), null);
            if (all != null) methods.Add(all);
            MethodInfo preprocess = type.GetMethod("OnPreprocessAsset", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (preprocess != null) methods.Add(preprocess);
            return methods.ToArray();
        }

        /// <summary>仅按已加载元数据枚举实际相关类型；读取字段和运行初始化均不在身份捕获阶段发生。</summary>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <param name="digest">成功时的稳定 SHA 摘要，不包含原始用户内容。</param>
        /// <param name="error">失败时的精确原因；成功时为 null。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        internal static bool TryCaptureIdentities(CancellationToken token, out string digest, out string error)
        {
            digest = null;
            error = null;
            using var sourceRead = AndroidRefreshSourceIdentityContract.BeginReadScope();
            var identities = new List<string>();
            var problems = new List<string>();
            bool observerFound = false;
            var assemblyHashes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Type type in TypeCache.GetTypesDerivedFrom<AssetPostprocessor>()
                         .OrderBy(type => type.FullName, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                MethodInfo[] methods = GetDispatchMethods(type);
                if (methods.Length == 0) continue;
                if (type == typeof(AndroidResolveRefreshObserver))
                { observerFound = true; identities.Add(type.FullName + "|Nova-observer-v1"); continue; }
                if (type == typeof(AndroidResolveCodeEditorTailObserver))
                { identities.Add(type.FullName + "|Nova-code-editor-tail-observer-v1"); continue; }
                Binding binding = s_Bindings.SingleOrDefault(item => item.Type == type.FullName);
                string reason = null;
                string sourceIdentity = null;
                if (binding == null || !VerifyBinding(type, binding, assemblyHashes, token, out sourceIdentity, out reason))
                { problems.Add(type.FullName + "：" + (binding == null ? "未审实际派发目标" : reason)); continue; }
                identities.Add(type.FullName + "|" + type.Module.ModuleVersionId.ToString() + "|" + assemblyHashes[type.Assembly.Location] + "|" + sourceIdentity + "|" +
                    string.Join(",", methods.Select(method => method.ToString()).OrderBy(value => value, StringComparer.Ordinal)));
            }
            if (!observerFound) problems.Add("Nova 自有刷新观察器没有出现在当前真实 TypeCache 派发域。");
            if (problems.Count != 0)
            { error = "资源导入/刷新实际回调身份不完整：" + string.Join("；", problems); return false; }
            digest = GenerateActionCommon.ComputeTextHash(string.Join("\n", identities));
            return true;
        }

        /// <summary>分别核发布插件与可重编译源码的真实身份；当前完整 DLL/MVID 仍进入原计划漂移摘要。</summary>
        /// <param name="type">已加载且需要核对的实际类型元数据。</param>
        /// <param name="binding">固定回调编号、包与源码角色；完整来源由对应已审合同维护。</param>
        /// <param name="hashes">当前一次身份捕获内的完整文件摘要缓存。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <param name="identity">来源、实际编译条件与当前映像身份，用于本次 Plan 冻结。</param>
        /// <param name="error">失败时的精确原因；成功时为 null。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        private static bool VerifyBinding(Type type, Binding binding, IDictionary<string, string> hashes,
            CancellationToken token, out string identity, out string error)
        {
            identity = null;
            error = null;
            if (type.BaseType != typeof(AssetPostprocessor))
            { error = "实际回调继承已变化。"; return false; }
            string location = type.Assembly.Location;
            if (!hashes.TryGetValue(location, out string hash))
                hashes[location] = hash = AndroidRefreshSourceIdentityContract.ScopedFileHash(location, token);
            string edmDll = binding.Id == 3 ? "Google.PackageManagerResolver.dll" : binding.Id == 5 ? "Google.VersionHandlerImpl.dll" :
                binding.Id == 6 ? "Google.JarResolver.dll" : binding.Id == 7 ? "Google.IOSResolver.dll" : null;
            if (edmDll != null) return AndroidEdmCompatibilityContract.TryGetIdentity(type, edmDll, token, out identity, out error);
            PackageInfo package = PackageInfo.FindForAssembly(type.Assembly);
            string source = package == null || string.IsNullOrEmpty(binding.Source) ? null : Path.Combine(package.resolvedPath, binding.Source);
            if (AndroidRefreshSourceIdentityContract.SupportsAssembly(type.Assembly.GetName().Name))
            {
                if (package?.name != binding.Package || string.IsNullOrEmpty(binding.Source) || !File.Exists(source))
                { error = "实际回调源码或 PackageInfo 归属不匹配。"; return false; }
                if (!AndroidRefreshSourceIdentityContract.TryVerifySourceAssembly(type, token, out identity, out error)) return false;
                return binding.Id != LegacyMemoryCallbackId || VerifyLegacyMemoryCompilerOwner(type, package, source, token, out error);
            }
            if (hash != binding.AssemblyHash || type.Module.ModuleVersionId.ToString() != binding.Mvid)
            { error = "预编译回调的实际 DLL/MVID 与已审发布实现不符。"; return false; }
            identity = hash + "|" + binding.Mvid + "|" + binding.SourceHash;
            if (binding.Package == null) return true;
            if (package?.name != binding.Package || !File.Exists(source) ||
                GenerateActionCommon.ComputeFileHash(source, token) != binding.SourceHash)
            { error = "完整源码或实际包归属与已审实现不符"; return false; }
            return true;
        }

        /// <summary>模板路径只准原生 Plugin/Default 导入域；父目录名称也逐项检查，避免后缀过滤的旁路。</summary>
        /// <param name="paths">本次冻结的精确路径集合，不授予递归写权限。</param>
        /// <param name="error">失败时的精确原因；成功时为 null。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        internal static bool IsTemplatePathDomain(string[] paths, out string error)
        {
            error = null;
            if (paths == null || paths.Length == 0) { error = "精确刷新路径为空。"; return false; }
            string[] sensitive = { ".cs", ".dll", ".asmdef", ".asmref", ".asset", ".inputactions", ".mat", "shadergraph",
                "shadersubgraph", ".hlsl", ".hlslinc", ".cginc", ".uxml", ".uss", ".tss", ".psd", ".psb", ".st", ".st9",
                ".spm", ".mixer", ".json", ".plist", ".xml", ".swift", ".jslib", ".shader", ".png", ".prefab", ".unity" };
            foreach (string path in paths)
            {
                if (path == AndroidResolveActionCommon.ResolutionStatePath) continue;
                string asset = path.EndsWith(".meta", StringComparison.Ordinal) ? path.Substring(0, path.Length - 5) : path;
                if (!asset.StartsWith("Assets/", StringComparison.Ordinal) || asset.Length > 1024 || asset.Contains("@") ||
                    sensitive.Any(extension => asset.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
                { error = "刷新路径触发本模板合同外导入语义：" + path; return false; }
            }
            return true;
        }

        /// <summary>对当前动态 backing 状态判自然支路；允许明确列出的派生缓存失效，不伪称所有回调无副作用。</summary>
        /// <param name="paths">本次冻结的精确路径集合，不授予递归写权限。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <param name="digest">成功时的稳定 SHA 摘要，不包含原始用户内容。</param>
        /// <param name="error">失败时的精确原因；成功时为 null。</param>
        /// <param name="provenHostDomain">仅同域且回调摘要已匹配的重核复用原正常 update 见证。</param>
        /// <param name="independentAiStores">新计划将五个Store交独立角色合同，其余默认包只走精确冷态读取规则。</param>
        /// <param name="independentUvcs">仅含独立UVCS快照的新计划使用已证日志after；旧快照继续真实四参合同。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        internal static bool TryCaptureNaturalBranches(string[] paths, CancellationToken token, out string digest, out string error, string provenHostDomain = null, bool independentAiStores = false, bool independentUvcs = false)
        {
            digest = null;
            error = null;
            if (!IsTemplatePathDomain(paths, out error)) return false;
            using var sourceRead = AndroidRefreshSourceIdentityContract.BeginReadScope();
            var rows = new List<string>();
            var problems = new List<string>();
            foreach (Binding binding in s_Bindings)
            {
                token.ThrowIfCancellationRequested();
                Type type = FindType(binding.Type);
                if (type == null || GetDispatchMethods(type).Length == 0) continue;
                try
                {
                    string reason = binding.Id == LegacyMemoryCallbackId
                        ? CheckLegacyMemoryBranch(type, paths, token, provenHostDomain)
                        : CheckBranch(binding.Id, type, paths, token, provenHostDomain, independentAiStores, independentUvcs);
                    if (reason == null) rows.Add(binding.Type + "|template-natural-v2|" + (UsesPeripheralContract(binding.Id) ? AndroidResolvePeripheralRefreshContracts.DeclaredEffects(binding.Id) :
                        binding.Id == LegacyMemoryCallbackId ? "own-legacy-config-inert-queued-false-v1" : "separately-bound-vendor-search-speedtree"));
                    else problems.Add(binding.Type + "：" + reason);
                }
                catch (Exception exception) when (!(exception is OperationCanceledException))
                { problems.Add(binding.Type + "：只读动态字段不能证明（" + exception.GetType().Name + "）"); }
            }
            if (problems.Count != 0)
            { error = "资源导入/刷新自然支路未闭合：" + string.Join("；", problems); return false; }
            digest = GenerateActionCommon.ComputeTextHash(string.Join("\n", rows));
            return true;
        }

        /// <summary>完整身份绑定后选择独立自然合同；厂商、Search、SpeedTree 由主合同另行捕获和后验，不伪称无副作用。</summary>
        /// <param name="id">完整源码和模块绑定表中的精确回调编号。</param>
        /// <param name="type">已加载且需要核对的实际类型元数据。</param>
        /// <param name="paths">本次冻结的精确路径集合，不授予递归写权限。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <param name="provenHostDomain">只有已冻结回调身份相等时，才可继承正常宿主初始化见证。</param>
        /// <param name="independentAiStores">是否使用新计划的固定AI冷态合同，不影响旧快照的严格路径。</param>
        /// <param name="independentUvcs">UVCS由独立前后态合同覆盖时为true，不授予首次物化许可。</param>
        /// <returns>对应独立合同可接纳时返回 null，否则返回具体原因。</returns>
        private static string CheckBranch(int id, Type type, string[] paths, CancellationToken token, string provenHostDomain, bool independentAiStores, bool independentUvcs)
        {
            if (id >= 0 && id <= 8 || id == 35 || id == 48) return null;
            return AndroidResolvePeripheralRefreshContracts.Check(id, type, paths, token, provenHostDomain, independentAiStores, independentUvcs);
        }

        /// <summary>仅捕获本次真实参与且外围 Check 已通过的编号；未加载且未参与的类型不被虚构为空。</summary>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <param name="independentAiStores">仅新计划排除已由独立快照完整覆盖的五个Store，其余编号保持原比较。</param>
        /// <param name="independentUvcs">只读取独立UVCS合同的已证after，不在外围比较首次创建日志项。</param>
        /// <returns>仅覆盖当前实际已通过检查的外围参与者快照。</returns>
        internal static AndroidResolvePeripheralRefreshContracts.StableSnapshot CapturePeripheralSnapshot(CancellationToken token, bool independentAiStores = false, bool independentUvcs = false)
        {
            using var sourceRead = AndroidRefreshSourceIdentityContract.BeginReadScope();
            int[] ids = s_Bindings.Where(binding => UsesPeripheralContract(binding.Id))
                .Where(binding => FindType(binding.Type) is Type type && GetDispatchMethods(type).Length != 0)
                .Where(binding => !independentAiStores || !AndroidResolveAiStoreContract.IsStoreId(binding.Id)).Select(binding => binding.Id).ToArray();
            return AndroidResolvePeripheralRefreshContracts.CaptureStableSnapshot(ids, token, independentAiStores, independentUvcs);
        }

        /// <summary>排除外围Store前核当前实际派发集合与独立快照逐项相等，不接受请求传入排除名单。</summary>
        internal static bool HasCompleteAiStoreCoverage(AndroidResolveAiStoreContract.Snapshot snapshot)
        {
            int[] actual = s_Bindings.Where(binding => AndroidResolveAiStoreContract.IsStoreId(binding.Id))
                .Where(binding => FindType(binding.Type) is Type type && GetDispatchMethods(type).Length != 0)
                .Select(binding => binding.Id).OrderBy(value => value).ToArray();
            return snapshot?.stores != null && snapshot.stores.All(item => item != null) &&
                snapshot.stores.Select(item => item.id).SequenceEqual(actual);
        }

        /// <summary>专属旧配置回调不进入通用外围名称、效应和状态快照表；既有编号规则保持。</summary>
        /// <param name="id">完整绑定表中的固定编号。</param>
        /// <returns>该编号确实由通用外围合同处理时为真。</returns>
        internal static bool UsesPeripheralContract(int id) => id >= 9 && id != 35 && id != 48 && id != LegacyMemoryCallbackId;

        /// <summary>只核对正常 Unity Editor 编译所有者与其自有源/asmdef，不加载程序集或初始化迁移类型。</summary>
        /// <param name="type">已通过完整编译来源与当前加载映像核验的实际回调类型。</param>
        /// <param name="package">Unity 对该程序集登记的真实包。</param>
        /// <param name="source">已核精确源码物理路径。</param>
        /// <param name="token">本次只读捕获取消令牌。</param>
        /// <param name="error">身份不完整时的具体原因。</param>
        /// <returns>唯一正常编译归属及初始化属性匹配时为真。</returns>
        private static bool VerifyLegacyMemoryCompilerOwner(Type type, PackageInfo package, string source,
            CancellationToken token, out string error)
        {
            error = null;
            const string name = "NovaFramework.MemoryProtection.Editor";
            if (type.Assembly.GetName().Name != name ||
                AppDomain.CurrentDomain.GetAssemblies().Count(item => item.GetName().Name == name) != 1)
            { error = "旧配置回调不是唯一已加载的 Unity 编译程序集。"; return false; }
            var owners = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Editor)
                .Where(item => item.name == name).ToArray();
            if (owners.Length != 1 || string.IsNullOrEmpty(owners[0].outputPath) || owners[0].sourceFiles == null)
            { error = "旧配置回调缺唯一 Unity Editor 编译所有者。"; return false; }
            string root = GenerateActionCommon.ProjectRoot;
            Func<string, string> physical = value => Path.GetFullPath(value.StartsWith(package.assetPath + "/", StringComparison.Ordinal)
                ? Path.Combine(package.resolvedPath, value.Substring(package.assetPath.Length + 1))
                : Path.IsPathRooted(value) ? value : Path.Combine(root, value));
            string definition = UnityEditor.Compilation.CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(name);
            string expectedDefinition = Path.GetFullPath(Path.Combine(package.resolvedPath, "Nova/Editor/NovaFramework.MemoryProtection.Editor.asmdef"));
            if (physical(owners[0].outputPath) != Path.GetFullPath(type.Assembly.Location) ||
                !owners[0].sourceFiles.Any(value => physical(value) == Path.GetFullPath(source)) ||
                string.IsNullOrEmpty(definition) || physical(definition) != expectedDefinition ||
                GenerateActionCommon.ComputeFileHash(expectedDefinition, token) != "33fb988a3294c96d4309c2b59e8bb420abfbb904eabbf71384638d547794bebd")
            { error = "旧配置回调的编译输出、源码或 asmdef 归属已变化。"; return false; }
            Type owner = type.Assembly.GetType(LegacyMemoryOwner, false);
            if (owner == null || !owner.IsAbstract || !owner.IsSealed ||
                !CustomAttributeData.GetCustomAttributes(owner).Any(item => item.AttributeType == typeof(InitializeOnLoadAttribute)))
            { error = "旧配置迁移 owner 或 Unity InitializeOnLoad 身份不完整。"; return false; }
            return true;
        }

        /// <summary>严格无 asset 域中该回调不派生迁移；只在正常宿主初始化依据成立后读取既有 queued 字段。</summary>
        /// <param name="type">已通过实际派发和编译归属核验的回调类型。</param>
        /// <param name="paths">包含资产、meta 和父目录的完整冻结域。</param>
        /// <param name="token">本次只读捕获取消令牌。</param>
        /// <param name="provenHostDomain">仅写前/写后复核传入已冻结且回调摘要一致的当前域；新 Plan 必须从真实 update 栈取得见证。</param>
        /// <returns>自然支路可接纳时为空，否则为具体阻断原因。</returns>
        private static string CheckLegacyMemoryBranch(Type type, string[] paths, CancellationToken token, string provenHostDomain)
        {
            if (!IsTemplatePathDomain(paths, out string pathError)) return pathError;
            Binding binding = s_Bindings.Single(item => item.Id == LegacyMemoryCallbackId);
            if (!VerifyBinding(type, binding, new Dictionary<string, string>(StringComparer.Ordinal), token, out _, out string bindingError))
                return bindingError;
            Type owner = type.Assembly.GetType(LegacyMemoryOwner, false);
            string contextError = AndroidResolveRefreshContract.CheckLegacyMigrationHost(owner, provenHostDomain);
            if (contextError != null) return contextError;
            FieldInfo queued = owner.GetField("s_Queued", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
            if (queued == null || queued.FieldType != typeof(bool) || queued.IsInitOnly)
                return "旧配置迁移 queued 字段身份改变。";
            // 前置已证明正常 Unity 初始化阶段及编译所有者；此处不以首次 GetValue 充当初始化手段。
            return queued.GetValue(null) is bool pending && !pending ? null : "旧配置迁移仍在正常队列中；等待自然返回后重新 Plan。";
        }

        /// <summary>只在已加载元数据找精确全名，不加载程序集、不调用单例 getter 或类型初始化入口。</summary>
        /// <param name="name">固定完整类型名或已审字段名，不接受业务输入作为反射指令。</param>
        /// <returns>唯一已加载的实际类型，或 null；不加载程序集。</returns>
        private static Type FindType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name, false)).Where(type => type != null).Distinct().SingleOrDefault();
        }
    }
}
