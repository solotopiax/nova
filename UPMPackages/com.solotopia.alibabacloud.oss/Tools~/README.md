# Unity DLL build

Run from the [package root](../), located from the current `com.solotopia.alibabacloud.oss` package's `resolvedPath`:

```bash
dotnet build Tools~/AlibabaCloud.OSS.V2.Unity.csproj -c Release
```

Copy the generated `Tools~/bin/Release/netstandard2.0/AlibabaCloud.OSS.V2.dll`, `AlibabaCloud.OSS.V2.xml`, and `Microsoft.Bcl.AsyncInterfaces.dll` to [Nova/Editor/Plugins/](../Nova/Editor/Plugins/) after a successful build. The output path is relative to the resolved package root and exists only after building. The Unity editor artifact targets `netstandard2.0` because Unity feeds precompiled plugin references through its Roslyn analyzer pipeline. The project compiles only the production sources from the pinned, read-only upstream snapshot and copies the upstream-required `Microsoft.Bcl.AsyncInterfaces` `9.0.2` dependency into the output.
