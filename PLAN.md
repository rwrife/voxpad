# voxpad — Plan

## Scope

voxpad is a **cross-platform (Windows 10/11 + macOS) desktop voice-to-text transcriber** that runs fully offline using `whisper.cpp`. In scope:

- Microphone recording and audio/video file import.
- Offline speech-to-text via bundled `whisper.cpp` with selectable GGUF models (tiny/base/small).
- Editable, word/segment-timestamped transcript with click-to-seek playback.
- Export to TXT, Markdown, SRT, and VTT.
- A model manager to download/verify/select models.
- Optional, opt-in local-AI post-processing (summarize / clean up / title) via a local Ollama or llama.cpp OpenAI-compatible endpoint.
- A headless CLI companion for batch transcription.

## Architecture / tech approach

- **Runtime:** .NET 8.
- **UI:** **Avalonia (MVVM)** for a single cross-platform codebase running on both Windows and macOS. (WPF rejected — Windows-only; native per-OS UIs rejected as too costly for a small tool. This mirrors the cross-platform approach used by sibling tools fontloom/metawipe.)
- **UI-free core (`Voxpad.Core`):** all logic behind interfaces so it is unit-testable without a UI:
  - `ITranscriber` → `WhisperTranscriber` wrapping `whisper.cpp`. Binding via [Whisper.net](https://github.com/sandrohanea/whisper.net) (managed NuGet with native `whisper.cpp` runtimes for win-x64 / osx-arm64 / osx-x64) as the primary path; fall back to invoking a bundled `whisper-cli` native binary if the managed binding is unsuitable.
  - `IAudioDecoder` → decode arbitrary audio/video to 16 kHz mono PCM (FFmpeg-based via a bundled `ffmpeg` or an in-process decoder), the format whisper expects.
  - `IAudioCapture` → mic capture: `NAudio`/WASAPI on Windows, `AVFoundation`/CoreAudio on macOS behind the same interface.
  - `TranscriptDocument` → segments of `{ Text, StartMs, EndMs, Words[] }`, editable, serializable.
  - `IExporter` → TXT / MD / SRT / VTT writers.
  - `IModelStore` → list/download/verify/select GGUF models with checksums; cache location per-OS.
  - `ITranscriptAiService` → optional local LLM post-processing.
- **Persistence:** JSON settings + model cache under `%APPDATA%\voxpad` (Windows) and `~/Library/Application Support/voxpad` (macOS).
- **Local-AI:** `ITranscriptAiService` → HTTP client to a local OpenAI-compatible endpoint (default `http://localhost:11434`). Reachability probe first; graceful fallback (feature simply hidden/disabled) when no endpoint. Sends only transcript text to the user's own local endpoint. Off by default.
- **Testing:** xUnit on `Voxpad.Core` (decoder resampling, SRT/VTT timestamp formatting, transcript editing/serialization, exporter round-trips, model-store verification, AI-service fallback logic).
- **CLI:** `voxpad-cli` thin wrapper over `Voxpad.Core` for batch/headless transcription.

## Milestones

1. **M1 — Core transcription engine.** `ITranscriber`/`WhisperTranscriber` + `IAudioDecoder` (→16 kHz mono PCM). Transcribe a WAV to timestamped segments in a test.
2. **M2 — Capture & import.** `IAudioCapture` mic recording (Win/mac) + drag-drop/open file import; level meter.
3. **M3 — Transcript editor UI.** Avalonia shell: transcript view, inline editing, click-word-to-seek, audio playback with progress.
4. **M4 — Model manager.** `IModelStore`: download/verify/select GGUF models; first-run guidance; cache management.
5. **M5 — Export.** TXT / MD / SRT / VTT with correct timestamp formatting; copy-to-clipboard.
6. **M6 — Optional local-AI.** `ITranscriptAiService`: summarize / clean up / auto-title; settings UI; reachability probe + fallback; off by default.
7. **M7 — Packaging & CI.** GitHub Actions matrix building Windows (portable zip + MSIX) and macOS (.app + .dmg) artifacts.

## Non-goals

- **No cloud / online transcription services** (no Whisper API, no Google/Azure/AWS speech). Local only.
- **No accounts, telemetry, or network calls** except optional user-configured local-AI and explicit model downloads.
- **No real-time live captioning / streaming dictation** in v1 (batch/record-then-transcribe first; live mode is a possible future milestone).
- **No speaker diarization / voice ID** in v1 (candidate for later).
- **No audio editing** (trim/mix) — voxpad transcribes; it is not a DAW.
- **No mobile / Linux targets** in v1 (Windows + macOS only per current mode).

## Packaging / distribution target

- **Windows 10/11:** self-contained win-x64 build — portable `.zip` + optional MSIX installer.
- **macOS:** `.app` bundle (osx-arm64 primary, osx-x64 secondary) packaged as a `.dmg`; document unsigned-build first-launch (Gatekeeper) steps until code signing/notarization is available.
- **CI:** GitHub Actions matrix (`windows-latest` + `macos-latest`) producing downloadable Release artifacts. Native `whisper.cpp` runtimes bundled per-RID.
