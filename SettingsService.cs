using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using BepInEx.Configuration;
using MessagePack;

namespace AIUnitHPBuff {
    internal class SettingsService {
        private static readonly TimeSpan DebugConfigReloadInterval = TimeSpan.FromSeconds(1);

        private readonly ConfigFile _config;
        private SaveData _saveData = new();

        private DateTime _nextDebugConfigReloadUtc = DateTime.MinValue;
        private bool? _lastDebugConfigOverride;
        private float _lastDebugHpMultiplier = float.NaN;

        public SettingsService(ConfigFile config) {
            _config = config;
            Debug = LoadDebugConfig();
        }

        public DebugConfig Debug { get; }

        public float EffectiveHpMultiplier =>
            Debug.DebugConfigOverride.Value
                ? DebugHpMultiplier
                : Constants.ClampHpMultiplier(_saveData.HpMultiplier);

        public float DebugHpMultiplier =>
            Constants.ClampHpMultiplier(Debug.HpMultiplier.Value);

        public bool IsDebugOverrideEnabled =>
            Debug.DebugConfigOverride.Value;

        public void ReloadConfig() {
            _config.Reload();
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
                    "If true, it overrides the ingame config multipliers. Also works in the Map Editor, where it affects all units."
                )
            };
        }

        private void ReloadDebugConfigIfDue() {
            DateTime now = DateTime.UtcNow;

            if (now < _nextDebugConfigReloadUtc)
                return;

            _nextDebugConfigReloadUtc = now + DebugConfigReloadInterval;

            try {
                ReloadConfig();
            } catch (Exception ex) {
                Logger.LogWarning($"Failed to reload debug config: {ex.Message}");
                return;
            }

            bool debugOverride = IsDebugOverrideEnabled;
            float debugHpMultiplier = DebugHpMultiplier;

            if (_lastDebugConfigOverride == debugOverride && _lastDebugHpMultiplier == debugHpMultiplier)
                return;

            _lastDebugConfigOverride = debugOverride;
            _lastDebugHpMultiplier = debugHpMultiplier;

            Logger.LogDebug(
                $"Debug config loaded: override={debugOverride}, HP multiplier={debugHpMultiplier}"
            );
        }
    }
}
