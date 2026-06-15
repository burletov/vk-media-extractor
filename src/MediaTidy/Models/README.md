# Recognition model files

ONNX weights are not stored in Git history.

From the repository root, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\download-model.ps1
```

The script downloads the bundled CLIP ViT-B/32 vision model from
`Xenova/clip-vit-base-patch32` and verifies its SHA-256 checksum before use.
