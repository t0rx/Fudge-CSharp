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
 * limitations under the License.
 * -->
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using Fudge.Encodings;
using System.Threading;
using Fudge;
using System.IO;

namespace Client
{
    /// <summary>
    /// Example program that connects to a server and sends it commands using FudgeMsgs.
    /// </summary>
    class Program
    {
        private FudgeContext context = new FudgeContext();
        private Socket socket;
        private bool closed;

        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine("Syntax: client.exe host port");
                return;
            }
            new Program().Run(args[0], int.Parse(args[1]));
        }

        private void Run(string host, int port)
        {
            // Connect to the server
            var stream = Connect(host, port);

            // Start the thread to receive messages from the server
            new Thread(() => { RunReceiver(stream); }).Start();

            // Create our output writer onto the stream to send messages to the server
            var output = new FudgeXmlStreamWriter(stream, "msg") { AutoFlushOnMessageEnd = true };

            // Now just loop getting input from the console until we're done
            while (!closed)
            {
                string line = Console.ReadLine();
                if (!closed)
                    SendCommand(output, line);
            }
        }

        private void SendCommand(IFudgeStreamWriter output, string commandLine)
        {
            // Package the input into a message and send it
            var msg = new FudgeMsg(context);

            var bits = commandLine.Split(' ');
            msg.Add("command", bits[0]);

            for (int i = 1; i < bits.Length; i++)
            {
                msg.Add("args", bits[i]);
            }

            output.WriteMsg(msg);
        }

        /// <summary>
        /// Receives messages from the server and handles them.
        /// </summary>
        /// <param name="stream"></param>
        private void RunReceiver(Stream stream)
        {
            // Here we demonstrate a slightly different way of receiving the messages than we're using
            // in the server.  In this case we set up a pipe to automatically generate FudgMsgs from
            // the incoming data then raise an event to tell us when we have a complete message to
            // process, whereas the server pulls a single message at a time directly from the input
            // reader.
            
            var inputReader = new FudgeXmlStreamReader(stream);                 // We're reading XML from the stream...
            var messages = new FudgeMsgStreamWriter(context);                   // ...and turning it into messages...
            var pipe = new FudgeStreamPipe(inputReader, messages);              // ...as they arrive (using a pipe)...
            pipe.MessageProcessed += () => { HandleMessage(pipe, messages); };  // ...then processing them.

            // Run until there's no more data
            pipe.Process();
        }

        private void HandleMessage(FudgeStreamPipe pipe, FudgeMsgStreamWriter messageSource)
        {
            // Get the message we've just received and output it
            FudgeMsg msg = messageSource.DequeueMessage();

            // Sample server puts the overall status in a field called "status"
            switch (msg.GetString("status"))
            {
                case "success":
                    // Any additional message is in a field called "output"
                    Console.WriteLine("[" + msg.GetString("output") + "]");
                    break;
                case "fail":
                    {
                        // May be more complex information in an "error" field, so just output as is
                        IFudgeFieldContainer errorMessage = msg.GetMessage("error");
                        Console.WriteLine("[FAIL: " + errorMessage + "]");
                        break;
                    }
                case "close":
                    // Server closing the connection
                    Console.WriteLine("[Server sent close]");
                    pipe.Abort();           // So it won't try to read any more data
                    closed = true;
                    socket.Close();
                    break;
                default:
                    Console.WriteLine("[Unknown message from server:\n" + msg.ToString() + "\n]");
                    break;
            }
        }

        private NetworkStream Connect(string host, int port)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(host, port);
            Console.WriteLine("Connected to " + socket.RemoteEndPoint + " local endpoint is " + socket.LocalEndPoint);

            var stream = new NetworkStream(socket);
            return stream;
        }
    }
}
