using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Configuration;
using BepInEx.Logging;
using MessagePack;

namespace AIUnitHPBuff {
    internal class SettingsService : IDisposable {
        private readonly ConfigFile _config;
        private readonly ManualLogSource _logger;
        private readonly FileSystemWatcher _configWatcher;
        private SaveData _saveData = new();
        private int _configReloadPending;
        private bool? _lastDebugConfigOverride;
        private float _lastDebugHpMultiplier = float.NaN;

        public SettingsService(ConfigFile config, ManualLogSource logger) {
            _config = config;
            _logger = logger;
            Debug = LoadDebugConfig();
            _configWatcher = CreateConfigWatcher();
            LogDebugConfigIfChanged();
        }

        public DebugConfig Debug { get; }

        public float HpMultiplier =>
            Debug.DebugConfigOverride.Value
                ? DebugHpMultiplier
                : Constants.ClampHpMultiplier(_saveData.HpMultiplier);

        public float DebugHpMultiplier =>
            Constants.ClampHpMultiplier(Debug.HpMultiplier.Value);

        public bool IsDebugOverrideEnabled =>
            Debug.DebugConfigOverride.Value;

        public void ProcessPendingConfigReload() {
            if (Interlocked.Exchange(ref _configReloadPending, 0) == 0)
                return;

            try {
                _config.Reload();
                LogDebugConfigIfChanged();
            } catch (Exception ex) {
                _logger.LogWarning($"Failed to reload debug config: {ex.Message}");
            }
        }

        public void InitFromLobby(float lobbyHpMultiplier) {
            _saveData = new SaveData {
                HpMultiplier = Constants.ClampHpMultiplier(lobbyHpMultiplier)
            };
        }

        public void Reset() {
            _saveData = new SaveData();
        }

        public byte[] Serialize() {
            return MessagePackSerializer.Serialize(_saveData);
        }

        public void Deserialize(byte[] bytes) {
            _saveData = MessagePackSerializer.Deserialize<SaveData>(bytes);
        }

        public float SavedHpMultiplier => Constants.ClampHpMultiplier(_saveData.HpMultiplier);

        public void Dispose() {
            _configWatcher?.Dispose();
        }

        private DebugConfig LoadDebugConfig() {
            return new DebugConfig {
                HpMultiplier = _config.Bind(
                    "Debug",
                    "DebugHpMultiplier",
                    Constants.DefaultHpMultiplier,
                    "Unit HP multiplier"
                ),

                DebugConfigOverride = _config.Bind(
                    "Debug",
                    "EnableDebugConfigOverride",
                    false,
                    "If true, it overrides the ingame config multipliers. Also works in the Map Editor, where it affects all troop units."
                )
            };
        }

        private FileSystemWatcher CreateConfigWatcher() {
            string configFilePath = _config.ConfigFilePath;
            string directory = Path.GetDirectoryName(configFilePath);
            string fileName = Path.GetFileName(configFilePath);

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
                return null;

            var watcher = new FileSystemWatcher(directory, fileName) {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
            };

            watcher.Changed += MarkConfigReloadPending;
            watcher.Created += MarkConfigReloadPending;
            watcher.Renamed += MarkConfigReloadPending;
            watcher.EnableRaisingEvents = true;

            return watcher;
        }

        private void MarkConfigReloadPending(object sender, FileSystemEventArgs e) {
            Interlocked.Exchange(ref _configReloadPending, 1);
        }

        private void LogDebugConfigIfChanged() {
            bool debugOverride = IsDebugOverrideEnabled;
            float debugHpMultiplier = DebugHpMultiplier;

            if (_lastDebugConfigOverride == debugOverride && _lastDebugHpMultiplier == debugHpMultiplier)
                return;

            _lastDebugConfigOverride = debugOverride;
            _lastDebugHpMultiplier = debugHpMultiplier;

            _logger.LogDebug(
                $"Debug config loaded: override={debugOverride}, HP multiplier={debugHpMultiplier}"
            );
        }
    }
}
