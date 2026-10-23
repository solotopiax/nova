/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidGenerateNativePreviewContract.cs
 * author:    taoye
 * created:   2026/10/20
 * descrip:   Native原预览查询的有限当前场景合同
 ***************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Newtonsoft.Json;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    /// <summary>只为已审当前场景建立typed计划，不另开场景、不读取Manager getter或改写任何业务状态。</summary>
    internal static class AndroidGenerateNativePreviewContract
    {
        internal const string ScenePath = "Assets/Samples/MainDemo/MainDemo.unity";
        internal const string InputPath = "Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions";
        private static readonly string[][] Sources = {
            new[] { "Assets/Framework/Prefabs/Nova.prefab", "7f33f69ad2925eab0c48696a0d22db57a060a1ce153000e8f4bc4f3891584d6c" },
            new[] { "Assets/Framework/Prefabs/Nova.prefab.meta", "b92550383edfad3e074e17f1fba78eeb5a198a6e8922d7ec30c074dcb28cbb2c" },
            new[] { "Assets/Framework/Scripts/Editor/BuildProcessor/Native/NativeBuildPreviewObservers.cs", "4094febb68b8dade04c9158afee5a77ea196ffd9165d0576b775d7aec9c135cd" },
            new[] { "Assets/Framework/Scripts/Editor/BuildProcessor/Native/NativeBuildProcessor.cs", "d39e80105ff80fda84e02263524b940d1185204fbb1a674e0831add27611ae01" },
            new[] { "Assets/Framework/Scripts/Editor/BuildProcessor/Native/NativeBuildProcessor.cs.meta", "e10983b0c25c8e269ff98215c2eae67f429f0e29fe50f63597b5852543c00bbe" },
            new[] { "Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.HybridCLR/EditorUtil.HybridCLR.Methods.cs", "a5541fd39e75f14dd9b5239508b16d4615407b8906ed513c233e21dd0b6005c4" },
            new[] { "Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.HybridCLR/EditorUtil.HybridCLR.Methods.cs.meta", "7a1a7a5631ec3b90cddafa08f8c7c1277020f5f281e08ee576d17f61fffffa26" },
            new[] { "Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.HybridCLR/EditorUtil.HybridCLR.cs", "d852442980f50fdcb18a3a92d07af5eaaa8a08a287160be7bba57869142384e6" },
            new[] { "Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.HybridCLR/EditorUtil.HybridCLR.cs.meta", "818e4541962180fdb6cebc812810e61e0d1b7313e2c5f90008ece13955ecdc59" },
            new[] { "Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.SceneRoute.cs", "00001e1d1ecf5984375f60d8adb06edb28205b3db9406d6f998128900656cbd7" },
            new[] { "Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.SceneRoute.cs.meta", "53dd7ef43f7c7ec1d5fd09e0a755b3093c9f3432ecec1b401fd824ee5714b36b" },
            new[] { "Assets/Framework/Scripts/Editor/Windows/ConfigWindow/ConfigWindow.Methods.cs", "f05c7eb35134ac58e7055d97fe104f2f5695e36324ac78ca5499547c33f6aefd" },
            new[] { "Assets/Framework/Scripts/Editor/Windows/ConfigWindow/ConfigWindow.Methods.cs.meta", "46123242e342624f60da045b7d881be4d96780b4b1f8680c8a54fc0359cc4ecd" },
            new[] { "Assets/Framework/Scripts/Editor/Windows/PipifyWindow/PipifyWindow.Methods.cs", "862c7e2888bbaa9920752580190bd31c91250cbb88c065508147f2c9e1e5e839" },
            new[] { "Assets/Framework/Scripts/Editor/Windows/PipifyWindow/PipifyWindow.Methods.cs.meta", "349bbc8a3ec54f061a99a84445b311e5925a3bcc4addb079b3137937508d3f87" },
            new[] { "Assets/Framework/Scripts/Runtime/Core/Collections/NovaLinkedList.cs", "6c47e5d2a7ff068b138e3928ef2e7b7b1919b4232467649915aab8a78a35279d" },
            new[] { "Assets/Framework/Scripts/Runtime/Core/FrameworkComponent.cs", "8ec97744969036d541172c8abc0bedb3a33684477b0f13a414d4b0e0da982b55" },
            new[] { "Assets/Framework/Scripts/Runtime/Core/FrameworkComponent.cs.meta", "9fc7f6f8519ac60bbe95587062d2c1908a7b839e147579cac3af57475b345ad6" },
            new[] { "Assets/Framework/Scripts/Runtime/Core/FrameworkComponentsGroup.cs", "1ed10c7f61d21a1a14f68740c17ac42561a19132e40a762b32fb1d6967fc79a8" },
            new[] { "Assets/Framework/Scripts/Runtime/Core/FrameworkComponentsGroup.cs.meta", "7f361c040069f20b22363d288a914de7ecbd05a116b90587ab9067f0d437edd8" },
            new[] { "Assets/Framework/Scripts/Runtime/Core/FrameworkManagersGroup.cs", "6b5b84bdadc7c490522285f1dc3194e7af35b0057cf2b43f8f52c2608e8fda1a" },
            new[] { "Assets/Framework/Scripts/Runtime/Core/Table/DataTableUnitSettingBase.cs", "207fb99e0b4b97a3b10ac3e1a059e944e52bada22e45b397b8a2e526d6455798" },
            new[] { "Assets/Framework/Scripts/Runtime/Core/Table/DataTableUnitSettingBase.cs.meta", "27d36bf815a1b7deddff5d3527f82e6a02650278e84eb459feef25170465fc11" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/App/AppComponent.Visitors.cs", "0618c22018941d4a32080e53f0045126a7689efa7dcf230a4bff38ac1cbe6ef9" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/App/AppComponent.Visitors.cs.meta", "33fa83f84a695521506382f7960b696db75b7d38687f8b6202720f899397f50f" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/App/AppComponent.cs", "545e1eb8ba1c7154937cb4daabdec227bc026955cb0f2ae82a85bdbdf60a9f3f" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/App/AppComponent.cs.meta", "fdcac97b61a0a84194baa5a18335387ddd172346772847cb90eb7f8938230649" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Asset/AssetComponent.Visitors.cs", "d2ec36049ca839a5fac45bc5ca7db0a639a2ea408de3b04aace6cfce37b1f976" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Asset/AssetComponent.Visitors.cs.meta", "da50ade785e8b0bcbcb2fe51498e70eb2ba95c725af7a9285657464d9eb65e04" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Asset/AssetComponent.cs", "21b59b2cc3208a5464a9add488cbba73d2ead79d07d8302145f4784d9669f348" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Asset/AssetComponent.cs.meta", "abde6260db9812e19a9aa12d2d134112424cad42cb21f8abcbde242cf8e629bb" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Config/ConfigComponent.Visitors.cs", "80d3071c62f1afcabe374b44e220505ad07b7da42b3ae90e4b3bcfed0a7852ca" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Config/ConfigComponent.Visitors.cs.meta", "c9f399c7b8c7b0fefb209c7c00bdef8538d36ef8960fd422a544e84846c01850" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Config/ConfigComponent.cs", "d9a2bcc92b0ab956db36952d30d0f01609ed0cf91f24b31510159769d054df70" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Config/ConfigComponent.cs.meta", "e6359d0ad720ab33fdc7608a5d9b41da3833e321feb118e98a5ddacf08b13217" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Debug/Definitions/DiskCheckingConfig.cs", "0b165705476ec9998e4ce40c4eab8d9a71f0febe014ce84ad5e112f951e48297" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Debug/Definitions/DiskCheckingConfig.cs.meta", "d3bc8531864f192a9e94c2718fcc52f3468f752c9fef6c2b85eb83e154d814c6" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Debug/Windows/DebugComponent.Methods.cs", "4fdbbb6ddb2f1015ed4dba49d80753c25d3209e60e838df60e6e0d9b6f35a5ee" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Debug/Windows/DebugComponent.Methods.cs.meta", "9317180b267026f8924bb9566fb2dfd1be29a3157fa20067f43dc333380db9c1" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Debug/Windows/DebugComponent.Visitors.cs", "cde88f5cf0237030f85a322bd3394e52cbf5f4b928878061d725890aa6a384e0" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Debug/Windows/DebugComponent.Visitors.cs.meta", "ecfddc1c3412e6eb04bec3fac1f58404ff8741db489a4160a7b2b0ef8e703855" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Debug/Windows/DebugComponent.cs", "900dd633e3f01ed7e366558ab103c1ff217b047c1005e83a49b40826d4a7e3c6" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Debug/Windows/DebugComponent.cs.meta", "22a645e47564dd65a5239ef8b7431005cb943b10a3268cc760e3e63839627294" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Event/EventComponent.Visitors.cs", "d6633f514063225bebb8367328f2443eade378cf7dc57bb83999c1c5e5332b8d" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Event/EventComponent.Visitors.cs.meta", "cc6a34957b551995c1c6695b76f3fb7f7e95e88ac581ce135416be8ab696db2d" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Event/EventComponent.cs", "369c0ba406c51f0607071c8e007fbf784190ebee6f35cadb8e8df52c96fd4c32" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Event/EventComponent.cs.meta", "e169354989de33de148ae93a99d31f26b7d95a8b3f8ec6912bac89d87940936e" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Localization/LocalizationComponent.Visitors.cs", "b5d3b1a7a38afbe58675b85bf937a57385db2a906d91b925312e4f365aba15a8" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Localization/LocalizationComponent.Visitors.cs.meta", "a30adb8bf20bd7a2c3098c875a94bd3f711c4c44b5fa01140549ff01a5f342ec" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Localization/LocalizationComponent.cs", "507b7e3debd7d8389741c94a6afbb035f6671e2fe7432c35a56066881beebc98" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Localization/LocalizationComponent.cs.meta", "27b4fff4eab7a0a8a52e2c35983f6d6e9fd49fa79358b26574c092c3f3c72393" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Localization/Managers/Definitions/LocalizationSettings.cs", "d511a81f5063418f322c24ed0f8444a0e13b4121a20557c173a7063da9460a73" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Localization/Managers/Definitions/LocalizationSettings.cs.meta", "512d83c8ac4689d00d37a360578b29ae5eff20d62110ed00821a6bf9c7abf6b4" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Native/Managers/Definitions/NativeManagerConfig.cs", "3503077f2a9b61dac73c7875601000979ad90c936087f430b322f9b6fda85cc0" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Native/Managers/Definitions/NativeManagerConfig.cs.meta", "6af045f8c178044b351d9ff8bb177220db634df43fe49f4a8595cb3e86bb44b5" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Native/NativeComponent.AppInstall.cs", "5fc27ff36a0dd96d48dd5c00a70f52f872eb02b99ff6ec1c3e2f416301bc9e3f" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Native/NativeComponent.AppInstall.cs.meta", "ebdc136ea315990ca7a70923b1f20cd86d84d90a148f372a9da8a279d9dd9b77" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Native/NativeComponent.Visitors.cs", "1e08223bb94813aa044181b764cf213726f63cc863d97d65cd0db5b4064535d5" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Native/NativeComponent.Visitors.cs.meta", "d4254195eb3c35f3f53d8c5c51a2084a8f7aead10c517fe6e2cbf99d04779e88" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Native/NativeComponent.cs", "08c7325edb582f327e9147eb91695dd63497cba3675d5a27a52a64aebc0f614a" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Native/NativeComponent.cs.meta", "3b9a210478c7d50bc3d0a0f27dab30f88babd6c6428419dc91f787b9df84d470" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Network/Definitions/HttpSettings.cs", "6e20cb048c2c6013032ef080fbc636aa879cc3b889dacaef8fba256b52066e1b" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Network/Definitions/HttpSettings.cs.meta", "fb9c993cbb0f3d5589c006b8b66de689350f8ce584f3f687bb759849d6a578b2" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Network/Definitions/NetworkSettings.cs", "414bbdbf4f9bc4979a4139c0a1beb16bfa9f09af2f28f54b6a1b8eae43f922db" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Network/Definitions/NetworkSettings.cs.meta", "54eab08878ea43caa30f2fe73ce05010cd657c8e310fc57dd76b7effb0cfc5b0" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Network/Definitions/ProtoSettings.cs", "b0315f2c4c2b7294e4816806dcb4016224b29fa265001969d2e4892a524d2002" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Network/Definitions/ProtoSettings.cs.meta", "4841cb55817ac6797ddc5a45904abafb73e3e6da092a746d9cf561975245b97f" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Network/Definitions/WebSocketSettings.cs", "1524522616e80143dce3003eedefebf2377011729e2333651a5a18c1ca7425fd" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Network/Definitions/WebSocketSettings.cs.meta", "7437a25394e503903947a7a00200fd8b2fde441dc778abdb2bebf81c3e3a1e62" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Network/NetworkComponent.Http.cs", "b909a1dcccf32e3de5c5e9a51c16f933778beaf0723b83a56092ee52732649eb" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Network/NetworkComponent.Http.cs.meta", "6289811eece990d14eff45755e5cdf731774d4b1093034f558085ad67704d404" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Network/NetworkComponent.Kit.cs", "1372542eeeab33f73f8b7e647a6d6d7f610747f1eb0b8d1ac72871d2f83c481f" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Network/NetworkComponent.Kit.cs.meta", "aa526c9cf342e696a0b40912b2c9432834ed843e9aeec66be6971ee6697c8c93" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Network/NetworkComponent.Methods.cs", "bdd1a686946c0cb77ccff34b1a0d37a6679f4587b09e9a3322eb1b5bee0d91db" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Network/NetworkComponent.Methods.cs.meta", "11f5809e02e51fc364fd011fcd5e4f922eb64a3d4e365efa6f49760eb21d2f88" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Network/NetworkComponent.Network.cs", "2c970813d9617893d189620764c389313770eae7ff45a22d1aba194ab6bcae66" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Network/NetworkComponent.Network.cs.meta", "4d532a79cad6de99c8312072318a54a6e1a78e41a634b0cc3d94783317f4a9e1" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Network/NetworkComponent.Visitors.cs", "df9ff04fe8aa1e4d643d42cdf4b6b07f8d4eb81f08427e3447e62636bacca5dd" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Network/NetworkComponent.Visitors.cs.meta", "27f89169fc9a0c135bd19a24442b73c0b3b417db758df3fb2da9ba14934e312e" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Network/NetworkComponent.WebSocket.cs", "1c68d48036d1ba4a3ff0f3379032853918677ae331a743c7f7b8487fb0e3f3bb" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Network/NetworkComponent.WebSocket.cs.meta", "b05b5887534254cdd29360d4fa4bf9bee53d3dbf667cf26d9711645977d54e12" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Network/NetworkComponent.cs", "727689e9009e509d1defe8da74043fccd2cd84198d71de07705e6d8cc05c7c08" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Network/NetworkComponent.cs.meta", "44f4f4f3a016c2980617b6f3b065bea508983cebb73bb68ef8f6f9e2092b1f88" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Nova/Nova.Methods.cs", "c6147a12f30b38e48d1503858b7e372ed95a5dcc15dd54cdb9f0335b07099a00" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Nova/Nova.Methods.cs.meta", "37e532b312461d9d9a70760c81cc42cfabd2fab1365dd6396f6107678a8de91c" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Nova/Nova.Visitors.cs", "1cff02d1910713f238576239fc1fd4e9c9e414a61baa2e144850fa7a8bcad446" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Nova/Nova.Visitors.cs.meta", "05c515ff29e9e6f7d6a2595f137085ab6eada90a54f72c228c38d16f57e406d6" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Nova/Nova.cs", "f3df6b21b36bfea74f7ff92203586f53e75aab73232271fd2d7cfa93545f832d" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Nova/Nova.cs.meta", "46291a4a2ee4f24b76a83b58348452e19fbdf2da5247d50fb8e97dd3b328d1c7" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/ObjectPool/ObjectPoolComponent.Methods.cs", "c6214520254d1f8c9d49f6e1d17abc9b23c3789a34497bb9f96a04a4e75c378b" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/ObjectPool/ObjectPoolComponent.Methods.cs.meta", "cece6a5ece4f151bcbd653eac2347e291eff0c18dcc03254d5cfac62ad012280" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/ObjectPool/ObjectPoolComponent.Visitors.cs", "88c9d1d11ce9ce6d199d643ead40a3224e36470d18375abcfc09f1d3d880a5f5" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/ObjectPool/ObjectPoolComponent.Visitors.cs.meta", "57cb7a6392115e66ce84289f9eede3368322f576129121c5b8e25ec59c85f3ed" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/ObjectPool/ObjectPoolComponent.cs", "a2d8946b1a8a3057ebadbdda25e64fe88b66e5b626e1ef5c2b2792376eef7819" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/ObjectPool/ObjectPoolComponent.cs.meta", "c0701ad69661dcd5924a84ac2c58797792f42494aa921fd2d1813064b22acbaf" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Persist/PersistComponent.Visitors.cs", "ce91fa5edba06ea515931a6800ceff8034387b87ee89209a6f2411d9293a829a" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Persist/PersistComponent.Visitors.cs.meta", "e8bf7956c5cf9bda4cfb9930687dc847b96ae4cf2aeaeb7bbd6df786b7ad3322" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Persist/PersistComponent.cs", "57b8b5d6d800b763c754befa19aa15e6597be586efa534d427d8eecc5e0fdb7e" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Persist/PersistComponent.cs.meta", "82653d60ca65f719e6fda06afbe6cf640748f15ac4603caffdb606ce3e66ba10" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Prefab/Managers/PrefabManager/Definitions/PrefabManagerConfig.cs", "5231e3df76caf92dbf39645f4411c7a07507a060643d6ad6b199fa19d4186a7c" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Prefab/Managers/PrefabManager/Definitions/PrefabManagerConfig.cs.meta", "82b9aa789b4717b8794a55009c1b869780290c68fe4de224f9b7f1e12fa0161c" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Prefab/PrefabComponent.Visitors.cs", "c2b79d24ca95e1a70bcceca5462d07104f625e0fff41f2a5ec0b8a5409ec0e3d" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Prefab/PrefabComponent.Visitors.cs.meta", "025f25129f7c652471db1fa88d3fcdb3c01cacdb61fad72788e52c09cab4b60a" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Prefab/PrefabComponent.cs", "d5b327b4044439ea5b2c772efff77c9c0910c2565699d08eb1c9189f15c19db7" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Prefab/PrefabComponent.cs.meta", "8de08838f4186d8d6d93e646be1e72518175a65ba54b61fa94e5747b9bf0c867" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Procedure/Definitions/LauncherSettings.cs", "51af5a979763ee02310bb2fb78ba0601e05253f4790e49fe822d8d4ae8f84e37" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Procedure/Definitions/LauncherSettings.cs.meta", "993c83f74bf94c3219238c4b27eae7714e7e72fd150e03cb94aff3c16a485eb3" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Procedure/ProcedureComponent.Methods.cs", "82658c8d9fb74f7008f303fd6ea87ca66c7a75bd74198a90455e40f7d22e3ac7" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Procedure/ProcedureComponent.Methods.cs.meta", "cfdd359dbccbacb1342017d6e295b7b6e915bdb5c48bba08a7c152122970216c" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Procedure/ProcedureComponent.Visitors.cs", "ab9769f92d8f3e95036c109509818f39b147d57aae23cbf7d31a1b9d4d942ef6" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Procedure/ProcedureComponent.Visitors.cs.meta", "43c120490684c691ff738b5a0a1f7661d43505d6f67883d7a44b986446f211b3" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Procedure/ProcedureComponent.cs", "4b8de4274200c127c652fc200ceb416278971f6374b13526bf8eac03b449b936" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Procedure/ProcedureComponent.cs.meta", "667fa31d302df8cf0525b31c277aa5a89ba6968709f95daf37fd4f31941d3bb9" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/SDK/Managers/Definitions/SDKPluginEntry.cs", "96c04242a7f02908e81981bccf167a0c0418407345ee0bfd8c426696c7dd20a1" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/SDK/Managers/Definitions/SDKPluginEntry.cs.meta", "c7c00ae0158c13aeb6188346f0d35f4863f0ff6e0c053e22d8952cdaf400c430" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/SDK/SDKComponent.Lifecycle.cs", "e9bf6f71655cea0c2cd55a664b7a0db96f2ea89d3b55cc42ae1ddb85d718579d" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/SDK/SDKComponent.Lifecycle.cs.meta", "13cc0b5d99c4a2cd199f1510b6e22f500e151aff98559a365e83a660a719c585" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/SDK/SDKComponent.Methods.cs", "e803d8f6a7f12dd5caf34944747063d96ab35a18aa2abc5d2824ec492e278ba7" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/SDK/SDKComponent.Methods.cs.meta", "2a2bc4937d8c8ad231a5d2d75ce016d4710623a0856d665f3ca71b3063072044" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/SDK/SDKComponent.Visitors.cs", "443ebb188328279720e80f1aca9bd5272771048e03d2d25a1c71ebf397fd9998" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/SDK/SDKComponent.Visitors.cs.meta", "7a04d2a9bd3402ca822d94ed5731b3527e7f547bdec57d4ae75c5edbd88c4155" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/SDK/SDKComponent.cs", "91bd53ac925857309fc78496172b0925e8aa23802aedf0590221f750f42f69fc" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/SDK/SDKComponent.cs.meta", "bd90c52ad494b6fc349581c1116c556ef21d3f9e7f0591fdd6458a402bafbd64" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Sound/Definitions/SoundGroupShell.cs", "ee38c1962dc55de5119a8ffb6f1af266266a78973707f3e9bd038b530dcf9389" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Sound/Definitions/SoundGroupShell.cs.meta", "51081ee02389ba05612ff7da544f82a43844dd3775e998b537abdb34609ffc0e" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Sound/Definitions/SoundSettings.cs", "064da8c0a099fb0303e84ddcb3c6dbf74a10c6e08fbdcac802ac7accab89fe41" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Sound/Definitions/SoundSettings.cs.meta", "1f46b308ffcd47ce6c67f10f4ea79565e6885faa82b6f9e83e71b92c702aeb66" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Sound/SoundComponent.Visitors.cs", "30457ee6c94a6c4f6d39d861d99ed972f208491ca255c98ed4dde184cfd91a3e" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Sound/SoundComponent.Visitors.cs.meta", "77304121b04c5ea2c09cfa99a82ce3e9072596840d43376a7ccb2d3eaf6812df" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Sound/SoundComponent.cs", "b746d02681263204c8af2b4fee570ede7b4fbebec7b300037c74606d6bfc02ca" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Sound/SoundComponent.cs.meta", "78ae50964a09bda4078658b57fff4326fa220cdd998f2d3c72e935f781d40903" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Table/Managers/Definitions/TableSettings.cs", "3ff519588349634082d1f79e76dd3a30df2ddfb52efd7a13a644209890762fb8" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Table/Managers/Definitions/TableSettings.cs.meta", "68464ef8feb00db3388a9cc3ce3138eaa4be959e5f091198f97686adf59ba374" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Table/TableComponent.Visitors.cs", "7057667b37bf5d8adeaac1c7608273839ecc72573b758e1d2fa30cce4ac80687" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Table/TableComponent.Visitors.cs.meta", "859fabe94a2550ba13e936996c42ac2648e4c558969694758ff06ddf35b6a570" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Table/TableComponent.cs", "5163bd4502ff65fd521e53d89941284201d4bfb6a0d0d241d4a95c5c17e1ee15" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Table/TableComponent.cs.meta", "c341b6ed59c7370dde36f97d10d392f14aafd1eb247a77e8fc667837af3b0335" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/UI/Managers/UIManager/Definitions/UISettings.cs", "a4286a8bd145cc1af5be5f5c4b3c8cbd48950af3e87c8c6a14a0191e16a7ddbd" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/UI/Managers/UIManager/Definitions/UISettings.cs.meta", "cfad6890e4dfde16be137b5292a544ff8da045fa9d57d3cb1ee10a0233ba35a8" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/UI/UIComponent.UIGroup.cs", "7c9e982c7ce89006e64e68d3f43f70b8b3e4d68f51a519b904a26519e1b0c214" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/UI/UIComponent.UIGroup.cs.meta", "89d7ed2779c12c79c862002f513cb525a6b5feda2792e820c03bc3c2375056fc" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/UI/UIComponent.Visitors.cs", "198e9d08447a910b3b83d2e5ae863798215b8d69fc43d317e77058ad040cd969" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/UI/UIComponent.Visitors.cs.meta", "5eac0a3ad52fbd68a2a5399481338c67c0d0846962bf97705359f3d0ff8a63ab" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/UI/UIComponent.cs", "0ac3253df765cacbbbf2c83012251cd09d40be9099c0f58cce89cc28f717683c" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/UI/UIComponent.cs.meta", "9b99356062bc50f4eafcc75f1af733302d345815b49b68ac114cb7ae70501ed7" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Vibrate/Managers/Definitions/VibrateSettings.cs", "12f4e94aac7573ff76b182b8570f9f1a4affc280286e9ae6486c283edfdd4ceb" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Vibrate/Managers/Definitions/VibrateSettings.cs.meta", "55b3743cf6000f682bb6fb6cde1bf9256739c380e05461b03464957f2e3ae61b" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Vibrate/Managers/Definitions/VibrateUnitSetting.cs", "bdc1160d5b5baca80ab7364b7bf97b7a38f51d0c7ae3b7a0bca807ee9f291c13" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Vibrate/Managers/Definitions/VibrateUnitSetting.cs.meta", "8503019f96e70f437c215f4e2df70f51dd0aaf4b050c4cf410251c0229e79842" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Vibrate/VibrateComponent.Visitors.cs", "b18c7de339175cef1afe67f863768529a1c3598a2044768c3106522108b0cba3" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Vibrate/VibrateComponent.Visitors.cs.meta", "2b967b8b9a1632b7b3d6186a32af3299c128cdd6fb3e53491e70ac543afac528" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Vibrate/VibrateComponent.cs", "b2d04d2aa0f7025413ab4aca42a7746797b48ab8b27a4e3e54831347205f42fd" },
            new[] { "Assets/Framework/Scripts/Runtime/Modules/Vibrate/VibrateComponent.cs.meta", "30f1f54900162b99959f139994765a2b6b4ee6efc96032ad5acf3057bfe84918" },
            new[] { "Assets/Framework/Scripts/Runtime/NovaFramework.Runtime.asmdef", "498b84a1db23dbddd5b70bc2d869248be85bb9ed925e7d9aca0216fb762803e7" },
            new[] { "Assets/Framework/Scripts/Runtime/NovaFramework.Runtime.asmdef.meta", "f5b070a55ecd4dafede0e8b6845fa1ff69cd1ea1ef66ff8bd8191581285ec5ce" },
            new[] { "Assets/Samples/MainDemo/MainDemo.unity", "7332d380c1e6221565db633502f45797ca3a7636ad27997cea4a964e6c570d6a" },
            new[] { "Assets/Samples/MainDemo/MainDemo.unity.meta", "874666e5820bd8bd42f87b22a9b2310a96cd9e67a6ad5e3b0a16d5e7bd78f6e3" },
            new[] { "Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Actions/InputAction.cs", "ee0d5b348b9abe6287590dc7478480e3888f60457eec6855823b54b95aa86dad" },
            new[] { "Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Actions/InputAction.cs.meta", "7b80d6ecad037fe0a825c58c0f124f1059acaf1593ef75ceeda6c5bd1fcb2cd1" },
            new[] { "Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Actions/InputActionAsset.cs", "c1dbb384b39f9d17d5d1feb68892e3de9eb495fc68912f68b5aa49bba4b0c731" },
            new[] { "Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Actions/InputActionAsset.cs.meta", "2cb1cfbe04c44b10a5ae6a32c8c32bff328df59351934866575d16c1ca802d67" },
            new[] { "Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Actions/InputActionMap.cs", "add0601187db154989b98a70d9d123e206c68e7a8acb2aa6391de1c0fdef8cea" },
            new[] { "Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Actions/InputActionMap.cs.meta", "ac58a03cd26443c7c955314766e5663357519f8a25081c3f7a02b79b3e5a535e" },
            new[] { "Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Actions/InputActionReference.cs", "b926393b9f8e74470b77e750635fdcaebd3ebd684dd8f28852db8d733bc2028e" },
            new[] { "Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Actions/InputActionReference.cs.meta", "91f82b09c58d930555e0828536bab1dad7e0559a0583c62b23b10de7acfdb1d5" },
            new[] { "Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions", "16872cf58474b0cf22ddd36298ae419a8cbae26b85571e282df311bf3d400d26" },
            new[] { "Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions.meta", "898d1ef8d3b49620e81265fb5d49aa45548cfe9bd2461b9939367c2165ffbaa7" },
            new[] { "Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Plugins/UI/InputSystemUIInputModule.cs", "1334661b7a824ead8b3b0a8ff3ce17e93f80a369ec3e3e37cd796d90f13223e2" },
            new[] { "Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Plugins/UI/InputSystemUIInputModule.cs.meta", "d6fbfb0f7ac63d39df2fe3ff62373bf46c4e90d3bd16c93d288c05d9f70baa07" },
            new[] { "Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Unity.InputSystem.asmdef", "6482e194ea960e1d4aef82c9012c5032f76880174d5caaadf8c834d9af2385cf" },
            new[] { "Library/PackageCache/com.unity.inputsystem@21a28c3a6c83/InputSystem/Unity.InputSystem.asmdef.meta", "9ee8fa1283cd421fc93599f6c200beb015ccce5c4a37f1df19c9410819e92dee" },
            new[] { "Library/PackageCache/com.unity.ugui@d8a2716f3013/Runtime/UGUI/EventSystem/EventSystem.cs", "284a5e996bc0047dac6a2fc0314ab7d154add68767c583ea8004606087de1e8c" },
            new[] { "Library/PackageCache/com.unity.ugui@d8a2716f3013/Runtime/UGUI/EventSystem/EventSystem.cs.meta", "38492b18fbb599b25b8407566441dedb3376722a86411c078089a4f1481769ad" },
            new[] { "Library/PackageCache/com.unity.ugui@d8a2716f3013/Runtime/UGUI/EventSystem/InputModules/BaseInputModule.cs", "122122d0a4edfcab6eef0013ed4d6a314e56b73658336a4ef56dde64a5dd8c52" },
            new[] { "Library/PackageCache/com.unity.ugui@d8a2716f3013/Runtime/UGUI/EventSystem/InputModules/BaseInputModule.cs.meta", "148e699781b551fb79a50f86d48b32aaf2b68b77966bff90b1efd7415aacdf7f" },
            new[] { "Library/PackageCache/com.unity.ugui@d8a2716f3013/Runtime/UGUI/EventSystem/RaycasterManager.cs", "8d48beb4ddf47a646d51557de74e71833260035af5afcbeba5853c90cbd9414f" },
            new[] { "Library/PackageCache/com.unity.ugui@d8a2716f3013/Runtime/UGUI/EventSystem/Raycasters/BaseRaycaster.cs", "a9adc1d293a70f99ff5be03d9d881677a4ee9e3bd7cc94075873e67d6a82e420" },
            new[] { "Library/PackageCache/com.unity.ugui@d8a2716f3013/Runtime/UGUI/EventSystem/Raycasters/BaseRaycaster.cs.meta", "9756a7d459982f6b034c264055f3748fde85160b19e7fd3608d78fbbf596fc88" },
            new[] { "Library/PackageCache/com.unity.ugui@d8a2716f3013/Runtime/UGUI/EventSystem/UIBehaviour.cs", "2a145c6aa56cfd949bad90853cce3bb5c20272c3cf1583e72d48fde786a6c1fb" },
            new[] { "Library/PackageCache/com.unity.ugui@d8a2716f3013/Runtime/UGUI/EventSystem/UIBehaviour.cs.meta", "774e86a476c4db8c3cf87c7475926169818fb7c032c82f8e48f024c7ef5bdac4" },
            new[] { "Library/PackageCache/com.unity.ugui@d8a2716f3013/Runtime/UGUI/UI/Core/GraphicRaycaster.cs", "d46836408d508bbb36b3a15a980252cfda49b2617aa48d11aae33e971af9cb64" },
            new[] { "Library/PackageCache/com.unity.ugui@d8a2716f3013/Runtime/UGUI/UI/Core/GraphicRaycaster.cs.meta", "bd85d59c1e6f2343419e25903d6ef60a7d4c39a491696cc04d1feb6865fd24b7" },
            new[] { "Library/PackageCache/com.unity.ugui@d8a2716f3013/Runtime/UGUI/UI/Core/Layout/CanvasScaler.cs", "dc05385101ef3274634e888a684f838d62aabe7c4c1e057fff8882b2dc403e0e" },
            new[] { "Library/PackageCache/com.unity.ugui@d8a2716f3013/Runtime/UGUI/UI/Core/Layout/CanvasScaler.cs.meta", "e60f5021b9d7f74b44c6c5aa722d924dfc48dad17afeac20f6114b02021442cd" },
            new[] { "Library/PackageCache/com.unity.ugui@d8a2716f3013/Runtime/UGUI/UnityEngine.UI.asmdef", "9cb2d9ab449842401295e78f89b0d8b2fd719f3ba777044f1813df7aab121716" },
            new[] { "Library/PackageCache/com.unity.ugui@d8a2716f3013/Runtime/UGUI/UnityEngine.UI.asmdef.meta", "5aa68e77cfca72b66d72505952e66c1ea2fc4967b93c84969a4d80c1b44ae1a7" },
        };
        internal static readonly string[] Components = {
            "NovaFramework.Runtime.SDKComponent",
            "NovaFramework.Runtime.DebugComponent",
            "NovaFramework.Runtime.TableComponent",
            "NovaFramework.Runtime.AssetComponent",
            "NovaFramework.Runtime.Nova",
            "UnityEngine.EventSystems.EventSystem",
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule",
            "NovaFramework.Runtime.EventComponent",
            "NovaFramework.Runtime.PersistComponent",
            "NovaFramework.Runtime.NetworkComponent",
            "NovaFramework.Runtime.VibrateComponent",
            "NovaFramework.Runtime.PrefabComponent",
            "NovaFramework.Runtime.ObjectPoolComponent",
            "NovaFramework.Runtime.SoundComponent",
            "NovaFramework.Runtime.UIComponent",
            "NovaFramework.Runtime.ConfigComponent",
            "NovaFramework.Runtime.ProcedureComponent",
            "NovaFramework.Runtime.NativeComponent",
            "NovaFramework.Runtime.AppComponent",
            "UnityEngine.UI.CanvasScaler",
            "UnityEngine.UI.GraphicRaycaster",
            "NovaFramework.Runtime.LocalizationComponent",
        };
        /// <summary>来源准入和本次实际编译身份分开保存；预期false来自已审源，尚非实际预览结果。</summary>
        [Serializable] internal sealed class Plan
        {
            public string[] scenes;
            public string[] sourcePins;
            public string[] modulePins;
            public string[] sceneOpenedSubscribers;
            public string expectedResultBasis;
            public bool predictedAppInstaller;
        }

        /// <summary>只从当前Build Settings、固定源闭包和实际加载类型建立计划；不打开预览或提前读取Native配置。</summary>
        /// <param name="scenes">原Action冻结的启用场景。</param><param name="token">当前取消。</param><returns>当前单场景的源码预测与实际身份。</returns>
        internal static Plan Capture(string[] scenes, CancellationToken token)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android || EditorApplication.isPlayingOrWillChangePlaymode || scenes == null || !scenes.SequenceEqual(new[] { ScenePath }, StringComparer.Ordinal))
                throw new InvalidOperationException("Native预览仅审当前MainDemo/canonical Nova闭包；其它场景不继承许可。");
            EditorBuildSettingsScene[] entries = EditorBuildSettings.scenes;
            if (entries == null || entries.Any(entry => entry == null) || !entries.Where(entry => entry.enabled).Select(entry => entry.path).SequenceEqual(scenes, StringComparer.Ordinal)) throw new InvalidOperationException("Native原Build Settings场景不符合冻结输入。");
            string[] sources = CaptureSources(token);
            Type[] types = Components.Select(ResolveType).Concat(new[] { typeof(NativeBuildProcessor), typeof(NativeBuildPreviewObservers) }).ToArray();
            foreach (Type type in types.Where(value => typeof(MonoBehaviour).IsAssignableFrom(value)))
            {
                var execution = new List<string>();
                for (Type item = type; item != null; item = item.BaseType)
                    execution.AddRange(CustomAttributeData.GetCustomAttributes(item).Select(value => value.AttributeType.FullName).Where(value => value == "UnityEngine.ExecuteAlways" || value == "UnityEngine.ExecuteInEditMode"));
                if (type.FullName == "UnityEngine.UI.CanvasScaler" ? execution.Count != 1 || execution[0] != "UnityEngine.ExecuteAlways" : execution.Count != 0)
                    throw new InvalidOperationException("Native预览实际编辑态生命周期属性改变。" + type.FullName);
            }
            string[] modules = types.Select(value => value.Assembly).Distinct().OrderBy(value => value.FullName, StringComparer.Ordinal)
                .Select(value => value.FullName + "|" + value.ManifestModule.ModuleVersionId.ToString("D") + "|" + GenerateActionCommon.ComputeFileHash(value.Location, token)).ToArray();
            Delegate[] subscribers = SceneOpenedDelegates(null); ValidateSceneOpenedSubscribers(subscribers);
            return new Plan { scenes = SceneRows(entries), sourcePins = sources, modulePins = modules, sceneOpenedSubscribers = subscribers.Select(DelegateIdentity).ToArray(), predictedAppInstaller = false,
                expectedResultBasis = "reviewed-exact-scene-prefab-source-default;not-observed-native-getter" };
        }

        /// <summary>核原完整source与实际编译身份，不把请求DTO改动或同名文件当源码见证。</summary>
        /// <param name="plan">原完整计划。</param><param name="token">当前取消。</param>
        internal static void VerifyPlan(Plan plan, CancellationToken token)
        {
            Plan current = Capture(new[] { ScenePath }, token);
            if (plan == null || JsonConvert.SerializeObject(plan) != JsonConvert.SerializeObject(current)) throw new InvalidOperationException("Native预览源码/场景/实际编译或原订阅集合漂移。");
        }

        /// <summary>验证原同步历史与当前持久源；编译后不要求旧域窗口目标身份，也不重开预览补历史。</summary>
        /// <param name="plan">原完整计划。</param><param name="evidence">原真实收据。</param><param name="token">当前取消。</param>
        internal static void VerifyCompleted(Plan plan, AndroidGenerateNativePreviewScope.Evidence evidence, CancellationToken token)
        {
            if (!HasCompleteHistory(plan, evidence)) throw new InvalidOperationException("Native原预览历史不完整或短路/关闭顺序漂移。");
            Plan current = Capture(new[] { ScenePath }, token);
            if (!current.scenes.SequenceEqual(plan.scenes, StringComparer.Ordinal) || !current.sourcePins.SequenceEqual(plan.sourcePins, StringComparer.Ordinal) || !current.modulePins.SequenceEqual(plan.modulePins, StringComparer.Ordinal)) throw new InvalidOperationException("Native重载后持久场景/源/实际编译身份变化。");
        }

        /// <summary>纯历史结构校验仅防篡改/缺帧；fixture不因此成为实际预览证据。</summary>
        internal static bool HasCompleteHistory(Plan plan, AndroidGenerateNativePreviewScope.Evidence evidence)
        {
            if (plan == null || evidence?.complete != true || !evidence.disposed || !evidence.queryStarted || !evidence.queryReturned || evidence.result != plan.predictedAppInstaller || evidence.failures == null || evidence.failures.Length != 0 || evidence.sceneEvents == null || evidence.sceneEvents.Any(value => !value.EndsWith("|Additive", StringComparison.Ordinal)) || evidence.stages == null || evidence.candidates?.Length != 1) return false;
            string[] order = { "query-started", "before-open", "opened", "scanned", "before-close", "closed", "query-returned" };
            if (!evidence.stages.Select(value => value?.phase).SequenceEqual(order, StringComparer.Ordinal)) return false;
            var candidate = evidence.candidates[0]; var opened = evidence.stages[2]; var closed = evidence.stages[5];
            int[] ordinals = evidence.stages.Select(value => value.ordinal).Concat(new[] { candidate.ordinal }).OrderBy(value => value).ToArray();
            return ordinals.SequenceEqual(Enumerable.Range(1, 8)) && evidence.stages.Zip(evidence.stages.Skip(1), (left, right) => left.ordinal < right.ordinal).All(value => value) && candidate.ordinal > opened.ordinal && candidate.ordinal < evidence.stages[3].ordinal && candidate.nativeId != 0 && candidate.enabled && candidate.activeEvaluated && candidate.active && candidate.configEvaluated && candidate.configNonNull && candidate.installEvaluated && candidate.installEnabled == plan.predictedAppInstaller &&
                opened.path == ScenePath && opened.preview && opened.objects?.Length == Components.Length && opened.canvas?.Length == 5 && opened.environment != null && evidence.stages[1].environment != null && closed.environment != null && evidence.stages[4].environment != null && evidence.stages[4].canvas?.Length == 5 && opened.previewCount == evidence.stages[1].previewCount + 1 && evidence.stages[4].previewCount == opened.previewCount &&
                !string.IsNullOrEmpty(opened.sceneHandle) && closed.sceneHandle == opened.sceneHandle && closed.closeReturned && closed.result && closed.previewCount == evidence.stages[1].previewCount && evidence.stages[3].sceneHandle == opened.sceneHandle && evidence.stages[4].sceneHandle == opened.sceneHandle && evidence.stages[3].result == plan.predictedAppInstaller;
        }

        /// <summary>保持原完整有序数组，包含原方法自然跳过的禁用项。</summary>
        internal static string[] SceneRows(EditorBuildSettingsScene[] scenes) => (scenes ?? Array.Empty<EditorBuildSettingsScene>()).Select(entry => entry == null ? "null" : entry.enabled + "|" + entry.path).ToArray();

        /// <summary>核全部固定源，包缓存根由实际已解析包决定；只读source/hash，不输出文件正文。</summary>
        internal static string[] CaptureSources(CancellationToken token) => Sources.Select(row =>
        {
            token.ThrowIfCancellationRequested(); string path = ResolvePath(row[0]);
            if (!File.Exists(path) || GenerateActionCommon.ComputeFileHash(path, token) != row[1]) throw new InvalidOperationException("Native预览已审源漂移：" + row[0]);
            return path + "|" + row[1];
        }).ToArray();

        /// <summary>仅解析两个已审Unity包角色与本项目固定路径，不加载程序集或业务资源。</summary>
        internal static string ResolvePath(string path)
        {
            foreach (string package in new[] { "com.unity.inputsystem", "com.unity.ugui" })
            {
                string prefix = "Library/PackageCache/" + package + "@";
                if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
                int split = path.IndexOf('/', prefix.Length);
                var info = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().SingleOrDefault(value => value.name == package);
                if (split < 0 || info == null) throw new InvalidOperationException("Native预览包来源缺失。");
                return Path.GetFullPath(Path.Combine(info.resolvedPath, path.Substring(split + 1)));
            }
            return Path.GetFullPath(Path.Combine(BuildActionCommon.ProjectRoot, path));
        }

        /// <summary>只找已自然加载的唯一准确类型，不触发LoadAssembly或创建对象。</summary>
        internal static Type ResolveType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(value => value.GetType(name, false)).Where(value => value != null).SingleOrDefault() ?? throw new InvalidOperationException("Native预览准确类型尚未自然加载：" + name);

        /// <summary>读取准确场景事件backing，不调用或屏蔽原监听；仅排除本scope自己登记的观察者。</summary>
        internal static Delegate[] SceneOpenedDelegates(object own)
        {
            FieldInfo field = typeof(EditorSceneManager).GetField("sceneOpened", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null || !typeof(Delegate).IsAssignableFrom(field.FieldType)) throw new InvalidOperationException("Native sceneOpened实际backing未知。");
            return ((Delegate)field.GetValue(null))?.GetInvocationList().Where(value => own == null || !ReferenceEquals(value.Target, own)).ToArray() ?? Array.Empty<Delegate>();
        }

        /// <summary>仅接纳四组已审监听的准确成员；未知自然监听仍在open前明确阻断。</summary>
        internal static void ValidateSceneOpenedSubscribers(Delegate[] subscribers)
        {
            string[] allowed = { "NovaFramework.Editor.EditorUtil+SceneRoute.OnSceneOpened", "NovaFramework.Editor.EditorUtil+HybridCLR.OnSceneOpenedAutoSyncActiveSampleDlls", "NovaFramework.Editor.ConfigWindow.OnSceneOpenedRefresh", "NovaFramework.Editor.PipifyWindow.OnSceneOpenedRefresh" };
            foreach (Delegate value in subscribers)
                if (!allowed.Contains(value.Method.DeclaringType?.FullName + "." + value.Method.Name, StringComparer.Ordinal) || value.Method.DeclaringType.Assembly != typeof(EditorUtil).Assembly || value.Method.DeclaringType != ResolveType(value.Method.DeclaringType.FullName) || value.Method.ReturnType != typeof(void) || !value.Method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(new[] { typeof(Scene), typeof(OpenSceneMode) }) || (value.Method.IsStatic ? value.Target != null : value.Target == null || value.Target.GetType() != value.Method.DeclaringType))
                    throw new InvalidOperationException("Native预览sceneOpened含未审自然监听：" + DelegateIdentity(value));
        }

        /// <summary>准确方法/模块/目标身份摘要，不调用delegate或目标Equals/GetHashCode。</summary>
        internal static string DelegateIdentity(Delegate value) => value.Method.DeclaringType?.FullName + "." + value.Method.Name + "|" + value.Method.Module.ModuleVersionId.ToString("D") + "|" + value.Method.MetadataToken.ToString(CultureInfo.InvariantCulture) + "|target=" + (value.Target == null ? "null" : value.Target.GetType().FullName + ":" + RuntimeHelpers.GetHashCode(value.Target).ToString(CultureInfo.InvariantCulture));
    }
}
