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
using System.Diagnostics;
using System.Reflection;

namespace Fudge.Serialization
{
    public class SerializableSurrogate : IFudgeSerializationSurrogate
    {
        private readonly Type type;
        private readonly ConstructorInfo constructor;

        public SerializableSurrogate(Type type)
        {
            if (type == null || !typeof(IFudgeSerializable).IsAssignableFrom(type))
            {
                throw new ArgumentOutOfRangeException("type");
            }
            this.type = type;
            this.constructor = type.GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (constructor == null)
            {
                throw new ArgumentOutOfRangeException("type", "Type " + type.FullName + " does not have a public default constructor.");
            }
        }

        #region IFudgeSerializationSurrogate Members

        /// <inheritdoc/>
        public void Serialize(object obj, IFudgeSerializer serializer)
        {
            IFudgeSerializable ser = (IFudgeSerializable)obj;
            ser.Serialize(serializer);
        }

        /// <inheritdoc/>
        public object BeginDeserialize(IFudgeDeserializer deserializer, int dataVersion)
        {
            IFudgeSerializable result = (IFudgeSerializable)constructor.Invoke(null);
            deserializer.Register(result);
            result.BeginDeserialize(deserializer, dataVersion);
            return result;
        }

        /// <inheritdoc/>
        public bool DeserializeField(IFudgeDeserializer deserializer, IFudgeField field, int dataVersion, object state)
        {
            IFudgeSerializable obj = (IFudgeSerializable)state;
            return obj.DeserializeField(deserializer, field, dataVersion);
        }

        /// <inheritdoc/>
        public object EndDeserialize(IFudgeDeserializer deserializer, int dataVersion, object state)
        {
            IFudgeSerializable obj = (IFudgeSerializable)state;
            obj.EndDeserialize(deserializer, dataVersion);
            return obj;
        }

        #endregion
    }
}