# voxpad

**AI transcription studio for Windows 10/11 and macOS.** Record rough narration (stutters, restarts, filler words, backtracked thoughts), transcribe it, optionally clean the transcript with AI, optionally translate it to other languages, and optionally regenerate polished narration audio in the user’s voice.

`voxpad` is pivoting from a single transcriber feature into a **full AI transcription system** with modular, opt-in pipeline stages.

## Product direction (pivot)

Instead of “just transcribe,” voxpad now targets an end-to-end workflow for demo creators, educators, and teams producing narrated content:

1. **Ingest** — record mic audio or import audio/video.
2. **Transcribe** — create timestamped editable transcripts.
3. **Clean up** *(optional)* — remove filler words, false starts, and disfluencies while preserving meaning.
4. **Translate** *(optional)* — generate translated transcripts/subtitles for target languages.
5. **Re-voice** *(optional)* — synthesize polished narration in the source language or translated language.
6. **Export** — deliver TXT/MD/SRT/VTT and narration audio artifacts.

Each stage is independently toggleable so users can run simple transcription-only jobs or full multi-stage AI pipelines.

## Why this matters

- **Live demo narration is naturally imperfect.** People stumble, restart phrases, and self-correct.
- **Re-recording is expensive.** Text cleanup + voice regeneration is often faster than endless retakes.
- **Localization is increasingly required.** Teams need multilingual subtitles and voice-over variants.
- **Users need control.** Some want local-only transcription; others want optional AI cleanup, translation, and dubbing.

## Primary use cases

- Rough one-take product walkthrough → clean transcript + polished voice-over.
- Video annotation workflow with quick subtitle generation and translation.
- Internal meeting recording → searchable transcript + optional cleaned summary text.
- Multilingual content publishing with translated subtitles and optional dubbed narration.
- Podcast/tutorial post-production from one imperfect recording pass.

## Current architecture stance

- **Runtime:** .NET 8
- **Desktop UI:** Avalonia (MVVM)
- **Core speech-to-text:** local `whisper.cpp` pipeline (existing direction)
- **AI augmentation:** optional adapters for cleanup, translation, and voice generation services
- **Artifacts:** editable transcript document + subtitle/audio exports

## Media playback and timestamp editing

Pipeline Studio uses a UI-free `IMediaPlayback` contract with a LibVLC desktop backend. Timestamped transcript rows keep their original start/end times while text edits update the canonical source transcript; selecting a start timestamp seeks the loaded media.

Native runtime notes:

- `win-x64` publishes include the VideoLAN LibVLC runtime package.
- `osx-x64` publishes include the available LibVLC macOS runtime package.
- On macOS (including Apple Silicon), voxpad also discovers VLC installed at `/Applications/VLC.app`; install VLC 3.x there until universal native libraries are bundled by the packaging milestone.
- If LibVLC cannot be loaded, voxpad shows an actionable playback warning while transcript-only editing and optional pipeline stages remain usable.

Platform smoke verification:

1. Publish the target: `dotnet publish src/Voxpad.Desktop/Voxpad.Desktop.csproj -c Release -r win-x64 --self-contained true` or replace the RID with `osx-x64`/`osx-arm64`.
2. Launch the published app and choose **Open media…** in Pipeline Studio.
3. Confirm **Play/Pause** and the current-position display update.
4. Load a timestamped `TranscriptDocument` through `MainWindowViewModel.LoadTranscript`, edit a segment, and select its start timestamp; confirm playback seeks without changing the displayed start/end values.
5. With an edited segment, choose a second media file; confirm the discard prompt appears and cancelling preserves the edit.
6. Temporarily remove/rename the native LibVLC runtime (or launch without VLC on macOS) and confirm the warning appears without disabling transcript editing or post-processing.

## Install a released build

Tagged releases publish self-contained desktop packages on the [GitHub Releases](https://github.com/rwrife/voxpad/releases) page. The packages include the .NET runtime, the managed whisper runtime, and the desktop app; use **Model Manager** after launch to download and verify a local whisper model. Baseline transcription does not require a cloud account.

### Windows 10/11

1. Download `voxpad-win-x64.zip` from the release.
2. Extract the entire archive to a writable folder.
3. Run `Voxpad.Desktop.exe`.

Keep the extracted files together because native speech and playback libraries are loaded from the application directory.

### macOS

1. Download the DMG matching the Mac: `voxpad-osx-arm64.dmg` for Apple Silicon or `voxpad-osx-x64.dmg` for Intel. Matching `.app.zip` archives are also attached for users who cannot mount a DMG.
2. Open the DMG and copy `Voxpad.app` to **Applications**. For the ZIP alternative, extract it first and copy the enclosed app.
3. Until signing and notarization are added, Control-click `Voxpad.app`, choose **Open**, and confirm the launch. If macOS still blocks the app, allow it from **System Settings → Privacy & Security**. Advanced users can remove quarantine with `xattr -dr com.apple.quarantine /Applications/Voxpad.app` after verifying the downloaded release.

The Apple Silicon package discovers VLC 3.x at `/Applications/VLC.app` for media playback. Transcript editing, local transcription, and configured pipeline stages continue to work when playback is unavailable.

## Release and CI policy

Pull requests run the Release test suite before any package job. Pushes to `main` build downloadable Windows x64 and macOS arm64/x64 packages. Semantic-version tags matching `v*` publish the Windows ZIP plus macOS app ZIPs and DMGs as GitHub release assets. Packaging validates the expected executable, macOS bundle metadata, and non-empty archives before upload.

Cleanup, translation, and re-voice remain optional. A disabled or unreachable stage reports its own actionable status without replacing the source transcript or preventing transcription-only workflows.

## Pipeline options

- **Transcription only** (fastest, minimal dependencies)
- **Transcription + Cleanup**
- **Transcription + Translation**
- **Transcription + Cleanup + Translation + Re-voice** (full flow)

## Example workflow (full system)

```text
1. Open voxpad and import demo_take.mp4
2. Run transcription to produce timestamped source transcript
3. Enable Cleanup to remove disfluencies
4. Enable Translation for Spanish + Japanese subtitle tracks
5. Enable Re-voice for cleaned EN and ES outputs
6. Export:
   - captions.en.srt
   - captions.es.srt
   - captions.ja.srt
   - voiceover.en.wav
   - voiceover.es.wav
```

## Milestones (updated)

- [x] M1 — Core transcription engine (`whisper.cpp` binding + audio decode/resample)
- [x] M2 — Audio capture/import + playback foundations
- [x] M3 — Transcript editor UI with timestamp seek and editing
- [x] M4 — Model/provider manager (ASR + AI pipeline providers)
- [x] M5 — Export layer (TXT/MD/SRT/VTT + job artifacts)
- [x] M6 — AI cleanup/summarize/title stage (optional)
- [x] M7 — Translation stage for multilingual transcripts/subtitles (optional)
- [x] M8 — Voice regeneration stage for polished narration/dubbing (optional)
- [x] M9 — Packaging & CI for Windows/macOS release artifacts

## Status

✅ The initial M1–M9 modular pipeline foundation is complete. See [PLAN.md](./PLAN.md) for the shipped architecture and use the GitHub issue backlog for subsequent improvements.
