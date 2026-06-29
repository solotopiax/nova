# License Notice

This file describes the license boundary of `com.solotopia.nova.framework.sdk.facebook`. It does not re-license the whole package as a single Solotopia MIT package.

## License Boundary

| Path | Ownership / source | License and obligations |
|---|---|---|
| `Nova/**` | Solotopia / Nova adapter layer | Solotopia / Nova MIT License below. |
| Root package metadata and docs authored by Solotopia, including `package.json`, `README.md`, `CHANGELOG.md`, `LICENSE.md`, and `THIRD_PARTY_NOTICES.md` | Solotopia / Nova UPM packaging and documentation | Solotopia / Nova MIT License below. |
| `Core/FacebookSDK/**` | Upstream Facebook Unity SDK material | Upstream Facebook SDK license, platform terms, developer policies, and notice requirements. See `Core/FacebookSDK/LICENSE.txt`. |
| `Core/Editor/DisableBitcode.cs` | Imported with the Facebook SDK package | Treated as upstream SDK material unless replaced by a Solotopia-owned implementation. |

The Solotopia / Nova MIT License does not override the original license, policy, EULA, trademark, or third-party notice chain for content under `Core/**`.

## Solotopia / Nova MIT License

MIT License

Copyright (c) 2026 Solotopia

Permission is hereby granted, free of charge, to any person obtaining a copy of the Solotopia-authored package layer and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Solotopia-authored package layer.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## Redistribution Notes

- Public distribution must retain `LICENSE.md`, `THIRD_PARTY_NOTICES.md`, and `Core/FacebookSDK/LICENSE.txt`.
- Do not describe this package as a pure MIT package. It combines Solotopia-authored MIT content with upstream Facebook SDK content.
- Recheck the current Facebook SDK license, platform policies, developer terms, App Review requirements, and trademark usage requirements at release time.
