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
using System.Reflection;

namespace Fudge.Serialization.Reflection
{
    /// <summary>
    /// Used to handle classes which are bean-style (i.e. have matching getters and setters and a default constructor).
    /// </summary>
    /// <remarks>
    /// For lists (i.e. properties that implement <see cref="IList{T}"/>, no setter is required but the object itself must construct the list
    /// so that values can be added to it.
    /// </remarks>
    public class PropertyBasedSerializationSurrogate : IFudgeSerializationSurrogate
    {
        private readonly FudgeContext context;
        private readonly Type type;
        private readonly ConstructorInfo constructor;
        private readonly Dictionary<string, TypeData.PropertyData> propMap = new Dictionary<string, TypeData.PropertyData>();

        public PropertyBasedSerializationSurrogate(FudgeContext context, Type type)
            : this(context, new TypeData(context, type))
        {
        }

        internal PropertyBasedSerializationSurrogate(FudgeContext context, TypeData typeData)
        {
            if (context == null)
                throw new ArgumentNullException("context");
            if (typeData == null)
                throw new ArgumentNullException("typeData");
            if (!CanHandle(typeData))
                throw new ArgumentOutOfRangeException("typeData", "PropertyBasedSerializationSurrogate cannot handle " + typeData.Type.FullName);

            this.context = context;
            this.type = typeData.Type;

            if (typeData.DefaultConstructor == null)
            {
                throw new FudgeRuntimeException("Type " + type.FullName + " cannot use reflection-based serialization as it does not have a public default constructor.");
            }
            this.constructor = typeData.DefaultConstructor;

            // Pull out all the properties
            foreach (var prop in typeData.Properties)
            {
                propMap.Add(prop.SerializedName, prop);
            }
        }

        public static bool CanHandle(FudgeContext context, Type type)
        {
            return CanHandle(new TypeData(context, type));
        }

        internal static bool CanHandle(TypeData typeData)
        {
            if (typeData.DefaultConstructor == null)
                return false;
            foreach (var prop in typeData.Properties)
            {
                switch (prop.Type)
                {
                    case TypeData.PropertyType.FudgePrimitive:
                    case TypeData.PropertyType.List:
                    case TypeData.PropertyType.Object:
                        // OK
                        break;
                    default:
                        // Unknown
                        return false;
                }

                if (!prop.HasPublicSetter && prop.Type != TypeData.PropertyType.List)
                {
                    // Not bean-style
                    return false;
                }
            }
            return true;
        }

        #region IFudgeSerializationSurrogate Members

        /// <inheritdoc/>
        public void Serialize(object obj, IFudgeSerializer serializer)
        {
            foreach (var entry in propMap)
            {
                string name = entry.Key;
                TypeData.PropertyData prop = entry.Value;
                prop.Serializer(obj, serializer);
            }
        }

        /// <inheritdoc/>
        public object BeginDeserialize(IFudgeDeserializer deserializer, int dataVersion)
        {
            object newObj = constructor.Invoke(null);
            deserializer.Register(newObj);
            return newObj;
        }

        /// <inheritdoc/>
        public bool DeserializeField(IFudgeDeserializer deserializer, IFudgeField field, int dataVersion, object state)
        {
            TypeData.PropertyData prop;
            if (propMap.TryGetValue(field.Name, out prop))
            {
                prop.Adder(state, field, deserializer);
                return true;
            }
            return false;
        }

        /// <inheritdoc/>
        public object EndDeserialize(IFudgeDeserializer deserializer, int dataVersion, object state)
        {
            return state;
        }

        #endregion
    }
}