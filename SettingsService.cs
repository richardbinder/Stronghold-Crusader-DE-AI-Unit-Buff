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

namespace AIUnitBuff {
    internal class SettingsService : IDisposable {
        private readonly ConfigFile _config;
        private readonly ManualLogSource _logger;
        private readonly FileSystemWatcher _configWatcher;
        private SaveData _saveData = new();
        private int _configReloadPending;
        private bool? _lastDebugConfigOverride;
        private float _lastDebugHpMultiplier = float.NaN;
        private float _lastDebugDmgMultiplier = float.NaN;

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

        public float DmgMultiplier =>
            Debug.DebugConfigOverride.Value
                ? DebugDmgMultiplier
                : Constants.ClampDmgMultiplier(_saveData.DmgMultiplier);

        public float DebugHpMultiplier =>
            Constants.ClampHpMultiplier(Debug.HpMultiplier.Value);

        public float DebugDmgMultiplier =>
            Constants.ClampDmgMultiplier(Debug.DmgMultiplier.Value);

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

        public void InitFromLobby(float lobbyHpMultiplier, float lobbyDmgMultiplier) {
            _saveData = new SaveData {
                HpMultiplier = Constants.ClampHpMultiplier(lobbyHpMultiplier),
                DmgMultiplier = Constants.ClampDmgMultiplier(lobbyDmgMultiplier)
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
        public float SavedDmgMultiplier => Constants.ClampDmgMultiplier(_saveData.DmgMultiplier);

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

                DmgMultiplier = _config.Bind(
                    "Debug",
                    "DebugDmgMultiplier",
                    Constants.DefaultDmgMultiplier,
                    "Unit damage multiplier"
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
            float debugDmgMultiplier = DebugDmgMultiplier;

            if (_lastDebugConfigOverride == debugOverride &&
                _lastDebugHpMultiplier == debugHpMultiplier &&
                _lastDebugDmgMultiplier == debugDmgMultiplier)
                return;

            _lastDebugConfigOverride = debugOverride;
            _lastDebugHpMultiplier = debugHpMultiplier;
            _lastDebugDmgMultiplier = debugDmgMultiplier;

            _logger.LogDebug(
                $"Debug config loaded: override={debugOverride}, HP multiplier={debugHpMultiplier}, damage multiplier={debugDmgMultiplier}"
            );
        }
    }
}
