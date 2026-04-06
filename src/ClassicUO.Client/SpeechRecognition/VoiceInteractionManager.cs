// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.IO;
using System.Threading.Tasks;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.SpeechRecognition.Basic;
using ClassicUO.SpeechRecognition.Commands;
using ClassicUO.SpeechRecognition.Diagnostics;
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
        private BasicVoiceProcessor _basicProcessor;
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

            // ── Initialize speech logging + settings dump ─────────────────────
            SpeechLog.Initialize();
            SpeechLog.Debug(SpeechLogChannel.Voice, "── Settings dump ───────────────────────────────");
            SpeechLog.Debug(SpeechLogChannel.Voice, $"  SpeechRecognitionEnabled : {settings.SpeechRecognitionEnabled}");
            SpeechLog.Debug(SpeechLogChannel.Voice, $"  SttEngine                : {settings.SttEngine}");
            SpeechLog.Debug(SpeechLogChannel.Voice, $"  VoskModelDirectory       : {settings.VoskModelDirectory}");
            SpeechLog.Debug(SpeechLogChannel.Voice, $"  VoskSampleRate           : {settings.VoskSampleRate}");
            SpeechLog.Debug(SpeechLogChannel.Voice, $"  ConfidenceThreshold      : {settings.ConfidenceThreshold}");
            SpeechLog.Debug(SpeechLogChannel.Voice, $"  MicDevice                : {settings.MicDevice}");
            SpeechLog.Debug(SpeechLogChannel.Voice, $"  MicCaptureRate           : {settings.MicCaptureRate}");
            SpeechLog.Debug(SpeechLogChannel.Voice, $"  MicCaptureChannels       : {settings.MicCaptureChannels}");
            SpeechLog.Debug(SpeechLogChannel.Voice, $"  MicAlwaysOn              : {settings.MicAlwaysOn}");
            SpeechLog.Debug(SpeechLogChannel.Voice, $"  InferenceModeEnabled     : {settings.InferenceModeEnabled}");
            SpeechLog.Debug(SpeechLogChannel.Voice, $"  InferenceBackend         : {settings.InferenceBackend}");
            SpeechLog.Debug(SpeechLogChannel.Voice, $"  LlmBaseUrl               : {settings.LlmBaseUrl}");
            SpeechLog.Debug(SpeechLogChannel.Voice, $"  LlmModel                 : {settings.LlmModel}");
            SpeechLog.Debug(SpeechLogChannel.Voice, $"  TtsEnabled               : {settings.TtsEnabled}");
            SpeechLog.Debug(SpeechLogChannel.Voice, $"  LlmTimeoutMs             : {settings.LlmTimeoutMs}");
            SpeechLog.Debug(SpeechLogChannel.Voice, $"  VoiceCommandMode         : {settings.VoiceCommandMode}");
            SpeechLog.Debug(SpeechLogChannel.Voice, $"  SpeechLogLevelDefault    : {settings.SpeechLogLevelDefault}");
            SpeechLog.Debug(SpeechLogChannel.Voice, $"  SpeechLogFile            : {settings.SpeechLogFile}");
            SpeechLog.Debug(SpeechLogChannel.Voice, $"  SpeechLogLevelInference  : {settings.SpeechLogLevelInference?.ToString() ?? "null (inherit)"}");
            SpeechLog.Debug(SpeechLogChannel.Voice, "─────────────────────────────────────────────────");

            if (!settings.SpeechRecognitionEnabled)
            {
                SpeechLog.Info(SpeechLogChannel.Voice, "SpeechRecognitionEnabled=false — aborting init.");
                return;
            }

            _llmClient = new LLMClient(settings.LlmBaseUrl, settings.LlmModel, settings.LlmMaxHistory);

            var (sttEngine, vadEngine) = CreateEngines(settings);
            _sttEngine = sttEngine;
            SpeechLog.Info(SpeechLogChannel.Voice, $"Engine created: {sttEngine.GetType().Name}  VAD: {(vadEngine != null ? vadEngine.GetType().Name : "none")}");

            try
            {
                string modelPath = settings.SttEngine == "whisper"
                    ? ModelManager.WhisperModelPath
                    : settings.VoskModelDirectory;

                SpeechLog.Info(SpeechLogChannel.Stt, $"Loading STT model from: {modelPath}");
                _sttEngine.LoadModelAsync(modelPath, settings.VoskSampleRate).GetAwaiter().GetResult();
                SpeechLog.Info(SpeechLogChannel.Stt, $"STT model loaded OK. IsLoaded={_sttEngine.IsLoaded}");

                if (vadEngine != null && !vadEngine.IsLoaded)
                    vadEngine.LoadModelAsync(ModelManager.SileroVadPath).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                SpeechLog.Error(SpeechLogChannel.Stt, $"STT model load FAILED: {ex}");
                _world.Journal.Add($"[Voice] STT model load failed: {ex.Message}",
                    0x03B2, "System", null, TextType.SYSTEM, true, MessageType.Regular);
                return;
            }

            if (settings.TtsEnabled)
                InitTts(settings);

            _audioPipeline = new AudioPipeline(_sttEngine, vadEngine);
            _audioPipeline.Initialize(settings.VoskSampleRate, settings.MicDevice, settings.MicCaptureRate, settings.MicCaptureChannels);
            SpeechLog.Info(SpeechLogChannel.Audio, $"AudioPipeline initialized. Capture={settings.MicCaptureRate}Hz/{settings.MicCaptureChannels}ch → STT={settings.VoskSampleRate}Hz mono, Device={settings.MicDevice}");
            if (_bargeIn != null)
                _audioPipeline.SetBargeInController(_bargeIn);
            _audioPipeline.PartialResultAvailable += OnPartialResult;
            _audioPipeline.FinalResultAvailable   += OnFinalResult;
            SpeechLog.Debug(SpeechLogChannel.Voice, "PartialResultAvailable + FinalResultAvailable subscribed.");

            // Basic mode processor - always initialized so user can switch modes at runtime
            _basicProcessor = new BasicVoiceProcessor(_world);
            _basicProcessor.Initialize();
            SpeechLog.Info(SpeechLogChannel.Voice, $"[BasicMode] Processor initialized: {_basicProcessor.TotalCommands} commands, {_basicProcessor.ShortcutCount} shortcuts");

            // Command pipeline (for simple/advanced modes)
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
            _world.CommandManager.Register("showspeech", HandleVoiceSettingsCommand);

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
                SpeechLog.Error(SpeechLogChannel.Tts, $"TTS init failed: {ex.Message}");
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

            SpeechLog.Warn(SpeechLogChannel.Voice, "Silero VAD not found — using energy fallback.");
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
            if (!Settings.GlobalSettings.SpeechRecognitionEnabled) return;

            SpeechLog.Debug(SpeechLogChannel.Voice, $"Final RAW text='{result.Text}' conf={result.Confidence:F2}");
            if (string.IsNullOrWhiteSpace(result.Text)) { SpeechLog.Trace(SpeechLogChannel.Voice, "Final dropped — empty text."); return; }
            string text = StripVoskLeadingThe(result.Text);
            if (string.IsNullOrWhiteSpace(text)) { SpeechLog.Trace(SpeechLogChannel.Voice, "Final dropped — stripped to empty (was 'the')."); return; }

            // Get current mode from settings (can be changed at runtime via gump)
            string currentMode = (Settings.GlobalSettings.VoiceCommandMode ?? "basic").ToLowerInvariant();
            SpeechLog.Debug(SpeechLogChannel.Voice, $"Final routing '{text}'  Mode={currentMode}");

            // Route based on mode - Basic mode ALWAYS takes priority for instant execution
            if (currentMode == "basic" && _basicProcessor != null)
            {
                // Basic mode: fast hash-based lookup, no HUD, instant execution
                if (_basicProcessor.TryProcess(text, out var cmdResult))
                {
                    _basicProcessor.Execute(cmdResult);
                }
            }
            else if (currentMode == "advanced" && Settings.GlobalSettings.InferenceModeEnabled)
            {
                // Advanced mode: use inference engine with HUD
                InferenceEngine.HandleFinalResult(text);
            }
            else
            {
                // Simple mode: original CommandRouter
                _commandRouter?.RouteFullResult(text);
            }
        }

        private void OnPartialResult(object sender, SttResult result)
        {
            if (!Settings.GlobalSettings.SpeechRecognitionEnabled) return;

            SpeechLog.Trace(SpeechLogChannel.Voice, $"Partial RAW text='{result.Text}' conf={result.Confidence:F2}");
            if (string.IsNullOrWhiteSpace(result.Text)) { SpeechLog.Trace(SpeechLogChannel.Voice, "Partial dropped — empty text."); return; }
            string text = StripVoskLeadingThe(result.Text);
            if (string.IsNullOrWhiteSpace(text)) { SpeechLog.Trace(SpeechLogChannel.Voice, "Partial dropped — stripped to empty (was 'the')."); return; }

            // Get current mode from settings (can be changed at runtime via gump)
            string currentMode = (Settings.GlobalSettings.VoiceCommandMode ?? "basic").ToLowerInvariant();
            SpeechLog.Trace(SpeechLogChannel.Voice, $"Partial routing '{text}'  Mode={currentMode}");

            // Route based on mode - Basic mode ALWAYS takes priority for instant execution
            if (currentMode == "basic" && _basicProcessor != null)
            {
                // Basic mode: fast hash-based lookup, no HUD, instant execution
                if (_basicProcessor.TryProcess(text, out var cmdResult))
                {
                    _basicProcessor.Execute(cmdResult);
                }
            }
            else if (currentMode == "advanced" && Settings.GlobalSettings.InferenceModeEnabled)
            {
                // Advanced mode: use inference engine with HUD
                InferenceEngine.HandlePartialResult(text);
            }
            else
            {
                // Simple mode: original CommandRouter
                _commandRouter?.RoutePartialResult(text, result.Confidence);
            }
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
            var existing = UIManager.GetGump<Gumps.SpeechSettingsGump>();

            if (existing != null)
            {
                existing.Dispose();
                return;
            }

            UIManager.Add(new Gumps.SpeechSettingsGump(_world));
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
            SpeechLog.Shutdown();
        }
    }
}
