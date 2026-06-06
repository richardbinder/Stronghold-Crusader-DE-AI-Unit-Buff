using System;
using System.Globalization;
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
        private string _hpMultiplierText = Constants.DefaultHpMultiplier.ToString("0.0", CultureInfo.InvariantCulture);
        private string _dmgMultiplierText = Constants.DefaultDmgMultiplier.ToString("0.0", CultureInfo.InvariantCulture);

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
                _hpMultiplierText = clamped.ToString("0.###", CultureInfo.InvariantCulture);
                OnPropertyChanged(nameof(HpMultiplier));
                OnPropertyChanged(nameof(HpMultiplierText));
                OnPropertyChanged(nameof(EffectiveHpMultiplier));
            }
        }

        public string HpMultiplierText
        {
            get => _hpMultiplierText;
            set
            {
                if (!TrySetMultiplierText(value, nameof(HpMultiplierText), ref _hpMultiplierText, Constants.ClampHpMultiplier, Constants.DefaultHpMultiplier, out bool updateMultiplier, out float clamped))
                    return;

                if (updateMultiplier)
                    _hpMultiplier = clamped;

                OnPropertyChanged(nameof(HpMultiplierText));
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
                _dmgMultiplierText = clamped.ToString("0.###", CultureInfo.InvariantCulture);
                OnPropertyChanged(nameof(DmgMultiplier));
                OnPropertyChanged(nameof(DmgMultiplierText));
                OnPropertyChanged(nameof(EffectiveDmgMultiplier));
            }
        }

        public string DmgMultiplierText
        {
            get => _dmgMultiplierText;
            set
            {
                if (!TrySetMultiplierText(value, nameof(DmgMultiplierText), ref _dmgMultiplierText, Constants.ClampDmgMultiplier, Constants.DefaultDmgMultiplier, out bool updateMultiplier, out float clamped))
                    return;

                if (updateMultiplier)
                    _dmgMultiplier = clamped;

                OnPropertyChanged(nameof(DmgMultiplierText));
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

        private bool TrySetMultiplierText(
            string value,
            string propertyName,
            ref string backingField,
            Func<float, float> clamp,
            float defaultValue,
            out bool updateMultiplier,
            out float clamped
        ) {
            value ??= string.Empty;
            updateMultiplier = false;
            clamped = 0.0f;

            if (value == string.Empty || value == ".") {
                clamped = defaultValue;
                backingField = defaultValue.ToString("0.0", CultureInfo.InvariantCulture);
                updateMultiplier = true;
                return true;
            }

            if (!IsDecimalText(value)) {
                OnPropertyChanged(propertyName);
                return false;
            }

            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                return true;

            clamped = clamp(parsed);
            backingField = parsed == clamped || value == "0" || value.EndsWith(".")
                ? value
                : clamped.ToString("0.###", CultureInfo.InvariantCulture);
            updateMultiplier = true;
            return true;
        }

        private static bool IsDecimalText(string value) {
            int decimalPoints = 0;

            foreach (char c in value) {
                if (c >= '0' && c <= '9')
                    continue;

                if (c == '.' && decimalPoints++ == 0)
                    continue;

                return false;
            }

            return true;
        }
    }
}
