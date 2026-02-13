using MessagePack;

namespace Unity.VisualScripting
{
    [MessagePackObject]
    public sealed class ControlOutputDefinition : ControlPortDefinition, IUnitOutputPortDefinition { }
}
