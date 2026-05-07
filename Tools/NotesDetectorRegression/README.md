# Notes Detector Regression

Runs deterministic offline note-detector checks without Unity.

## Run

```powershell
dotnet run --project Tools/NotesDetectorRegression/NotesDetectorRegression.csproj
```

The tool:

1. loads `fixture_library.json`
2. generates WAV fixtures into `Generated/<timestamp>/audio/`
3. calls the native detector debug export directly
4. writes detector JSON results into `Generated/<timestamp>/results/`
5. writes a session summary file

It exits non-zero if any detector result does not match the expected accept/reject outcome.
