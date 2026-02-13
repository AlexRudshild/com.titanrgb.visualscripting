using System;
using UnityEngine;
using MessagePack;



using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.VisualScripting;
using MessagePack;
using MessagePack.Unity;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using System.Text;
using System.Linq;
using System;
using System.Reflection;
using Unity.VisualScripting.FullSerializer.Internal;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using System.Data;
using System.Buffers;


public class LookUpContext<T>
{
    protected List<T> context = new();
    protected Dictionary<T, int> lookUpTable = new();

    public LookUpContext(List<T> ctx)
    {
        Context = ctx;
    }

    public LookUpContext()
    {
        context = new();
        lookUpTable = new();
    }


    public List<T> Context
    {
        get
        {
            return context;
        }
        set
        {
            context = value;
            lookUpTable.Clear();

            for (int i = 0; i < context.Count; i++)
            {
                lookUpTable.Add(context[i], i);
            }
        }
    }

    public void Clear()
    {
        context.Clear();
        lookUpTable.Clear();
    }

    public int GetOrAdd(T item)
    {
        if (!lookUpTable.TryGetValue(item, out var index))
        {
            index = context.Count;
            context.Add(item);
            lookUpTable[item] = index;
        }

        return index;
    }

    public T Get(int index)
    {
        if (index < 0 || index >= context.Count)
        {
            throw new MessagePackSerializationException($"Unknown type index {index}");
        }

        return context[index];
    }
}

public struct NullType
{

}

public class FallbackLookUpTypeContext
{
    private List<string> originalAssembly;

    protected List<Type> context;
    protected Dictionary<Type, int> lookUpTable;

    public FallbackLookUpTypeContext()
    {
        context = new();
        lookUpTable = new();
    }

    public void Clear()
    {
        context.Clear();
        lookUpTable.Clear();
    }

    public int GetOrAdd(Type item)
    {
        if (!lookUpTable.TryGetValue(item, out var index))
        {
            index = context.Count;
            context.Add(item);
            lookUpTable[item] = index;
        }

        return index;
    }

    public Type Get(int index)
    {
        if (index < 0 || index >= context.Count)
        {
            throw new MessagePackSerializationException($"Unknown type index {index}");
        }

        return context[index];
    }

    public string GetAsembly(int index)
    {
        return originalAssembly[index];
    }

    public List<string> AssemblyDefenitions
    {
        get
        {
            HashSet<string> assemblys = new HashSet<string>(context.Count);

            foreach (var ctx in context)
            {
                assemblys.Add(ctx.FullName);
            }

            if (originalAssembly != null)
            {
                foreach (var assembly in originalAssembly)
                {
                    assemblys.Add(assembly);
                }
            }

            return assemblys.ToList();
        }
        set
        {
            List<string> assemblys = value;

            originalAssembly = value;

            List<Type> types = new List<Type>(assemblys.Count);

            foreach (var typeAssembly in assemblys)
            {
                if (RuntimeCodebase.TryDeserializeType(typeAssembly, out Type type))
                {
                    types.Add(type);
                }
                else
                {
                    types.Add(null);
                }
            }

            context = types;
            lookUpTable.Clear();

            for (int i = 0; i < context.Count; i++)
            {
                if (context[i] != null)
                {
                    lookUpTable.Add(context[i], i);
                }
            }
        }
    }
}

public readonly struct UnityObjectReference
{
    public UnityObjectReference(int index) => Index = index;
    public int Index { get; }
}

public sealed class UnityObjectReferenceFormatter : IMessagePackFormatter<UnityObjectReference>
{
    private const byte Marker = 0x42;

    public void Serialize(ref MessagePackWriter writer, UnityObjectReference value, MessagePackSerializerOptions options)
    {
        writer.WriteArrayHeader(2);
        writer.Write(Marker);
        writer.Write(value.Index);
    }

    public UnityObjectReference Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var length = reader.ReadArrayHeader();
        if (length != 2)
        {
            throw new MessagePackSerializationException($"Invalid UnityObjectReference array length: {length}");
        }

        var marker = reader.ReadByte();
        if (marker != Marker)
        {
            throw new MessagePackSerializationException("Unexpected marker while reading UnityObjectReference");
        }

        var index = reader.ReadInt32();
        return new UnityObjectReference(index);
    }

    public static bool TryReadReference(ref MessagePackReader reader, out UnityObjectReference reference)
    {
        if (reader.NextMessagePackType != MessagePackType.Array)
        {
            reference = default;
            return false;
        }

        var clone = reader;
        if (clone.ReadArrayHeader() != 2)
        {
            reference = default;
            return false;
        }

        if (clone.NextMessagePackType != MessagePackType.Integer)
        {
            reference = default;
            return false;
        }

        if (clone.ReadByte() != Marker)
        {
            reference = default;
            return false;
        }

        var index = clone.ReadInt32();
        reference = new UnityObjectReference(index);
        reader = clone;
        return true;
    }
}

public class CustomObjectFormatter : IMessagePackFormatter<object>
{
    private readonly LookUpContext<UnityEngine.Object> _ctx;
    public CustomObjectFormatter(LookUpContext<UnityEngine.Object> ctx) => _ctx = ctx;

    public void Serialize(ref MessagePackWriter writer, object value, MessagePackSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNil();
            return;
        }
        if (value is UnityEngine.Object unityObj)
        {
            int idx = _ctx.GetOrAdd(unityObj);
            MessagePackSerializer.Serialize(ref writer, new UnityObjectReference(idx), options);
            return;
        }

        MessagePackSerializer.Serialize(ref writer, value.GetType(), options);
        MessagePackSerializer.Serialize(ref writer, value, options.WithResolver(UnityResolver.InstanceWithStandardResolver));
    }

    public object Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        if (reader.TryReadNil())
            return null;

        if (UnityObjectReferenceFormatter.TryReadReference(ref reader, out var reference))
        {
            return _ctx.Get(reference.Index);
        }

        var type = MessagePackSerializer.Deserialize<Type>(ref reader, options);
        return MessagePackSerializer.Deserialize(type, ref reader, options.WithResolver(UnityResolver.InstanceWithStandardResolver));
    }
}

public class TypeFormatter : IMessagePackFormatter<Type>
{
    private readonly FallbackLookUpTypeContext _ctx;
    public TypeFormatter(FallbackLookUpTypeContext ctx) => _ctx = ctx;

    public void Serialize(ref MessagePackWriter writer, Type value, MessagePackSerializerOptions options)
    {
        writer.Write(_ctx.GetOrAdd(value));
    }

    public Type Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        return _ctx.Get(reader.ReadInt32());
    }
}

public class VariableDeclarationCollectionFormatter : IMessagePackFormatter<VariableDeclarations>
{
    public void Serialize(ref MessagePackWriter writer, VariableDeclarations value, MessagePackSerializerOptions options)
    {
        options.Resolver.GetFormatterWithVerify<VariableKind>().Serialize(ref writer, value.Kind, options);
        options.Resolver.GetFormatterWithVerify<List<VariableDeclaration>>()
            .Serialize(ref writer, new List<VariableDeclaration>(value.Collection), options);
    }

    public VariableDeclarations Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var kind = options.Resolver.GetFormatterWithVerify<VariableKind>()
            .Deserialize(ref reader, options);

        var list = options.Resolver.GetFormatterWithVerify<List<VariableDeclaration>>()
            .Deserialize(ref reader, options);

        var collection = new VariableDeclarationCollection();

        foreach (var item in list)
        {
            collection.Add(item);
        }

        var t = new VariableDeclarations(kind, collection);

        return t;
    }
}

public static class UnitHelperFormatter
{
    private readonly static Dictionary<Type, List<MemberInfo>> CachedMembers = new();

    private const BindingFlags BINDING_FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    public static List<MemberInfo> GetAllFields(Type valueType)
    {
        if (CachedMembers.TryGetValue(valueType, out var cached))
        {
            return cached;
        }

        var members = new List<MemberInfo>();

        Type type = valueType;
        while (type != null)
        {
            if (type == typeof(Unit)) break;
            if (type == typeof(GraphElement<FlowGraph>)) break;

            var fields = type.GetFields(BINDING_FLAGS);
            foreach (var field in fields)
            {
                if (HasSerializationAttribute(field))
                {
                    members.Add(field);
                }
            }

            var properties = type.GetProperties(BINDING_FLAGS);
            foreach (var property in properties)
            {
                if (!property.CanRead)
                {
                    continue;
                }

                if (HasSerializationAttribute(property))
                {
                    members.Add(property);
                }
            }

            type = type.BaseType; // ����������� ����� �� ��������
        }

        CachedMembers[valueType] = members;

        return members;
    }

    private static bool HasSerializationAttribute(MemberInfo member)
    {
        return member.GetCustomAttribute<SerializeAttribute>() != null
            //|| member.GetCustomAttribute<SerializableAttribute>() != null
            || member.GetCustomAttribute<SerializeAsAttribute>() != null;
    }
}

public class MemberFormatter : IMessagePackFormatter<Member>
{
    public void Serialize(ref MessagePackWriter writer, Member value, MessagePackSerializerOptions options)
    {
        MessagePackSerializer.Serialize(ref writer, value.name, options);
        MessagePackSerializer.Serialize(ref writer, value.parameterTypes, options);
        MessagePackSerializer.Serialize(ref writer, value.targetType, options);
    }

    public Member Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var name = MessagePackSerializer.Deserialize<string>(ref reader, options);
        var parameterTypes = MessagePackSerializer.Deserialize<Type[]>(ref reader, options);
        var targetType = MessagePackSerializer.Deserialize<Type>(ref reader, options);

        var member = new Member(targetType, name, parameterTypes);

        return member;
    }
}

public class GraphElementFormatter : IMessagePackFormatter<IGraphElement>
{
    private readonly LookUpContext<IGraphElement> _ctx;
    private readonly FallbackLookUpTypeContext _typeContext;

    public GraphElementFormatter(LookUpContext<IGraphElement> ctx, FallbackLookUpTypeContext typeContext)
    {
        _ctx = ctx;
        _typeContext = typeContext;
    }

    public void CheckTypeAndSerialize(ref MessagePackWriter writer, Type type, object value, MessagePackSerializerOptions options)
    {
        if (type == typeof(IUnit))
        {
            writer.Write(_ctx.GetOrAdd(value as IUnit));
            return;
        }
        if (value is UnityEngine.Object)
        {
            MessagePackSerializer.Serialize<object>(ref writer, value, options);
            return;
        }

        MessagePackSerializer.Serialize(type, ref writer, value, options);
    }

    public object CheckTypeAndDeserialize(ref MessagePackReader reader, Type type, MessagePackSerializerOptions options)
    {
        if (type == typeof(IUnit))
        {
            var index = reader.ReadInt32();

            return _ctx.Get(index);
        }

        return MessagePackSerializer.Deserialize(type, ref reader, options);
    }

    public void Serialize(ref MessagePackWriter writer, IGraphElement value, MessagePackSerializerOptions options)
    {
        _ctx.GetOrAdd(value);

        var valueType = value.GetType();

        //writer.Write(value.guid.ToString());

        int elementType = _typeContext.GetOrAdd(valueType);
        writer.Write(elementType);
        //MessagePackSerializer.Serialize(ref writer, valueType, options);

        if (value is Unit unit)
        {
            MessagePackSerializer.Serialize(ref writer, unit.position, options);
        }

        var members = UnitHelperFormatter.GetAllFields(valueType);

        foreach (var member in members)
        {
            if (member is FieldInfo fieldInfo)
            {
                var fieldValue = fieldInfo.GetValue(value);

                CheckTypeAndSerialize(ref writer, fieldInfo.FieldType, fieldValue, options);
            }
            else if (member is PropertyInfo propertyInfo)
            {
                var propertyValue = propertyInfo.GetValue(value);

                CheckTypeAndSerialize(ref writer, propertyInfo.PropertyType, propertyValue, options);
            }
        }
    }

    public IGraphElement Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var elementTypeIndex = reader.ReadInt32();
        Type elementType = _typeContext.Get(elementTypeIndex);

        if (elementType == null)
        {
            var position = MessagePackSerializer.Deserialize<Vector2>(ref reader, options);
            
            var assembly = _typeContext.GetAsembly(elementTypeIndex);
            var missing = new MissingType();

            missing.position = position;

            missing.formerType = assembly;

            //var raw = reader.ReadRaw();              // возвращает ReadOnlySequence<byte>
            //var bytes = raw.ToArray();

            //missing.formerByteVaule = bytes;

            _ctx.GetOrAdd(missing);

            return missing;
        }

        var graphElement = (IGraphElement)Activator.CreateInstance(elementType);

        _ctx.GetOrAdd(graphElement);

        if (graphElement is Unit unit)
        {
            unit.position = MessagePackSerializer.Deserialize<Vector2>(ref reader, options);
        }

        var members = UnitHelperFormatter.GetAllFields(elementType);

        foreach (var member in members)
        {
            if (member is FieldInfo fieldInfo)
            {
                var fieldValue = CheckTypeAndDeserialize(ref reader, fieldInfo.FieldType, options);
                fieldInfo.SetValue(graphElement, fieldValue);
            }
            else if (member is PropertyInfo propertyInfo && propertyInfo.CanWrite)
            {
                var propertyValue = CheckTypeAndDeserialize(ref reader, propertyInfo.PropertyType, options);
                propertyInfo.SetValue(graphElement, propertyValue);
            }
        }

        return graphElement;
    }
}

public class FlowGraphFormatter : IMessagePackFormatter<FlowGraph>
{
    public void Serialize(ref MessagePackWriter writer, FlowGraph value, MessagePackSerializerOptions options)
    {
        value.OnBeforeSerialize();

        writer.Write(value.title);
        writer.Write(value.summary);
        writer.Write(value.zoom);

        MessagePackSerializer.Serialize(ref writer, value.pan, options);
        MessagePackSerializer.Serialize(ref writer, value.variables, options);

        MessagePackSerializer.Serialize(ref writer, value.RawElements, options);
    }

    public FlowGraph Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var fg = new FlowGraph();

        fg.title = reader.ReadString();
        fg.summary = reader.ReadString();
        fg.zoom = reader.ReadSingle(); // float = single!

        fg.OnAfterDeserialize();

        fg.pan = MessagePackSerializer.Deserialize<UnityEngine.Vector2>(ref reader, options);
        fg.variables = MessagePackSerializer.Deserialize<VariableDeclarations>(ref reader, options);
        fg.RawElements = MessagePackSerializer.Deserialize<List<IGraphElement>>(ref reader, options);

        fg.OnAfterDependenciesDeserialized();

        return fg;
    }
}

[Serializable]
public class SavedData
{
    [SerializeField] public byte[] Data;
    [SerializeField] public List<string> TypeAssemblys = new();
    [SerializeField] public List<UnityEngine.Object> ObjectReferences = new();

    public void Serialize()
    {
        ObjectReferences = objectContext.Context;
        TypeAssemblys = typeContext.AssemblyDefenitions;
    }

    public void Deserialize()
    {
        objectContext.Context = ObjectReferences;
        typeContext.AssemblyDefenitions = TypeAssemblys;
    }

    [DoNotSerialize] public LookUpContext<UnityEngine.Object> objectContext = new();
    [DoNotSerialize] public FallbackLookUpTypeContext typeContext = new();
}


namespace Unity.VisualScripting
{
    [AddComponentMenu("Visual Scripting/Script Machine")]
    [RequireComponent(typeof(Variables))]
    [DisableAnnotation]
    [RenamedFrom("Bolt.FlowMachine")]
    [RenamedFrom("Unity.VisualScripting.FlowMachine")]
    [VisualScriptingHelpURL(typeof(ScriptMachine))]
    public sealed class ScriptMachine : EventMachine<FlowGraph, ScriptGraphAsset>, ISerializationCallbackReceiver
    {
        [SerializeField, DoNotSerialize] private bool embed = false;
        [SerializeField, DoNotSerialize] private SavedData _savedData = null;

        private MessagePackSerializerOptions GetMessagePackSerializerOptions(ref SavedData savedData)
        {
            var graphElementContext = new LookUpContext<IGraphElement>();

            var customResolver = CompositeResolver.Create(
                new IMessagePackFormatter[] {
                    new MemberFormatter(),
                    new TypeFormatter(savedData.typeContext),
                    new GraphElementFormatter(graphElementContext, savedData.typeContext),
                    new VariableDeclarationCollectionFormatter(),
                    new UnityObjectReferenceFormatter(),
                    new CustomObjectFormatter(savedData.objectContext),
                    new FlowGraphFormatter()
                },
                new IFormatterResolver[] { UnityResolver.InstanceWithStandardResolver } // ��� ���� ������ ���������
            );

            return MessagePackSerializerOptions.Standard.WithResolver(customResolver);
        }


        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            if (Serialization.isCustomSerializing)
            {
                return;
            }

            Serialization.isUnitySerializing = true;

            try
            {
                OnBeforeSerialize();

                if (nest.source == GraphSource.Embed)
                {
                    var saveData = new SavedData();
                    var options = GetMessagePackSerializerOptions(ref saveData);

                    saveData.Data = MessagePackSerializer.Serialize(nest.embed, options);

                    saveData.Serialize();

                    _savedData = saveData;
                    _data.Clear();
                    embed = true;
                }
                else
                {
                    _data = this.Serialize(true);
                    embed = false;
                }

                OnAfterSerialize();
            }
            catch (Exception ex)
            {
                // Don't abort the whole serialization thread because this one object failed
                //Debug.LogError($"Failed to serialize behaviour.\n{ex}", this);
                Debug.LogError(ex);
            }

            Serialization.isUnitySerializing = false;
        }


        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (Serialization.isCustomSerializing)
            {
                return;
            }

            Serialization.isUnitySerializing = true;

            try
            {
                object @this = this;
                OnBeforeDeserialize();

                if (embed)
                {
                    var options = GetMessagePackSerializerOptions(ref _savedData);

                    _savedData.Deserialize();

                    nest.source = GraphSource.Embed;
                    nest.embed = MessagePackSerializer.Deserialize<FlowGraph>(_savedData.Data, options);
                }
                else
                {
                    _data.DeserializeInto(ref @this, true);
                }

                OnAfterDeserialize();
                _data.Clear();
            }
            catch (Exception ex)
            {
                // Don't abort the whole deserialization thread because this one object failed
                Debug.LogError($"Failed to deserialize behaviour.\n{ex}", this);
            }

            Serialization.isUnitySerializing = false;
        }


        public override FlowGraph DefaultGraph()
        {
            return FlowGraph.WithStartUpdate();
        }

        protected override void OnEnable()
        {
            if (hasGraph)
            {
                graph.StartListening(reference);
            }

            base.OnEnable();
        }

        protected override void OnInstantiateWhileEnabled()
        {
            if (hasGraph)
            {
                graph.StartListening(reference);
            }

            base.OnInstantiateWhileEnabled();
        }

        protected override void OnUninstantiateWhileEnabled()
        {
            base.OnUninstantiateWhileEnabled();

            if (hasGraph)
            {
                graph.StopListening(reference);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (hasGraph)
            {
                graph.StopListening(reference);
            }
        }

        [ContextMenu("Show Data...")]
        protected override void ShowData()
        {
            base.ShowData();
        }
    }
}
