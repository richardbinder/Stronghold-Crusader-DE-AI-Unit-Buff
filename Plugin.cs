using System;
using System.Collections.Generic;
using AIUnitBuff;
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

namespace AIUnitBuff {
    [BepInDependency("000shcdese", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin("AIUnitBuff", "AI Unit Buff", "1.0.0")]
    public class Plugin : BaseUnityPlugin {
        private readonly GameUnitManagerAPI UnitManager = GameUnitManagerAPI.Instance;
        private readonly GamePlayerManagerAPI PlayerManager = GamePlayerManagerAPI.Instance;
        private readonly GameProjectileManagerAPI ProjectileAPI = GameProjectileManagerAPI.Instance;

        private LobbySettingsModel _lobbySettings;
        private SettingsService _settings;

        private void Awake() {
            _settings = new SettingsService(Config, Logger);

            Logger.LogInfo("AI Unit Buff loaded.");

            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;

            MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(OnMapStart);
            MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(OnMapUnload);

            ModSaveDataAPI.Instance.RegisterModDataHandler(
                modIdentifier: "AIUnitBuff-savedata",
                saveCallback: OnSave,
                loadCallback: OnLoad
            );
        }

        private void OnLibraryLoaded(IntPtr moduleHandle, ReadOnlySpan<byte> memory) {
            _lobbySettings = new LobbySettingsModel();

            GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(
                plugin: this,
                modName: "AIUnitBuff",
                viewModel: _lobbySettings,
                xamlSourceFile: "LobbySettings.xaml"
            );

            UnitR3EventHooks.OnUnitTakeMeleeDamage.Observable.Subscribe(OnUnitReceiveMeleeDmg);
            UnitR3EventHooks.OnUnitTakeProjectileDamageEx.Observable.Subscribe(OnUnitReceiveProjectileDmg);

            Logger.LogInfo($"Lobby multipliers loaded: {_lobbySettings.EffectiveHpMultiplier}, damage multiplier loaded: {_lobbySettings.EffectiveDmgMultiplier}");
        }

        private void OnUnitReceiveMeleeDmg(UnitTakeDamageByMeleeEventArgs e) {
            if (e.Phase != EventHookPhase.Pre)
                return;

            try {
                e.Damage = GetModifiedMeleeDamage(e.AttackingUnitId, e.DamagedUnitId, e.Damage);
            } catch (Exception ex) {
                Logger.LogError(ex);
            }
        }

        private void OnUnitReceiveProjectileDmg(UnitTakeDamageByProjectileExEventArgs e) {
            if (e.Phase != EventHookPhase.Pre)
                return;

            try {
                e.Damage = GetModifiedProjectileDamage(e.AttackingUnitId, e.AttackedUnitId, e.ProjectileId, e.Damage);
            } catch (Exception ex) {
                Logger.LogError(ex);
            }
        }

        private unsafe int GetModifiedMeleeDamage(int attackingUnit, int defendingUnit, int damage) {
            _settings.ProcessPendingConfigReload();

            if (!TryGetUnitDamageContext(defendingUnit, out eChimps defendingUnitType, out float defenderHpMultiplier))
                return damage;

            if (Constants.CivilianTypes.Contains(defendingUnitType))
                return damage;

            if (!UnitManager.TryGetUnitById(attackingUnit, out GameUnit* attackingGameUnit))
                return damage;

            int attackingUnitOwner = UnitManager.GetOwner(attackingUnit);
            float attackerDmgMultiplier = GetUnitDamageMultiplier(attackingUnitOwner);
            eChimps attackingUnitType = UnitManager.GetType(attackingUnit);

            if (damage == 0) {
                damage = UnitManager.GetMeleeDamageFromTo(attackingUnitType, defendingUnitType);
            }

            return ApplyDamageMultipliers(damage, defenderHpMultiplier, attackerDmgMultiplier);
        }

        private unsafe int GetModifiedProjectileDamage(int attackingUnit, int defendingUnit, int projectile, int damage) {
            _settings.ProcessPendingConfigReload();

            if (!TryGetUnitDamageContext(defendingUnit, out eChimps defendingUnitType, out float defenderHpMultiplier))
                return damage;

            if (Constants.CivilianTypes.Contains(defendingUnitType))
                return damage;

            float attackerDmgMultiplier = Constants.DefaultDmgMultiplier;

            if (UnitManager.TryGetUnitById(attackingUnit, out GameUnit* attackingGameUnit)) {
                int attackingUnitOwner = UnitManager.GetOwner(attackingUnit);
                attackerDmgMultiplier = GetUnitDamageMultiplier(attackingUnitOwner);
            } else if (ProjectileAPI.TryGetProjectileById(projectile, out GameProjectile* proj)) {
                int attackingUnitOwner = ProjectileAPI.GetSourcePlayer(projectile);
                attackerDmgMultiplier = GetUnitDamageMultiplier(attackingUnitOwner);
            }

            return ApplyDamageMultipliers(damage, defenderHpMultiplier, attackerDmgMultiplier);
        }

        private unsafe bool TryGetUnitDamageContext(int unitId, out eChimps unitType, out float hpMultiplier) {
            unitType = default;
            hpMultiplier = Constants.DefaultHpMultiplier;

            if (!UnitManager.TryGetUnitById(unitId, out GameUnit* unit))
                return false;

            int owner = UnitManager.GetOwner(unitId);
            unitType = UnitManager.GetType(unitId);
            hpMultiplier = GetUnitHpMultiplier(owner);

            return true;
        }

        private int ApplyDamageMultipliers(int damage, float defenderHpMultiplier, float attackerDmgMultiplier) {
            if (damage <= 0)
                return damage;

            float finalDmgMultiplier = 1.0f / defenderHpMultiplier * attackerDmgMultiplier;
            return Math.Max(1, (int)(damage * finalDmgMultiplier));
        }

        private float GetUnitDamageMultiplier(int ownerId) {
            if (UsesAIMultipliers(ownerId)) {
                return _settings.DmgMultiplier;
            } else {
                return Constants.DefaultDmgMultiplier;
            }
        }

        private float GetUnitHpMultiplier(int ownerId) {
            if (UsesAIMultipliers(ownerId)) {
                return _settings.HpMultiplier;
            } else {
                return Constants.DefaultHpMultiplier;
            }
        }

        private bool UsesAIMultipliers(int playerId) {
            // Debug override allows easy debugging, affects all troops in the map editor
            return _settings.IsDebugOverrideEnabled || PlayerManager.IsAIPlayer(playerId);
        }

        private void OnMapStart(MapStartEventArgs e) {
            _settings.InitFromLobby(_lobbySettings.EffectiveHpMultiplier, _lobbySettings.EffectiveDmgMultiplier);

            Logger.LogDebug(
                $"Initialized data on map start. HP multiplier={_settings.SavedHpMultiplier}, damage multiplier={_settings.SavedDmgMultiplier}"
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

            Logger.LogDebug($"Saving: HP multiplier={_settings.SavedHpMultiplier}, damage multiplier={_settings.SavedDmgMultiplier}");

            return bytes;
        }

        private void OnLoad(byte[] bytes, LoadContext context) {
            if (!context.IsSaveFile)
                return;

            _settings.Deserialize(bytes);

            Logger.LogDebug($"Loaded: HP multiplier={_settings.SavedHpMultiplier}, damage multiplier={_settings.SavedDmgMultiplier}");
        }
    }
}
