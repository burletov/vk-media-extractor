<p align="center">
  <img src="docs/logo.png" width="180" alt="MediaTidy">
</p>

# MediaTidy

MediaTidy is a free, local-first photo and video organizer for Windows. It detects exact and visually similar files, provides side-by-side comparison, uses a reversible quarantine, exports reports, and supports resumable VK media imports.

## Download

Download `MediaTidy.exe` and `SHA256SUMS.txt` from the [latest GitHub Release](https://github.com/burletov/MediaTidy/releases/latest). The application is portable and does not require installation.

The executable is not yet code-signed and may trigger Windows SmartScreen. Download it only from `burletov/MediaTidy` and verify its SHA-256 checksum.

## Requirements

- Windows 10 or Windows 11 x64;
- 4 GB RAM minimum, 8 GB recommended;
- approximately 400 MB of free disk space;
- network access only for VK imports and optional model downloads.

Scanning, OCR, and image recognition run locally. Recognition categories are probabilistic hints and never trigger automatic deletion.

## Build

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), then run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\download-model.ps1
dotnet restore .\MediaTidy.sln
dotnet run --project .\tests\MediaTidy.SmokeTests\MediaTidy.SmokeTests.csproj -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-release.ps1
```

The self-contained executable and checksum will be written to `artifacts`.

## VK notice

MediaTidy is unofficial and is not affiliated with VK. Users must provide their own access token and comply with VK rules and applicable rights. For a single chat import, open the VK dialog or group chat and copy the chat ID from the browser address bar. The optional token helper opens the third-party VKHost website; review its address and requested permissions carefully.

## Support the author

MediaTidy was created by **Igor Burletov**. Donations are optional:

- BTC: `115M2wYM1UJ5RDQi2BBsc41R3XaTNxyLjp`
- ETH (ERC-20): `0xc3fc88bb6b415a1822b7989df2865795f5351c97`
- USDT (TRC-20): `TH2UKCXXfdPYxsgxcp1BtPijkEDZWAo3Km`
- USDT (BEP-20): `0xc3fc88bb6b415a1822b7989df2865795f5351c97`

Crypto transfers are irreversible. Verify the asset, network, and address before sending.

## License

The source code is licensed under the [MIT License](LICENSE). See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for third-party components and models.
