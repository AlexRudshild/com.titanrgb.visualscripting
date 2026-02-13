using MessagePack;
using System;

namespace Unity.VisualScripting
{
    [SerializationVersion("A")]
    [MessagePackObject]
    public sealed class VariableDeclaration
    {
        [Obsolete(Serialization.ConstructorWarning)]
        public VariableDeclaration() { }

        public VariableDeclaration(string name, object value)
        {
            this.name = name;
            this.value = value;
        }

        [Serialize]
        [Key(0)]
        public string name { get; private set; }

        [Serialize, Value]
        [Key(1)]
        public object value { get; set; }

        [Serialize]
        [Key(2)]
        public SerializableType typeHandle { get; set; }
    }
}
