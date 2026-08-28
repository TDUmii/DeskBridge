import { cp, mkdir, rm } from "node:fs/promises";
import { build } from "esbuild";
await rm("dist", { recursive: true, force: true });
await mkdir("dist", { recursive: true });
await build({
  entryPoints: {
    "background/index": "src/background/index.ts",
    "content/index": "src/content/index.ts",
    "popup/popup": "src/popup/popup.ts"
  },
  outdir: "dist",
  bundle: true,
  format: "esm",
  platform: "browser",
  target: "chrome120"
});
await cp("manifest.json", "dist/manifest.json");
await cp("src/content/content.css", "dist/content/content.css");
await cp("src/popup/popup.html", "dist/popup/popup.html");
await cp("src/popup/popup.css", "dist/popup/popup.css");
