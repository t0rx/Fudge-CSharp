﻿/*
 * <!--
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
using Xunit;
using Fudge.Encodings;
using Fudge.Types;
using Fudge.Util;

namespace Fudge.Tests.Unit.Encodings
{
    public class FudgeJSONStreamReaderTest
    {
        private FudgeContext context = new FudgeContext();
        [Fact]
        public void StringField()
        {
            string json = @"{""name"" : ""fred""}";

            var msg = new FudgeJSONStreamReader(context, json).ReadMsg();

            Assert.Equal("fred", msg.GetString("name"));
        }

        [Fact]
        public void NumberFields()
        {
            string json = @"{""int"" : 1234, ""float"" : 123.45, ""exp"" : -123.45e4}";

            var msg = new FudgeJSONStreamReader(context, json).ReadMsg();

            Assert.Equal(1234, msg.GetInt("int"));
            Assert.Equal(123.45, msg.GetDouble("float"));
            Assert.Equal(-1234500, msg.GetDouble("exp"));
        }

        [Fact]
        public void BooleanFields()
        {
            string json = @"{""old"" : true, ""young"" : false}";

            var msg = new FudgeJSONStreamReader(context, json).ReadMsg();

            Assert.Equal(true, msg.GetBoolean("old"));
            Assert.Equal(false, msg.GetBoolean("young"));
        }

        [Fact]
        public void NullFields()
        {
            string json = @"{""old"" : null}";

            var msg = new FudgeJSONStreamReader(context, json).ReadMsg();

            Assert.Equal(IndicatorType.Instance, msg.GetByName("old").Value);
        }

        [Fact]
        public void SubObjects()
        {
            string json = @"{""inner"" : { ""a"" : 3, ""b"" : 17.3 }}";

            var msg = new FudgeJSONStreamReader(context, json).ReadMsg();

            var inner = msg.GetMessage("inner");
            Assert.NotNull(inner);
            Assert.Equal(3, inner.GetInt("a"));
            Assert.Equal(17.3, inner.GetDouble("b"));
        }

        [Fact]
        public void Arrays_FRN88()
        {
            string json = @"{""mixed"" : [ 1, 2, ""fred"", 2.3 ], ""numbers"" : [ 1, 2, 4 ], ""submsgs"" : [ { ""a"" : -3 }, { ""b"" : 28 } ], ""empty"" : [ ], ""bools"" : [ true, false ] }";

            var msg = new FudgeJSONStreamReader(context, json).ReadMsg();

            var mixed = msg.GetAllByName("mixed");
            Assert.Equal(4, mixed.Count);
            Assert.Equal(1, (sbyte)mixed[0].Value);
            Assert.Equal(2, (sbyte)mixed[1].Value);
            Assert.Equal("fred", (string)mixed[2].Value);
            Assert.Equal(2.3, (double)mixed[3].Value);

            // Numbers should collapse to a primitive array
            var numbers = msg.GetAllByName("numbers");
            Assert.Equal(1, numbers.Count);
            var arr = (int[])numbers[0].Value;
            Assert.Equal(1, arr[0]);
            Assert.Equal(2, arr[1]);
            Assert.Equal(4, arr[2]);
            
            var messages = msg.GetAllByName("submsgs");
            Assert.Equal(2, messages.Count);
            Assert.IsType<FudgeMsg>(messages[1].Value);
            var message2 = (FudgeMsg)messages[1].Value;
            Assert.Equal(28, (sbyte)message2.GetInt("b"));

            var empty = msg.GetAllByName("empty");
            Assert.Equal(0, empty.Count);

            // There is no bool[] Fudge field type, so it must be a repeating field
            var bools = msg.GetAllByName("bools");
            Assert.Equal(2, bools.Count);
            Assert.True((bool)bools[0].Value);
            Assert.False((bool)bools[1].Value);
        }

        [Fact]
        public void FieldNamesAndOrdinalsLogic_FRN86()
        {
            string jsonMixed = @"{""name"" : 1, ""2"" : 2}";
            string jsonEmpty = @"{"""" : 3}";

            var msg1 = new FudgeJSONStreamReader(context, jsonMixed).ReadMsg();
            var msg2 = new FudgeJSONStreamReader(context, new JSONSettings { NumbersAreOrdinals = false }, jsonMixed).ReadMsg();
            var msg3 = new FudgeJSONStreamReader(context, jsonEmpty).ReadMsg();

            Assert.Equal("FudgeMsg[name => 1, 2:  => 2]", msg1.ToString()); // Second went to ordinal because NumbersAreOrdinals is true
            Assert.Equal("FudgeMsg[name => 1, 2 => 2]", msg2.ToString());   // Both went to names because we set NumbersAreOrdinals to false
            Assert.Equal("FudgeMsg[ => 3]", msg3.ToString());               // Anonymous field
        }

        [Fact]
        public void UnicodeEscaping()
        {
            string json = @"{""name"" : ""fr\u0065d""}";

            var msg = new FudgeJSONStreamReader(context, json).ReadMsg();

            Assert.Equal("fred", msg.GetString("name"));
        }

        [Fact]
        public void BadToken()
        {
            string json = @"{""old"" : ajshgd}";
            Assert.Throws<FudgeParseException>(() => { new FudgeJSONStreamReader(context, json).ReadMsg(); });

            json = @"{abcd : 16}";      // Field names must be quoted
            Assert.Throws<FudgeParseException>(() => { new FudgeJSONStreamReader(context, json).ReadMsg(); });
        }

        [Fact]
        public void PrematureEOF()
        {
            string json = @"{""old"" : ";
            Assert.Throws<FudgeParseException>(() => { new FudgeJSONStreamReader(context, json).ReadMsg(); });
        }

        [Fact]
        public void MultipleMessages()
        {
            string json = @"{""name"" : ""fred""} {""number"" : 17}";
            var reader = new FudgeJSONStreamReader(context, json);
            var writer = new FudgeMsgStreamWriter();
            new FudgeStreamPipe(reader, writer).Process();

            Assert.Equal(2, writer.PeekAllMessages().Count);
            Assert.Equal("fred", writer.DequeueMessage().GetString("name"));
            Assert.Equal(17, writer.DequeueMessage().GetInt("number"));
        }

        [Fact]
        public void StringRepresentations_FRN89()
        {
            string json = FudgeJSONStreamWriterTest.StringsTestString;
            var reader = new FudgeJSONStreamReader(context, json);
            FudgeMsg msg = reader.ReadMsg();

            Assert.Equal(2.375e15f, msg.GetFloat("float"));
            Assert.Equal(1.234e50, msg.GetDouble("double"));
            Assert.Equal("abc\\\"de", msg.GetString("string"));
            Assert.Equal(new FudgeDate(20100202), msg.GetValue<FudgeDate>("date"));
            Assert.Equal(new FudgeTime(14, 1, 12, 123456789, 60, FudgeDateTimePrecision.Nanosecond), msg.GetValue<FudgeTime>("time"));
            Assert.Equal(new FudgeDateTime(1953, 7, 31, 0, 56, 23, 987654321, -60, FudgeDateTimePrecision.Nanosecond), msg.GetValue<FudgeDateTime>("datetime"));
        }
    }
}
