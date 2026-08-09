# voxpad

**Local, offline voice-to-text transcriber for Windows 10/11 and macOS.** Record from your microphone or drop in an audio/video file and get an accurate transcript computed entirely on your machine — powered by `whisper.cpp` (tiny/base/small GGUF models). Edit the transcript inline, jump to any word by clicking it, and export to TXT, Markdown, or SRT/VTT subtitles. Optional local-AI can summarize or clean up the transcript. **Privacy-first: nothing ever leaves your device.**

## Overview

voxpad is a small desktop app that turns speech into editable, timestamped text without any cloud service or account. It bundles a native `whisper.cpp` inference backend so transcription works completely offline, on CPU, on both Windows and macOS. It is designed for quick, everyday transcription tasks — voice memos, meeting notes, interviews, lecture recordings, podcast drafts — where privacy and offline capability matter.

## Motivation

- **Cloud transcription is a privacy problem.** Uploading recordings of meetings, interviews, or personal voice memos to a third-party service is often unacceptable. voxpad keeps every byte local.
- **Offline-first.** Works on a plane, in a SCIF, or on a locked-down corporate laptop with no internet.
- **No subscription, no account.** Download a model once; transcribe forever.
- **Editable output, not a black box.** The transcript is a first-class editable document with word-level timestamps, so you can fix names, punctuation, and jargon quickly.

## Use cases

- Transcribe a **voice memo** into a text note.
- Turn a **recorded meeting** into searchable minutes with timestamps.
- Generate **SRT/VTT subtitles** for a screen recording or lecture video.
- Draft a **podcast/interview transcript** you can clean up and publish.
- Dictate a quick **email or document** by recording and copying the text.
- Batch-transcribe a **folder of audio files** overnight, fully offline.

## How to use

### Windows 10/11 quickstart

1. Download the latest `voxpad-win-x64.zip` from Releases and extract it (or install the MSIX).
2. Launch `voxpad.exe`.
3. On first run, open **Settings → Models** and download a model (start with `base` for a good speed/accuracy balance). Models are cached under `%APPDATA%\voxpad\models`.
4. Click **Record** to capture from your default microphone, or **Open File** (drag-and-drop works too) to transcribe an existing audio/video file.
5. When transcription finishes, edit the text, click any word to seek playback, then **Export** to TXT / MD / SRT / VTT.

### macOS quickstart

1. Download the latest `voxpad-macos.dmg` from Releases, open it, and drag **voxpad.app** to Applications.
2. Launch it (first launch: right-click → Open to bypass Gatekeeper for the unsigned build, or allow it in System Settings → Privacy & Security).
3. Grant **Microphone** permission when prompted (Settings → Privacy & Security → Microphone).
4. Open **Settings → Models** and download a model (cached under `~/Library/Application Support/voxpad/models`).
5. **Record** or **Open File**, edit, then **Export**.

## Example workflow

```
1. Open voxpad → Settings → Models → download "base"
2. Drag "interview.m4a" onto the window
3. voxpad decodes audio → 16 kHz mono → whisper.cpp → timestamped segments
4. Review the transcript; click a misheard name to jump audio there and fix it
5. Export → Subtitles (SRT) for the video, and Text (MD) for your notes
```

Headless / batch (CLI companion):

```
voxpad-cli transcribe ./recordings --model base --format srt --out ./transcripts
```

## Local-AI integration (optional)

Transcription itself is always local via `whisper.cpp`. On top of that, voxpad can optionally connect to a **local** LLM runtime you already run — [Ollama](https://ollama.com) or any `llama.cpp` server exposing an OpenAI-compatible endpoint — to:

- **Summarize** a long transcript into bullet minutes / action items.
- **Clean up** filler words, false starts, and punctuation.
- **Title** the transcript automatically.

Recommended tiny models: Llama 3.2 3B, Qwen2.5 3B, Phi-3-mini, or MiniCPM-class — anything your machine can run. This is **off by default**, opt-in, local-only (default `http://localhost:11434`), with a reachability probe and graceful fallback. Only the transcript text is sent, and only to your own local endpoint. No cloud, ever.

## Current status / milestones

🚧 **Early scaffolding.** This repo was just bootstrapped. Tracked work lives in the issue backlog.

- [ ] M1 — Core transcription engine (`whisper.cpp` binding + audio decode/resample)
- [ ] M2 — Audio capture (mic recording) + file import
- [ ] M3 — Desktop UI: transcript editor with word-level seek + playback
- [ ] M4 — Model manager (download/verify/select GGUF models)
- [ ] M5 — Export: TXT / MD / SRT / VTT
- [ ] M6 — Optional local-AI summarize/cleanup
- [ ] M7 — Packaging & CI (Windows zip/MSIX, macOS .app/.dmg)

See [PLAN.md](./PLAN.md) for scope, architecture, and non-goals.
