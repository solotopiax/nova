# Unity DLL build

Run from the package root:

```bash
dotnet build Tools~/AlibabaCloud.OSS.V2.Unity.csproj -c Release
```

Copy `Tools~/bin/Release/netstandard2.1/AlibabaCloud.OSS.V2.dll` and `AlibabaCloud.OSS.V2.xml` to `Core/Plugins/` after a successful build. The project compiles only the production sources from the pinned, read-only upstream snapshot and introduces no runtime NuGet dependencies.
