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
using System.IO;

namespace Fudge.Tests.Unit.Types
{
    public class FudgeDateTimeTypeTest
    {
        [Fact]
        public void EncodeDecode()
        {
            var testDate = FudgeDateTime.Now;

            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            FudgeDateTimeType.Instance.WriteValue(writer, testDate, null);

            var stream2 = new MemoryStream(stream.ToArray());
            var reader = new BinaryReader(stream2);
            var resultDate = FudgeDateTimeType.Instance.ReadTypedValue(reader, 0, null);

            Assert.Equal(testDate, resultDate);
        }

        [Fact]
        public void HandlingDateTimeInMessages()
        {
            var dt = new DateTime(2005, 4, 3, 19, 0, 7, 123, DateTimeKind.Unspecified);

            FudgeMsg msg = new FudgeMsg();
            msg.Add("dateTime", dt);

            Assert.Same(FudgeDateTimeType.Instance, msg.GetByName("dateTime").Type);

            var dt2 = msg.GetValue<DateTime>("dateTime");
            Assert.Equal(dt, dt2);
        }
    }
}
