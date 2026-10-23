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
        /// <summary>完整 DLL/MVID 及有源码包的完整源文件绑定；不是按类型名称猜测实现。</summary>
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
            new Binding { Id = 3, Type = "Google.PackageManagerResolver", AssemblyHash = "fba008a3876e13ec9acaff7e4dc75f88781c4ab16416648cf37ab939be6e1699", Mvid = "722dd370-da1f-4152-8c4e-276677b83385" },
            new Binding { Id = 4, Type = "Firebase.Messaging.Editor.AndroidManifestPatcher", AssemblyHash = "decbd91fb4c7a257882a2d07235fe825ddf33c62d6af935a4e15868f4b8a147d", Mvid = "671306aa-fb2c-42c3-883e-4387e6f3456e" },
            new Binding { Id = 5, Type = "Google.VersionHandlerImpl", AssemblyHash = "c3a6d0419b41b0cbc7ad518b0cc2b311af043b5d71ed709ea3baa576b79862ce", Mvid = "57534187-1fe2-4de8-b0f0-c598228153ae" },
            new Binding { Id = 6, Type = "GooglePlayServices.PlayServicesResolver", AssemblyHash = "75283de7763a24b04bf29f75fa1f90fe6113ef8cf99cf89edc67ffe8e7b3c9d0", Mvid = "f55ed918-6037-46ea-9954-936cacb5de0b" },
            new Binding { Id = 7, Type = "Google.IOSResolver", AssemblyHash = "ca00858a5f4a657e949480015124f9c783ea80c42bb6000a9c5ec0112ffa5ca5", Mvid = "38a009e7-a985-40fe-b3c8-6f87b1103a7e" },
            new Binding { Id = 8, Type = "Facebook.Unity.Editor.FacebookImporter", AssemblyHash = "5645b83e43ed8179e72d9f0cacc267280df27e6cd2da00ec71ad17f6a18a80e1", Mvid = "948cdfa7-bc07-4962-9e42-d34623513f9a" },
            new Binding { Id = 9, Type = "UnityEditor.Timeline.SampleDependencyImporter+SamplePostprocessor", AssemblyHash = "06207e491fde41d6348bf400dbfecc889fd48d6d7dd7ab0cbbb623af92219ffc", Mvid = "5fcf00c2-be8c-4b4a-9e7d-45bdcbac5861", Package = "com.unity.timeline", Source = "Editor/Samples/SampleDependencyImporter.cs", SourceHash = "cf732aa188dd3d2e700015858f7a9c0a3676df5578ef0481994019edb60818e7" },
            new Binding { Id = 10, Type = "TMPro.EditorUtilities.TMPro_TexturePostProcessor", AssemblyHash = "25384482a1f8c12f38ca475d59c9b0eb7eef8d7d48b2d8a51f0e3c63d6c6f3c9", Mvid = "6cc3a159-b8f4-44bd-a27b-1abb18b89c13", Package = "com.unity.ugui", Source = "Editor/TMP/TMPro_TexturePostProcessor.cs", SourceHash = "3f9ed9c864e22ca1a90b791ef0d77a0c39343d4f422a9209e47b5c11be1a68e0" },
            new Binding { Id = 11, Type = "UnityEditor.Rendering.BuiltIn.MaterialPostprocessor", AssemblyHash = "0ae624838ae217a0a8cf24e88273c070d9c67c63cc55f2a8b5beb3e079096448", Mvid = "4cf747a4-8249-4b1a-ab13-0e353d8e2706", Package = "com.unity.shadergraph", Source = "Editor/Generation/Targets/BuiltIn/Editor/AssetPostProcessors/MaterialPostprocessor.cs", SourceHash = "71c09dffa68b5473952eff8d5cabc50bc75eb10931d8b29922a4d9c07c796eea" },
            new Binding { Id = 12, Type = "UnityEditor.ShaderGraph.NodeClassCache+PostProcessor", AssemblyHash = "0ae624838ae217a0a8cf24e88273c070d9c67c63cc55f2a8b5beb3e079096448", Mvid = "4cf747a4-8249-4b1a-ab13-0e353d8e2706", Package = "com.unity.shadergraph", Source = "Editor/Data/Nodes/NodeClassCache.cs", SourceHash = "dda975e83fdbc565c79061c2ba2b30fe997cfcffe387273c467c0c2c4c0b99bd" },
            new Binding { Id = 13, Type = "UnityEditor.ShaderGraph.ShaderGraphAssetPostProcessor", AssemblyHash = "0ae624838ae217a0a8cf24e88273c070d9c67c63cc55f2a8b5beb3e079096448", Mvid = "4cf747a4-8249-4b1a-ab13-0e353d8e2706", Package = "com.unity.shadergraph", Source = "Editor/Importers/ShaderGraphAssetPostProcessor.cs", SourceHash = "8c14e8d0e9e1f1bf195b90eb5d0a1e16127ba1d63c862a802a0c8cb17f11d8b0" },
            new Binding { Id = 14, Type = "UnityEngine.InputSystem.Editor.ProjectWideActionsAsset+ProjectSettingsProjectWideActionsAssetConverter+ProjectSettingsPostprocessor", AssemblyHash = "7f255bd4a489c9188dcc11b3089efd084fed86e4aa77f1c2a6f0bb9652aa6817", Mvid = "3d4dabe8-0d94-486c-be32-6c4cdbcf664b", Package = "com.unity.inputsystem", Source = "InputSystem/Editor/ProjectWideActions/ProjectWideActionsAsset.cs", SourceHash = "bcb6f1343085f9631f7498842ca0f23dc3f7c39e5ddbeb019c7f507d72bb991c" },
            new Binding { Id = 15, Type = "UnityEngine.InputSystem.Editor.InputActionImporter+InputActionJsonNameModifierAssetProcessor", AssemblyHash = "7f255bd4a489c9188dcc11b3089efd084fed86e4aa77f1c2a6f0bb9652aa6817", Mvid = "3d4dabe8-0d94-486c-be32-6c4cdbcf664b", Package = "com.unity.inputsystem", Source = "InputSystem/Editor/AssetImporter/InputActionImporter.cs", SourceHash = "514c3be5d9ce74c4188193b7a50f5bd80ff427ba91f5f509d6ef337420a2d371" },
            new Binding { Id = 16, Type = "UnityEngine.InputSystem.Editor.InputActionAssetEditor+InputActionAssetPostprocessor", AssemblyHash = "7f255bd4a489c9188dcc11b3089efd084fed86e4aa77f1c2a6f0bb9652aa6817", Mvid = "3d4dabe8-0d94-486c-be32-6c4cdbcf664b", Package = "com.unity.inputsystem", Source = "InputSystem/Editor/AssetImporter/InputActionAssetEditor.cs", SourceHash = "38c82a808ae143365869b5b2dcc9e2c1c68f5ab3c13a3ca63e8562416096d374" },
            new Binding { Id = 17, Type = "Unity.Cinemachine.Editor.SampleDependencyImporter+SamplePostprocessor", AssemblyHash = "1c542cfc4c5e4606d914c9477ec7d92538724e3acdb18441f223de7ce1e2477e", Mvid = "24c33adf-41d7-41de-8efb-ee04c90b791c", Package = "com.unity.cinemachine", Source = "Editor/Samples/SampleDependencyImporter.cs", SourceHash = "43be14e6b1a73c7f2661975e83b85d0be2ecf7519f69d501cdd88c0f10ddf66f" },
            new Binding { Id = 18, Type = "Unity.Cinemachine.Editor.SampleDependencyImporter+DomainReloadProcessor", AssemblyHash = "1c542cfc4c5e4606d914c9477ec7d92538724e3acdb18441f223de7ce1e2477e", Mvid = "24c33adf-41d7-41de-8efb-ee04c90b791c", Package = "com.unity.cinemachine", Source = "Editor/Samples/SampleDependencyImporter.cs", SourceHash = "43be14e6b1a73c7f2661975e83b85d0be2ecf7519f69d501cdd88c0f10ddf66f" },
            new Binding { Id = 19, Type = "Unity.Cinemachine.Editor.CinemachineSettings", AssemblyHash = "1c542cfc4c5e4606d914c9477ec7d92538724e3acdb18441f223de7ce1e2477e", Mvid = "24c33adf-41d7-41de-8efb-ee04c90b791c", Package = "com.unity.cinemachine", Source = "Editor/Windows/CinemachineSettings.cs", SourceHash = "9b360bfa0c57d7a26678b694ad9aa5e54acd7baffef60e74e7cd7ed7ccefb709" },
            new Binding { Id = 20, Type = "UnityEditor.Tilemaps.GridPaletteBrushes+AssetProcessor", AssemblyHash = "fa153a745d260c6ad2ee51dfc7e8de0db3a522037576cb210ea44d0b71920741", Mvid = "0463dda6-b969-4326-9533-be27a1a86375", Package = "com.unity.2d.tilemap", Source = "Editor/GridPaletteBrushes.cs", SourceHash = "31e4a6289a0478bdcb94c4f46ba35c285d6d8b6abce3b9d566431ef7ad41e159" },
            new Binding { Id = 21, Type = "UnityEditor.Tilemaps.GridPaintPaletteClipboard+AssetProcessor", AssemblyHash = "fa153a745d260c6ad2ee51dfc7e8de0db3a522037576cb210ea44d0b71920741", Mvid = "0463dda6-b969-4326-9533-be27a1a86375", Package = "com.unity.2d.tilemap", Source = "Editor/GridPaintPaletteClipboard.cs", SourceHash = "8598211220a5be7550b883a6483217ce46b32be7d08a850c436bd9c1832eb61f" },
            new Binding { Id = 22, Type = "UnityEditor.Tilemaps.GridPalettes+AssetProcessor", AssemblyHash = "fa153a745d260c6ad2ee51dfc7e8de0db3a522037576cb210ea44d0b71920741", Mvid = "0463dda6-b969-4326-9533-be27a1a86375", Package = "com.unity.2d.tilemap", Source = "Editor/GridPalettes.cs", SourceHash = "906fe2107a9d41c64943253128b3d77ce475923ca3919920e6901bfa47577574" },
            new Binding { Id = 23, Type = "UnityEditor.U2D.SpriteShapeAssetPostProcessor", AssemblyHash = "7bb139ef38e254a9c723eeb31468407d5ad36b09d1bfc11ad52ac4e08937d9f3", Mvid = "a2e981e0-e622-416b-9b00-2fafc115be7b", Package = "com.unity.2d.spriteshape", Source = "Editor/SpriteShapeAssetPostProcessor.cs", SourceHash = "baa4f3570238dd79ac513ce2ec34bef65239b975805be56e894dcfaee28b4186" },
            new Binding { Id = 24, Type = "UnityEditor.U2D.Sprites.SpriteEditorTexturePostprocessor", AssemblyHash = "454461173bc76dabc86e5e080d62b5ec09354766d4f9bbdf358be20fc56dfa73", Mvid = "fa0aab0e-90a0-4fde-a3bb-17b84de8cc6f", Package = "com.unity.2d.sprite", Source = "Editor/SpriteEditor/SpriteEditorWindow.cs", SourceHash = "89153821c318caa736f79211a8e88903be3b33c136453b254839d554c8bfed22" },
            new Binding { Id = 25, Type = "UnityEditor.U2D.PSD.PSDImporterAssetPostProcessor", AssemblyHash = "01868ff68015d822b4a85091cc5f901a05987e7ebc6e08fa241bb5d2a745c7ae", Mvid = "6c8f3c84-5a7f-4493-8341-153aff5f36b4", Package = "com.unity.2d.psdimporter", Source = "Editor/PSDImporterAssetPostProcessor.cs", SourceHash = "ee2979b8a9cf8f47fd159bf4f236591bc2539a8bbb568c708cc9950f0a68d650" },
            new Binding { Id = 26, Type = "UnityEditor.U2D.PSD.PSDImportPostProcessor", AssemblyHash = "01868ff68015d822b4a85091cc5f901a05987e7ebc6e08fa241bb5d2a745c7ae", Mvid = "6c8f3c84-5a7f-4493-8341-153aff5f36b4", Package = "com.unity.2d.psdimporter", Source = "Editor/PSDImportPostProcessor.cs", SourceHash = "e9f6768f47b17c598ec72f3225dd466a3334cb6536950075b60b895d86b2d830" },
            new Binding { Id = 27, Type = "UnityEditor.U2D.Animation.SpriteLibraryEditor.SpriteLibraryAssetPostprocessor", AssemblyHash = "e13301a8f8e89c401657147e4093c497637f5ed9db38aab60d73b1a44ec1dc60", Mvid = "4ee3f94f-93e0-40dc-a96a-1a39bb6e26f2", Package = "com.unity.2d.animation", Source = "Editor/SpriteLib/SpriteLibraryEditor/SpriteLibraryAssetPostprocessor.cs", SourceHash = "537af268dee9928e0812efb916424bbc2c4c8cd281fc509943f131762c2776ed" },
            new Binding { Id = 28, Type = "UnityEditor.U2D.Animation.SpritePostProcess", AssemblyHash = "e13301a8f8e89c401657147e4093c497637f5ed9db38aab60d73b1a44ec1dc60", Mvid = "4ee3f94f-93e0-40dc-a96a-1a39bb6e26f2", Package = "com.unity.2d.animation", Source = "Editor/SpritePostProcess.cs", SourceHash = "159a130c2176f765658a3ca5f122f54637fccf9215e108815ddc248d582e5815" },
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
            new Binding { Id = LegacyMemoryCallbackId, Type = "NovaFramework.Editor.MemoryProtectionLegacyConfigPostprocessor", Package = "com.solotopia.nova.framework.memory.protection", Source = "Nova/Editor/MemoryProtectionLegacyConfigMigration.cs", SourceHash = "34d5b612d79e7c7cd1e3e917294ad2d9121f6fb1affcc62d2b9b66a912e35276", },
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
                Binding binding = s_Bindings.SingleOrDefault(item => item.Type == type.FullName);
                string reason = null;
                if (binding == null || !VerifyBinding(type, binding, assemblyHashes, token, out reason))
                { problems.Add(type.FullName + "：" + (binding == null ? "未审实际派发目标" : reason)); continue; }
                identities.Add(type.FullName + "|" + type.Module.ModuleVersionId.ToString() + "|" + assemblyHashes[type.Assembly.Location] + "|" + binding.SourceHash + "|" +
                    string.Join(",", methods.Select(method => method.ToString()).OrderBy(value => value, StringComparer.Ordinal)));
            }
            if (!observerFound) problems.Add("Nova 自有刷新观察器没有出现在当前真实 TypeCache 派发域。");
            if (problems.Count != 0)
            { error = "资源导入/刷新实际回调身份不完整：" + string.Join("；", problems); return false; }
            digest = GenerateActionCommon.ComputeTextHash(string.Join("\n", identities));
            return true;
        }

        /// <summary>源码、包归属、完整程序集与实际 MVID 都相等才使用已审语义；无 vendor getter 调用。</summary>
        /// <param name="type">已加载且需要核对的实际类型元数据。</param>
        /// <param name="binding">该实际类型对应的完整 DLL/MVID/源码绑定。</param>
        /// <param name="hashes">当前一次身份捕获内的完整文件摘要缓存。</param>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <param name="error">失败时的精确原因；成功时为 null。</param>
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        private static bool VerifyBinding(Type type, Binding binding, IDictionary<string, string> hashes,
            CancellationToken token, out string error)
        {
            error = null;
            string location = type.Assembly.Location;
            if (!hashes.TryGetValue(location, out string hash))
                hashes[location] = hash = GenerateActionCommon.ComputeFileHash(location, token);
            string expectedHash = binding.AssemblyHash, expectedMvid = binding.Mvid;
            if (binding.Id == LegacyMemoryCallbackId &&
                !TryReadLegacyCompiledIdentity(out expectedHash, out expectedMvid, out error)) return false;
            if (type.BaseType != typeof(AssetPostprocessor) || hash != expectedHash ||
                type.Module.ModuleVersionId.ToString() != expectedMvid)
            { error = "实际继承/DLL/MVID 与已审实现不符"; return false; }
            if (binding.Package == null) return true;
            PackageInfo package = PackageInfo.FindForAssembly(type.Assembly);
            string source = package == null ? null : Path.Combine(package.resolvedPath, binding.Source);
            if (package?.name != binding.Package || !File.Exists(source) ||
                GenerateActionCommon.ComputeFileHash(source, token) != binding.SourceHash)
            { error = "完整源码或实际包归属与已审实现不符"; return false; }
            return binding.Id != LegacyMemoryCallbackId || VerifyLegacyMemoryCompilerOwner(type, package, source, token, out error);
        }

        /// <summary>从随包的已审 reference 读取完整编译身份，避免依赖本 Editor 的模块形成内嵌指纹重编译循环。</summary>
        /// <param name="hash">已审完整 DLL 的 SHA-256。</param><param name="mvid">已审模块标识。</param>
        /// <param name="error">缺失、超界或格式改变时的具体阻断。</param><returns>只读成功为真；从不自动采纳当前 DLL。</returns>
        private static bool TryReadLegacyCompiledIdentity(out string hash, out string mvid, out string error)
        {
            hash = mvid = null;
            try
            {
                string path = Path.Combine(EditorUtil.AgentSkills.ResolveAgentsRoot(), "Skills",
                    "nova-project-resolve-android-dependencies", "references", "android-refresh-compiled-identity.json");
                if (!File.Exists(path) || new FileInfo(path).Length > 512)
                { error = "缺少有界已审旧配置回调编译身份 reference。"; return false; }
                return TryParseLegacyCompiledIdentity(File.ReadAllText(path), out hash, out mvid, out error);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            { error = "旧配置回调编译身份 reference 读取失败：" + exception.GetType().Name; return false; }
        }

        /// <summary>严格解析唯一编译身份；拒绝额外字段、重复键、错误类型和无效 SHA/MVID，不做兼容猜测。</summary>
        /// <param name="json">最多 512 字符的已审 reference 原文。</param><param name="hash">完整 DLL 摘要。</param>
        /// <param name="mvid">D 格式模块标识。</param><param name="error">拒绝时的具体原因。</param><returns>唯一完整身份成立时为真。</returns>
        internal static bool TryParseLegacyCompiledIdentity(string json, out string hash, out string mvid, out string error)
        {
            hash = mvid = null;
            error = "旧配置回调编译身份 reference 格式不完整。";
            if (string.IsNullOrEmpty(json) || json.Length > 512) return false;
            try
            {
                var value = Newtonsoft.Json.Linq.JObject.Parse(json, new Newtonsoft.Json.Linq.JsonLoadSettings
                { DuplicatePropertyNameHandling = Newtonsoft.Json.Linq.DuplicatePropertyNameHandling.Error });
                if (value.Count != 2 || value["assemblySha256"]?.Type != Newtonsoft.Json.Linq.JTokenType.String ||
                    value["mvid"]?.Type != Newtonsoft.Json.Linq.JTokenType.String) return false;
                string parsedHash = (string)value["assemblySha256"], parsedMvid = (string)value["mvid"];
                if (parsedHash.Length != 64 || parsedHash.Any(c => !(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) ||
                    !Guid.TryParseExact(parsedMvid, "D", out Guid id) || id == Guid.Empty) return false;
                hash = parsedHash; mvid = parsedMvid; error = null; return true;
            }
            catch (Newtonsoft.Json.JsonException) { return false; }
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
        /// <returns>满足上述精确条件时为 true，否则为 false；不会为通过检查而改变环境。</returns>
        internal static bool TryCaptureNaturalBranches(string[] paths, CancellationToken token, out string digest, out string error, string provenHostDomain = null)
        {
            digest = null;
            error = null;
            if (!IsTemplatePathDomain(paths, out error)) return false;
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
                        : CheckBranch(binding.Id, type, paths, token);
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
        /// <returns>对应独立合同可接纳时返回 null，否则返回具体原因。</returns>
        private static string CheckBranch(int id, Type type, string[] paths, CancellationToken token)
        {
            if (id >= 0 && id <= 8 || id == 35 || id == 48) return null;
            return AndroidResolvePeripheralRefreshContracts.Check(id, type, paths, token);
        }

        /// <summary>仅捕获本次真实参与且外围 Check 已通过的编号；未加载且未参与的类型不被虚构为空。</summary>
        /// <param name="token">本次只读捕获或遍历的取消令牌；不取消厂商或引擎工作。</param>
        /// <returns>仅覆盖当前实际已通过检查的外围参与者快照。</returns>
        internal static AndroidResolvePeripheralRefreshContracts.StableSnapshot CapturePeripheralSnapshot(CancellationToken token)
        {
            int[] ids = s_Bindings.Where(binding => UsesPeripheralContract(binding.Id))
                .Where(binding => FindType(binding.Type) is Type type && GetDispatchMethods(type).Length != 0).Select(binding => binding.Id).ToArray();
            return AndroidResolvePeripheralRefreshContracts.CaptureStableSnapshot(ids, token);
        }

        /// <summary>专属旧配置回调不进入通用外围名称、效应和状态快照表；既有编号规则保持。</summary>
        /// <param name="id">完整绑定表中的固定编号。</param>
        /// <returns>该编号确实由通用外围合同处理时为真。</returns>
        internal static bool UsesPeripheralContract(int id) => id >= 9 && id != 35 && id != 48 && id != LegacyMemoryCallbackId;

        /// <summary>只核对正常 Unity Editor 编译所有者与其自有源/asmdef，不加载程序集或初始化迁移类型。</summary>
        /// <param name="type">已通过完整 DLL/MVID/源码绑定的实际回调类型。</param>
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
            if (!VerifyBinding(type, binding, new Dictionary<string, string>(StringComparer.Ordinal), token, out string bindingError))
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
