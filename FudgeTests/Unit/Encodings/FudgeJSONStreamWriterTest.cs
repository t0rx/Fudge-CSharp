/*
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
 * -->
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fudge.Encodings;
using System.IO;
using Xunit;
using Fudge.Types;

namespace Fudge.Tests.Unit.Encodings
{
    public class FudgeJSONStreamWriterTest
    {
        private readonly FudgeContext context = new FudgeContext();

        [Fact]
        public void SimpleExample()
        {
            var msg = context.NewMessage(new Field("outer",
                                            new Field("a", 7),
                                            new Field("b", "fred")));
            var stringWriter = new StringWriter();
            var writer = new FudgeJSONStreamWriter(context, stringWriter);
            writer.WriteMsg(msg);
            string s = stringWriter.ToString();

            AssertEqualsNoWhiteSpace("{\"outer\" : {\"a\" : 7, \"b\" : \"fred\"} }", s);
        }

        [Fact]
        public void FieldsWithSameNameBecomeArrays()
        {
            var msg = context.NewMessage(new Field("outer",
                                            new Field("a", false),
                                            new Field("b", 17.3),
                                            new Field("a", "test")));

            var stringWriter = new StringWriter();
            var writer = new FudgeJSONStreamWriter(context, stringWriter);
            writer.WriteMsg(msg);
            string s = stringWriter.ToString();

            AssertEqualsNoWhiteSpace("{\"outer\" : {\"a\" : [false, \"test\"], \"b\" : 17.3} }", s);
        }

        [Fact]
        public void EscapingAndEncodingStrings()
        {
            var msg = context.NewMessage(new Field("s", "\\\b\f\t\n\r\"\u0001"));

            var stringWriter = new StringWriter();
            var writer = new FudgeJSONStreamWriter(context, stringWriter);
            writer.WriteMsg(msg);
            string s = stringWriter.ToString();

            AssertEqualsNoWhiteSpace(@"{""s"" : ""\\\b\f\t\n\r\""\u0001""}", s);
        }

        [Fact]
        public void ArraysOfPrimitiveTypes()
        {
            var msg = context.NewMessage(new Field("bytes", new byte[] {123, 76, 2, 3}),
                                         new Field("shorts", new short[] {10, 11, 12}),
                                         new Field("ints", new int[] {13, 14, 15}),
                                         new Field("longs", new long[] {16, 17, 18}),
                                         new Field("floats", new float[] {1.2f, 3.4f, 5.6f}),
                                         new Field("doubles", new double[] {-1.2, -3.4, -5.6}));

            var stringWriter = new StringWriter();
            var writer = new FudgeJSONStreamWriter(context, stringWriter);
            writer.WriteMsg(msg);
            string s = stringWriter.ToString();

            var testString = "{\"bytes\" : [123, 76, 2, 3]," +
                              "\"shorts\" : [10, 11, 12]," +
                              "\"ints\" : [13, 14, 15]," +
                              "\"longs\" : [16, 17, 18]," +
                              "\"floats\" : [1.2, 3.4, 5.6]," +
                              "\"doubles\" : [-1.2, -3.4, -5.6]}";
            AssertEqualsNoWhiteSpace(testString, s);
        }

        [Fact]
        public void ArraysOfObjects()
        {
            var msg = context.NewMessage(new Field("a",
                                            new Field("x", 1),
                                            new Field("y", 2)),
                                         new Field("a",
                                             new Field("z", 3)));

            var stringWriter = new StringWriter();
            var writer = new FudgeJSONStreamWriter(context, stringWriter);
            writer.WriteMsg(msg);
            string s = stringWriter.ToString();

            AssertEqualsNoWhiteSpace("{\"a\" : [ {\"x\" : 1, \"y\" : 2}, {\"z\" : 3} ] }", s);
        }

        [Fact]
        public void RoundTrip()
        {
            var msg = context.NewMessage(new Field("outer",
                                            new Field("a", 7),
                                            new Field("b", "fred")));
            var stringWriter = new StringWriter();
            var writer = new FudgeJSONStreamWriter(context, stringWriter);
            writer.WriteMsg(msg);
            string s = stringWriter.ToString();

            var reader = new FudgeJSONStreamReader(context, s);
            var msg2 = reader.ReadMsg();

            FudgeUtils.AssertAllFieldsMatch(msg, msg2);
        }

        [Fact]
        public void SubMessageAsValueHandled()
        {
            var subMsg = context.NewMessage(new Field("a", 1));
            var stringWriter = new StringWriter();
            var writer = new FudgeJSONStreamWriter(context, stringWriter);

            writer.StartMessage();
            writer.WriteField("test", null, null, subMsg);
            writer.EndMessage();

            string s = stringWriter.ToString();
            AssertEqualsNoWhiteSpace("{\"test\" : {\"a\" : 1} }", s);
        }

        [Fact]
        public void IndicatorWrittenAsNull()
        {
            var msg = context.NewMessage(new Field("test", IndicatorType.Instance));

            var stringWriter = new StringWriter();
            var writer = new FudgeJSONStreamWriter(context, stringWriter);
            writer.WriteMsg(msg);
            string s = stringWriter.ToString();
            AssertEqualsNoWhiteSpace("{\"test\" : null }", s);
        }

        [Fact]
        public void ConstuctorRangeChecking()
        {
            var stringWriter = new StringWriter();
            Assert.Throws<ArgumentNullException>(() => new FudgeJSONStreamWriter(null, stringWriter));
            Assert.Throws<ArgumentNullException>(() => new FudgeJSONStreamWriter(context, null));
        }

        [Fact]
        public void DepthChecks()
        {
            var writer = new FudgeJSONStreamWriter(context, new StringWriter());

            Assert.Throws<InvalidOperationException>(() => writer.StartSubMessage("test", null));
            Assert.Throws<InvalidOperationException>(() => writer.WriteField("test", null, null, "test"));
            Assert.Throws<InvalidOperationException>(() => writer.EndSubMessage());
            Assert.Throws<InvalidOperationException>(() => writer.EndMessage());

            writer.StartMessage();
            Assert.Throws<InvalidOperationException>(() => writer.StartMessage());
            Assert.Throws<InvalidOperationException>(() => writer.EndSubMessage());

            writer.StartSubMessage("test", null);
            Assert.Throws<InvalidOperationException>(() => writer.EndMessage());
        }

        [Fact]
        public void FieldNamesAndOrdinalsLogic_FRN86()
        {
            var stringWriter = new StringWriter();
            var writer = new FudgeJSONStreamWriter(context, stringWriter);

            var msg = context.NewMessage(new Field(1, "ord"),
                                         new Field("A", "name"),
                                         new Field("B", 2, "name and ord"),
                                         new Field(null, null, "empty"));
            writer.WriteMsg(msg);
            AssertEqualsNoWhiteSpace("{\"1\" : \"ord\", \"A\" : \"name\", \"B\" : \"name and ord\", \"\" : \"empty\"}", stringWriter.ToString());

            // Now try it preferring ordinals to names
            var stringWriter2 = new StringWriter();
            var settings = new JSONSettings() { PreferFieldNames = false };
            var writer2 = new FudgeJSONStreamWriter(context, settings, stringWriter2);
            writer2.WriteMsg(msg);
            AssertEqualsNoWhiteSpace("{\"1\" : \"ord\", \"A\" : \"name\", \"2\" : \"name and ord\", \"\" : \"empty\"}", stringWriter2.ToString());
        }

        [Fact]
        public void StringRepresentations_FRN89()
        {
            var stringWriter = new StringWriter();
            var writer = new FudgeJSONStreamWriter(context, stringWriter);
            writer.WriteMsg(StringsTestMsg);

            // Float/double as per IEEE7 54-2008, string escaped as per RFC 3339, dates and times as per RFC 3339.
            AssertEqualsNoWhiteSpace(StringsTestString, stringWriter.ToString());
        }

        internal static readonly FudgeMsg StringsTestMsg = new FudgeMsg(new Field("float", 2.375e15f),
                                                                       new Field("double", 1.234e50),
                                                                       new Field("string", "abc\\\"de"),
                                                                       new Field("date", new FudgeDate(20100202)),
                                                                       new Field("time", new FudgeTime(14, 1, 12, 123456789, 60, FudgeDateTimePrecision.Nanosecond)),
                                                                       new Field("datetime", new FudgeDateTime(1953, 7, 31, 0, 56, 23, 987654321, -60, FudgeDateTimePrecision.Nanosecond)));
        internal static readonly string StringsTestString = @"{""float"" : 2.375E+15, 
                                                               ""double"" : 1.234E+50,
                                                               ""string"" : ""abc\\\""de"",
                                                               ""date"" : ""2010-02-02"",
                                                               ""time"" : ""14:01:12.123456789+01:00"",
                                                               ""datetime"" : ""1953-07-31T00:56:23.987654321-01:00""}";

        #region Helpers
        [Fact]
        public void TestEqualsNoWhiteSpaceWorks()
        {
            // Test our test works
            AssertEqualsNoWhiteSpace("\ta b\r\nc", " \rab\n\tc ");
            Assert.Throws<Xunit.Sdk.EqualException>(() => AssertEqualsNoWhiteSpace("ab", "ac"));
        }

        private void AssertEqualsNoWhiteSpace(string a, string b)
        {
            var whiteSpace = new string[] { " ", "\t", "\r", "\n" };
            foreach (var s in whiteSpace)
            {
                a = a.Replace(s, "");
                b = b.Replace(s, "");
            }

            Assert.Equal(a, b);
        }
        #endregion
    }
}
