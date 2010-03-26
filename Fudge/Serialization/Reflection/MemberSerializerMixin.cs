using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace Fudge.Serialization.Reflection
{
    /// <summary>
    /// Mixin to help with (de)serialization of properties and fields
    /// </summary>
    internal sealed class MemberSerializerMixin : IFudgeSerializationSurrogate
    {
        private readonly FudgeContext context;
        private readonly TypeData typeData;
        private readonly MemberData[] members;      // We use an array rather than a dictionary as the number of members will typically be small (e.g. <20)
        private readonly BeforeAfterSerializationMixin beforeAfterMixin;
        private readonly Func<object> objectCreator;

        public MemberSerializerMixin(FudgeContext context, TypeData typeData, IEnumerable<TypeData.PropertyData> properties, BeforeAfterSerializationMixin beforeAfterMixin, Func<object> objectCreator)
        {
            this.context = context;
            this.typeData = typeData;
            this.beforeAfterMixin = beforeAfterMixin;
            this.objectCreator = objectCreator;

            members = ExtractProperties(properties);
        }

        #region IFudgeSerializationSurrogate Members

        public void Serialize(object obj, IAppendingFudgeFieldContainer msg, IFudgeSerializer serializer)
        {
            beforeAfterMixin.CallBeforeSerialize(obj);
            for (int i = 0; i < members.Length; i++)
            {
                var member = members[i];
                object val = member.Getter(obj);

                if (val != null)
                {
                    member.Serializer(member, val, msg, serializer);
                }
            }
            beforeAfterMixin.CallAfterSerialize(obj);
        }

        public object Deserialize(IFudgeFieldContainer msg, IFudgeDeserializer deserializer)
        {
            // Create without construction
            object newObj = objectCreator();

            // Register now in case any cycles in the object graph
            deserializer.Register(msg, newObj);

            // Deserialize the message
            beforeAfterMixin.CallBeforeDeserialize(newObj);
            int nFields = msg.GetNumFields();
            for (int i = 0; i < nFields; i++)
            {
                DeserializeField(msg.GetByIndex(i), deserializer, newObj);
            }
            beforeAfterMixin.CallAfterDeserialize(newObj);

            // And we're done
            return newObj;
        }

        #endregion

        private MemberData[] ExtractProperties(IEnumerable<TypeData.PropertyData> properties)
        {
            var members = new List<MemberData>();
            foreach (var prop in properties)
            {
                SerializerDelegate serializer = null;
                AdderDelegate adder = null;
                switch (prop.Kind)
                {
                    case TypeData.TypeKind.FudgePrimitive:
                        adder = this.PrimitiveAdd;
                        serializer = this.PrimitiveSerialize;
                        break;
                    case TypeData.TypeKind.Inline:
                        if (prop.HasPublicSetter)
                            adder = this.ObjectAdd;
                        else
                        {
                            // Must be a list
                            adder = ReflectionUtil.CreateInstanceMethodDelegate<AdderDelegate>(this, "ListAppend", new Type[] { prop.TypeData.SubType });
                        }
                        serializer = this.InlineSerialize;
                        break;
                    case TypeData.TypeKind.Reference:
                        adder = this.ObjectAdd;
                        serializer = this.ReferenceSerialize;
                        break;
                }
                members.Add(new MemberData(prop, serializer, adder));
            }
            return members.ToArray();
        }

        private bool DeserializeField(IFudgeField field, IFudgeDeserializer deserializer, object obj)
        {
            string fieldName = field.Name;
            if (fieldName == null)
                return false;           // Can't process without a name (yet)

            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].SerializedName == fieldName)
                {
                    members[i].Adder(members[i], obj, field, deserializer);
                    return true;
                }
            }
            return false;
        }

        private void PrimitiveSerialize(MemberData prop, object val, IAppendingFudgeFieldContainer msg, IFudgeSerializer serializer)
        {
            msg.Add(prop.SerializedName, null, prop.FudgeFieldType, val);
        }

        private void PrimitiveAdd(MemberData prop, object obj, IFudgeField field, IFudgeDeserializer deserializer)
        {
            object val = context.TypeHandler.ConvertType(field.Value, prop.Type);
            prop.Setter(obj, val);
        }

        private void InlineSerialize(MemberData prop, object val, IAppendingFudgeFieldContainer msg, IFudgeSerializer serializer)
        {
            serializer.WriteInline(msg, prop.SerializedName, val);
        }

        private void ObjectAdd(MemberData prop, object obj, IFudgeField field, IFudgeDeserializer deserializer)
        {
            // Handles both reference and inline
            object subObject = deserializer.FromField(field, prop.Type);
            prop.Setter(obj, subObject);
        }

        private void ListAppend<T>(MemberData prop, object obj, IFudgeField field, IFudgeDeserializer deserializer) where T : class
        {
            IList<T> newList = deserializer.FromField<IList<T>>(field);
            IList<T> currentList = (IList<T>)prop.Getter(obj);
            foreach (T item in newList)
                currentList.Add(item);
        }

        private void ReferenceSerialize(MemberData prop, object val, IAppendingFudgeFieldContainer msg, IFudgeSerializer serializer)
        {
            // Serializer will in-line or not as appropriate
            msg.Add(prop.SerializedName, null, prop.FudgeFieldType, val);
        }

        private delegate void SerializerDelegate(MemberData data, object obj, IAppendingFudgeFieldContainer msg, IFudgeSerializer serializer);
        private delegate void AdderDelegate(MemberData data, object obj, IFudgeField field, IFudgeDeserializer serializer);

        // We pull out all the fields we possibly need to minimize number of calls
        private sealed class MemberData
        {
            public MemberData(TypeData.PropertyData propertyData, SerializerDelegate serializer, AdderDelegate adder)
            {
                //this.PropertyData = propertyData;
                this.Serializer = serializer;
                this.Adder = adder;
                this.SerializedName = propertyData.SerializedName;
                this.Getter = propertyData.Getter;
                this.Setter = propertyData.Setter;
                this.Type = propertyData.Type;
                this.FudgeFieldType = propertyData.TypeData.FieldType;
            }

            //public readonly TypeData.PropertyData PropertyData;
            public readonly SerializerDelegate Serializer;
            public readonly AdderDelegate Adder;
            public readonly string SerializedName;
            public readonly Func<object, object> Getter;
            public readonly Action<object, object> Setter;
            public readonly Type Type;
            public readonly FudgeFieldType FudgeFieldType;
        }
    }
}
