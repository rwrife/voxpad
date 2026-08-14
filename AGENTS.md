# AGENTS.md — voxpad contributor/agent guide

## Mission

`voxpad` is an **AI transcription system**, not a single-feature transcriber.

Every substantial change should align to this pipeline mindset:

1. Ingest audio/video
2. Transcribe to timestamped editable text
3. Optional cleanup (disfluency removal, punctuation, readability)
4. Optional translation (multilingual transcript/subtitle outputs)
5. Optional voice regeneration (same-language polish or translated dubbing)
6. Export transcript/subtitle/audio deliverables

## Product principles

- **Modular pipeline:** each stage is independently enabled/disabled.
- **Non-destructive editing:** preserve original transcript and AI-derived variants.
- **Traceability:** users can see which stages ran and with what settings.
- **Privacy-conscious defaults:** default to local-first where practical; clearly disclose when an external provider is used.
- **Creator-first UX:** optimize for demo builders and video annotators who speak imperfectly in live takes.

## Engineering boundaries

- Keep domain logic in `Voxpad.Core` behind interfaces; desktop code should remain orchestration/UI.
- New AI capabilities should be added as explicit stage services (cleanup, translation, re-voice), not ad-hoc calls scattered through UI code.
- Prefer deterministic data contracts for stage inputs/outputs (e.g., transcript document variants, language tags, artifact manifests).
- Preserve compatibility with current export targets (TXT/MD/SRT/VTT) while extending to multilingual and audio outputs.

## Backlog and issue hygiene

When creating/updating issues:

- Frame work in terms of **pipeline stage outcomes** and user-facing artifacts.
- Include acceptance criteria that verify optional-stage behavior and fallback behavior.
- Distinguish **core engine**, **UI wiring**, and **packaging** concerns.
- For translation work, require language metadata and subtitle artifact correctness.
- For re-voice work, require clear handling of voice profile/reference input and generated output artifacts.

## Definition of done (feature-level)

A feature is not done until:

- It has tests (or explicit validation rationale if UI-only).
- It degrades gracefully when optional AI stages are disabled/unavailable.
- It preserves existing transcript-only workflow.
- It updates README/PLAN/issue references when scope changes.

## Current pivot focus

Near-term focus areas:

1. Transcription + transcript editor foundation
2. Optional cleanup stage
3. Optional translation stage
4. Optional voice regeneration stage
5. End-to-end pipeline UX with export artifacts
