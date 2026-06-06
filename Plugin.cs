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
        private readonly GameUnitManagerAPI UnitManager = GameUnitManagerAPI.Instance;
        private readonly GamePlayerManagerAPI PlayerManager = GamePlayerManagerAPI.Instance;
        private readonly GameProjectileManagerAPI ProjectileAPI = GameProjectileManagerAPI.Instance;

        private LobbySettingsModel _lobbySettings;
        private SettingsService _settings;

        private void Awake() {
            _settings = new SettingsService(Config, Logger);

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
                e.Damage = GetAlternatedDamage(e.AttackingUnitId, e.DamagedUnitId, -1, e.Damage);
            } catch (Exception ex) {
                Logger.LogError(ex);
            }
        }

        private void OnUnitReceiveProjectileDmg(UnitTakeDamageByProjectileExEventArgs e) {
            if (e.Phase != EventHookPhase.Pre)
                return;

            try {
                e.Damage = GetAlternatedDamage(e.AttackingUnitId, e.AttackedUnitId, e.ProjectileId, e.Damage);
            } catch (Exception ex) {
                Logger.LogError(ex);
            }
        }

        private unsafe int GetAlternatedDamage(int attackingUnit, int defendingUnit, int projectile, int damage) {
            _settings.ProcessPendingConfigReload();

            float defenderHpMultiplier;
            float attackerDmgMultiplier;
            int damagedUnitOwner;
            int attackingUnitOwner;

            if (!UnitManager.TryGetUnitById(defendingUnit, out GameUnit* unit)) {
                // if defender doesn't exist anymore, the damage doesn't matter so we just return
                return damage;
            } else {
                damagedUnitOwner = UnitManager.GetOwner(defendingUnit);
            }

            if (!UnitManager.TryGetUnitById(attackingUnit, out GameUnit* unit)) {
                // check projectile owner if attacking unit doesnt exist anymore
                if (!ProjectileAPI.TryGetProjectileById(projectile, out GameProjectile* proj)) {
                    // just set to default if projectile also doesn't exist anymore (not sure if this can even happen, maybe on melee attacks)
                    attackerDmgMultiplier = Constants.DefaultDmgMultiplier;
                } else {
                    attackingUnitOwner = ProjectileAPI.GetSourcePlayer(projectile);
                }
            } else {
                attackingUnitOwner = UnitManager.GetOwner(attackingUnit);
            }

            eChimps type = UnitManager.GetType(defendingUnit);

            if (Constants.CivilianTypes.Contains(type))
                return damage;

            float finalDmgMultiplier = 1.0f / defenderHpMultiplier * attackerDmgMultiplier;
            int targetDamage = Math.Max(1, (int)(damage * finalDmgMultiplier));

            return targetDamage;
        }

        private float getUnitDamageMultiplier(int ownerId) {
            if (usesAIMultipliers(ownerId)) {
                return _settings.DmgMultiplier;
            } else {
                return Constants.DefaultDmgMultiplier;
            }
        }

        private float getUnitHpMultiplier(int ownerId) {
            if (usesAIMultipliers(ownerId)) {
                return _settings.HpMultiplier;
            } else {
                return Constants.DefaultHpMultiplier;
            }
        }

        private bool usesAIMultipliers(int playerId) {
            // Debug override allows easy debugging when the setting is active, affects all troops in the map editor
            return _settings.IsDebugOverrideEnabled || PlayerManager.IsAIPlayer(ownerId);
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
