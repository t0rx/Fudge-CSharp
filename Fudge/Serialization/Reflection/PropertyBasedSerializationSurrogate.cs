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
using System.Diagnostics;
using System.Runtime.Serialization;

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
        private readonly MemberSerializerMixin memberSerializer;

        /// <summary>
        /// Constructs a new <see cref="PropertyBasedSerializationSurrogate"/>.
        /// </summary>
        /// <param name="context"><see cref="FudgeContext"/> to use.</param>
        /// <param name="typeData"><see cref="TypeData"/> for the type for this surrogate.</param>
        public PropertyBasedSerializationSurrogate(FudgeContext context, TypeData typeData)
        {
            if (context == null)
                throw new ArgumentNullException("context");
            if (typeData == null)
                throw new ArgumentNullException("typeData");
            if (!CanHandle(typeData))
                throw new ArgumentOutOfRangeException("typeData", "PropertyBasedSerializationSurrogate cannot handle " + typeData.Type.FullName);

            Debug.Assert(typeData.DefaultConstructor != null);      // Should have been caught in CanHandle()

            var constructor = typeData.DefaultConstructor;
            this.memberSerializer = new MemberSerializerMixin(context, typeData, typeData.Properties, new BeforeAfterSerializationMixin(context, typeData), () => constructor.Invoke(null));
        }

        /// <summary>
        /// Determines whether this kind of surrogate can handle a given type
        /// </summary>
        /// <param name="cache"><see cref="TypeDataCache"/> for type data.</param>
        /// <param name="fieldNameConvention">Convention to use for renaming fields.</param>
        /// <param name="type">Type to test.</param>
        /// <returns>True if this kind of surrogate can handle the type.</returns>
        public static bool CanHandle(TypeDataCache cache, FudgeFieldNameConvention fieldNameConvention, Type type)
        {
            return CanHandle(cache.GetTypeData(type, fieldNameConvention));
        }

        internal static bool CanHandle(TypeData typeData)
        {
            if (typeData.DefaultConstructor == null)
                return false;
            foreach (var prop in typeData.Properties)
            {
                switch (prop.Kind)
                {
                    case TypeData.TypeKind.FudgePrimitive:
                    case TypeData.TypeKind.Inline:
                    case TypeData.TypeKind.Reference:
                        // OK
                        break;
                    default:
                        // Unknown
                        return false;
                }

                if (!prop.HasPublicSetter && !ListSurrogate.IsList(prop.Type))      // Special case for lists, which we can just append to if no setter present
                {
                    // Not bean-style
                    return false;
                }
            }
            return true;
        }

        #region IFudgeSerializationSurrogate Members

        /// <inheritdoc/>
        public void Serialize(object obj, IAppendingFudgeFieldContainer msg, IFudgeSerializer serializer)
        {
            memberSerializer.Serialize(obj, msg, serializer);
        }

        /// <inheritdoc/>
        public object Deserialize(IFudgeFieldContainer msg, IFudgeDeserializer deserializer)
        {
            return memberSerializer.Deserialize(msg, deserializer);
        }

        #endregion
    }
}
