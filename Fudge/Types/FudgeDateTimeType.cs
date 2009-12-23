/*
 * Copyright (C) 2009 - 2009 by OpenGamma Inc. and other contributors.
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
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Fudge.Taxon;

namespace Fudge.Types
{
    /// <summary>
    /// The type definition for a date/time value.
    /// </summary>
    public class FudgeDateTimeType : FudgeFieldType<FudgeDateTime>
    {
        // TODO t0rx 20091129 -- Control behaviour of converting to/from DateTime through context property

        /// <summary>A type definition for date/time values.</summary>
        public static readonly FudgeDateTimeType Instance = new FudgeDateTimeType();

        #region Magic numbers
        private const byte AccuracyMask = 0x1f;
        private const byte TimeZoneOption = 0x20;
        private const int OffsetUnitMinutes = 15;
        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public FudgeDateTimeType()
            : base(FudgeTypeDictionary.DATETIME_TYPE_ID, false, 12)
        {
        }

        /// <inheritdoc />
        public override FudgeDateTime ReadTypedValue(BinaryReader input, int dataSize)
        {
            byte options = input.ReadByte();
            byte offset = (byte)input.ReadSByte();
            long seconds = input.ReadInt64();
            int nanos = (int)input.ReadUInt32();

            FudgeDateTime.DateTimeAccuracy accuracy = (FudgeDateTime.DateTimeAccuracy)(options & AccuracyMask);
            bool hasOffset = (options & TimeZoneOption) != 0;

            return hasOffset ? new FudgeDateTime(seconds, nanos, offset * OffsetUnitMinutes, accuracy) : new FudgeDateTime(seconds, nanos, accuracy);
        }

        /// <inheritdoc />
        public override void WriteValue(BinaryWriter output, FudgeDateTime value)
        {
            byte options = (byte)value.Accuracy;
            sbyte offset = 0;
            if (value.HasOffset)
            {
                options |= TimeZoneOption;
                offset = (sbyte)(value.OffsetMinutes / OffsetUnitMinutes);
            }
            long seconds = value.SecondsSinceEpoch;
            uint nanos = (uint)value.Nanos;

            output.Write(options);
            output.Write(offset);
            output.Write(seconds);
            output.Write(nanos);
        }
    }
}
