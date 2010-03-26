/* <!--
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
using System.Reflection;
using System.Globalization;
using Fudge.Types;

namespace Fudge.Serialization.Reflection
{
    /// <summary>
    /// Surrogate for classes implementing <see cref="ISerializable"/> from .net serialization.
    /// </summary>
    /// <remarks>
    /// NOTE that when deserializing data that has not been serialized through <see cref="ISerializable"/> (e.g.
    /// recieved from another platform, fields which are <c>null</c> may have been omitted.  Using the normal
    /// methods of <see cref="SerializationInfo"/> such as <see cref="SerializationInfo.GetString"/> will throw
    /// an exception in this situation as the field is missing.  The only way around this is to use
    /// <see cref="SerializationInfo.GetEnumerator"/> to process the data instead.
    /// </remarks>
    public class DotNetSerializableSurrogate : IFudgeSerializationSurrogate
    {
        private readonly FudgeContext context;
        private readonly Type type;
        private readonly ConstructorInfo constructor;
        private readonly SerializationInfoMixin helper;

        /// <summary>
        /// Constructs a new <see cref="DotNetSerializableSurrogate"/>.
        /// </summary>
        /// <param name="context"><see cref="FudgeContext"/> to use.</param>
        /// <param name="typeData"><see cref="TypeData"/> for the type for this surrogate.</param>
        public DotNetSerializableSurrogate(FudgeContext context, TypeData typeData)
        {
            if (context == null)
                throw new ArgumentNullException("context");
            if (typeData == null)
                throw new ArgumentNullException("typeData");
            if (!CanHandle(typeData))
                throw new ArgumentOutOfRangeException("typeData", "DotNetSerializableSurrogate cannot handle " + typeData.Type.FullName);

            this.context = context;
            this.type = typeData.Type;
            this.constructor = FindConstructor(typeData);
            helper = new SerializationInfoMixin(context, typeData.Type, new BeforeAfterSerializationMixin(context, typeData));
        }

        /// <summary>
        /// Detects whether a given type can be serialized with this class.
        /// </summary>
        /// <param
        /// name="typeData">Type to test.</param>
        /// <returns><c>true</c> if this class can handle the type.</returns>
        public static bool CanHandle(TypeData typeData)
        {
            return typeof(ISerializable).IsAssignableFrom(typeData.Type) && FindConstructor(typeData) != null;
        }

        private static ConstructorInfo FindConstructor(TypeData typeData)
        {
            var constructor = typeData.Type.GetConstructor(BindingFlags.CreateInstance | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new Type[] { typeof(SerializationInfo), typeof(StreamingContext) }, null);
            return constructor;
        }

        #region IFudgeSerializationSurrogate Members

        /// <inheritdoc/>
        public void Serialize(object obj, IAppendingFudgeFieldContainer msg, IFudgeSerializer serializer)
        {
            helper.Serialize(msg, obj, (o, si, sc) => {((ISerializable)o).GetObjectData(si, sc);});
        }

        /// <inheritdoc/>
        public object Deserialize(IFudgeFieldContainer msg, IFudgeDeserializer deserializer)
        {
            return helper.Deserialize(msg, deserializer, (obj, si, sc) =>
                {
                    var args = new object[] { si, sc };
                    constructor.Invoke(obj, args);
                });

        }

        #endregion
    }
}
