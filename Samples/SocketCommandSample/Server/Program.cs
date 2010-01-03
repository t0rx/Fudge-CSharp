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
using Fudge;

namespace Server
{
    /// <summary>
    /// Example program that runs commands sent to it as FudgeMsgs from a client.
    /// </summary>
    class Program
    {
        private FudgeContext context;
        private bool shutDown;

        static void Main(string[] args)
        {
            new Program().Run();
        }

        void Run()
        {
            context = new FudgeContext();

            using (Socket socket = StartListening())
            {
                while (!shutDown)
                {
                    // Wait for a new connection to process
                    using (var connectionSocket = socket.Accept())
                    {
                        // Now handle it (note we won't accept any others until we're done with this one
                        ProcessConnectionWithLoop(connectionSocket);
                    }
                }
            }
        }

        private void ProcessConnectionWithLoop(Socket socket)
        {
            Console.WriteLine("Got connection from " + socket.RemoteEndPoint);
            try
            {
                // Create our output channel and send a welcome message
                var stream = new NetworkStream(socket);
                var output = new FudgeXmlStreamWriter(stream, "msg") { AutoFlushOnMessageEnd = true };
                SendSuccess(output, "Welcome to " + socket.LocalEndPoint + ", type ? for help");

                // Create an input channel
                var input = new FudgeXmlStreamReader(stream);

                while (true)
                {
                    var msg = input.ReadMsg();            // This will block until a whole message is read
                    if (msg == null)
                    {
                        // Done
                        Console.WriteLine("Data from " + socket.RemoteEndPoint.ToString() + " finished.");
                        break;
                    }
                    if (!ProcessMessage(msg, output, socket))
                    {
                        // Done
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private bool ProcessMessage(FudgeMsg msg, IFudgeStreamWriter writer, Socket socket)
        {
            // We've received a message, so deal with it

            bool keepGoing = true;
            string command = msg.GetString("command");
            Console.WriteLine("Received command " + command);
            if (command == null)
            {
                SendError(writer, "No command found");
            }
            else
            {
                switch (command.ToLower())
                {
                    case "?":
                    case "help":
                        DoHelp(writer);
                        break;
                    case "echo":
                        DoEcho(writer, msg);
                        break;
                    case "exit":
                        SendSuccess(writer, "Closing connection.");
                        SendClose(writer);
                        Console.WriteLine("Closing connection from " + socket.RemoteEndPoint.ToString());
                        socket.Close();
                        keepGoing = false;
                        break;
                    case "shutdown":
                        SendSuccess(writer, "Shutting down.");
                        SendClose(writer);
                        socket.Close();
                        Console.WriteLine("Shutting down.");
                        shutDown = true;
                        keepGoing = false;
                        break;
                    case "time":
                        DoTime(writer);
                        break;
                    default:
                        SendError(writer, "Unknown command: " + command);
                        break;
                }
            }
            return keepGoing;
        }

        private void DoTime(IFudgeStreamWriter writer)
        {
            SendSuccess(writer, DateTime.Now.ToString("u"));
        }

        private void DoEcho(IFudgeStreamWriter writer, FudgeMsg msg)
        {
            var argFields = msg.GetAllByName("args");
            string returnMsg;
            if (argFields.Count == 0)
                returnMsg = "echo";
            else
            {
                string[] args = argFields.Select(field => field.Value.ToString()).ToArray();
                returnMsg = string.Join(" ", args);
            }
            SendSuccess(writer, returnMsg);
        }

        private void DoHelp(IFudgeStreamWriter writer)
        {
            string message = "Available commands:\n";
            message += "  ?         - Show help\n";
            message += "  echo text - Send text back to client\n";
            message += "  exit      - Close the client\n";
            message += "  shutdown  - Shut the server down\n";
            message += "  time      - Show the current time\n";
            SendSuccess(writer, message);
        }

        private void SendSuccess(IFudgeStreamWriter writer, string message)
        {
            var msg = new FudgeMsg(context, new Field("status", "success"),
                                            new Field("output", message));
            writer.WriteMsg(msg);
        }

        private void SendClose(IFudgeStreamWriter writer)
        {
            var msg = new FudgeMsg(context, new Field("status", "close"));
            writer.WriteMsg(msg);
        }

        private void SendError(IFudgeStreamWriter writer, string message)
        {
            var msg = new FudgeMsg(context, new Field("status", "fail"),
                                            new Field("error",
                                                new Field("message", message)));
            writer.WriteMsg(msg);
        }

        static Socket StartListening()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Listen on all network interfaces, using any free port
            IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
            socket.Bind(ep);
            socket.Listen(0);       // Don't allow any pending connections if we've got one

            Console.WriteLine("Listening on port {0}...", ((IPEndPoint)socket.LocalEndPoint).Port);
            return socket;
        }
    }
}
