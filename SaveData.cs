using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessagePack;

namespace AIUnitHPBuff {

    [MessagePackObject(true)]
    public class SaveData {
        public float HpMultiplier { get; set; } = Constants.DefaultHpMultiplier;
    }
}
