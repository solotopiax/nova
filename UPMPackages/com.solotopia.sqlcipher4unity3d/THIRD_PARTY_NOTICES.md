# Third-Party Notices

## Scope
- This file describes, at the root level of `com.solotopia.sqlcipher4unity3d`, the third-party sources, license boundaries, and public-distribution requirements.
- For the packaging-layer license boundary, see [LICENSE.md](./LICENSE.md).

## Upstream components and licenses
- `SqlCipher4Unity3D`
  - Upstream project: `https://github.com/netpyoung/SqlCipher4Unity3D`
  - License: `MIT`
  - Corresponding content in this repo: `Core/**`
  - Bundled upstream license file: `Core/LICENSE`
- `sqlite-net`
  - Upstream project: `https://github.com/praeclarum/sqlite-net`
  - License: `MIT`
  - File directly verifiable in this package: `Core/SQLiteAsync.cs` retains the original MIT license text in its file header
- `SQLite4Unity3d`
  - Upstream project: `https://github.com/codecoding/SQLite4Unity3d`
  - License: `MIT`
  - The `SqlCipher4Unity3D` upstream README explicitly lists this dependency; in this package, `Core/Sqlite3.cs`, `Core/Sqlite3Connection.cs`, and `Core/SQLite.Attribute/**` are part of this packaging chain
- `SQLite`
  - Official license stance: Public Domain
  - Form in this package: the SQLite kernel capability distributed together with the `SQLCipher` native libraries
- `SQLCipher`
  - Upstream project: `https://github.com/sqlcipher/sqlcipher`
  - License: see the `SQLCipher license summary` below
  - Form in this package: `Core/Plugins/**/sqlcipher.*`, `Core/Plugins/Android/libs/sqlcipher-android-4.6.1.aar`
- OpenSSL-related runtime libraries
  - Form in this package: `Core/Plugins/x64/libcrypto-1_1-x64.dll`, `Core/Plugins/x86/libcrypto-1_1.dll`, `Core/Plugins/x64/libcrypto.so`
  - These files are bundled together with the SQLCipher native binaries; if the native libraries are later replaced or upgraded, the notice must be re-reviewed and re-completed according to the new native-library source

## Nova packaging boundary
- The root `package.json`, `README.md`, `CHANGELOG.md`, and `LICENSE.md` are the Solotopia / Nova UPM packaging, integration notes, and release-maintenance content.
- These packaging files do not override the original license boundary of `Core/**` and each native plugin file.

## Public distribution requirements
- When distributing publicly, you must at minimum retain `Core/LICENSE`, this file, and the third-party copyright notices in the source file headers.
- If the `sqlcipher` / `libcrypto` native libraries or the Android AAR on any platform are replaced in the future, the corresponding version's license and notice must be re-verified; the current conclusions must not be reused directly.

The upstream license original text is retained in the section below.

---

# 第三方声明

## 适用范围

- 本文件用于说明 `com.solotopia.sqlcipher4unity3d` 包根层面的第三方来源、许可证边界与公开分发要求。
- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。

## 上游组件与许可证

- `SqlCipher4Unity3D`
  - 上游项目：`https://github.com/netpyoung/SqlCipher4Unity3D`
  - 许可证：`MIT`
  - 本仓库内对应内容：`Core/**`
  - 本仓库内许可文件：`Core/LICENSE`
- `sqlite-net`
  - 上游项目：`https://github.com/praeclarum/sqlite-net`
  - 许可证：`MIT`
  - 当前包内可直接确认的文件：`Core/SQLiteAsync.cs` 文件头保留了原始 MIT 许可文本
- `SQLite4Unity3d`
  - 上游项目：`https://github.com/codecoding/SQLite4Unity3d`
  - 许可证：`MIT`
  - `SqlCipher4Unity3D` 上游 README 已明确列出该依赖关系；当前包内 `Core/Sqlite3.cs`、`Core/Sqlite3Connection.cs`、`Core/SQLite.Attribute/**` 属于该封装链的一部分
- `SQLite`
  - 官方许可口径：公共领域（Public Domain）
  - 当前包内表现形式：随 `SQLCipher` 原生库一起分发的 SQLite 内核能力
- `SQLCipher`
  - 上游项目：`https://github.com/sqlcipher/sqlcipher`
  - 许可证：见下方 `SQLCipher 许可证摘要`
  - 当前包内表现形式：`Core/Plugins/**/sqlcipher.*`、`Core/Plugins/Android/libs/sqlcipher-android-4.6.1.aar`
- OpenSSL 相关运行时库
  - 当前包内表现形式：`Core/Plugins/x64/libcrypto-1_1-x64.dll`、`Core/Plugins/x86/libcrypto-1_1.dll`、`Core/Plugins/x64/libcrypto.so`
  - 这些文件随 SQLCipher 原生二进制一起打包；后续若替换或升级原生库，必须按新原生库来源重新复核并补齐 notice

## Nova 封装边界

- 包根 `package.json`、`README.md`、`CHANGELOG.md`、`LICENSE.md` 为 Solotopia / Nova 的 UPM 封装、接入说明与发版维护内容。
- 上述封装文件不覆盖 `Core/**` 及各原生插件文件的原始许可证边界。

## 公开分发要求

- 对外公开时，至少必须同时保留 `Core/LICENSE`、本文件，以及源文件头部中的第三方版权声明。
- 若未来替换任意平台的 `sqlcipher` / `libcrypto` 原生库或 Android AAR，应重新核对对应版本的许可证与 notice，不得直接沿用当前结论。

## SQLCipher 许可证摘要

以下许可证摘要来自 `SqlCipher4Unity3D` 上游 README 中引用的 SQLCipher 许可条款：

```text
Copyright (c) 2008-2012 Zetetic LLC
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
    * Redistributions of source code must retain the above copyright
      notice, this list of conditions and the following disclaimer.
    * Redistributions in binary form must reproduce the above copyright
      notice, this list of conditions and the following disclaimer in the
      documentation and/or other materials provided with the distribution.
    * Neither the name of the ZETETIC LLC nor the
      names of its contributors may be used to endorse or promote products
      derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY ZETETIC LLC ''AS IS'' AND ANY
EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL ZETETIC LLC BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```
