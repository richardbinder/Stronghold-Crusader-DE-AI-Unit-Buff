using System;
using System.Collections.Generic;
using AIUnitHPBuff;
using BepInEx;
using BepInEx.Configuration;
using MessagePack;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.SaveData;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;

namespace AIUnitHPBuff {
    [BepInDependency("000shcdese", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin("AIUnitHPBuff", "AI Unit HP Buff", "1.0.0")]
    public class Plugin : BaseUnityPlugin {
        private LobbySettingsModel _lobbySettings;
        private SettingsService _settings;

        private void Awake() {
            _settings = new SettingsService(Config);

            Logger.LogInfo("AI Unit HP Buff loaded.");

            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;

            MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(OnMapStart);
            MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(OnMapUnload);

            ModSaveDataAPI.Instance.RegisterModDataHandler(
                modIdentifier: "AIUnitHPBuff-savedata",
                saveCallback: OnSave,
                loadCallback: OnLoad
            );
        }

        private void OnLibraryLoaded(IntPtr moduleHandle, ReadOnlySpan<byte> memory) {
            _lobbySettings = new LobbySettingsModel();

            GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(
                plugin: this,
                modName: "AIUnitHPBuff",
                viewModel: _lobbySettings,
                xamlSourceFile: "LobbySettings.xaml"
            );

            UnitR3EventHooks.OnUnitTakeMeleeDamage.Observable.Subscribe(OnUnitReceiveMeleeDmg);
            UnitR3EventHooks.OnUnitTakeProjectileDamageEx.Observable.Subscribe(OnUnitReceiveProjectileDmg);

            Logger.LogInfo($"Lobby HP multiplier loaded: {_lobbySettings.HpMultiplier}");
        }

        private void OnUnitReceiveMeleeDmg(UnitTakeDamageByMeleeEventArgs e) {
            if (e.Phase != EventHookPhase.Pre)
                return;

            try {
                e.Damage = GetReducedDamage(e.DamagedUnitId, e.Damage);
            } catch (Exception ex) {
                Logger.LogError(ex);
            }
        }

        private void OnUnitReceiveProjectileDmg(UnitTakeDamageByProjectileExEventArgs e) {
            if (e.Phase != EventHookPhase.Pre)
                return;

            try {
                e.Damage = GetReducedDamage(e.AttackedUnitId, e.Damage);
            } catch (Exception ex) {
                Logger.LogError(ex);
            }
        }

        private unsafe int GetReducedDamage(int attackedUnit, int damage) {
            ReloadDebugConfigIfDue();

            var unitManager = GameUnitManagerAPI.Instance;
            var playerManager = GamePlayerManagerAPI.Instance;

            if (!unitManager.TryGetUnitById(attackedUnit, out GameUnit* unit))
                return damage;

            int owner = unitManager.GetOwner(attackedUnit);

            if (!playerManager.IsAIPlayer(owner) && !_settings.IsDebugOverrideEnabled)
                return damage;

            eChimps type = unitManager.GetType(attackedUnit);

            if (Constants.CivilianTypes.Contains(type))
                return damage;

            float dmgMultiplier = 1.0f / _settings.EffectiveHpMultiplier;
            int targetDamage = Math.Max(1, (int)(damage * dmgMultiplier));

            return targetDamage;
        }

        private void OnMapStart(MapStartEventArgs e) {
            _settings.InitFromLobby(_lobbySettings.HpMultiplier);

            Logger.LogDebug(
                $"Initialized save data on map start. HP multiplier={_settings.SavedHpMultiplier}"
            );
        }

        private void OnMapUnload(MapUnloadEventArgs e) {
            _settings.Reset();

            Logger.LogDebug("Reset save data on map unload.");
        }

        private byte[] OnSave(SaveContext context) {
            if (!context.IsSaveFile)
                return null;

            byte[] bytes = _settings.Serialize();

            Logger.LogDebug($"Saving: HP multiplier={_settings.SavedHpMultiplier}");

            return bytes;
        }

        private void OnLoad(byte[] bytes, LoadContext context) {
            if (!context.IsSaveFile)
                return;

            _settings.Deserialize(bytes);

            Logger.LogDebug($"Loaded: HP multiplier={_settings.SavedHpMultiplier}");
        }
    }
}
