# Privacy

Tito keeps runtime data in the `Dados` directory beside its launcher. This directory is excluded from source control and distribution packages.

- Windows media titles, artists, cover thumbnails and playback state are read locally through the Windows media-session API.
- Foreground window bounds, process names and selected game installation paths are checked locally to hide the island during games or full-screen use. A user-selected per-app hide list is stored locally.
- The mascot reads the cursor position to move its eyes. It does not record cursor history.
- Optional Claude hooks store the latest event, session identifier, tool name, project basename and affected file basename. Hook payloads containing prompts or file contents are not stored.
- Robo runs an embedded llama.cpp server on `127.0.0.1` (random port) and sends conversation text, the date and time, the focus-timer status and, unless "Enviar dados do PC à IA" is off, battery, volume, foreground app name, current media and today's agenda only to it, after a user sends a message. The only network access is the first-use download of the engine (GitHub releases) and the chosen model (Hugging Face), verified by SHA-256. If the user selects Ollama instead, text goes to `127.0.0.1:11434`; whether that model is local or cloud-backed is up to the user.
- Chat history (last 100 messages) is saved in `Dados\chat.json` so it survives restarts. Settings → Privacidade can turn this off (the file is deleted) or erase the conversation.
- Clipboard history (last 15 texts) stays in memory only and can be turned off in Settings → Privacidade. Pinned items are saved in `Dados\clip-pinned.json`.
- Notes (`notes*.txt`), agenda (`agenda.txt`), delivered reminders (`agenda-avisados.txt`), focus time (`focus.txt`, `focus-log.txt`) and preferences (`experience.json`) stay in `Dados`. Settings → Privacidade → "Abrir pasta de dados" opens it.
- A server on `127.0.0.1` only proves the program runs on this PC; that program may still forward text to a cloud model. Tito cannot verify this for external servers.
- There is no telemetry, advertising, screenshot capture or background upload in this implementation.

The manual certificate-signing utility contacts the selected timestamp provider when a maintainer explicitly signs a build. GitHub and SignPath receive build artifacts only as part of the separately configured publication/signing workflow.

This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it.
