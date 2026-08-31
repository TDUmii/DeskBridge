# Third-party notices

DeskBridge uses [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) 3.1.12 for local image decoding, resizing, compression, and encoding.

ImageSharp is distributed under the [Six Labors Split License, Version 1.0](https://github.com/SixLabors/ImageSharp/blob/main/LICENSE). Review that license and Six Labors' current commercial licensing terms before distributing DeskBridge in a use case that does not qualify for the license's Apache 2.0 option. DeskBridge itself is MIT licensed; that does not replace ImageSharp's license.

The optional document-to-Markdown integration downloads and invokes [`@firecrawl/anydoc`](https://github.com/firecrawl/anydoc) at runtime. Anydoc is distributed under the MIT License. It is not bundled in the DeskBridge package; npm supplies its package and dependency notices when the user enables and runs the adapter.

DeskBridge's bounded workspace-context design was informed by [`codex-with-chatgpt`](https://github.com/XiaoDuoYa/codex-with-chatgpt). DeskBridge does not bundle that project's MCP, OAuth, tunnel, or Codex execution components. The upstream project is distributed under the MIT License:

```text
MIT License

Copyright (c) 2026 codex-with-chatgpt contributors

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
