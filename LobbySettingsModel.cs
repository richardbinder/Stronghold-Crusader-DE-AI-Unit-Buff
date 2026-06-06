using System;
using SHCDESE.API.Components.Network;
using SHCDESE.ViewModels;

namespace AIUnitBuff {
    public class LobbySettingsModel : LobbyModSettingsBaseViewModel
    {
        private static readonly float[] DifficultyMultipliers = [
            0.5f,
            1.0f,
            2.0f,
            5.0f,
            10.0f,
            50.0f
        ];

        private int _selectedDifficultyIndex = 1;
        private bool _customizeMultipliers;
        private float _hpMultiplier = Constants.DefaultHpMultiplier;
        private float _dmgMultiplier = Constants.DefaultDmgMultiplier;

        [SyncHostOnly]
        public int SelectedDifficultyIndex
        {
            get => _selectedDifficultyIndex;
            set
            {
                int clamped = Math.Max(0, Math.Min(DifficultyMultipliers.Length - 1, value));

                if (_selectedDifficultyIndex == clamped)
                    return;

                _selectedDifficultyIndex = clamped;
                OnPropertyChanged(nameof(SelectedDifficultyIndex));
                OnPropertyChanged(nameof(SelectedDifficultySliderValue));
                OnMultiplierSourceChanged();
            }
        }

        public double SelectedDifficultySliderValue
        {
            get => SelectedDifficultyIndex;
            set => SelectedDifficultyIndex = (int)Math.Round(value);
        }

        [SyncHostOnly]
        public bool CustomizeMultipliers
        {
            get => _customizeMultipliers;
            set
            {
                if (_customizeMultipliers == value)
                    return;

                _customizeMultipliers = value;
                OnPropertyChanged(nameof(CustomizeMultipliers));
                OnPropertyChanged(nameof(IsPresetSelectionEnabled));
                OnMultiplierSourceChanged();
            }
        }

        public bool IsPresetSelectionEnabled => !CustomizeMultipliers;

        [SyncHostOnly]
        public float HpMultiplier
        {
            get => _hpMultiplier;
            set
            {
                float clamped = Constants.ClampHpMultiplier(value);

                if (_hpMultiplier == clamped)
                    return;

                _hpMultiplier = clamped;
                OnPropertyChanged(nameof(HpMultiplier));
                OnPropertyChanged(nameof(EffectiveHpMultiplier));
            }
        }

        [SyncHostOnly]
        public float DmgMultiplier
        {
            get => _dmgMultiplier;
            set
            {
                float clamped = Constants.ClampDmgMultiplier(value);

                if (_dmgMultiplier == clamped)
                    return;

                _dmgMultiplier = clamped;
                OnPropertyChanged(nameof(DmgMultiplier));
                OnPropertyChanged(nameof(EffectiveDmgMultiplier));
            }
        }

        public float EffectiveHpMultiplier =>
            CustomizeMultipliers ? HpMultiplier : DifficultyMultipliers[SelectedDifficultyIndex];

        public float EffectiveDmgMultiplier =>
            CustomizeMultipliers ? DmgMultiplier : DifficultyMultipliers[SelectedDifficultyIndex];

        private void OnMultiplierSourceChanged() {
            OnPropertyChanged(nameof(EffectiveHpMultiplier));
            OnPropertyChanged(nameof(EffectiveDmgMultiplier));
        }
    }
}
