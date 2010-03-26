using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using Fudge.Types;
using System.Globalization;

namespace Fudge.Serialization.Reflection
{
    /// <summary>
    /// Helps out with surrogates that are working through SerializationInfo-based methods (i.e. the original .net serialization stuff)
    /// </summary>
    internal sealed class SerializationInfoMixin
    {
        private readonly FormatterConverter formatterConverter = new FormatterConverter();
        private readonly StreamingContext streamingContext;
        private readonly Type type;
        private readonly BeforeAfterSerializationMixin beforeAfterMixin;

        public SerializationInfoMixin(FudgeContext context, Type type, BeforeAfterSerializationMixin beforeAfterMixin)
        {
            this.streamingContext = new StreamingContext(StreamingContextStates.Persistence, context);
            this.type = type;
            this.beforeAfterMixin = beforeAfterMixin;
        }

        public void Serialize(IAppendingFudgeFieldContainer msg, object obj, Action<object, SerializationInfo, StreamingContext> serializeMethod)
        {
            var si = new SerializationInfo(type, formatterConverter);

            beforeAfterMixin.CallBeforeSerialize(obj);
            serializeMethod(obj, si, streamingContext);
            beforeAfterMixin.CallAfterSerialize(obj);

            // Pull the data out of the SerializationInfo and add to the message
            var e = si.GetEnumerator();
            while (e.MoveNext())
            {
                string name = e.Name;
                object val = e.Value;

                if (val != null)
                {
                    msg.Add(name, val);
                }
                else
                {
                    // .net binary serialization still outputs the member with a null, so we have to do
                    // the same (using Indicator), otherwise deserialization blows up.
                    msg.Add(name, IndicatorType.Instance);
                }
            }
        }

        public object Deserialize(IFudgeFieldContainer msg, IFudgeDeserializer deserializer, Action<object, SerializationInfo, StreamingContext> deserializeMethod)
        {
            // Create without construction and register before we call the constructor in case there are any cycles
            object result = FormatterServices.GetUninitializedObject(type);
            deserializer.Register(msg, result);

            var converter = new DeserializingFormatterConverter(deserializer);
            var si = new SerializationInfo(this.type, converter);
            PopulateSerializationInfo(si, msg);

            beforeAfterMixin.CallBeforeDeserialize(result);
            deserializeMethod(result, si, streamingContext);
            beforeAfterMixin.CallAfterDeserialize(result);

            return result;
        }

        public void PopulateSerializationInfo(SerializationInfo si, IFudgeFieldContainer msg)
        {
            foreach (var field in msg)
            {
                if (field.Name != null)
                {
                    if (field.Type == IndicatorFieldType.Instance)
                    {
                        // This is actually a null
                        si.AddValue(field.Name, null);
                    }
                    else
                    {
                        si.AddValue(field.Name, field.Value);
                    }
                }
            }
        }

        private class DeserializingFormatterConverter : IFormatterConverter
        {
            private readonly IFudgeDeserializer deserializer;

            public DeserializingFormatterConverter(IFudgeDeserializer deserializer)
            {
                this.deserializer = deserializer;
            }

            #region IFormatterConverter Members

            public object Convert(object value, TypeCode typeCode)
            {
                return System.Convert.ChangeType(value, typeCode, CultureInfo.InvariantCulture);
            }

            public object Convert(object value, Type type)
            {
                var fieldType = deserializer.Context.TypeHandler.DetermineTypeFromValue(value);
                var field = new TemporaryField(fieldType, value);
                object result = deserializer.FromField(field, type);
                return result;
            }

            public bool ToBoolean(object value)
            {
                return System.Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            }

            public byte ToByte(object value)
            {
                return System.Convert.ToByte(value, CultureInfo.InvariantCulture);
            }

            public char ToChar(object value)
            {
                return System.Convert.ToChar(value, CultureInfo.InvariantCulture);
            }

            public DateTime ToDateTime(object value)
            {
                return System.Convert.ToDateTime(value, CultureInfo.InvariantCulture);
            }

            public decimal ToDecimal(object value)
            {
                return System.Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            }

            public double ToDouble(object value)
            {
                return System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }

            public short ToInt16(object value)
            {
                return System.Convert.ToInt16(value, CultureInfo.InvariantCulture);
            }

            public int ToInt32(object value)
            {
                return System.Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }

            public long ToInt64(object value)
            {
                return System.Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }

            public sbyte ToSByte(object value)
            {
                return System.Convert.ToSByte(value, CultureInfo.InvariantCulture);
            }

            public float ToSingle(object value)
            {
                return System.Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }

            public string ToString(object value)
            {
                return System.Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            public ushort ToUInt16(object value)
            {
                return System.Convert.ToUInt16(value, CultureInfo.InvariantCulture);
            }

            public uint ToUInt32(object value)
            {
                return System.Convert.ToUInt32(value, CultureInfo.InvariantCulture);
            }

            public ulong ToUInt64(object value)
            {
                return System.Convert.ToUInt64(value, CultureInfo.InvariantCulture);
            }

            #endregion

            private class TemporaryField : IFudgeField
            {
                private readonly FudgeFieldType type;
                private readonly object value;

                public TemporaryField(FudgeFieldType type, object value)
                {
                    this.type = type;
                    this.value = value;
                }

                #region IFudgeField Members

                public FudgeFieldType Type
                {
                    get { return type; }
                }

                public object Value
                {
                    get { return value; }
                }

                public short? Ordinal
                {
                    get { return null; }
                }

                public string Name
                {
                    get { return null; }
                }

                #endregion
            }
        }
    }
}
