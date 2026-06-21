# Privacy

Media Extractor for VK is designed as a local-first desktop application.

## Local processing

Photo and video scanning, duplicate detection, OCR, EXIF analysis, recognition, reports, quarantine journals, settings, and import checkpoints are processed and stored on the user's computer.

Media Extractor for VK has no developer-operated backend, analytics service, advertising SDK, or telemetry.

## Network access

Network access occurs only when the user starts one of these actions:

- VK import: requests are sent to the official VK API and media hosts returned by VK;
- optional recognition model download: files are downloaded from Hugging Face;
- token helper: the browser opens the third-party VKHost website after a warning and user confirmation.

The VK token is held in memory for the import operation and is not saved by Media Extractor for VK. Users should review token permissions, protect the token as a password, and revoke it when no longer needed.

## Local files

Application settings and downloaded models are stored under `%LOCALAPPDATA%\MediaExtractorForVK`. Quarantine journals, category-operation journals, and VK import checkpoints are stored in or near user-selected folders.

## Contact

Do not post tokens, private media, personal paths, or other sensitive data in public GitHub issues.
