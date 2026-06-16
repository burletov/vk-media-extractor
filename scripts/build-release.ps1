param(
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot "MediaTidy.sln"
$project = Join-Path $repoRoot "src\MediaTidy\MediaTidy.csproj"
$smokeProject = Join-Path $repoRoot "tests\MediaTidy.SmokeTests\MediaTidy.SmokeTests.csproj"
$artifacts = Join-Path $repoRoot "artifacts"
$publishDirectory = Join-Path $artifacts "publish"

function Clear-DirectoryInsideRepo {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $repoRootResolved = [System.IO.Path]::GetFullPath($repoRoot)
    $targetResolved = [System.IO.Path]::GetFullPath($Path)
    if (-not $targetResolved.StartsWith($repoRootResolved, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove path outside repository: $targetResolved"
    }

    Remove-Item -LiteralPath $targetResolved -Recurse -Force
}

& (Join-Path $PSScriptRoot "download-model.ps1")

Clear-DirectoryInsideRepo (Join-Path $repoRoot "src\MediaTidy\bin")
Clear-DirectoryInsideRepo (Join-Path $repoRoot "src\MediaTidy\obj")
Clear-DirectoryInsideRepo (Join-Path $repoRoot "tests\MediaTidy.SmokeTests\bin")
Clear-DirectoryInsideRepo (Join-Path $repoRoot "tests\MediaTidy.SmokeTests\obj")

dotnet restore $solution
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed." }

dotnet run --project $smokeProject -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "Smoke test failed." }

Remove-Item -LiteralPath $artifacts -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

dotnet publish $project `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDirectory
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

$executable = Join-Path $publishDirectory "MediaTidy.exe"
$releaseExecutable = Join-Path $artifacts "MediaTidy.exe"
Copy-Item -LiteralPath $executable -Destination $releaseExecutable -Force

$hash = (Get-FileHash -LiteralPath $releaseExecutable -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content `
    -LiteralPath (Join-Path $artifacts "SHA256SUMS.txt") `
    -Value "$hash  MediaTidy.exe" `
    -Encoding ascii

Write-Host "Release artifacts:"
Write-Host "  $releaseExecutable"
Write-Host "  $(Join-Path $artifacts 'SHA256SUMS.txt')"
