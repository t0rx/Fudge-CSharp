﻿/* <!--
 * Copyright (C) 2009 - 2010 by OpenGamma Inc. and other contributors.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 *     
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 * -->
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using Fudge.Types;
using Fudge.Encodings;
using System.Diagnostics;
using Fudge.Util;

namespace Fudge.Serialization
{
    /// <summary>
    /// Provides an implementation of <see cref="IFudgeDeserializer"/> used by the <see cref="FudgeSerializer"/>.
    /// </summary>
    /// <remarks>
    /// You should not need to use this class directly.
    /// </remarks>
    internal class FudgeDeserializationContext : IFudgeDeserializer
    {
        private readonly FudgeContext context;
        private readonly SerializationTypeMap typeMap;
        private readonly IFudgeStreamReader reader;
        private readonly List<MsgAndObj> objectList;        // Holds messages and the objects they've deserialized into (for use with references)
        private readonly ObjectIDGenerator msgToIndexMap = new ObjectIDGenerator();     // Note that this starts at one rather than zero
        private readonly Stack<State> stack;
        private readonly IFudgeTypeMappingStrategy typeMappingStrategy;

        public FudgeDeserializationContext(FudgeContext context, SerializationTypeMap typeMap, IFudgeStreamReader reader, IFudgeTypeMappingStrategy typeMappingStrategy)
        {
            this.context = context;
            this.typeMap = typeMap;
            this.reader = reader;
            this.objectList = new List<MsgAndObj>();
            this.stack = new Stack<State>();
            this.typeMappingStrategy = typeMappingStrategy;
        }

        public object DeserializeGraph()
        {
            // We simply return the first object
            LoadMessage();

            object result = GetFromRef(0, null);

            return result;
        }

        #region IFudgeDeserializer Members

        /// <inheritdoc/>
        public FudgeContext Context
        {
            get { return context; }
        }

        /// <inheritdoc/>
        public T FromField<T>(IFudgeField field)
        {
            return (T)FromField(field, typeof(T));
        }

        /// <inheritdoc/>
        public object FromField(IFudgeField field, Type type)
        {
            if (field == null)
                return null;

            if (field.Type == FudgeMsgFieldType.Instance)
            {
                // SubMsg
                var subMsg = (FudgeMsg)field.Value;
                bool firstTime;
                int refId = (int)msgToIndexMap.GetId(subMsg, out firstTime) - 1;
                Debug.Assert(!firstTime);
                Debug.Assert(objectList[refId].Msg == subMsg);

                return GetFromRef(refId, type);             // It is possible that we've already deserialized this, so we call GetFromRef rather than just processing the message
            }
            else if (field.Type == IndicatorFieldType.Instance)
            {
                // Indicator means null
                return null;
            }
            else
            {
                int relativeRef = Convert.ToInt32(field.Value);
                int refIndex = relativeRef + stack.Peek().RefId;

                return GetFromRef(refIndex, type);
            }
        }

        /// <inheritdoc/>
        public void Register(IFudgeFieldContainer msg, object obj)
        {
            State state = stack.Peek();
            if (msg != state.Msg)
            {
                throw new InvalidOperationException("Registering object of type " + obj.GetType() + " for message that it did not originate from.");
            }

            int index = state.RefId;
            Debug.Assert(objectList.Count > index);

            if (objectList[index].Obj != null)
            {
                throw new SerializationException("Attempt to register same deserialized object twice for type " + obj.GetType() + " refID=" + index);
            }

            objectList[index].Obj = obj;
        }

        #endregion

        private void LoadMessage()
        {
            if (reader.MoveNext() != FudgeStreamElement.MessageStart)
            {
                throw new SerializationException("No message start found in stream");
            }

            ProcessMessage();
        }

        /// <summary>
        /// Loads the message from the stream, at the same time remembering the indices of the
        /// submessage for use in references
        /// </summary>
        /// <returns></returns>
        private FudgeMsg ProcessMessage()
        {
            var message = context.NewMessage();
            MsgAndObj msgAndObj = new MsgAndObj { Msg = message };
            bool firstTime;
            int index = (int)msgToIndexMap.GetId(message, out firstTime) - 1;
            Debug.Assert(index == objectList.Count);
            objectList.Add(msgAndObj);

            while (reader.HasNext)
            {
                switch (reader.MoveNext())
                {
                    case FudgeStreamElement.MessageStart:
                        throw new SerializationException("Unexpected message start in stream");
                    case FudgeStreamElement.SubmessageFieldEnd:
                    case FudgeStreamElement.MessageEnd:
                        return message;                 // We're done now
                    case FudgeStreamElement.SimpleField:
                        message.Add(reader.FieldName, reader.FieldOrdinal, reader.FieldType, reader.FieldValue);
                        break;
                    case FudgeStreamElement.SubmessageFieldStart:
                        message.Add(reader.FieldName, reader.FieldOrdinal, FudgeMsgFieldType.Instance, ProcessMessage());
                        break;
                    default:
                        break;      // Unknown
                }
            }

            throw new SerializationException("Premature end of stream encountered");
        }

        /// <summary>
        /// Get the real object from a reference ID
        /// </summary>
        /// <remarks>It is possible that the reference has not yet been deserialized if (for example) it is a child of
        /// an object that has evolved elsewhere but where in this version that field has not been read.</remarks>
        private object GetFromRef(int? refId, Type hintType)
        {
            if (refId == null)
            {
                return null;
            }

            int index = refId.Value;

            if (index < 0 || index >= objectList.Count)
            {
                throw new FudgeRuntimeException("Attempt to deserialize object reference with ID " + refId + " but only " + objectList.Count + " objects in stream so far.");
            }

            var msgAndObj = objectList[index];
            if (msgAndObj.Obj == null)
            {
                // Not processed yet
                DeserializeFromMessage(index, hintType);

                Debug.Assert(msgAndObj.Obj != null);
            }
            return msgAndObj.Obj;
        }

        private object DeserializeFromMessage(int index, Type hintType)
        {
            Debug.Assert(objectList[index].Obj == null);
            Debug.Assert(objectList[index].Msg != null);

            var message = objectList[index].Msg;
            objectList[index].Msg = null;                           // Just making sure we don't try to process the same one twice

            Type objectType = GetObjectType(index, hintType, message);
            var surrogate = GetSurrogate(objectType);

            var state = new State(message, index);
            stack.Push(state);
            object result = surrogate.Deserialize(message, this);
            stack.Pop();

            // Make sure the object was registered by the surrogate
            if (objectList[index].Obj == null || objectList[index].Obj != result)
            {
                throw new SerializationException("Object not registered during deserialization with type " + result.GetType());
            }

            return result;
        }

        private IFudgeSerializationSurrogate GetSurrogate(Type objectType)
        {
            int typeId = typeMap.GetTypeId(objectType);
            var surrogate = typeMap.GetSurrogate(typeId);
            if (surrogate == null)
            {
                throw new SerializationException("Type ID " + typeId + " not registered with serialization type map");
            }
            return surrogate;
        }

        private Type GetObjectType(int refId, Type hintType, FudgeMsg message)
        {
            Type objectType = null;
            IFudgeField typeField = message.GetByOrdinal(FudgeSerializer.TypeIdFieldOrdinal);
            if (typeField == null)
            {
                if (hintType == null)
                {
                    throw new FudgeRuntimeException("Serialized object has no type ID");
                }

                objectType = hintType;
            }
            else if (typeField.Type == StringFieldType.Instance)
            {
                // It's the first time we've seen this type in this graph, so it contains the type names
                string typeName = (string)typeField.Value;
                objectType = typeMappingStrategy.GetType(typeName);
                if (objectType == null)
                {
                    var typeNames = message.GetAllValues<string>(FudgeSerializer.TypeIdFieldOrdinal);
                    for (int i = 1; i < typeNames.Count; i++)       // 1 because we've already tried the first
                    {
                        objectType = typeMappingStrategy.GetType(typeNames[i]);
                        if (objectType != null)
                            break;                   // Found it
                    }
                }
            }
            else
            {
                // We've got a type field, but it's not a string so it must be a reference back to where we last saw the type
                int previousObjId = refId + Convert.ToInt32(typeField.Value);

                if (previousObjId < 0 || previousObjId >= refId)
                {
                    throw new FudgeRuntimeException("Illegal relative type ID in sub-message: " + typeField.Value);
                }

                if (objectList[previousObjId].Obj != null)
                {
                    // Already deserialized it
                    objectType = objectList[previousObjId].Obj.GetType();
                }
                else
                {
                    // Scan it's fields rather than deserializing (we don't have the same type hint as might be in its correct location)
                    objectType = GetObjectType(previousObjId, hintType, objectList[previousObjId].Msg);
                }
            }
            return objectType;
        }

        private struct State
        {
            public readonly FudgeMsg Msg;
            public readonly int RefId;

            public State(FudgeMsg msg, int refId)
            {
                this.Msg = msg;
                this.RefId = refId;
            }
        }

        private sealed class MsgAndObj
        {
            public FudgeMsg Msg;
            public object Obj;
        }
    }
}
