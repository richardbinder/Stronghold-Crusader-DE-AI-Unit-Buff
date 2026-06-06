using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepInEx.Configuration;

namespace AIUnitBuff {
    public class DebugConfig {
        public ConfigEntry<float> HpMultiplier { get; set; }
        public ConfigEntry<float> DmgMultiplier { get; set; }
        public ConfigEntry<bool> DebugConfigOverride { get; set; }
    }
}
