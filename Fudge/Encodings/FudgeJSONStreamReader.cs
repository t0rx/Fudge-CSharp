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
using System.IO;
using System.Diagnostics;
using Fudge.Types;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Fudge.Encodings
{
    /// <summary>
    /// Implementation of <see cref="IFudgeStreamReader"/> that reads JSON messages
    /// </summary>
    /// <remarks>
    /// Parsing based on definition of syntax at http://www.json.org/ as of 2009-12-18.
    /// </remarks>
    public class FudgeJSONStreamReader : FudgeStreamReaderBase
    {
        private readonly FudgeContext context;
        private readonly TextReader reader;
        private Queue<Token> tokenQueue = new Queue<Token>();
        private bool done = false;
        private Stack<State> stack = new Stack<State>();
        private readonly JSONSettings settings;
        private static readonly Regex ordinalRegEx = new Regex("^-?[0-9]+$", RegexOptions.Compiled);

        /// <summary>
        /// Constructs a <see cref="FudgeJSONStreamReader"/> on a given <see cref="TextReader"/>.
        /// </summary>
        /// <param name="context">Context to control behaviours.</param>
        /// <param name="reader"><see cref="TextReader"/> providing the data.</param>
        public FudgeJSONStreamReader(FudgeContext context, TextReader reader)
        {
            if (context == null)
                throw new ArgumentNullException("context");
            if (reader == null)
                throw new ArgumentNullException("reader");

            this.context = context;
            this.reader = reader;
            this.settings = (JSONSettings)context.GetProperty(JSONSettings.JSONSettingsProperty) ?? new JSONSettings();
        }

        /// <summary>
        /// Constructs a <see cref="FudgeJSONStreamReader"/> on a given <see cref="TextReader"/>.
        /// </summary>
        /// <param name="context">Context to control behaviours.</param>
        /// <param name="settings">Settings to override any in the <see cref="FudgeContext"/>.</param>
        /// <param name="reader"><see cref="TextReader"/> providing the data.</param>
        public FudgeJSONStreamReader(FudgeContext context, JSONSettings settings, TextReader reader)
        {
            if (context == null)
                throw new ArgumentNullException("context");
            if (reader == null)
                throw new ArgumentNullException("reader");
            if (settings == null)
                throw new ArgumentNullException("settings");

            this.context = context;
            this.reader = reader;
            this.settings = settings;
        }

        /// <summary>
        /// Constructs a <see cref="FudgeJSONStreamReader"/> using a <c>string</c> for the underlying data.
        /// </summary>
        /// <param name="context">Context to control behaviours.</param>
        /// <param name="text">Text containing JSON message.</param>
        /// <example>This example shows a simple JSON string being converted into a <see cref="FudgeMsg"/> object:
        /// <code>
        /// string json = @"{""name"" : ""fred""}";
        /// FudgeMsg msg = new FudgeJSONStreamReader(json).ReadToMsg();
        /// </code>
        /// </example>
        public FudgeJSONStreamReader(FudgeContext context, string text)
            : this(context, new StringReader(text))
        {
        }

        /// <summary>
        /// Constructs a <see cref="FudgeJSONStreamReader"/> using a <c>string</c> for the underlying data.
        /// </summary>
        /// <param name="context">Context to control behaviours.</param>
        /// <param name="settings">Settings to override any in the <see cref="FudgeContext"/>.</param>
        /// <param name="text">Text containing JSON message.</param>
        /// <example>This example shows a simple JSON string being converted into a <see cref="FudgeMsg"/> object:
        /// <code>
        /// string json = @"{""name"" : ""fred""}";
        /// FudgeMsg msg = new FudgeJSONStreamReader(json).ReadToMsg();
        /// </code>
        /// </example>
        public FudgeJSONStreamReader(FudgeContext context, JSONSettings settings, string text)
            : this(context, settings, new StringReader(text))
        {
        }

        #region IFudgeStreamReader Members

        /// <inheritdoc/>
        public override bool HasNext
        {
            get
            {
                return !done && (PeekNextToken() != Token.EOF);
            }
        }

        /// <inheritdoc/>
        public override FudgeStreamElement MoveNext()
        {
            Token token;

            if (Depth == 0)
            {
                // Have to start with a {
                token = GetNextToken();
                if (token != Token.BeginObject)
                {
                    throw new FudgeParseException("Expected '{' at start of JSON stream");
                }
                stack.Push(State.InObject);
                CurrentElement = FudgeStreamElement.MessageStart;
                return CurrentElement;
            }

            while (true)
            {
                token = GetNextToken();

                if (token == Token.EOF)
                    throw new FudgeParseException("Premature EOF in JSON stream");

                var top = stack.Peek();
                string jsonFieldName;
                if (top.IsInArray)
                {
                    // If we're directly inside an array then the field name was outside
                    jsonFieldName = top.ArrayFieldName;
                }
                else
                {
                    if (token == Token.EndObject)
                    {
                        HandleObjectEnd(token);
                        return CurrentElement;
                    }

                    // Not at the end of an object, so must be a field
                    if (token.Type != TokenType.String)
                        throw new FudgeParseException("Expected field name in JSON stream, got " + token + "");
                    jsonFieldName = token.StringData;

                    token = GetNextToken();
                    if (token != Token.NameSeparator)
                        throw new FudgeParseException("Expected ':' in JSON stream for field \"" + FieldName + "\", got " + token + "");

                    token = GetNextToken();
                }
                HandleJSONFieldName(jsonFieldName);
                bool producedValue = HandleValue(token);        // It's possible that we don't actually come up with a field - e.g. for an emtpy array
                if (producedValue)
                    break;
            }
            return CurrentElement;
        }

        #endregion

        private void HandleObjectEnd(Token token)
        {
            stack.Pop();
            if (stack.Count == 0)
            {
                CurrentElement = FudgeStreamElement.MessageEnd;
            }
            else
            {
                CurrentElement = FudgeStreamElement.SubmessageFieldEnd;

                var top = stack.Peek();
                if (top.IsInArray)
                {
                    SkipCommaPostValue(token.ToString());
                }
            }
        }

        private void HandleJSONFieldName(string jsonFieldName)
        {
            FieldName = null;
            FieldOrdinal = null;
            if (jsonFieldName == "")
            {
                // Anonymous field - leave blank
            }
            else if (!settings.NumbersAreOrdinals)
            {
                FieldName = jsonFieldName;
            }
            else if (ordinalRegEx.IsMatch(jsonFieldName))
            {
                // It's a number, so do as an ordinal
                FieldOrdinal = Int32.Parse(jsonFieldName);
            }
            else
            {
                FieldName = jsonFieldName;
            }
        }

        private bool HandleValue(Token token)
        {
            if (token.IsSimpleValue)
            {
                HandleSimpleValue(token);
                SkipCommaPostValue(token.ToString());
                return true;
            }
            else if (token == Token.BeginObject)
            {
                stack.Push(State.InObject);
                CurrentElement = FudgeStreamElement.SubmessageFieldStart;
                return true;
            }
            else if (token == Token.BeginArray)
            {
                return HandleArray();
            }
            else
                throw new FudgeParseException("Unexpected token \"" + token + "\" in JSON stream when looking for a value");
        }

        private bool HandleArray()
        {
            // We have to look ahead to see whether the array should be represented as a primitive array or not
            var arrayTokens = new List<Token>();
            var allTokens = new List<Token>();
            var itemType = TokenType.Error;

            // Keep pulling tokens until we have an inconsistency (therefore repeating field) or the end of the array (therefore consistent)
            while (true)
            {
                Token nextToken = GetNextToken();
                if (nextToken == Token.EndArray)
                {
                    // If we've got this far then we're consistent
                    SkipCommaPostValue("array");
                    return HandlePrimitiveArray(arrayTokens, itemType);
                }
                if (itemType != TokenType.Error)
                {
                    if (nextToken != Token.ValueSeparator)
                    {
                        // Got past the first value so it should have been a separator
                        throw new FudgeParseException("Unexpected token \"" + nextToken + "\" in JSON stream when looking for a value in an array");
                    }
                    allTokens.Add(nextToken);
                    nextToken = GetNextToken();
                }

                arrayTokens.Add(nextToken);
                allTokens.Add(nextToken);
                if (nextToken.Type != TokenType.Double && nextToken.Type != TokenType.Integer && nextToken.Type != TokenType.Long)
                {
                    // Can't be a primitive array
                    break;
                }

                if (itemType == TokenType.Error)
                {
                    // First time around
                    itemType = nextToken.Type;
                }
                else if (nextToken.Type != itemType)
                {
                    if (itemType == TokenType.Long && nextToken.Type == TokenType.Integer)
                    {
                        // That's OK
                    }
                    else if (itemType == TokenType.Integer && nextToken.Type == TokenType.Long)
                    {
                        // Upscale
                        itemType = TokenType.Long;
                    }
                    else
                    {
                        // Inconsistent
                        break;
                    }
                }
            }

            // We've got here because it can't be a primitive array, but we've read a number of tokens to get here
            RequeueTokens(allTokens);

            // Now we can start processing the array as repeating fields
            stack.Push(new State(FieldName));
            return HandleValue(GetNextToken());
        }

        private void RequeueTokens(List<Token> tokens)
        {
            // No native way of doing this to a queue, so have to work around
            tokens.AddRange(tokenQueue);
            tokenQueue.Clear();
            foreach (var token in tokens)
                tokenQueue.Enqueue(token);
        }

        private bool HandlePrimitiveArray(List<Token> tokens, TokenType type)
        {
            if (tokens.Count == 0)
            {
                // Empty array - this results in nothing in the Fudge message, so indicate we've not done anything
                return false;
            }

            switch (type)
            {
                case TokenType.Double:
                    FieldType = DoubleArrayFieldType.Instance;
                    FieldValue = tokens.Select(t => t.DoubleData).ToArray();
                    break;
                case TokenType.Integer:
                    FieldType = IntArrayFieldType.Instance;
                    FieldValue = tokens.Select(t => t.IntData).ToArray();
                    break;
                case TokenType.Long:
                    FieldType = LongArrayFieldType.Instance;
                    FieldValue = tokens.Select(t => t.LongData).ToArray();
                    break;
                default:
                    // Shouldn't happen
                    throw new FudgeRuntimeException("Attempt to convert JSON array of " + type + " to primitive array");
            }

            return true;
        }

        private int Depth { get { return stack.Count; } }

        private void HandleSimpleValue(Token token)
        {
            CurrentElement = FudgeStreamElement.SimpleField;
            switch (token.Type)
            {
                case TokenType.String:
                    FieldType = StringFieldType.Instance;
                    FieldValue = token.StringData;
                    break;
                case TokenType.Integer:
                    FieldType = PrimitiveFieldTypes.IntType;
                    FieldValue = token.IntData;
                    break;
                case TokenType.Long:
                    FieldType = PrimitiveFieldTypes.LongType;
                    FieldValue = token.LongData;
                    break;
                case TokenType.Double:
                    FieldType = PrimitiveFieldTypes.DoubleType;
                    FieldValue = token.DoubleData;
                    break;
                case TokenType.Boolean:
                    FieldType = PrimitiveFieldTypes.BooleanType;
                    FieldValue = token.BooleanData;
                    break;
                default:
                    if (token == Token.Null)
                    {
                        FieldType = IndicatorFieldType.Instance;
                        FieldValue = IndicatorType.Instance;
                    }
                    else
                    {
                        Debug.Assert(false, "Unknown simple value token " + token);
                    }
                    break;
            }
        }

        private void SkipCommaPostValue(string context)
        {
            while (true)
            {
                var token = PeekNextToken();
                if (token == Token.ValueSeparator)
                {
                    // Skip past it
                    GetNextToken();
                    return;
                }
                else if (token == Token.EndArray)
                {
                    // Skip it and pop the stack as well
                    GetNextToken();
                    stack.Pop();

                    // Go round again as there may be a comma or } next
                }
                else if (token == Token.EndObject)
                {
                    return;
                }
                else
                {
                    throw new FudgeParseException("Expected , or } after " + context);
                }
            }
        }

        private Token PeekNextToken()
        {
            if (tokenQueue.Count == 0)
            {
                var nextToken = ParseNextToken();
                tokenQueue.Enqueue(nextToken);
            }
            return tokenQueue.Peek();
        }

        private Token GetNextToken()
        {
            if (tokenQueue.Count == 0)
                return ParseNextToken();
            else
                return tokenQueue.Dequeue();
        }

        private Token ParseNextToken()
        {
            while (true)
            {
                int next = reader.Read();
                if (next == -1)
                {
                    return Token.EOF;
                }

                switch (next)
                {
                    case JSONConstants.BeginObject:
                        return Token.BeginObject;
                    case JSONConstants.EndObject:
                        return Token.EndObject;
                    case JSONConstants.BeginArray:
                        return Token.BeginArray;
                    case JSONConstants.EndArray:
                        return Token.EndArray;
                    case '"':
                        return ParseString();
                    case JSONConstants.NameSeparator:
                        return Token.NameSeparator;
                    case JSONConstants.ValueSeparator:
                        return Token.ValueSeparator;
                    case ' ':
                    case '\r':
                    case '\n':
                    case '\f':
                    case '\b':
                    case '\t':
                        // Ignore white space
                        break;
                    default:
                        return ParseLiteral((char)next);
                }
            }
        }

        private Token ParseLiteral(char startChar)
        {
            string literal = startChar + ReadLiteral();

            if (literal == JSONConstants.TrueLiteral)
                return Token.True;
            if (literal == JSONConstants.FalseLiteral)
                return Token.False;
            if (literal == JSONConstants.NullLiteral)
                return Token.Null;

            var token = ParseNumber(literal);
            if (token.Type != TokenType.Error)
                return token;

            // No idea what it is then
            throw new FudgeParseException("Unrecognised JSON token \"" + literal + "\"");
        }

        private string ReadLiteral()
        {
            var sb = new StringBuilder();
            bool done = false;
            while (!done)
            {
                int next = reader.Peek();
                switch (next)
                {
                    case -1:        // EOF
                        done = true;
                        break;
                    case ' ':
                    case '\b':
                    case '\t':
                    case '\r':
                    case '\f':
                    case '\n':
                    case '\\':
                    case JSONConstants.BeginObject:
                    case JSONConstants.EndObject:
                    case JSONConstants.ValueSeparator:
                    case JSONConstants.BeginArray:
                    case JSONConstants.EndArray:
                        done = true;
                        break;
                    default:
                        sb.Append((char)next);
                        reader.Read();
                        break;
                }
            }
            return sb.ToString();
        }

        private Token ParseNumber(string literal)
        {
            bool isDouble = literal.Contains('.') || literal.Contains('e') || literal.Contains('E');
            try
            {
                if (isDouble)
                {
                    return new Token(double.Parse(literal), literal);
                }
                else
                {
                    long val = long.Parse(literal);
                    if (val >= int.MinValue && val <= int.MaxValue)
                        return new Token((int)val, literal);
                    else
                        return new Token(val, literal);
                }
            }
            catch (FormatException)
            {
                return new Token(TokenType.Error, literal);
            }
        }

        private Token ParseString()
        {
            StringBuilder sb = new StringBuilder();

            // We've already had the opening quote
            while (true)
            {
                int next = reader.Read();
                if (next == -1)
                {
                    return Token.EOF;
                }
                char nextChar = (char)next;

                switch (nextChar)
                {
                    case '"':
                        // We're done
                        return new Token(sb.ToString());
                    case '\\':
                        {
                            // Escaped char
                            next = reader.Read();
                            if (next == -1)
                            {
                                return Token.EOF;
                            }
                            switch (next)
                            {
                                case '"':
                                case '\\':
                                case '/':
                                    sb.Append((char)next);
                                    break;
                                case 'b':
                                    sb.Append('\b');
                                    break;
                                case 'f':
                                    sb.Append('\f');
                                    break;
                                case 'n':
                                    sb.Append('\n');
                                    break;
                                case 'r':
                                    sb.Append('\r');
                                    break;
                                case 't':
                                    sb.Append('\t');
                                    break;
                                case 'u':
                                    sb.Append(ReadUnicode());
                                    break;
                            }
                            break;
                        }
                    default:
                        sb.Append(nextChar);
                        break;
                }
            }
        }

        private char ReadUnicode()
        {
            char[] buffer = new char[4];
            if (reader.Read(buffer, 0, 4) != 4)
                throw new FudgeParseException("Premature EOF whilst trying to read \\u in string");

            StringBuilder sb = new StringBuilder();
            sb.Append(buffer);
            int val = int.Parse(sb.ToString(), NumberStyles.HexNumber);
            return (char)val;
        }

        private class State
        {
            private readonly string arrayFieldName;

            public State(string arrayFieldName) // We're in an array
            {
                this.arrayFieldName = arrayFieldName;
            }

            public State()                      // We're not in an array
            {
                this.arrayFieldName = null;
            }

            public bool IsInArray { get { return arrayFieldName != null; } }

            public string ArrayFieldName { get { return arrayFieldName; } }

            public static readonly State InObject = new State();
        }

        private class Token
        {
            private readonly string toString;
            private readonly TokenType type;
            private readonly string stringData;
            private readonly double doubleData;
            private readonly long longData;

            public Token(TokenType type, string toString)
            {
                this.type = type;
                this.toString = toString;
            }

            public Token(TokenType type, char ch)
                : this(type, ch.ToString())
            {
            }

            public Token(string val)
                : this(TokenType.String, val)
            {
                stringData = val;
            }

            public Token(double val, string str)
                : this(TokenType.Double, str)
            {
                doubleData = val;
            }

            public Token(long val, string str)
                : this(TokenType.Long, str)
            {
                longData = val;
            }

            public Token(int val, string str)
                : this(TokenType.Integer, str)
            {
                longData = val;
            }

            public TokenType Type { get { return type; } }

            public string StringData { get { return stringData; } }

            public long LongData { get { return longData; } }

            public int IntData { get { return (int)longData; } }

            public double DoubleData { get { return doubleData; } }

            public bool BooleanData { get { return this == True; } }

            public bool IsSimpleValue
            {
                get
                {
                    return Type == TokenType.Double ||
                           Type == TokenType.Integer ||
                           Type == TokenType.Long ||
                           Type == TokenType.String ||
                           Type == TokenType.Boolean ||
                           this == Token.Null;
                }
            }

            public override string ToString()
            {
                return toString;
            }

            public static readonly Token EOF = new Token(TokenType.Special, "EOF");
            public static readonly Token BeginObject = new Token(TokenType.Special, JSONConstants.BeginObject);
            public static readonly Token EndObject = new Token(TokenType.Special, JSONConstants.EndObject);
            public static readonly Token BeginArray = new Token(TokenType.Special, JSONConstants.BeginArray);
            public static readonly Token EndArray = new Token(TokenType.Special, JSONConstants.EndArray);
            public static readonly Token NameSeparator = new Token(TokenType.Special, JSONConstants.NameSeparator);
            public static readonly Token ValueSeparator = new Token(TokenType.Special, JSONConstants.ValueSeparator);
            public static readonly Token True = new Token(TokenType.Boolean, JSONConstants.TrueLiteral);
            public static readonly Token False = new Token(TokenType.Boolean, JSONConstants.FalseLiteral);
            public static readonly Token Null = new Token(TokenType.Special, JSONConstants.NullLiteral);
        }

        enum TokenType
        {
            Special,
            String,
            Integer,
            Long,
            Double,
            Boolean,
            Error
        }
    }
}
