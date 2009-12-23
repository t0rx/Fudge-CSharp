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

namespace Fudge.Types
{
    /// <summary>
    /// <c>FudgeDateTime</c> encapsulates the Fudge representation of a date and time.
    /// </summary>
    /// <remarks>
    /// Unlike the .net <see cref="DateTime"/> class, <c>FudgeDateTime</c> has the ability to
    /// specify that the date and time are relative to a particular timezone offset.  It also
    /// carries an indicator of how accurate the date/time is - e.g. just the date part, or
    /// to the nearest second, etc.
    /// </remarks>
    public sealed class FudgeDateTime
    {
        private readonly int nanos;
        private readonly long secondsSinceEpoch;
        private readonly int offset;        // In units of 15 mins
        private readonly bool hasOffset;
        private readonly DateTimeAccuracy accuracy;
        /// <summary><c>Epoch</c> is the base date from which dates are relatve - 1st January 1970.</summary>
        public static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        #region Magic numbers
        private static readonly long EpochTicks = Epoch.Ticks;
        private const int NanosPerTick = 100;
        private const int NanosPerSecond = 1000 * 1000 * 1000;
        private const int TicksPerSecond = NanosPerSecond / NanosPerTick;
        private const int OffsetUnitMinutes = 15;
        private const int OffsetUnitsPerHour = 60 / OffsetUnitMinutes;
        private const int OffsetUnitSeconds = OffsetUnitMinutes * 60;
        #endregion

        /// <summary>
        /// Constructs a <c>FudgeDateTime</c> based on a .net <see cref="DateTime"/>.
        /// </summary>
        /// <param name="dt">.net <see cref="DateTime"/> value to use.</param>
        /// <remarks>
        /// This will base the offset of the <c>FudgeDateTime</c> on the <see cref="DateTimeKind"/> of the <see cref="DateTime"/>.
        /// </remarks>
        public FudgeDateTime(DateTime dt)
            : this(dt, DateTimeAccuracy.Nanosecond)
        {
        }

        /// <summary>
        /// Constructs a <c>FudgeDateTime</c> based on a .net <see cref="DateTime"/>, specifying the <see cref="Accuracy"/> of the <c>DateTime</c>.
        /// </summary>
        /// <param name="dt">.net <see cref="DateTime"/> value to use.</param>
        /// <param name="accuracy">Indicates the <see cref="Accuracy"/> of the <see cref="DateTime"/>.</param>
        /// <remarks>
        /// <para>This will base the offset of the <c>FudgeDateTime</c> on the <see cref="DateTimeKind"/> of the <see cref="DateTime"/>.</para>
        /// <para>The <see cref="Accuracy"/> is used to indicate (for example) that only the date portion of this <c>DateTime</c> is meaningful.</para>
        /// </remarks>
        public FudgeDateTime(DateTime dt, DateTimeAccuracy accuracy)
        {
            this.nanos = GetRawNanos(dt);
            long rawSeconds = GetRawSecondsSinceEpoch(dt);

            switch (dt.Kind)
            {
                case DateTimeKind.Utc:
                    this.secondsSinceEpoch = rawSeconds;
                    this.hasOffset = true;
                    this.offset = 0;
                    break;
                case DateTimeKind.Unspecified:
                    this.secondsSinceEpoch = rawSeconds;
                    this.hasOffset = false;
                    this.offset = 0;
                    break;
                case DateTimeKind.Local:
                    this.secondsSinceEpoch = rawSeconds;
                    this.hasOffset = true;
                    var tzOffset = TimeZoneInfo.Local.GetUtcOffset(dt);
                    this.offset = tzOffset.Hours * OffsetUnitsPerHour + tzOffset.Minutes / OffsetUnitMinutes;
                    break;
                default:
                    // They've added a new value to the DateTimeKind enum
                    throw new NotSupportedException("DateTimeKind " + dt.Kind + " not supported by Fudge");
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="year"></param>
        /// <param name="month"></param>
        /// <param name="day"></param>
        /// <param name="hour"></param>
        /// <param name="minute"></param>
        /// <param name="second"></param>
        /// <param name="nanos"></param>
        /// <param name="accuracy"></param>
        public FudgeDateTime(int year, int month, int day, int hour, int minute, int second, int nanos, DateTimeAccuracy accuracy)
        {
            // TODO t0rx 20091129 -- Expand to handle dates outside the DateTime range
            this.nanos = nanos;
            this.secondsSinceEpoch = GetRawSecondsSinceEpoch(new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc));
            this.hasOffset = false;
            this.offset = 0;
            this.accuracy = accuracy;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="year"></param>
        /// <param name="month"></param>
        /// <param name="day"></param>
        /// <param name="hour"></param>
        /// <param name="minute"></param>
        /// <param name="second"></param>
        /// <param name="nanos"></param>
        /// <param name="offsetMinutes"></param>
        /// <param name="accuracy"></param>
        public FudgeDateTime(int year, int month, int day, int hour, int minute, int second, int nanos, int offsetMinutes, DateTimeAccuracy accuracy)
        {
            // TODO t0rx 20091129 -- Expand to handle dates outside the DateTime range
            if (offsetMinutes % OffsetUnitMinutes != 0)
            {
                throw new ArgumentOutOfRangeException("offsetMinutes", offsetMinutes + " must be divisible by 15.");
            }
            this.nanos = nanos;
            this.secondsSinceEpoch = GetRawSecondsSinceEpoch(new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc));
            this.hasOffset = true;
            this.hasOffset = true;
            this.offset = offsetMinutes / OffsetUnitMinutes;
            this.accuracy = accuracy;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="secondsSinceEpoch">Number of seconds after (or before) 1970-01-01 00:00:00</param>
        /// <param name="nanos">Fraction of a second, expressed in nanoseconds.</param>
        /// <param name="accuracy">Accuracy of this date/time - see <see cref="DateTimeAccuracy"/>.</param>
        public FudgeDateTime(long secondsSinceEpoch, int nanos, DateTimeAccuracy accuracy)
        {
            this.nanos = nanos;
            this.secondsSinceEpoch = secondsSinceEpoch;
            this.hasOffset = false;
            this.offset = 0;
            this.accuracy = accuracy;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="secondsSinceEpoch">Number of seconds after (or before) 1970-01-01 00:00:00</param>
        /// <param name="nanos">Fraction of a second, expressed in nanoseconds.</param>
        /// <param name="offsetMinutes">Number of minutes that this timezone is offset from UTC.</param>
        /// <param name="accuracy">Accuracy of this date/time - see <see cref="DateTimeAccuracy"/>.</param>
        public FudgeDateTime(long secondsSinceEpoch, int nanos, int offsetMinutes, DateTimeAccuracy accuracy)
        {
            if (offsetMinutes % OffsetUnitMinutes != 0)
            {
                throw new ArgumentOutOfRangeException("offsetMinutes", offsetMinutes + " must be divisible by 15.");
            }
            this.nanos = nanos;
            this.secondsSinceEpoch = secondsSinceEpoch;
            this.hasOffset = true;
            this.offset = offsetMinutes / OffsetUnitMinutes;
            this.accuracy = accuracy;
        }

        /// <summary>
        /// Gets the fraction of a second for this <c>FudgeDateTime</c> represented as nanoseconds.
        /// </summary>
        public int Nanos
        {
            get { return nanos; }
        }

        /// <summary>
        /// Gets the total number of seconds since the epoch (midnight on 1 Jan 1970).
        /// </summary>
        public long SecondsSinceEpoch
        {
            get { return secondsSinceEpoch; }
        }

        /// <summary>
        /// Indicates whether the <c>FudgeDateTime</c> is representing a time offset from UTC.
        /// </summary>
        /// <remarks>Use <see cref="OffsetMinutes"/> to retrieve the offset.</remarks>
        public bool HasOffset
        {
            get { return hasOffset; }
        }

        /// <summary>
        /// Gets the timezone offset from UTC in minutes, if any.
        /// </summary>
        /// <remarks>Only valid if <see cref="HasOffset"/> is <c>true</c>.</remarks>
        public int OffsetMinutes
        {
            get { return offset * OffsetUnitMinutes; }
        }

        /// <summary>
        /// Gets the <see cref="Accuracy"/> of this datetime.
        /// </summary>
        public DateTimeAccuracy Accuracy
        {
            get { return accuracy; }
        }

        /// <summary>
        /// Converts the <c>FudgeDateTime</c> to a .net <see cref="DateTime"/>.
        /// </summary>
        /// <param name="kind">Controls how the <c>FudgeDateTime</c> is converted.  See <see cref="DateTimeKind"/> for more info on the different types of .net <see cref="DateTime"/>.</param>
        /// <returns><c>DateTime</c> converted to a .net <see cref="DateTime"/>.</returns>
        public DateTime ToDateTime(DateTimeKind kind)
        {
            switch (kind)
            {
                case DateTimeKind.Unspecified:
                    {
                        long ticks = secondsSinceEpoch * TicksPerSecond + nanos / NanosPerTick + EpochTicks;     // May overflow
                        return new DateTime(ticks, DateTimeKind.Unspecified);
                    }
                case DateTimeKind.Utc:
                    {
                        long utcSeconds = secondsSinceEpoch;
                        if (hasOffset)
                        {
                            utcSeconds -= offset * OffsetUnitSeconds;
                        }
                        long ticks = utcSeconds * TicksPerSecond + nanos / NanosPerTick + EpochTicks;     // May overflow
                        return new DateTime(ticks, DateTimeKind.Utc);
                    }
                case DateTimeKind.Local:
                    {
                        // Let .net do the hard work
                        var utcDT = ToDateTime(DateTimeKind.Utc);
                        return utcDT.ToLocalTime();
                    }
                default:
                    throw new NotSupportedException("DateTimeKind " + kind + " not supported by Fudge");
            }
        }

        /// <summary>
        /// Converts from a .net <see cref="DateTime"/> to a <see cref="FudgeDateTime"/>.
        /// </summary>
        /// <param name="dt">The <see cref="DateTime"/> to convert.</param>
        /// <returns>Equivalent <see cref="FudgeDateTime"/>.</returns>
        /// <remarks>The <see cref="FudgeDateTime"/> will take into account whether the .net <see cref="DateTime"/> is
        /// local time, UTC or without a timezone, and map to the equivalent offsets.</remarks>
        public static implicit operator FudgeDateTime(DateTime dt)
        {
            return new FudgeDateTime(dt);
        }

        /// <summary>
        /// Gets a <see cref="FudgeDateTime"/>object that is set to the current date and time on
        /// this computer, expressed with the local time offset.
        /// </summary>
        /// <returns>A FudgeDateTime whose value is the current local date and time.</returns>
        public static FudgeDateTime Now
        {
            get { return new FudgeDateTime(DateTime.Now); }
        }

        /// <summary>
        /// <c>DateTimeAccuracy</c> expresses the resolution of a <see cref="FudgeDateTime"/> object.
        /// </summary>
        public enum DateTimeAccuracy
        {
            /// <summary>The object is accurate to the nearest nanosecond.</summary>
            Nanosecond,
            /// <summary>The object is accurate to the nearest microsecond.</summary>
            Microsecond,
            /// <summary>The object is accurate to the nearest millisecond.</summary>
            Millisecond,
            /// <summary>The object is accurate to the nearest second.</summary>
            Second,
            /// <summary>The object is accurate to the nearest minute.</summary>
            Minute,
            /// <summary>The object is accurate to the nearest hour.</summary>
            Hour,
            /// <summary>The object is accurate to the nearest day.</summary>
            Day,
            /// <summary>The object is accurate to the nearest month.</summary>
            Month,
            /// <summary>The object is accurate to the nearest year.</summary>
            Year,
            /// <summary>The object is accurate to the nearest century.</summary>
            Century
        }

        private readonly string[] AccuracyFormatters =
            {
                "yyyy-MM-dd HH:mm:ss",              // Special case for nanos as DateTime doesn't go that far
                "yyyy-MM-dd HH:mm:ss.ffffff",
                "yyyy-MM-dd HH:mm:ss.fff",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm",
                "yyyy-MM-dd HH",
                "yyyy-MM-dd",
                "yyyy-MM",
                "yyyy",
                "cc"                                // Special case for centuries as not supported by DateTime
            };

        /// <inheritdoc />
        public override string ToString()
        {
            string format = AccuracyFormatters[(int)accuracy];
            var dt = ToDateTime(DateTimeKind.Unspecified);
            string result;
            switch (accuracy)
            {
                case DateTimeAccuracy.Nanosecond:
                    result = dt.ToString(format) + "." + nanos.ToString("D9");
                    break;
                case DateTimeAccuracy.Century:
                    result = (dt.Year / 100).ToString("D2");
                    break;
                default:
                    result = dt.ToString(format);
                    break;
            }
            if (hasOffset && accuracy < DateTimeAccuracy.Day)
            {
                string offsetFormat = offset < 0 ? " {0:00}:{1:00}" : " +{0:00}:{1:00}";
                string offsetString = string.Format(offsetFormat, OffsetMinutes / 60, Math.Abs(OffsetMinutes) % 60);
                result += offsetString;
            }
            return result;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            FudgeDateTime other = obj as FudgeDateTime;
            if (other == null) return false;

            return this.secondsSinceEpoch == other.secondsSinceEpoch &&
                this.nanos == other.nanos &&
                this.offset == other.offset &&
                this.hasOffset == other.hasOffset &&
                this.accuracy == other.accuracy;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return secondsSinceEpoch.GetHashCode() ^ nanos.GetHashCode();
        }

        private static long GetRawSecondsSinceEpoch(DateTime dt)
        {
            long ticksSinceEpoch = dt.Ticks - EpochTicks;
            return ticksSinceEpoch / TicksPerSecond;
        }

        private static int GetRawNanos(DateTime dt)
        {
            var ticks = dt.Ticks;
            return (int)(ticks % TicksPerSecond) * NanosPerTick;
        }
    }
}
