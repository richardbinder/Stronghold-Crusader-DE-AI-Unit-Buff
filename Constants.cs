using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SHCDESE.Interop;

namespace AIUnitBuff {
    public class Constants {
        public const float DefaultHpMultiplier = 1.0f;
        public const float DefaultDmgMultiplier = 1.0f;
        public const float MinHpMultiplier = 0.01f;
        public const float MaxHpMultiplier = 100.0f;
        public const float MinDmgMultiplier = 0.01f;
        public const float MaxDmgMultiplier = 100.0f;

        public static float ClampHpMultiplier(float value) {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return DefaultHpMultiplier;

            return Math.Max(MinHpMultiplier, Math.Min(MaxHpMultiplier, value));
        }

        public static float ClampDmgMultiplier(float value) {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return DefaultDmgMultiplier;

            return Math.Max(MinDmgMultiplier, Math.Min(MaxDmgMultiplier, value));
        }

        public static readonly HashSet<eChimps> CivilianTypes =
        [
            eChimps.CHIMP_TYPE_PEASANT,
            eChimps.CHIMP_TYPE_WOODCUTTER,
            eChimps.CHIMP_TYPE_FLETCHER,
            eChimps.CHIMP_TYPE_HUNTER,
            eChimps.CHIMP_TYPE_QUARRY_MASON,
            eChimps.CHIMP_TYPE_QUARRY_GRUNT,
            eChimps.CHIMP_TYPE_QUARRY_OX,
            eChimps.CHIMP_TYPE_PITCHMAN,
            eChimps.CHIMP_TYPE_FARMER_WHEAT,
            eChimps.CHIMP_TYPE_FARMER_HOPS,
            eChimps.CHIMP_TYPE_FARMER_APPLE,
            eChimps.CHIMP_TYPE_FARMER_CATTLE,
            eChimps.CHIMP_TYPE_MILLER,
            eChimps.CHIMP_TYPE_BAKER,
            eChimps.CHIMP_TYPE_BREWER,
            eChimps.CHIMP_TYPE_POLETURNER,
            eChimps.CHIMP_TYPE_BLACKSMITH,
            eChimps.CHIMP_TYPE_ARMOURER,
            eChimps.CHIMP_TYPE_TANNER,
            eChimps.CHIMP_TYPE_INNKEEPER,
            eChimps.CHIMP_TYPE_FIREMAN,
            eChimps.CHIMP_TYPE_MINER1,
            eChimps.CHIMP_TYPE_MINER2,
            eChimps.CHIMP_TYPE_TRADER,
            eChimps.CHIMP_TYPE_TRADER_HORSE
        ];
    }
}
