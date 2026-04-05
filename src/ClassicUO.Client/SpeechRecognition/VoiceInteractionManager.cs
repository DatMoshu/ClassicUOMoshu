// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.IO;
using System.Threading.Tasks;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.SpeechRecognition.Commands;
using ClassicUO.SpeechRecognition.Engines;
using ClassicUO.SpeechRecognition.Inference;
using ClassicUO.SpeechRecognition.Interfaces;

namespace ClassicUO.SpeechRecognition
{
    /// <summary>
    /// Top-level voice interaction orchestrator.
    /// Layered architecture: AudioPipeline → ISttEngine → routing → game actions.
    ///
    /// Two routing paths (toggle via settings.InferenceModeEnabled):
    ///   OFF: CommandRouter — keyword/macro pipeline (fast, deterministic)
    ///   ON:  ActionInferenceEngine — ranked scoring + HUD overlay
    ///
    /// Engine is swappable: Vosk (streaming) or Whisper+VAD (batch).
    /// </summary>
    internal sealed class VoiceInteractionManager : IDisposable
    {
        private AudioPipeline _audioPipeline;
        private ISttEngine _sttEngine;
        private LLMClient _llmClient;
        private SpeechAvatarManager _avatarManager;
        private SpeechCommandsManager _commandsManager;
        private CommandRouter _commandRouter;
        private TtsPlaybackManager _ttsPlayback;
        private GenerativeSpeech _generativeSpeech;
        private BargeInController _bargeIn;
        private World _world;
        private SpeechRecognitionStatusGump _statusGump;
        private bool _speechDebug = true;
        private bool _disposed;
        private bool _started;

        /// <summary>
        /// The active inference engine when InferenceMode is enabled.
        /// Exposed so GameSceneInputHandler can forward [2]/[3]/[Esc] key events.
        /// </summary>
        public ActionInferenceEngine InferenceEngine { get; private set; }

        private static readonly Random _random = new Random();

        // ── Initialization ────────────────────────────────────────────────────

        public void Initialize(World world)
        {
            _world = world;

            var settings = Settings.GlobalSettings;

            // ── Settings dump ─────────────────────────────────────────────────
            Console.WriteLine("[Voice:Init] ── Settings dump ───────────────────────────────");
            Console.WriteLine($"[Voice:Init]  SpeechRecognitionEnabled : {settings.SpeechRecognitionEnabled}");
            Console.WriteLine($"[Voice:Init]  SttEngine                : {settings.SttEngine}");
            Console.WriteLine($"[Voice:Init]  VoskModelDirectory       : {settings.VoskModelDirectory}");
            Console.WriteLine($"[Voice:Init]  VoskSampleRate           : {settings.VoskSampleRate}");
            Console.WriteLine($"[Voice:Init]  ConfidenceThreshold      : {settings.ConfidenceThreshold}");
            Console.WriteLine($"[Voice:Init]  MicDevice                : {settings.MicDevice}");
            Console.WriteLine($"[Voice:Init]  MicCaptureRate           : {settings.MicCaptureRate}");
            Console.WriteLine($"[Voice:Init]  MicCaptureChannels       : {settings.MicCaptureChannels}");
            Console.WriteLine($"[Voice:Init]  MicAlwaysOn              : {settings.MicAlwaysOn}");
            Console.WriteLine($"[Voice:Init]  InferenceModeEnabled     : {settings.InferenceModeEnabled}");
            Console.WriteLine($"[Voice:Init]  InferenceBackend         : {settings.InferenceBackend}");
            Console.WriteLine($"[Voice:Init]  LlmBaseUrl               : {settings.LlmBaseUrl}");
            Console.WriteLine($"[Voice:Init]  LlmModel                 : {settings.LlmModel}");
            Console.WriteLine($"[Voice:Init]  TtsEnabled               : {settings.TtsEnabled}");
            Console.WriteLine("[Voice:Init] ─────────────────────────────────────────────────");

            if (!settings.SpeechRecognitionEnabled)
            {
                Console.WriteLine("[Voice:Init] SpeechRecognitionEnabled=false — aborting init.");
                return;
            }

            _llmClient = new LLMClient(settings.LlmBaseUrl, settings.LlmModel, settings.LlmMaxHistory);

            var (sttEngine, vadEngine) = CreateEngines(settings);
            _sttEngine = sttEngine;
            Console.WriteLine($"[Voice:Init] Engine created: {sttEngine.GetType().Name}  VAD: {(vadEngine != null ? vadEngine.GetType().Name : "none")}");

            try
            {
                string modelPath = settings.SttEngine == "whisper"
                    ? ModelManager.WhisperModelPath
                    : settings.VoskModelDirectory;

                Console.WriteLine($"[Voice:Init] Loading STT model from: {modelPath}");
                _sttEngine.LoadModelAsync(modelPath, settings.VoskSampleRate).GetAwaiter().GetResult();
                Console.WriteLine($"[Voice:Init] STT model loaded OK. IsLoaded={_sttEngine.IsLoaded}");

                if (vadEngine != null && !vadEngine.IsLoaded)
                    vadEngine.LoadModelAsync(ModelManager.SileroVadPath).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Voice:Init] STT model load FAILED: {ex}");
                _world.Journal.Add($"[Voice] STT model load failed: {ex.Message}",
                    0x03B2, "System", null, TextType.SYSTEM, true, MessageType.Regular);
                return;
            }

            if (settings.TtsEnabled)
                InitTts(settings);

            _audioPipeline = new AudioPipeline(_sttEngine, vadEngine);
            _audioPipeline.Initialize(settings.VoskSampleRate, settings.MicDevice, settings.MicCaptureRate, settings.MicCaptureChannels);
            Console.WriteLine($"[Voice:Init] AudioPipeline initialized. Capture={settings.MicCaptureRate}Hz/{settings.MicCaptureChannels}ch → STT={settings.VoskSampleRate}Hz mono, Device={settings.MicDevice}");
            if (_bargeIn != null)
                _audioPipeline.SetBargeInController(_bargeIn);
            _audioPipeline.PartialResultAvailable += OnPartialResult;
            _audioPipeline.FinalResultAvailable   += OnFinalResult;
            Console.WriteLine("[Voice:Init] PartialResultAvailable + FinalResultAvailable subscribed.");

            // Command pipeline
            _avatarManager  = new SpeechAvatarManager(_world, _llmClient, _generativeSpeech);
            _commandsManager = new SpeechCommandsManager();
            var uowwMap     = new UowwCommandMap(_world);
            _commandRouter  = new CommandRouter(_world, _commandsManager, _avatarManager, uowwMap);

            // Inference engine (always built; activated only when InferenceModeEnabled)
            var fullRegistry = CommandRegistry.Build();
            var registry = CommandRegistry.FilterByMode(fullRegistry, settings.VoiceCommandMode);
            InferenceEngine = new ActionInferenceEngine(_world);
            InferenceEngine.Initialize(registry);

            // Status widget — shown whenever inference mode is active
            if (settings.InferenceModeEnabled)
            {
                _statusGump = new SpeechRecognitionStatusGump(_world);
                Client.Game.EnqueueAction(0, () => UIManager.Add(_statusGump));
                InferenceEngine.StateChanged += (state, label) => _statusGump.SetState(state, label);
            }

            _world.CommandManager.Register("llm", HandleLlmCommand);
            _world.CommandManager.Register("voicesettings", HandleVoiceSettingsCommand);

            string engineLabel = settings.SttEngine == "whisper" ? "Whisper" : "Vosk";
            bool useVad = vadEngine != null;
            string inferenceLabel = settings.InferenceModeEnabled
                ? $" | Inference: {settings.InferenceBackend}" : string.Empty;

            _world.Journal.Add(
                $"[Voice] Ready — STT: {engineLabel}{(useVad ? "+VAD" : "")}  TTS: {(settings.TtsEnabled ? settings.TtsEngine : "off")}{inferenceLabel}",
                0x03B2, "System", null, TextType.SYSTEM, true, MessageType.Regular);

            if (settings.MicAlwaysOn)
            {
                _audioPipeline.Start();
                _started = true;
                _world.Journal.Add("[Voice] Mic always-on: listening.",
                    0x03B2, "System", null, TextType.SYSTEM, true, MessageType.Regular);
            }
        }

        private void InitTts(Settings settings)
        {
            try
            {
                ITtsEngine ttsEngine = null;

                if (settings.TtsEngine == "kokoro" && File.Exists(ModelManager.KokoroModelPath))
                {
                    var kokoro = new KokoroTtsEngine(settings.TtsSpeed);
                    kokoro.LoadModelAsync(ModelManager.KokoroModelPath).GetAwaiter().GetResult();

                    string voicePath = ModelManager.KokoroVoicePath(settings.TtsVoiceId);
                    if (File.Exists(voicePath))
                        kokoro.LoadVoiceAsync(voicePath).GetAwaiter().GetResult();

                    ttsEngine = kokoro;
                }

                if (ttsEngine != null)
                {
                    _ttsPlayback      = new TtsPlaybackManager(ttsEngine, settings.TtsVolume);
                    _generativeSpeech = new GenerativeSpeech(_llmClient, _ttsPlayback);

                    if (settings.BargeInEnabled)
                    {
                        IVadEngine bargeVad = File.Exists(ModelManager.SileroVadPath)
                            ? (IVadEngine)new SileroVadEngine(0.5f)
                            : new EnergyVadFallback();

                        if (!bargeVad.IsLoaded && File.Exists(ModelManager.SileroVadPath))
                            bargeVad.LoadModelAsync(ModelManager.SileroVadPath).GetAwaiter().GetResult();

                        _bargeIn = new BargeInController(bargeVad, _ttsPlayback, _generativeSpeech);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Voice] TTS init failed: {ex.Message}");
            }
        }

        private (ISttEngine stt, IVadEngine vad) CreateEngines(Settings settings)
        {
            if (settings.SttEngine == "whisper" && File.Exists(ModelManager.WhisperModelPath))
            {
                var whisper = new WhisperSttEngine();
                IVadEngine vad = LoadVad(settings);
                return (whisper, vad);
            }
            return (new VoskSttEngine(), null);
        }

        private static IVadEngine LoadVad(Settings settings)
        {
            if (File.Exists(ModelManager.SileroVadPath))
                return new SileroVadEngine(settings.VadThreshold, settings.VadMinSpeechMs, settings.VadSilenceMs);

            Console.WriteLine("[Voice] Silero VAD not found — using energy fallback.");
            return new EnergyVadFallback();
        }

        public void Start()
        {
            if (_started) return;
            _started = true;
            _audioPipeline?.Start();
        }

        public void Stop()  => _audioPipeline?.Stop();

        // ── Result handlers (NAudio callback thread) ──────────────────────────

        private void OnFinalResult(object sender, SttResult result)
        {
            Console.WriteLine($"[Voice:Final] RAW text='{result.Text}' conf={result.Confidence:F2}");
            if (string.IsNullOrWhiteSpace(result.Text)) { Console.WriteLine("[Voice:Final] Dropped — empty text."); return; }
            string text = StripVoskLeadingThe(result.Text);
            if (string.IsNullOrWhiteSpace(text)) { Console.WriteLine("[Voice:Final] Dropped — stripped to empty (was 'the')."); return; }
            Console.WriteLine($"[Voice:Final] Routing '{text}'  InferenceMode={Settings.GlobalSettings.InferenceModeEnabled}  SpeechEnabled={Settings.GlobalSettings.SpeechRecognitionEnabled}");

            if (Settings.GlobalSettings.InferenceModeEnabled)
                InferenceEngine.HandleFinalResult(text);
            else
                _commandRouter?.RouteFullResult(text);
        }

        private void OnPartialResult(object sender, SttResult result)
        {
            Console.WriteLine($"[Voice:Partial] RAW text='{result.Text}' conf={result.Confidence:F2}");
            if (string.IsNullOrWhiteSpace(result.Text)) { Console.WriteLine("[Voice:Partial] Dropped — empty text."); return; }
            string text = StripVoskLeadingThe(result.Text);
            if (string.IsNullOrWhiteSpace(text)) { Console.WriteLine("[Voice:Partial] Dropped — stripped to empty (was 'the')."); return; }
            Console.WriteLine($"[Voice:Partial] Routing '{text}'  InferenceMode={Settings.GlobalSettings.InferenceModeEnabled}  SpeechEnabled={Settings.GlobalSettings.SpeechRecognitionEnabled}");

            if (Settings.GlobalSettings.InferenceModeEnabled)
                InferenceEngine.HandlePartialResult(text);
            else
                _commandRouter?.RoutePartialResult(text, result.Confidence);
        }

        /// <summary>
        /// Strips the spurious "the" artifact that Vosk's lgraph model frequently produces.
        /// Handles two cases:
        ///   1. "the " prefix  — strip it and return the rest
        ///   2. entire text is exactly "the" — return empty (treated as no result)
        /// </summary>
        private static string StripVoskLeadingThe(string text)
        {
            // Entire result is just "the" — Vosk noise artifact, discard
            if (string.Equals(text, "the", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            const string prefix = "the ";
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return text.Substring(prefix.Length).TrimStart();
            return text;
        }

        // ── Command handlers ──────────────────────────────────────────────────

        private async void HandleLlmCommand(string[] args)
        {
            if (args.Length == 0) { PlayerSay("Usage: [llm <prompt>"); return; }
            string prompt = string.Join(" ", args);
            try
            {
                string response = await _llmClient.CallLlamaAsync(prompt);
                PlayerSay(response);
            }
            catch (Exception ex) { PlayerSay($"[LLM] Error: {ex.Message}"); }
        }

        private void HandleVoiceSettingsCommand(string[] args)
        {
            PlayerSay("[Voice] Settings gump coming in Phase 3.");
        }

        // ── Utilities ─────────────────────────────────────────────────────────

        private void PlayerSay(string message)
        {
            if (_world?.Player != null) GameActions.Say(message);
        }

        public void SendMessageToJournal(string message)
        {
            _world?.Journal?.Add(message, 0x03B2, "System", null, TextType.SYSTEM, true, MessageType.Regular);
        }

        public static bool IsHostile(Mobile mobile)
            => mobile.NotorietyFlag == NotorietyFlag.Criminal
            || mobile.NotorietyFlag == NotorietyFlag.Enemy;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            InferenceEngine?.Dispose();
            _statusGump?.Dispose();
            _audioPipeline?.Dispose();
            _ttsPlayback?.Dispose();
        }
    }
}
