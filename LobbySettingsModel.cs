using System;
using SHCDESE.API.Components.Network;
using SHCDESE.ViewModels;

namespace AIUnitHPBuff {
    public class LobbySettingsModel : LobbyModSettingsBaseViewModel
    {
        private float _hpMultiplier = Constants.DefaultHpMultiplier;

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
            }
        }
    }
}
