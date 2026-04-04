// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Threading.Tasks;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.SpeechRecognition.Commands;
using ClassicUO.SpeechRecognition.Engines;
using ClassicUO.SpeechRecognition.Gumps;
using ClassicUO.SpeechRecognition.Interfaces;
using System.IO;

namespace ClassicUO.SpeechRecognition
{
    /// <summary>
    /// Top-level voice interaction orchestrator.
    /// Replaces the monolithic SpeechRecognitionManager with a layered architecture:
    ///   AudioPipeline → ISttEngine → command routing → game actions.
    ///
    /// Phase 1: uses VoskSttEngine, same routing logic as before.
    /// Phase 2+: drop-in Whisper + VAD without changing callers.
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
        private bool _speechDebug;
        private bool _disposed;

        private static readonly Random _random = new Random();

        // ── Initialization ───────────────────────────────────────────────────

        public void Initialize(World world)
        {
            _world = world;

            var settings = Settings.GlobalSettings;

            // Build LLM client with configurable URL and model
            _llmClient = new LLMClient(settings.LlmBaseUrl, settings.LlmModel, settings.LlmMaxHistory);

            // Build STT + VAD engines based on settings
            var (sttEngine, vadEngine) = CreateEngines(settings);
            _sttEngine = sttEngine;

            // Load model synchronously
            try
            {
                string modelPath = settings.SttEngine == "whisper"
                    ? ModelManager.WhisperModelPath
                    : settings.VoskModelDirectory;

                _sttEngine.LoadModelAsync(modelPath, settings.VoskSampleRate).GetAwaiter().GetResult();

                if (vadEngine != null && !vadEngine.IsLoaded)
                    vadEngine.LoadModelAsync(ModelManager.SileroVadPath).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _world.Journal.Add($"[Voice] STT model load failed: {ex.Message}", 0x03B2, "System", null, TextType.SYSTEM, true, MessageType.Regular);
                return;
            }

            // Build TTS (and barge-in) before wiring the pipeline
            if (settings.TtsEnabled)
                InitTts(settings);

            // Wire audio pipeline with optional VAD
            _audioPipeline = new AudioPipeline(_sttEngine, vadEngine);
            _audioPipeline.Initialize(settings.VoskSampleRate);
            if (_bargeIn != null)
                _audioPipeline.SetBargeInController(_bargeIn);
            _audioPipeline.PartialResultAvailable += OnPartialResult;
            _audioPipeline.FinalResultAvailable += OnFinalResult;

            // Build managers
            _avatarManager = new SpeechAvatarManager(_world, _llmClient, _generativeSpeech);
            _commandsManager = new SpeechCommandsManager();
            var uowwMap = new UowwCommandMap(_world);
            _commandRouter = new CommandRouter(_world, _commandsManager, _avatarManager, uowwMap);

            // Register [llm command
            _world.CommandManager.Register("llm", HandleLlmCommand);
            // Register [voice settings shortcut
            _world.CommandManager.Register("voicesettings", HandleVoiceSettingsCommand);

            string engineLabel = settings.SttEngine == "whisper" ? "Whisper" : "Vosk";
            bool useVad = vadEngine != null;
            _world.Journal.Add($"[Voice] Ready — STT: {engineLabel}{(useVad ? " + VAD" : "")}  TTS: {(settings.TtsEnabled ? settings.TtsEngine : "off")}",
                0x03B2, "System", null, TextType.SYSTEM, true, MessageType.Regular);
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
                    _ttsPlayback = new TtsPlaybackManager(ttsEngine, settings.TtsVolume);
                    _generativeSpeech = new GenerativeSpeech(_llmClient, _ttsPlayback);

                    // Barge-in: create a separate lightweight VAD for interruption detection
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
                // Whisper (batch) + VAD gating
                var whisper = new WhisperSttEngine();
                IVadEngine vad = LoadVad(settings);
                return (whisper, vad);
            }

            // Default: Vosk streaming (no VAD needed — it processes every chunk)
            return (new VoskSttEngine(settings.ConfidenceThreshold), null);
        }

        private static IVadEngine LoadVad(Settings settings)
        {
            if (File.Exists(ModelManager.SileroVadPath))
            {
                return new SileroVadEngine(
                    settings.VadThreshold,
                    settings.VadMinSpeechMs,
                    settings.VadSilenceMs);
            }
            // Silero model not downloaded — fall back to energy threshold
            Console.WriteLine("[Voice] Silero VAD model not found — using energy fallback.");
            return new EnergyVadFallback();
        }

        public void Start() => _audioPipeline?.Start();

        public void Stop() => _audioPipeline?.Stop();

        // ── Result handlers (called on NAudio thread) ─────────────────────────

        private void OnFinalResult(object sender, SttResult result)
        {
            if (string.IsNullOrWhiteSpace(result.Text)) return;
            if (_speechDebug) Console.WriteLine($"[Voice] Final: {result.Text}");
            _commandRouter?.RouteFullResult(result.Text);
        }

        private void OnPartialResult(object sender, SttResult result)
        {
            if (string.IsNullOrWhiteSpace(result.Text)) return;
            if (_speechDebug) Console.WriteLine($"[Voice] Partial ({result.Confidence:F2}): {result.Text}");
            _commandRouter?.RoutePartialResult(result.Text, result.Confidence);
        }

        // ── GM / command handlers ────────────────────────────────────────────

        private void HandleLlmCommand(string[] args)
        {
            string prompt = args.Length > 1
                ? string.Join(" ", args, 1, args.Length - 1)
                : null;

            Client.Game.EnqueueAction(0, () =>
            {
                var gump = UIManager.GetGump<LlmChatGump>();
                if (gump == null || gump.IsDisposed)
                {
                    gump = new LlmChatGump(_world, async p => await _llmClient.ChatAsync(p));
                    UIManager.Add(gump);
                }

                if (!string.IsNullOrEmpty(prompt))
                    gump.AutoSubmit(prompt);
            });
        }

        private void HandleVoiceSettingsCommand(string[] args)
        {
            // Phase 3: open VoiceSettingsGump here
            PlayerSay("[Voice] Settings gump coming in Phase 3.");
        }

        // ── Utilities ────────────────────────────────────────────────────────

        private void PlayerSay(string message)
        {
            if (_world?.Player != null)
                GameActions.Say(message);
        }

        public void SendMessageToJournal(string message)
        {
            _world?.Journal?.Add(message, 0x03B2, "System", null, TextType.SYSTEM, true, MessageType.Regular);
        }

        private static string GetRandom(string[] array)
        {
            if (array == null || array.Length == 0) return string.Empty;
            return array[_random.Next(array.Length)];
        }

        public static bool IsHostile(Mobile mobile)
            => mobile.NotorietyFlag == NotorietyFlag.Criminal || mobile.NotorietyFlag == NotorietyFlag.Enemy;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _audioPipeline?.Dispose();
            _ttsPlayback?.Dispose();
        }
    }
}
