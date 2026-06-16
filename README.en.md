<p align="center">
  <img src="docs/logo.png" width="180" alt="MediaTidy">
</p>

# MediaTidy

**MediaTidy** is a Windows app for importing, deduplicating, and safely cleaning large photo and video archives.

Use it to import VK media, scan folders, find exact duplicates and similar images, compare files in a visual interface, and move unwanted items to a reversible quarantine.

## Highlights

- **Import all VK photos and videos in a few clicks.** Personal chats, group chats, one selected chat, photos, videos, and GIF files. Imports can be stopped and resumed.
- **Find duplicate media.** Exact photo and video copies are detected by SHA-256; visually similar images are grouped as review candidates.
- **Work with files comfortably.** Table filters, sortable columns, preview, side-by-side comparison, quarantine, restore, and CSV reports.
- **Handle large archives.** Multiple folders, optional merged media library, quick candidate selection, and report export before file operations.
- **Dark theme included.** Not the main feature, but useful for long cleanup sessions.

MediaTidy never deletes files automatically. You choose what to keep, quarantine, or restore.

## Download

Download `MediaTidy.exe` from the [latest GitHub Release](https://github.com/burletov/MediaTidy/releases/latest). The app is portable and does not require installation.

`SHA256SUMS.txt` is optional. It is provided only for integrity checks, so you can verify that `MediaTidy.exe` was not corrupted during download and matches the release file.

The executable is not code-signed yet and may trigger Windows SmartScreen. Download it only from `burletov/MediaTidy` and verify the SHA-256 checksum against `SHA256SUMS.txt` if needed.

## VK import

1. Open **File → Import from VK**.
2. Enter your VK token, destination folder, and media types.
3. Import all chats or select one chat.
4. For one chat, copy the chat ID from the VK browser address bar.
5. Optionally enable GIF import and file size limits.
6. Start import. If interrupted, the next run resumes from the saved checkpoint.

MediaTidy is unofficial and is not affiliated with VK. Users are responsible for following VK rules and respecting rights to downloaded materials. The optional token helper opens the third-party VKHost website; review its address and requested permissions carefully.

## Requirements

- Windows 10 or Windows 11 x64;
- 4 GB RAM minimum, 8 GB recommended;
- about 400 MB of free disk space;
- internet access only for VK imports and optional model downloads.

Scanning, OCR, and image recognition run locally.

## Build

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\download-model.ps1
dotnet restore .\MediaTidy.sln
dotnet run --project .\tests\MediaTidy.SmokeTests\MediaTidy.SmokeTests.csproj -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-release.ps1
```

The self-contained executable and checksum will be written to `artifacts`.

## Support the author

MediaTidy was created by **Igor Burletov**. Donations are optional:

- BTC: `115M2wYM1UJ5RDQi2BBsc41R3XaTNxyLjp`
- ETH (ERC-20): `0xc3fc88bb6b415a1822b7989df2865795f5351c97`
- USDT (TRC-20): `TH2UKCXXfdPYxsgxcp1BtPijkEDZWAo3Km`
- USDT (BEP-20): `0xc3fc88bb6b415a1822b7989df2865795f5351c97`

Crypto transfers are irreversible. Verify the asset, network, and address before sending.

## License

The source code is licensed under the [MIT License](LICENSE). See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for third-party components and models.
