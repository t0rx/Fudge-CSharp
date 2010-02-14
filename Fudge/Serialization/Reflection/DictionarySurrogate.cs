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

namespace Fudge.Serialization.Reflection
{
    public class DictionarySurrogate : CollectionSurrogateBase
    {
        private const int keysOrdinal = 1;
        private const int valuesOrdinal = 2;

        public DictionarySurrogate(FudgeContext context, TypeData typeData)
            : base(context, typeData, "SerializeDictionary", "DeserializeDictionary")
        {
        }

        public static bool CanHandle(TypeData typeData)
        {
            return IsDictionary(typeData.Type);
        }

        public static bool IsDictionary(Type type)
        {
            Type keyType, valueType;
            return IsDictionary(type, out keyType, out valueType);
        }

        public static bool IsDictionary(Type type, out Type keyType, out Type valueType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            {
                // It's a dictionary
                keyType = type.GetGenericArguments()[0];
                valueType = type.GetGenericArguments()[1];
                return true;
            }

            foreach (var interfaceType in type.GetInterfaces())
            {
                if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                {
                    // It's a dictionary
                    keyType = type.GetGenericArguments()[0];
                    valueType = type.GetGenericArguments()[1];
                    return true;
                }
            }

            keyType = null;
            valueType = null;
            return false;
        }

        protected void SerializeDictionary<K, V>(object obj, IFudgeSerializer serializer)
        {
            var dictionary = (IDictionary<K, V>)obj;

            SerializeList(dictionary.Keys, serializer, typeData.SubTypeData.Kind, keysOrdinal);
            SerializeList(dictionary.Values, serializer, typeData.SubType2Data.Kind, valuesOrdinal);    // Guaranteed to be matching order
        }

        protected object DeserializeDictionary<K, V>(IFudgeFieldContainer msg, IFudgeDeserializer deserializer)
            where K : class
            where V : class
        {
            var keys = new List<K>();
            var values = new List<V>();

            foreach (var field in msg)
            {
                if (field.Ordinal == 1)
                {
                    keys.Add(DeserializeField<K>(field, deserializer, typeData.SubTypeData.Kind));
                }
                else if (field.Ordinal == 2)
                {
                    values.Add(DeserializeField<V>(field, deserializer, typeData.SubType2Data.Kind));
                }
                else
                {
                    throw new FudgeRuntimeException("Sub-message doesn't contain a map (bad field " + field + ")");
                }
            }

            int nVals = Math.Min(keys.Count, values.Count);         // Consistent with Java implementation, rather than throwing an exception if they don't match
            var result = new Dictionary<K, V>(nVals);
            for (int i = 0; i < nVals; i++)
            {
                result[keys[i]] = values[i];
            }

            return result;
        }
    }
}