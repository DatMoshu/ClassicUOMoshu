// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClassicUO.Configuration.Json;
using Microsoft.Xna.Framework;

namespace ClassicUO.Configuration
{
    [JsonSourceGenerationOptions(WriteIndented = true, GenerationMode = JsonSourceGenerationMode.Metadata)]
    [JsonSerializable(typeof(Settings), GenerationMode = JsonSourceGenerationMode.Metadata)]
    sealed partial class SettingsJsonContext : JsonSerializerContext
    {
        // horrible fix: https://github.com/ClassicUO/ClassicUO/issues/1663
        public static SettingsJsonContext RealDefault { get; } = new SettingsJsonContext(
            new JsonSerializerOptions()
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
    }

    internal sealed class Settings
    {
        public const string SETTINGS_FILENAME = "settings.json";
        public static Settings GlobalSettings = new Settings();
        public static string CustomSettingsFilepath = null;


        [JsonPropertyName("username")] public string Username { get; set; } = string.Empty;

        [JsonPropertyName("password")] public string Password { get; set; } = string.Empty;

        [JsonPropertyName("ip")] public string IP { get; set; } = "127.0.0.1";

        [JsonPropertyName("port"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] public ushort Port { get; set; } = 2593;

        /**
         * Ignores the login servers relay packet, connects back with the settings IP
         */
        [JsonPropertyName("ignore_relay_ip")] public bool IgnoreRelayIp { get; set; } = false;

        [JsonPropertyName("ultimaonlinedirectory")] public string UltimaOnlineDirectory { get; set; } = "";

        [JsonPropertyName("profilespath")] public string ProfilesPath { get; set; } = string.Empty;

        [JsonPropertyName("clientversion")] public string ClientVersion { get; set; } = string.Empty;

        [JsonPropertyName("lang")] public string Language { get; set; } = "";

        [JsonPropertyName("lastservernum")] public ushort LastServerNum { get; set; } = 1;

        [JsonPropertyName("last_server_name")] public string LastServerName { get; set; } = string.Empty;

        [JsonPropertyName("fps")] public int FPS { get; set; } = 60;

        [JsonPropertyName("screen_scale")] public float ScreenScale { get; set; } = 1f;

        [JsonConverter(typeof(NullablePoint2Converter))] [JsonPropertyName("window_position")] public Point? WindowPosition { get; set; }
        [JsonConverter(typeof(NullablePoint2Converter))] [JsonPropertyName("window_size")] public Point? WindowSize { get; set; }

        [JsonPropertyName("is_win_maximized")] public bool IsWindowMaximized { get; set; } = true;

        [JsonPropertyName("saveaccount")] public bool SaveAccount { get; set; }

        [JsonPropertyName("autologin")] public bool AutoLogin { get; set; }

        [JsonPropertyName("reconnect")] public bool Reconnect { get; set; }

        [JsonPropertyName("reconnect_time")] public int ReconnectTime { get; set; } = 1;

        [JsonPropertyName("login_music")] public bool LoginMusic { get; set; } = true;

        [JsonPropertyName("login_music_volume")] public int LoginMusicVolume { get; set; } = 70;

        [JsonPropertyName("fixed_time_step")] public bool FixedTimeStep { get; set; } = true;

        [JsonPropertyName("run_mouse_in_separate_thread")]
        public bool RunMouseInASeparateThread { get; set; } = true;

        [JsonPropertyName("force_driver")] public byte ForceDriver { get; set; }

        [JsonPropertyName("use_verdata")] public bool UseVerdata { get; set; }

        [JsonPropertyName("maps_layouts")] public string MapsLayouts { get; set; }

        [JsonPropertyName("encryption")] public byte Encryption { get; set; }

        [JsonPropertyName("plugins")] public string[] Plugins { get; set; } = { @"./Assistant/Razor.dll" };
        
        [JsonPropertyName("files_override")] public string OverrideFile { get; set; }

        // ── Voice / STT settings ────────────────────────────────────────────
        [JsonPropertyName("enable_speech_recognition")] public bool SpeechRecognitionEnabled { get; set; } = true;
        [JsonPropertyName("stt_engine")] public string SttEngine { get; set; } = "vosk";
        [JsonPropertyName("vosk_model")] public string VoskModelDirectory { get; set; } = "D:\\_repos2026\\UltimaOnlineWorldWar\\tools\\vosk-model-en-us-0.22-lgraph";
        [JsonPropertyName("vosk_sample_rate")] public int VoskSampleRate { get; set; } = 16000;
        [JsonPropertyName("confidence_threshold")] public float ConfidenceThreshold { get; set; } = 0.7f;

        // ── TTS settings ────────────────────────────────────────────────────
        [JsonPropertyName("tts_enabled")] public bool TtsEnabled { get; set; }
        [JsonPropertyName("tts_engine")] public string TtsEngine { get; set; } = "kokoro";
        [JsonPropertyName("tts_voice_id")] public string TtsVoiceId { get; set; } = "af_heart";
        [JsonPropertyName("tts_volume")] public float TtsVolume { get; set; } = 0.8f;
        [JsonPropertyName("tts_speed")] public float TtsSpeed { get; set; } = 1.0f;

        // ── VAD settings ────────────────────────────────────────────────────
        [JsonPropertyName("vad_threshold")] public float VadThreshold { get; set; } = 0.5f;
        [JsonPropertyName("vad_min_speech_ms")] public int VadMinSpeechMs { get; set; } = 250;
        [JsonPropertyName("vad_silence_ms")] public int VadSilenceMs { get; set; } = 700;

        // ── LLM / Avatar settings ───────────────────────────────────────────
        [JsonPropertyName("llm_base_url")] public string LlmBaseUrl { get; set; } = "http://localhost:11434";
        [JsonPropertyName("llm_model")] public string LlmModel { get; set; } = "gemma4:e2b";
        [JsonPropertyName("llm_max_history")] public int LlmMaxHistory { get; set; } = 20;

        // ── Barge-in / NLP ──────────────────────────────────────────────────
        [JsonPropertyName("barge_in_enabled")] public bool BargeInEnabled { get; set; }
        [JsonPropertyName("nlp_intent_enabled")] public bool NlpIntentEnabled { get; set; } = true;
        [JsonPropertyName("fuzzy_match_threshold")] public float FuzzyMatchThreshold { get; set; } = 0.85f;
        [JsonPropertyName("voice_activation_mode")] public string VoiceActivationMode { get; set; } = "vad";
        [JsonPropertyName("ptt_key")] public string PttKey { get; set; } = "None";
        [JsonPropertyName("mic_device")] public int MicDevice { get; set; } = 0;
        [JsonPropertyName("mic_capture_rate")] public int MicCaptureRate { get; set; } = 48000;
        [JsonPropertyName("mic_capture_channels")] public int MicCaptureChannels { get; set; } = 2;

        // ── Model paths (override defaults; empty = use Models/Voice/ defaults) ─
        [JsonPropertyName("whisper_model_path")] public string WhisperModelPath { get; set; } = string.Empty;
        [JsonPropertyName("vad_model_path")] public string VadModelPath { get; set; } = string.Empty;
        [JsonPropertyName("tts_model_path")] public string TtsModelPath { get; set; } = string.Empty;

        // ── Activation mode ──────────────────────────────────────────────────
        /// <summary>When true, mic is always live (no PTT required). Useful for client 1 in local testing.</summary>
        [JsonPropertyName("mic_always_on")] public bool MicAlwaysOn { get; set; } = true;

        // ── Action Inference settings ─────────────────────────────────────────
        /// <summary>When true, voice transcripts are routed through the ActionInferenceEngine instead of CommandRouter.</summary>
        [JsonPropertyName("inference_mode_enabled")] public bool InferenceModeEnabled { get; set; } = true;
        /// <summary>"token" (built-in TokenScorer, zero latency) or "llm" (Ollama, higher accuracy).</summary>
        [JsonPropertyName("inference_backend")] public string InferenceBackend { get; set; } = "llm";
        /// <summary>Milliseconds before the top inferred action auto-executes. Range: 500–5000.</summary>
        [JsonPropertyName("inference_auto_execute_ms")] public int InferenceAutoExecuteMs { get; set; } = 1500;

        // ── Safe word recall ──────────────────────────────────────────────────
        /// <summary>
        /// A single distinct word/phrase that triggers instant emergency recall to a preset rune.
        /// Leave empty to disable. Must not match confirm/cancel phrases.
        /// Example: "hearthstone"
        /// </summary>
        [JsonPropertyName("recall_safe_word")] public string RecallSafeWord { get; set; } = "shit fuck";
        /// <summary>UO object serial of the rune or runebook to auto-target when the safe word fires.</summary>
        [JsonPropertyName("recall_rune_serial")] public uint RecallRuneSerial { get; set; } = 0;

        public static string GetSettingsFilepath()
        {
            if (CustomSettingsFilepath != null)
            {
                if (Path.IsPathRooted(CustomSettingsFilepath))
                {
                    return CustomSettingsFilepath;
                }

                return Path.Combine(CUOEnviroment.ExecutablePath, CustomSettingsFilepath);
            }

            return Path.Combine(CUOEnviroment.ExecutablePath, SETTINGS_FILENAME);
        }


        public void Save()
        {
            // Make a copy of the settings object that we will use in the saving process
            var json = JsonSerializer.Serialize(this, SettingsJsonContext.RealDefault.Settings);
            var settingsToSave = JsonSerializer.Deserialize(json, SettingsJsonContext.RealDefault.Settings);

            // Make sure we don't save username and password if `saveaccount` flag is not set
            // NOTE: Even if we pass username and password via command-line arguments they won't be saved
            if (!settingsToSave.SaveAccount)
            {
                settingsToSave.Username = string.Empty;
                settingsToSave.Password = string.Empty;
            }

            settingsToSave.ProfilesPath = string.Empty;

            // NOTE: We can do any other settings clean-ups here before we save them

            ConfigurationResolver.Save(settingsToSave, GetSettingsFilepath(), SettingsJsonContext.RealDefault.Settings);
        }
    }
}