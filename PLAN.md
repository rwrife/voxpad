# voxpad — Plan

## Scope

voxpad is a **cross-platform (Windows 10/11 + macOS) AI transcription system** for creators and teams producing narrated content.

Core scope:

- Microphone recording and audio/video import.
- Timestamped transcription with editable transcript documents.
- Optional AI cleanup pass for disfluency removal and readability improvements.
- Optional translation pass for multilingual transcript/subtitle outputs.
- Optional voice regeneration pass for polished narration/dubbing outputs.
- Export to TXT, Markdown, SRT, VTT, plus generated narration artifacts.
- Model/provider management for local STT models and optional AI stages.

## Product workflow model

Each run is a configurable pipeline:

1. **Ingest** (record/import)
2. **Transcribe** (required)
3. **Cleanup** (optional)
4. **Translate** (optional, one or more target languages)
5. **Re-voice** (optional)
6. **Export**

Pipelines must remain modular so users can run transcription-only flows or full AI post-processing flows.

## Architecture / tech approach

- **Runtime:** .NET 8
- **UI:** Avalonia (MVVM)
- **Core library (`Voxpad.Core`):** UI-independent logic behind interfaces

### Core interfaces (current + target)

- `ITranscriber` → source transcript generation with timestamps.
- `ITranscriptAiService` → cleanup/summarize/title operations (existing optional stage).
- `ITranslationService` → translated transcript variants + language metadata.
- `IVoiceGenerationService` → synthesized narration from transcript text.
- `IModelStore` → model download/verify/select for STT and stage-specific providers.
- `IExporter` → transcript/subtitle/artifact exports.

### Data model direction

- Keep a canonical source transcript.
- Store derived variants (cleaned, translated) without destructive overwrite.
- Track provenance per variant (stage, provider/model, timestamp, settings snapshot).
- Support artifact manifests for generated files (captions/audio per language).

## Milestones

- [x] **M1 — Core transcription engine** (`whisper.cpp` binding + decode/resample).
- [x] **M2 — Capture/import + playback foundations**.
- [x] **M3 — Transcript editor UI** (timestamp seek/edit/progress).
- [x] **M4 — Model/provider manager** (STT + stage settings).
- [x] **M5 — Export framework** (TXT/MD/SRT/VTT and artifact bookkeeping).
- [x] **M6 — Optional cleanup stage** (AI transcript refinement).
- [x] **M7 — Optional translation stage** (multilingual transcript/subtitles).
- [x] **M8 — Optional re-voice stage** (narration generation/dubbing).
- [x] **M9 — Packaging & CI** (Windows + macOS release artifacts).

The initial milestone set is complete. Future work should preserve the modular,
local-first baseline and be tracked as focused follow-up issues.

## Non-goals (v1)

- Real-time low-latency live caption streaming.
- Full DAW/video editing feature set.
- Mobile/Linux targets (focus remains Windows + macOS).
- Mandatory cloud dependency for baseline transcription.

## Packaging target

- **Windows:** self-contained win-x64 portable ZIP + optional MSIX.
- **macOS:** `.app` + `.dmg` (arm64 primary, x64 secondary).
- **CI:** matrix builds, test gates, and downloadable release artifacts.
