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
using Xunit;
using Fudge.Types;

namespace Fudge.Tests.Unit.Types
{
    public class FudgeDateTimeTest
    {
        [Fact]
        public void BasicTest()
        {
            var dt = new DateTime(2009, 11, 29, 7, 35, 18, 123);     // This defaults to an "unspecified" DateTimeKind
            var fdt = new FudgeDateTime(dt);
            Assert.Equal(Math.Floor(dt.Subtract(FudgeDateTime.Epoch).TotalSeconds), fdt.SecondsSinceEpoch);
            Assert.Equal(123 * 1000 * 1000, fdt.Nanos);
            Assert.False(fdt.HasOffset);

            var dt2 = fdt.ToDateTime(DateTimeKind.Unspecified);
            Assert.Equal(dt, dt2);
        }

        [Fact]
        public void DateTimeKinds()
        {
            var dt1 = new DateTime(1999, 10, 17, 2, 3, 4, DateTimeKind.Local);
            var dt2 = new DateTime(1999, 10, 17, 2, 3, 4, DateTimeKind.Unspecified);
            var dt3 = new DateTime(1999, 10, 17, 2, 3, 4, DateTimeKind.Utc);
            FudgeDateTime fdt1 = dt1;       // Implicit cast
            FudgeDateTime fdt2 = dt2;
            FudgeDateTime fdt3 = dt3;

            Assert.Equal(true, fdt1.HasOffset);
            Assert.Equal(TimeZoneInfo.Local.GetUtcOffset(dt1).TotalMinutes, fdt1.OffsetMinutes);
            Assert.Equal(dt1, fdt1.ToDateTime(DateTimeKind.Local));

            Assert.Equal(false, fdt2.HasOffset);
            Assert.Equal(dt2, fdt2.ToDateTime(DateTimeKind.Unspecified));

            Assert.Equal(true, fdt3.HasOffset);
            Assert.Equal(0, fdt3.OffsetMinutes);
            Assert.Equal(dt3, fdt3.ToDateTime(DateTimeKind.Utc));
        }

        [Fact]
        public void FriendlyConstructors()
        {
            var dt1 = new DateTime(2050, 7, 8, 23, 12, 7, 834, DateTimeKind.Unspecified);
            var fdt1 = new FudgeDateTime(2050, 7, 8, 23, 12, 7, 834000000);
            Assert.Equal(dt1, fdt1.ToDateTime(DateTimeKind.Unspecified));

            var dt2 = new DateTime(2050, 7, 8, 22, 12, 7, 834, DateTimeKind.Utc);
            var fdt2 = new FudgeDateTime(2050, 7, 8, 23, 12, 7, 834000000, 60);     // One hour ahead

            Assert.Equal(dt2, fdt2.ToDateTime(DateTimeKind.Utc));
        }

        [Fact]
        public void RawConstructors()
        {
            var testDate = new DateTime(1930, 1, 5, 12, 35, 17, 456, DateTimeKind.Utc);
            int epochSecs = (int)Math.Floor((testDate - FudgeDateTime.Epoch).TotalSeconds);
            int nanos = 456*1000*1000;

            var fdt1 = new FudgeDateTime(epochSecs, nanos);
            var fdt2 = new FudgeDateTime(epochSecs, nanos, 0);
            var fdt3 = new FudgeDateTime(epochSecs, nanos, 60);

            Assert.Equal(testDate, fdt1.ToDateTime(DateTimeKind.Utc));
            Assert.Equal(testDate, fdt2.ToDateTime(DateTimeKind.Utc));
            Assert.Equal(testDate - new TimeSpan(1, 0, 0), fdt3.ToDateTime(DateTimeKind.Utc));
        }

        [Fact]
        public void OffsetMustBe15Minutes()
        {
            Assert.Throws(typeof(ArgumentOutOfRangeException), () =>
            {
                new FudgeDateTime(1, 2, 3);
            });
        }
    }
}
