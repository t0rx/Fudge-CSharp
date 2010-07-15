/**
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
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using System.Diagnostics;
using Fudge.Serialization;
using System.IO;
using Fudge.Encodings;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Fudge.Tests.Perf
{
    public class SerializationComparison
    {
        private const int defaultCycles = 10000;
        private const int padWidth = 24;
        private FudgeContext context = new FudgeContext();

        [Fact(Skip = "Performance testing not part of normal unit test run")]
        public void TickInVariousWays()
        {
            RunTickInVariousWays(defaultCycles);
        }

        public void RunTickInVariousWays(int nCycles)
        {
            Console.Out.WriteLine(String.Format("Cycles: {0}", nCycles));
            Cycle("Bean", new TickBean(), nCycles);
            Cycle("Immutable", new ImmutableBean(1, 2, 3, 4, 5), nCycles);
            Cycle("[Serializable]", new SerializableBean(), nCycles);
            Cycle("ISerializable", new ISerializableBean(), nCycles);
            Cycle("[DataContract]", new DataContractBean(), nCycles);
            Cycle("FudgeSerializable", new FudgeSerializableBean(), nCycles);
            DotNetCycle(".net [Serializable]", new SerializableBean(), nCycles);
            DotNetCycle(".net ISerializable", new ISerializableBean(), nCycles);
            DotNetDataContractCycle(".net [DataContract]", new DataContractBean(), nCycles);
        }

        private void Cycle(string msg, object obj, int nCycles)
        {
            Console.Out.Write((msg + ":").PadRight(padWidth));
            var serializer = new FudgeSerializer(context);
            serializer.SerializeToMsg(obj);     // Just get the reflection stuff out of the way
            var stopWatch = new Stopwatch();
            var stream = new MemoryStream();
            var writer = new FudgeEncodedStreamWriter(context, stream);
            var reader = new FudgeEncodedStreamReader(context, stream);
            stopWatch.Start();
            for (int i = 0; i < nCycles; i++)
            {
                stream.Position = 0;
                serializer.Serialize(writer, obj);
                stream.Flush();
                stream.Position = 0;
                var obj2 = serializer.Deserialize(reader, null);
            }
            stopWatch.Stop();
            double speed = (double)Stopwatch.Frequency * nCycles / stopWatch.ElapsedTicks;

            Console.Out.WriteLine(String.Format("{0:F0}/s", speed));
        }

        private void DotNetCycle(string msg, object obj, int nCycles)
        {
            Console.Out.Write((msg + ":").PadRight(padWidth));
            var serializer = new BinaryFormatter();

            var stopWatch = new Stopwatch();
            var stream = new MemoryStream();
            serializer.Serialize(stream, obj);     // Just get the reflection stuff out of the way
            stopWatch.Start();
            for (int i = 0; i < nCycles; i++)
            {
                stream.Position = 0;
                serializer.Serialize(stream, obj);
                stream.Flush();
                stream.Position = 0;
                var obj2 = serializer.Deserialize(stream);
            }
            stopWatch.Stop();
            double speed = (double)Stopwatch.Frequency * nCycles / stopWatch.ElapsedTicks;

            Console.Out.WriteLine(String.Format("{0:F0}/s", speed));
        }

        private void DotNetDataContractCycle(string msg, object obj, int nCycles)
        {
            Console.Out.Write((msg + ":").PadRight(padWidth));
            var serializer = new DataContractSerializer(typeof(DataContractBean));

            var stopWatch = new Stopwatch();
            var stream = new MemoryStream();
            stopWatch.Start();
            for (int i = 0; i < nCycles; i++)
            {
                stream.Position = 0;
                serializer.WriteObject(stream, obj);
                stream.Flush();
                stream.Position = 0;
                var obj2 = serializer.ReadObject(stream);
            }
            stopWatch.Stop();
            double speed = (double)Stopwatch.Frequency * nCycles / stopWatch.ElapsedTicks;

            Console.Out.WriteLine(String.Format("{0:F0}/s", speed));
        }

        private class TickBean
        {
            public double Bid { get; set; }
            public double Ask { get; set; }
            public double BidVolume { get; set; }
            public double AskVolume { get; set; }
            public long Timestamp { get; set; }
        }

        private class ImmutableBean
        {
            private readonly double bid;
            private readonly double ask;
            private readonly double bidVolume;
            private readonly double askVolume;
            private readonly long timestamp;

            public ImmutableBean(double bid, double ask, double bidVolume, double askVolume, long timestamp)
            {
                this.bid = bid;
                this.ask = ask;
                this.bidVolume = bidVolume;
                this.askVolume = askVolume;
                this.timestamp = timestamp;
            }

            public double Bid { get { return bid; } }
            public double Ask { get { return ask; } }
            public double BidVolume { get { return bidVolume; } }
            public double AskVolume { get { return askVolume; } }
            public long Timestamp { get { return timestamp; } }
        }

        private class FudgeSerializableBean : IFudgeSerializable
        {
            public double Bid { get; set; }
            public double Ask { get; set; }
            public double BidVolume { get; set; }
            public double AskVolume { get; set; }
            public long Timestamp { get; set; }

            #region IFudgeSerializable Members

            public void Serialize(IAppendingFudgeFieldContainer msg, IFudgeSerializer serializer)
            {
                msg.Add("Bid", Bid);
                msg.Add("Ask", Ask);
                msg.Add("BidVolume", BidVolume);
                msg.Add("AskVolume", AskVolume);
                msg.Add("Timestamp", Timestamp);
            }

            public void Deserialize(IFudgeFieldContainer msg, IFudgeDeserializer deserializer)
            {
                Bid = msg.GetDouble("Bid") ?? 0.0;
                Ask = msg.GetDouble("Ask") ?? 0.0;
                BidVolume = msg.GetDouble("BidVolume") ?? 0.0;
                AskVolume = msg.GetDouble("AskVolume") ?? 0.0;
                Timestamp = msg.GetLong("Timestamp") ?? 0;
            }

            #endregion
        }

        [Serializable]
        private class SerializableBean
        {
            public double Bid { get; set; }
            public double Ask { get; set; }
            public double BidVolume { get; set; }
            public double AskVolume { get; set; }
            public long Timestamp { get; set; }
        }

        [Serializable]
        private class ISerializableBean : ISerializable
        {
            public ISerializableBean()
            {
            }

            protected ISerializableBean(SerializationInfo info, StreamingContext context)
            {
                Bid = info.GetDouble("Bid");
                Ask = info.GetDouble("Ask");
                BidVolume = info.GetDouble("BidVolume");
                AskVolume = info.GetDouble("AskVolume");
                Timestamp = info.GetInt64("Timestamp");
            }

            public double Bid { get; set; }
            public double Ask { get; set; }
            public double BidVolume { get; set; }
            public double AskVolume { get; set; }
            public long Timestamp { get; set; }

            #region ISerializable Members

            public void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                info.AddValue("Bid", Bid);
                info.AddValue("Ask", Ask);
                info.AddValue("BidVolume", BidVolume);
                info.AddValue("AskVolume", AskVolume);
                info.AddValue("Timestamp", Timestamp);
            }

            #endregion
        }

        [DataContract]
        private class DataContractBean
        {
            [DataMember]
            public double Bid { get; set; }
            [DataMember]
            public double Ask { get; set; }
            [DataMember]
            public double BidVolume { get; set; }
            [DataMember]
            public double AskVolume { get; set; }
            [DataMember]
            public long Timestamp { get; set; }
        }

    }
}
