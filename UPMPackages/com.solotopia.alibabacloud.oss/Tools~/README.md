# Unity DLL build

Run from the package root:

```bash
dotnet build Tools~/AlibabaCloud.OSS.V2.Unity.csproj -c Release
```

Copy `Tools~/bin/Release/netstandard2.0/AlibabaCloud.OSS.V2.dll`, `AlibabaCloud.OSS.V2.xml`, and `Microsoft.Bcl.AsyncInterfaces.dll` to `Nova/Editor/Plugins/` after a successful build. The Unity editor artifact targets `netstandard2.0` because Unity feeds precompiled plugin references through its Roslyn analyzer pipeline. The project compiles only the production sources from the pinned, read-only upstream snapshot and copies the upstream-required `Microsoft.Bcl.AsyncInterfaces` `9.0.2` dependency into the output.
