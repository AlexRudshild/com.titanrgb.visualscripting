using MessagePack;

namespace Unity.VisualScripting
{
    [MessagePackObject]
    public sealed class ControlInputDefinition : ControlPortDefinition, IUnitInputPortDefinition { }
}
