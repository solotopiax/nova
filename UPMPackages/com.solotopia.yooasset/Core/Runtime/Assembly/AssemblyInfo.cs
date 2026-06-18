using System.Runtime.CompilerServices;

// modify: by taoye - 新增 NovaFramework.Editor 友元，使 EditorUtil.Config.YooAssetInjector 可访问 internal SetSettings。

// 内部友元
[assembly: InternalsVisibleTo("YooAsset.Editor")]
[assembly: InternalsVisibleTo("YooAsset.Tests")]
[assembly: InternalsVisibleTo("YooAsset.Tests.Editor")]

// 外部友元
[assembly: InternalsVisibleTo("YooAsset.MiniGame")]
[assembly: InternalsVisibleTo("YooAsset.Extension")]
[assembly: InternalsVisibleTo("YooAsset.Extension.Editor")]
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]

// Nova 框架友元
[assembly: InternalsVisibleTo("NovaFramework.Editor")]
