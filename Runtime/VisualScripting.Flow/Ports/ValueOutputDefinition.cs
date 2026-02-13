using MessagePack;

namespace Unity.VisualScripting
{
    [MessagePackObject]
    public sealed class ValueOutputDefinition : ValuePortDefinition, IUnitOutputPortDefinition { }
}
