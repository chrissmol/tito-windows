# Tito for Windows

A Windows desktop companion that rests as a small black capsule with two eyes and expands into a translucent dashboard on hover.

## Project status

**In development.** The goal is a complete reproduction of the interaction and functionality requested from the visual references. That goal has not yet been achieved. This is a newly written implementation with original source code and an original icon.

Visual references: [glass island](https://www.instagram.com/reel/DfmGp5ZSFKK/) and [night-owl desktop](https://www.instagram.com/reel/DfxcJk9mUEU/).

Implemented in source:

- A 128 × 34 black resting capsule with blinking eyes and gaze movement.
- Hover expansion and automatic collapse after the pointer leaves.
- A dashboard containing Claude, music and focus cards.
- Windows media-session adapter, full-bleed cover-based music background, interpolated progress and local audio playback.
- Synced lyrics loaded from a user-selected `.lrc` file through the + menu. Lyrics stay associated with the current track.
- Focus countdown with start, pause, reset and minute selection.
- Claude Code event hooks enabled per session.
- A Robo chat interface with an embedded local AI (llama.cpp + a small GGUF model downloaded on first use; Ollama optional). See [docs/MOTOR_IA.md](docs/MOTOR_IA.md).
- A tray menu with restart/update and shutdown controls.
- A tool-window style that excludes the island from Alt+Tab.
- Suppression during foreground full-screen applications, known game window classes and supported game installations, with an additional per-app hide list.

Still requiring completion or broader verification: fidelity across all reference screens, full-screen/game behavior across more applications, media-session compatibility, live Claude integration and end-to-end AI conversations. The latest executable could not be tested on the development PC because Smart App Control rejected its unsigned build.

## Run

Requires Windows with .NET Framework 4.8. Native desktop acrylic requires Windows 11 build 22621 or later; other systems use an opaque fallback.

Extract the entire distribution ZIP into a normal folder, then open **Tito.exe**. Running `Iniciar.cmd` from inside the ZIP does not extract its dependencies.

The + menu provides local music, synced lyrics, focus and new-chat actions. The capsule appears at the top of the primary screen. Hover to expand; move away to collapse. Use the tray icon to restart/update or shut down. A pinned shortcut to `Tito.exe` can stay on the taskbar while the floating window remains outside Alt+Tab.

Uso local e pessoal: dê duplo clique em **Rodar.cmd**. Ele compila o Tito neste PC (com o compilador da Microsoft já instalado no Windows) e abre o app; se não abrir, mostra um diagnóstico (Smart App Control, modo do PowerShell, bloqueios). Binários sem assinatura podem ser bloqueados pelo Smart App Control mesmo compilados localmente; veja `scripts\Diagnostico.ps1`.

## Build

On Windows, using the .NET Framework compiler already installed with the framework:

```powershell
powershell.exe -NoProfile -File .\Compilar.ps1
powershell.exe -NoProfile -File .\scripts\Test.ps1
powershell.exe -NoProfile -File .\scripts\Package.ps1 -SkipBuild
```

Builds are stored under `Versions/`. `Tito.exe` is a stable launcher that reads `current-version.txt`, enabling version replacement without overwriting a running runtime executable.

## Connections

**Windows media:** reads the currently selected system media session. Features depend on what the player exposes. The app displays connection failures instead of sample track data.

**Claude Code:** use the Claude panel to copy a command that starts a Claude session with generated hooks. Existing global settings and approval decisions are preserved. The hook stores event names, tool names and file basenames locally.

**Robo:** runs a local AI by itself. On first use it downloads the llama.cpp engine (~25 MB) and a model (1.1 or 2.1 GB) into `Dados\motor`, verified by SHA-256; chats stay on `127.0.0.1`. Ollama, if running, is offered as an alternative. Details and Smart App Control notes: [docs/MOTOR_IA.md](docs/MOTOR_IA.md).

## Privacy and uninstall

See [Privacy](PRIVACY.md). To uninstall, shut down the tray application, remove any pinned shortcut and delete the extracted folder. A per-session Claude connection stops when that Claude session ends. This app does not install an auto-start entry.

## Code signing policy

See [CODE_SIGNING_POLICY.md](CODE_SIGNING_POLICY.md). The SignPath Foundation application was submitted on 2026-11-15 and is awaiting review; no acceptance, certificate or signed release is claimed.

## License

[MIT](LICENSE).

## Visual checks

The GitHub workflow uploads an `tito-visual-previews` artifact containing offscreen WPF renders. Files named `preview-reference-*` use explicit synthetic playback/chat fixtures for visual review; they are not live service results. Native desktop acrylic is composited by Windows and is not included in these offscreen images.

### Experiência ampliada

- Mídia: seleção automática ou por aplicativo das sessões SMTC expostas pelo Windows. Os comandos são endereçados à origem exibida. Áudio de apps sem SMTC não fornece capa, título ou transporte por esta integração.
- Áudio: volume do dispositivo padrão via Core Audio e seleção de saída. A mudança de saída utiliza PolicyConfig, uma interface não documentada que pode variar entre versões do Windows; erros são exibidos ao usuário.
- Prateleira: arraste arquivos, textos e imagens. Clique para copiar ou arraste um item para outro app. Até 12 itens, somente na memória, expiram em uma hora. Arquivos são referências: mover ou excluir o original invalida a referência. Nada é enviado à rede. A área de transferência é controlada pelo Windows e pode manter o conteúdo copiado após a expiração.
- Mascote: olhar suavizado, foco em violeta, expressão de recebimento/conclusão e tempo restante no estado compacto.
- Preferências: escolha de monitor e não incomodar; este modo desativa expansão por hover e interrupções do timer, mantendo abertura por clique/bandeja.
- As animações respeitam a preferência de animação do Windows. O foco continua ao trocar de aba, e o rascunho do chat é preservado.
- A prévia no navegador demonstra origens e saídas fictícias. Ela não controla o áudio do computador. A cópia de arquivos e imagens pode ser limitada pelo navegador.

### Ondas de áudio reais

Na aba Mídia, os olhos se transformam em sete barras que mostram o histórico recente de picos da saída padrão (Core Audio / IAudioMeterInformation). Não é um espectro de frequências. A leitura ocorre a cada 50 ms em uma thread separada, e a saída padrão é verificada a cada segundo para acompanhar trocas de dispositivo. A leitura pausa quando a Tito está oculta ou não apresenta mídia. Nenhum microfone é aberto; não há captura, gravação ou transmissão de áudio. O medidor do Windows é anterior à atenuação do volume do dispositivo, portanto pode continuar indicando sinal mesmo com volume reduzido ou mudo. As barras representam a mistura da saída padrão, e não exclusivamente o aplicativo escolhido no seletor de mídia. Em indisponibilidade, ficam em repouso com explicação no tooltip. Movimento reduzido mantém o indicador estático. No navegador, a animação é demonstrativa e não mede o som do PC.

- Área de transferência: o histórico (15 itens) fica só na memória. Itens fixados (📌, até 30) são salvos em `Dados\clip-pinned.json`, no seu PC; use ✕ para remover. A busca filtra com Enter.

- Notas: várias páginas (botões 1, 2, … e +), em `Dados\notes.txt`, `notes-2.txt` etc. A busca (Enter) abre a próxima página que contém o texto. O Robo grava e lê só a página 1.

- Claude Code: o cartão do dashboard conta as sessões abertas (um arquivo por sessão em `Dados\sessions`, apagado no fim da sessão) e mostra "N aguardando você" quando alguma pede aprovação.

- Caixa de comando (Ctrl+Alt+Espaço): além de foco, nota, agenda e abrir, agora aceita `pausa 5`, `lembrar em 10 min texto`, `volume 40` e `não incomodar`.

## Laboratório

Depois de qualquer atualização, dê dois cliques em `Laboratorio.cmd`: compila, confere abrir/fechar, lembretes (perdidos, adiados, não incomodar), notas, foco e frases de lembrete, e tira fotos de cada tela, tema e olhar. O resultado fica em `Dados\laboratorio.txt`, `Dados\laboratorio-telas.png` e `Dados\laboratorio-olhos.png` (abrem sozinhas). Roda numa pasta temporária: não mexe nas suas notas nem lembretes. No GitHub o mesmo laboratório roda em cada PR (resumo na página da execução, imagens no artefato `tito-visual-previews`).
