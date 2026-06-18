# Third-Party Notices

## Scope

- This file describes, at the root level of `com.solotopia.simplediskutils`, the third-party sources, license boundaries, and public-distribution requirements.
- For the packaging-layer license boundary, see [LICENSE.md](./LICENSE.md).

## Upstream components and licenses

- `simple-disk-utils`
  - Upstream project: `https://github.com/dkrprasetya/simple-disk-utils`
  - License: `MIT`
  - Corresponding content in this repo: `Core/**`
  - Upstream material directly verifiable here: `Core/README.txt`, `Core/Plugins/DiskUtils/DiskUtils.cs`
- The Windows DLLs, Android JAR, macOS bundle, and iOS source in this package all belong to the same upstream plugin distribution and must be retained together with this file.

## Nova packaging boundary

- `Nova/**`, the root `package.json`, `README.md`, `CHANGELOG.md`, and `LICENSE.md` are the Solotopia / Nova UPM packaging, integration notes, and release-maintenance layer; they do not override the original license boundary of `Core/**`.

## Public distribution requirements

- When distributing publicly, retain this file and keep the copyright notices in `Core/README.txt` and the source file headers.
- The upstream MIT license original text is retained in the Chinese section below.

---

# 第三方声明

## 适用范围

- 本文件用于说明 `com.solotopia.simplediskutils` 包根层面的第三方来源、许可证边界与公开分发要求。
- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。

## 上游组件与许可证

- `simple-disk-utils`
  - 上游项目：`https://github.com/dkrprasetya/simple-disk-utils`
  - 许可证：`MIT`
  - 本仓库内对应内容：`Core/**`
  - 当前包内可直接确认的上游材料：
    - `Core/README.txt`
    - `Core/Plugins/DiskUtils/DiskUtils.cs`
- 当前包内的 Windows DLL、Android JAR、macOS bundle 与 iOS 源文件均归属于同一上游插件分发内容，应与本文件一起保留。

## Nova 封装边界

- `Nova/**`、包根 `package.json`、`README.md`、`CHANGELOG.md`、`LICENSE.md` 为 Solotopia / Nova 的 UPM 封装、接入说明与发版维护内容。
- 上述文件不覆盖 `Core/**` 的原始许可证边界。

## 公开分发要求

- 对外公开时，必须保留本文件，并保留 `Core/README.txt` 与源文件头部中的版权声明。
- 若未来替换任一平台原生插件或新增平台实现，应在包根继续追加新的归属说明。

## simple-disk-utils 上游 MIT 许可证原文

以下许可文本来自当前包内 `Core/Plugins/DiskUtils/DiskUtils.cs` 的文件头：

```text
MIT License

Copyright (c) 2016 M Dikra Prasetya

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
