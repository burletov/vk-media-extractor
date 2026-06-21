# Contributing

Bug reports and focused pull requests are welcome.

1. Do not attach private media or access tokens.
2. Open an issue describing the behavior and expected result.
3. Keep changes scoped and follow the existing WinForms patterns.
4. Run the smoke test before submitting:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\download-model.ps1
dotnet run --project .\tests\MediaExtractorForVK.SmokeTests\MediaExtractorForVK.SmokeTests.csproj -c Release
```

Recognition changes should include measurable validation. A larger model is not sufficient evidence of better classification for the app's fixed categories.
