using System;
using UnityEngine;

namespace Unity.VisualScripting
{
    [SpecialUnit]
    [UnitTitle("Node script is missing!")]
    // This title should get replaced by this unit's widget on instantiation.
    [UnitShortTitle("Missing Script!")]

    // Ideally, this unit's icon would be the same as the one of a 'script asset' as shown in the project files window.
    // Unfortunately our TypeIcon attribute does not have support for unity's core types.
    public sealed class MissingType : Unit
    {
        [Serialize]
        public string formerType { get; set; } // Private set is required by the deserializer.

        [Serialize]
        public string formerValue { get; set; }

        [Serialize]
        public byte[] formerByteVaule { get; set; }

        // Although this unit will have no ports, the already existing graph
        // connections will create invalid ones to connect themselves to.
        protected override void Definition() { }
    }
}
