using MessagePack;
using System;

namespace Unity.VisualScripting
{
    [MessagePackObject]
    [Union(0, typeof(ValueInputDefinition))]
    [Union(1, typeof(ValueOutputDefinition))]
    public abstract partial class ValuePortDefinition : UnitPortDefinition, IUnitValuePortDefinition
    {
        // For the virtual inheritors
        [SerializeAs(nameof(_type))]
        [IgnoreMember]
        private Type _type { get; set; }

        [Inspectable]
        [DoNotSerialize]
        [Key(0)]
        public virtual Type type
        {
            get
            {
                return _type;
            }
            set
            {
                _type = value;
            }
        }

        [IgnoreMember]
        public override bool isValid => base.isValid && type != null;
    }
}
