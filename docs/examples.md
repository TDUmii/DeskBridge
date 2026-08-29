# Action examples

Paste these as fenced blocks with language `deskbridge` in ChatGPT. The extension adds a button; it never runs them automatically. Replace example paths with paths inside the workspace selected in the desktop app.

## Read a file

````markdown
```deskbridge
{"version":1,"id":"read-1","action":"read_file","arguments":{"path":"D:\\Projects\\Demo\\README.md"}}
```
````

## Create a static website

```json
{
  "version": 1,
  "id": "site-1",
  "action": "create_project",
  "arguments": {
    "rootPath": "D:\\Projects\\Demo\\DemoWebsite",
    "projectType": "static-web",
    "files": [
      { "path": "index.html", "content": "<!doctype html><link rel=\"stylesheet\" href=\"./css/style.css\"><h1>Demo</h1><script src=\"./js/script.js\" defer></script>" },
      { "path": "css/style.css", "content": "body { font-family: system-ui; }" },
      { "path": "js/script.js", "content": "console.log('DeskBridge');" }
    ]
  }
}
```

## Patch one unique fragment

```json
{"version":1,"id":"patch-1","action":"patch_file","arguments":{"path":"D:\\Projects\\Demo\\css\\style.css","replacements":[{"oldText":"min-height: 70vh;","newText":"min-height: 90vh;"}]}}
```

## Git status

```json
{"version":1,"id":"git-1","action":"run_command","arguments":{"program":"git","args":["status"],"workingDirectory":"D:\\Projects\\Demo"}}
```

## Download and optimize a public image

Run `download_asset`, then inspect and convert it:

```json
{"version":1,"id":"asset-1","action":"download_asset","arguments":{"url":"https://example.com/photo.jpg","destination":"D:\\Projects\\Demo\\assets\\images\\photo.jpg"}}
```

```json
{"version":1,"id":"asset-2","action":"resize_image","arguments":{"source":"D:\\Projects\\Demo\\assets\\images\\photo.jpg","destination":"D:\\Projects\\Demo\\assets\\images\\photo-1600.jpg","width":1600,"height":null,"keepAspectRatio":true}}
```

```json
{"version":1,"id":"asset-3","action":"convert_image","arguments":{"source":"D:\\Projects\\Demo\\assets\\images\\photo-1600.jpg","destination":"D:\\Projects\\Demo\\assets\\images\\photo.webp","format":"webp","quality":82}}
```

## Preview

```json
{"version":1,"id":"preview-1","action":"preview_web","arguments":{"rootPath":"D:\\Projects\\Demo","entryFile":"index.html"}}
```

## Convert a document to Markdown

Enable **Convert documents to Markdown** in DeskBridge Settings first. Both paths must be inside the selected workspace.

```json
{"version":1,"id":"document-1","action":"convert_document_to_markdown","arguments":{"source":"D:\\Projects\\Demo\\lesson.docx","destination":"D:\\Projects\\Demo\\notes\\lesson.md","overwrite":false}}
```

## Read enabled skill guidance

```json
{"version":1,"id":"skills-1","action":"get_skill_profile","arguments":{}}
```
