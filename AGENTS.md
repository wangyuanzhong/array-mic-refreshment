# AGENTS.md

## Cursor Cloud specific instructions

This repository is currently in a **planning/documentation phase** with no implemented code. The `main` branch contains only a `README.md` placeholder.

### Project Vision

The planned product is a Python-based Windows desktop tool for array microphone voice capture and ASR-driven AI tool interaction (see the `origin/cursor/readme-project-vision-0b88` branch README for full architectural vision).

### Current State

- No source code, dependencies, tests, or runnable services exist.
- No `requirements.txt`, `package.json`, `Makefile`, or build system is present.
- There is nothing to lint, test, build, or run.

### When Code is Added

Based on the project vision, the planned tech stack is:
- **Language:** Python (or C#)
- **Audio:** sounddevice / WASAPI / PortAudio
- **ASR:** faster-whisper, Vosk
- **Platform:** Windows 10/11

Future agents should check for `requirements.txt` or `pyproject.toml` and install dependencies accordingly once code is committed.
