# Third-party notices

DeskBridge uses [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) 3.1.12 for local image decoding, resizing, compression, and encoding.

ImageSharp is distributed under the [Six Labors Split License, Version 1.0](https://github.com/SixLabors/ImageSharp/blob/main/LICENSE). Review that license and Six Labors' current commercial licensing terms before distributing DeskBridge in a use case that does not qualify for the license's Apache 2.0 option. DeskBridge itself is MIT licensed; that does not replace ImageSharp's license.

The optional document-to-Markdown integration downloads and invokes [`@firecrawl/anydoc`](https://github.com/firecrawl/anydoc) at runtime. Anydoc is distributed under the MIT License. It is not bundled in the DeskBridge package; npm supplies its package and dependency notices when the user enables and runs the adapter.
