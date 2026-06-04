using System;
using System.Collections.Generic;
using BepInEx;
using R3;
using SHCDESE;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using System.Collections.Concurrent;

namespace AIHPBoost {
    [BepInDependency("000shcdese", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin("AIUnitHPBuff", "AI Unit HP Buff", "1.0.0")]
    public class Plugin : BaseUnityPlugin {
        private AIHpLobbySettings _lobbySettings;

        private readonly ConcurrentDictionary<(eChimps Type, int Owner), int> _computedMaxHp = new ConcurrentDictionary<(eChimps Type, int Owner), int>();

        private void Awake() {
            Logger.LogInfo("AI Unit HP Buff loaded.");
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;

            MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(e => {
                _computedMaxHp.Clear();
                Logger.LogInfo("Cleared AI HP cache on map start.");
            });

            MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(e => {
                _computedMaxHp.Clear();
                Logger.LogInfo("Cleared AI HP cache on map unload.");
            });
        }

        private void OnLibraryLoaded(IntPtr moduleHandle, ReadOnlySpan<byte> memory) {
            _lobbySettings = new AIHpLobbySettings();

            GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(
                plugin: this,
                modName: "AIUnitHPBuff",
                viewModel: _lobbySettings,
                xamlSourceFile: "LobbySettings.xaml"
            );

            Logger.LogInfo($"Lobby HP multiplier loaded: {_lobbySettings.HpMultiplierPercent}%");
            Logger.LogInfo("Crusader library loaded. Registering hooks...");

            TribeR3EventHooks.OnTribeAssignUnit.Observable.Subscribe(e => {
                if (e.Phase == EventHookPhase.Pre)
                    return;

                int unitId = (int)e.UnitId;

                GameTimeManagerAPI.Instance.GetTimerEngine().AddDelayedAction(1000, action: () => {
                    try {
                        TryBuffAiUnit(unitId, "TRIBE_DELAYED");
                    }
                    catch (Exception ex) {
                        Logger.LogError(ex);
                    }
                }, null);
            });
        }

        private unsafe void TryBuffAiUnit(int unitId, string source) {
            var unitManager = GameUnitManagerAPI.Instance;
            var playerManager = GamePlayerManagerAPI.Instance;

            if (!unitManager.TryGetUnitById(unitId, out GameUnit* unit))
                return;

            int owner = unitManager.GetOwner(unitId);
            if (!playerManager.IsAIPlayer(owner))
                return;

            int maxHp = unitManager.GetMaxHealth(unitId);
            int hp = unitManager.GetCurrentHealth(unitId);
            eChimps type = unitManager.GetType(unitId);

            var key = (type, owner);

            int targetMaxHp = _computedMaxHp.GetOrAdd(key, _ => {
                int multiplier = _lobbySettings.HpMultiplierPercent;

                return (int)Math.Min(
                    100_000_000,
                    (long)maxHp * multiplier / 100
                );
            });

            if (maxHp == targetMaxHp) {
                Logger.LogDebug($"{source} skipped already buffed unit: UnitId={unitId}, type={type}, Owner={owner}, HP={hp}/{maxHp}");
                return;
            }

            unitManager.SetMaxHealth(unitId, targetMaxHp);
            unitManager.SetCurrentHealth(unitId, targetMaxHp);

            int newHp = unitManager.GetCurrentHealth(unitId);
            int newMaxHp = unitManager.GetMaxHealth(unitId);

            Logger.LogDebug($"{source} changed unit HP: UnitId={unitId}, type={type}, Owner={owner}, HP={hp}/{maxHp} -> {newHp}/{newMaxHp}");
        }
    }
}