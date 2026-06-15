param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$modelDirectory = Join-Path $repoRoot "src\MediaTidy\Models"
$modelPath = Join-Path $modelDirectory "clip-vision-int8.onnx"
$temporaryPath = "$modelPath.part"
$modelUri = "https://huggingface.co/Xenova/clip-vit-base-patch32/resolve/main/onnx/vision_model_quantized.onnx"
$expectedHash = "583FD1110A514667812FEE7D684952AAF82A99B959760C8D7DCA7E0AB9839299"

if ((Test-Path -LiteralPath $modelPath) -and -not $Force) {
    $currentHash = (Get-FileHash -LiteralPath $modelPath -Algorithm SHA256).Hash
    if ($currentHash -eq $expectedHash) {
        Write-Host "Recognition model is already present."
        exit 0
    }
}

New-Item -ItemType Directory -Path $modelDirectory -Force | Out-Null
Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue

Write-Host "Downloading the bundled CLIP vision model (about 89 MB)..."
Invoke-WebRequest -Uri $modelUri -OutFile $temporaryPath

$downloadedHash = (Get-FileHash -LiteralPath $temporaryPath -Algorithm SHA256).Hash
if ($downloadedHash -ne $expectedHash) {
    Remove-Item -LiteralPath $temporaryPath -Force
    throw "Downloaded model checksum mismatch. Expected $expectedHash, got $downloadedHash."
}

Move-Item -LiteralPath $temporaryPath -Destination $modelPath -Force
Write-Host "Model saved to $modelPath"

