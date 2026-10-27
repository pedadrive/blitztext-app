# Spec: Windows + Android mit offenem, konfigurierbarem LLM-Standard

Stand: 2026-10-27 · Status: **abgestimmt** — Empfehlungen aus §10 vom Nutzer am 2026-10-27 übernommen (siehe §11)

## 1. Ziel

Blitztext (Hotkey/Taste → sprechen → transkribieren → optional umschreiben → einfügen) soll
auf **Windows** und einem **Android-Gerät** laufen. Transkription und Umschreiben gehen über
**einen offenen, in der App konfigurierbaren Anbieter-Standard**, damit sie wahlweise

- **lokal auf dem Gerät**,
- auf einem **eigenen Heimserver** (erreichbar über ein privates Overlay-Netz wie Tailscale) oder
- bei einem **Cloud-Anbieter**

laufen. Leitlinie: *bestenfalls alles lokal*; Cloud ist optional, nie Voraussetzung.

## 2. Nicht-Ziele

- Kein eigener gehosteter Blitztext-Backend-Dienst, kein Konto-System.
- Keine iOS-Version; die macOS-App bleibt unverändert (Upstream-Stand), wird aber nicht weiter
  auf diesen Standard umgebaut.
- Keine Veröffentlichung im Store unter dem Namen „Blitztext" (siehe `TRADEMARKS.md`: eine
  veröffentlichte eigene App braucht eigenen Namen und eigenes Icon).
- Keine offenen Ports am Heimserver ins Internet; keine Telemetrie.
- Kein Training/Fine-Tuning von Modellen.

## 3. Architektur

```
            ┌───────────────────────── Blitztext.Core (net10.0, plattformfrei) ─────────────────────────┐
            │ Workflows · AppCoordinator · Settings · LlmPromptBuilder · TranscriptionQualityService    │
            │ ProviderProfile ── OpenAiCompatibleTranscriptionClient ── OpenAiCompatibleChatClient      │
            └───────────▲───────────────────────────────▲───────────────────────────────▲───────────────┘
                        │ Interfaces                     │                               │
      Windows-Shell (WPF): Tray, Hotkeys,       Android-Shell (s. §6)            Tests (xUnit, Linux):
      NAudio, SendInput, Credential Manager,    Mikrofon, Einfügen, Keystore     Fakes + lokaler Mock-Server
      lokale Engine (Whisper.net / sherpa-onnx)
```

- **`Blitztext.Core` bleibt plattformfrei** (keine Windows-/Android-Abhängigkeit). Alles
  Plattformspezifische bleibt hinter den bestehenden Interfaces (`IAudioRecorder`,
  `ICredentialStore`, `IPasteService`, `IHotkeyService`, `IRemoteTranscriptionService`,
  `ILocalTranscriptionService`, `ILlmService`).
- Die heutigen Klassen `OpenAiTranscriptionService` / `OpenAiLlmService` haben fest verdrahtete
  URLs (`https://api.openai.com/v1/...`) und das Modell `whisper-1`. Sie werden durch
  **OpenAI-kompatible Clients mit konfigurierbarer Basis-URL** ersetzt (Migration, kein
  Parallelbetrieb).

## 4. Der offene Anbieter-Standard

### 4.1 API-Muster

Wir nutzen das de-facto-Standardmuster der OpenAI-API, das viele lokale und selbst gehostete
Server sprechen (u. a. whisper.cpp-Server, faster-whisper-/Speaches-Server, llama.cpp-Server,
Ollama, vLLM, LocalAI, LM Studio):

| Zweck | Endpunkt | Format |
|---|---|---|
| Transkription | `POST {baseUrl}/v1/audio/transcriptions` | `multipart/form-data`: `file`, `model`, optional `language`, `prompt`; **`response_format=json`**, Ergebnisfeld `text` |
| Umschreiben | `POST {baseUrl}/v1/chat/completions` | JSON: `model`, `messages`, optional `temperature`; Ergebnis `choices[0].message.content` |
| Erreichbarkeit/Modelle (optional) | `GET {baseUrl}/v1/models` | für „Verbindung testen" und Modellauswahl |

Bewusst **nur** diese Teilmenge; keine anbieterspezifischen Erweiterungen im Core.
`response_format=json` statt `text`/`verbose_json`, weil das die am breitesten unterstützte
Variante ist und die Abhängigkeit vom Modell `whisper-1` löst (dessen Abschaltung bei OpenAI
laut Sekundärquellen für 2027 angekündigt ist — vor Umsetzung prüfen).

### 4.2 Profile

Ein **Profil** = `{ name, baseUrl, transcriptionModel, chatModel, apiKeyRef, timeouts }`.
Transkription und Umschreiben wählen **je ein Profil unabhängig** (z. B. Transkription lokal,
Umschreiben auf dem Heimserver).

Vorbelegte Profile:

| Profil | Basis-URL (Beispiel) | Schlüssel | Hinweis |
|---|---|---|---|
| **Lokal (Gerät)** | — (In-Process-Engine, kein HTTP) | — | Standard für Transkription |
| **Heimserver** | `http://<tailnet-hostname>:<port>` | optional (Bearer) | vom Nutzer einzutragen |
| **Cloud (OpenAI)** | `https://api.openai.com` | Pflicht | optional, opt-in |

- Schlüssel liegen **nie** in `settings.json`, sondern im Plattform-Speicher
  (Windows Credential Manager, Android Keystore); `settings.json` hält nur `apiKeyRef`.
- `http://` wird nur für Adressen im privaten Overlay-Netz bzw. `localhost` zugelassen
  (Warnhinweis sonst); öffentliche Ziele erfordern `https://`.
- Migration: vorhandene Einstellungen werden beim ersten Start in ein Profil „Cloud (OpenAI)"
  überführt; vorhandener Schlüssel bleibt im Credential Manager.

## 5. Lokale Transkription unter Windows

- **Heute:** Whisper.net (ggml), Modelle unter `%APPDATA%\Blitztext\models\whisper`.
- **Zu prüfen:** **NVIDIA Parakeet** (z. B. `parakeet-tdt-0.6b-v3`, mehrsprachig inkl. Deutsch)
  über **sherpa-onnx** (C#-Bindings, ONNX Runtime, CPU-tauglich). Upstream nutzt auf macOS
  inzwischen Parakeet (local-first). Prüfkriterien: Deutsch-Wortfehlerrate gegenüber Whisper
  small/medium, Latenz auf CPU, Modellgröße, Lizenz, Paketgröße der App.
- Umsetzung als zweite `ILocalTranscriptionService`-Implementierung; Auswahl im
  `LocalModelCatalog`. Whisper.net bleibt Rückfallebene.
- Lokales Umschreiben unter Windows ist **kein** Ziel der ersten Etappe (dafür Heimserver-Profil).

## 6. Android-Optionen

| Option | Vorteile | Nachteile |
|---|---|---|
| **A. .NET MAUI (Android) mit `Blitztext.Core`** | Core-Wiederverwendung inkl. Workflows und Tests; eine Sprache (C#) | MAUI-Overhead, IME/Accessibility-Dienste nur über Android-Bindings; größere APK; Build braucht Android-Workload (im Container ggf. gesperrt) |
| **B. Natives Kotlin** | Beste Integration (IME, Keystore, Foreground-Service, Mikrofon), kleinste App; sherpa-onnx hat offizielle Android-Beispiele | Core-Logik muss in Kotlin neu geschrieben werden (Workflows, Prompts); zwei Codebasen |
| **B′. Kotlin-Shell + gemeinsame Spezifikation** | wie B, plus geteilte Prompt-/Profil-Definitionen als JSON und gemeinsame Vertragstests gegen denselben Mock-Server | Disziplin bei der Synchronhaltung nötig |

**Einfügen in andere Apps:**

| Weg | Vorteile | Nachteile |
|---|---|---|
| **Eigene Eingabemethode (IME)** | Offizieller Weg zum Text einfügen (`InputConnection.commitText`), funktioniert in fast allen Textfeldern, Mikrofon-Taste direkt in der Tastatur | Nutzer muss Tastatur wechseln; eigene Tastatur-UI (minimal reicht: Mikrofon + Wechseltaste) |
| **Bedienungshilfe (Accessibility-Service)** | Arbeitet mit jeder Tastatur; kann ins fokussierte Feld schreiben | Sehr weitreichende Berechtigung, Play-Store-Richtlinien streng, Sicherheitsrisiko, fragil |
| Zwischenablage + Benachrichtigung | trivial | Nutzer fügt selbst ein; kein „Diktat"-Gefühl |

**Empfehlung:** **B (natives Kotlin) als IME**, Transkription lokal über sherpa-onnx (Parakeet
bzw. Whisper), Umschreiben über das Heimserver-Profil. Accessibility-Service nicht verwenden.

## 7. Heimserver-Anbindung

- Erreichbarkeit **nur über ein privates Overlay-Netz** (Tailscale o. ä.); der Server lauscht
  auf der Overlay-Schnittstelle bzw. hinter `tailscale serve`, **keine Portfreigabe** am Router,
  kein `tailscale funnel`.
- **Auth:** zusätzlich zum Netz ein **Bearer-Token je Gerät** (widerrufbar), gesendet als
  `Authorization: Bearer …` — dasselbe Feld wie beim Cloud-Schlüssel, also ohne Sonderlogik.
- **TLS:** bevorzugt `https` über die vom Overlay-Netz ausgestellten Zertifikate; `http` nur
  innerhalb des Overlay-Netzes zulässig (siehe §4.2).
- Serverseite (nicht Teil dieses Repos, nur dokumentiert): ein OpenAI-kompatibler
  Transkriptions-Server und ein OpenAI-kompatibler Chat-Server (z. B. llama.cpp-Server oder
  Ollama), ggf. hinter einem kleinen Reverse-Proxy, der das Token prüft.
- Datenschutz: Audio und Text verlassen das eigene Netz bei Profil „Lokal"/„Heimserver" nicht;
  keine Speicherung von Aufnahmen/Transkripten auf dem Server (Server-Konfiguration).

## 8. Teststrategie (in der Cloud, nicht über GitHub)

Prüfweg ist der **Linux-Cloud-Container**; GitHub-CI läuft zusätzlich beim Push.

1. **Core-Unit-Tests** (`dotnet test windows/src/Blitztext.Core.Tests`) im Container.
   *Voraussetzung:* .NET-10-SDK im Container. Derzeit ist der SDK-Download über den Proxy
   gesperrt (`dot.net` → 403) → Setup-Skript der Umgebung oder Netzwerkfreigabe nötig.
2. **Vertragstests** gegen einen **lokalen Mock-Server** (z. B. kleines Python-/ASP.NET-Skript
   im Test), der `/v1/audio/transcriptions`, `/v1/chat/completions`, `/v1/models` nachbildet,
   inklusive Fehlerfällen (401, 429, Timeout, kaputtes JSON).
3. **Echte lokale Engines im Container** (optional, CPU): whisper.cpp-Server bzw. sherpa-onnx
   mit kleinem Modell und einer synthetischen Testaufnahme (TTS-erzeugt, keine echten
   Aufnahmen im Repo) → Ende-zu-Ende-Test des Clients.
4. **Android:** JVM-Unit-Tests (Gradle) im Container; Instrumentation-Tests nur, wenn ein
   Emulator verfügbar ist (vermutlich nicht → manuell auf dem Android-Gerät).
5. **Windows-Shell (WPF)** ist im Container nicht baubar → bleibt bei GitHub-CI
   (`windows-latest`) plus manuellem Test.

## 9. Etappenplan

| # | Etappe | Ergebnis |
|---|---|---|
| 0 | CI reparieren, Upstream einmergen | grüne CI auf aktuellen Runnern |
| 1 | Test-Umgebung im Container (.NET-SDK, Mock-Server) | `dotnet test` lokal im Container |
| 2 | Core: `ProviderProfile` + OpenAI-kompatible Clients, Migration, Vertragstests | Windows nutzt Profile; `whisper-1`-Abhängigkeit weg |
| 3 | Windows: Einstellungs-UI für Profile, „Verbindung testen" | Heimserver nutzbar |
| 4 | Windows: Parakeet über sherpa-onnx (Spike → Entscheidung) | lokale Engine-Wahl |
| 5 | Heimserver-Doku (Tailscale, Token, Beispiel-Compose) | reproduzierbares Setup |
| 6 | Android-Spike: Kotlin-IME mit Mikrofon, sherpa-onnx lokal, Profil-Client | Diktat auf dem Android-Gerät |
| 7 | Android: Umschreiben über Profil, Workflows angleichen | Funktionsgleichstand Kern-Workflows |
| 8 | Eigener App-Name/Icon vor jeder Veröffentlichung | markenrechtlich sauber |

## 10. Fragen an den Nutzer (je mit Empfehlung)

1. **Android-Technik:** MAUI mit Core oder natives Kotlin?
   → *Empfehlung:* natives Kotlin (IME-Integration, Größe, sherpa-onnx-Beispiele).
2. **Einfügeweg auf Android:** eigene Tastatur (IME) oder Bedienungshilfe?
   → *Empfehlung:* IME.
3. **Lokale Engine Windows:** bei Whisper.net bleiben oder Parakeet (sherpa-onnx) als Standard?
   → *Empfehlung:* Spike in Etappe 4, Entscheidung nach Deutsch-Messung; bis dahin Whisper.net.
4. **Heimserver-Software:** welcher Transkriptions- und Chat-Server?
   → *Empfehlung:* whisper.cpp- oder Speaches-Server für Transkription, llama.cpp-Server oder
   Ollama für Chat — beide OpenAI-kompatibel.
5. **Cloud-Profil behalten?** → *Empfehlung:* ja, aber nicht vorausgewählt (opt-in).
6. **Container-Testumgebung:** darf das Umgebungs-Setup das .NET-SDK (und später Android-SDK)
   installieren bzw. dürfen die Download-Hosts freigegeben werden?
   → *Empfehlung:* ja, über das Setup-Skript der Cloud-Umgebung.
7. **App-Name/Icon** für eine eigene Veröffentlichung? → *Empfehlung:* vor Etappe 6 festlegen.

## 11. Entscheidungen (Nutzer, 2026-10-27)

Alle Empfehlungen aus §10 sind übernommen:

| Nr | Entscheidung |
|---|---|
| 1 | Android als natives Kotlin |
| 2 | Einfügen über eine eigene Tastatur (IME) |
| 3 | Windows: Whisper.net bleibt bis zum Parakeet-Spike (Etappe 4), Wechsel nach Deutsch-Messung |
| 4 | Heimserver: whisper.cpp oder Speaches (Transkription), llama.cpp oder Ollama (Chat) |
| 5 | Cloud-Profil bleibt, nur auf ausdrücklichen Wunsch (opt-in) |
| 6 | Container-Testumgebung: .NET-SDK (später Android-SDK) über Setup-Hook; Host-Freigaben setzt der Nutzer in der Cloud-Umgebung |
| 7 | Eigener App-Name und Icon vor Etappe 6 (TRADEMARKS.md) |

Weitere Vorgaben des Nutzers: Zielplattformen Windows und Android (Pixel 9 Pro); offener,
in der App konfigurierbarer LLM-Standard; Transkription später auf den Heimserver
(Tailscale) auslagerbar; Tests in der Cloud statt über GitHub; möglichst alles lokal.
