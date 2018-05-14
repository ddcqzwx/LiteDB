﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace LiteDB
{
    /// <summary>
    /// A class that read a json string using a tokenizer (without regex)
    /// </summary>
    internal class JsonReader
    {
        private Tokenizer _tokenizer = null;

        public long Position { get { return _tokenizer.Position; } }

        public JsonReader(TextReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            _tokenizer = new Tokenizer(reader);
        }

        public BsonValue Deserialize()
        {
            var token = _tokenizer.ReadToken();

            if (token.Type == TokenType.EOF) return BsonValue.Null;

            var value = this.ReadValue(token);

            return value;
        }

        public IEnumerable<BsonValue> DeserializeArray()
        {
            var token = _tokenizer.ReadToken();

            if (token.Type == TokenType.EOF) yield break;

            token.Expect(TokenType.OpenBracket);

            token = _tokenizer.ReadToken();

            while (token.Type != TokenType.CloseBracket)
            {
                yield return this.ReadValue(token);

                token = _tokenizer.ReadToken();

                if (token.Type == TokenType.Comma)
                {
                    token = _tokenizer.ReadToken();
                }
            }

            token.Expect(TokenType.CloseBracket);

            yield break;
        }

        internal BsonValue ReadValue(Token token)
        {
            switch (token.Type)
            {
                case TokenType.String: return token.Value;
                case TokenType.OpenBrace: return this.ReadObject();
                case TokenType.OpenBracket: return this.ReadArray();
                case TokenType.Number:
                    return token.Value.Contains(".") ?
                        new BsonValue(Convert.ToDouble(token.Value, CultureInfo.InvariantCulture.NumberFormat)) :
                        new BsonValue(Convert.ToInt32(token.Value));
                case TokenType.Word:
                    switch (token.Value)
                    {
                        case "null": return BsonValue.Null;
                        case "true": return true;
                        case "false": return false;
                        default: throw LiteException.UnexpectedToken(token.Value);
                    }
            }

            throw LiteException.UnexpectedToken(token.Value);
        }

        private BsonValue ReadObject()
        {
            var obj = new BsonDocument();

            var token = _tokenizer.ReadToken(); // read "<key>"

            while (token.Type != TokenType.CloseBrace)
            {
                token.Expect(TokenType.String, TokenType.Word);

                var key = token.Value;

                token = _tokenizer.ReadToken(); // read ":"

                token.Expect(TokenType.Colon);

                token = _tokenizer.ReadToken(); // read "<value>"

                // check if not a special data type - only if is first attribute
                if (key[0] == '$' && obj.Count == 0)
                {
                    var val = this.ReadExtendedDataType(key, token.Value);

                    // if val is null then it's not a extended data type - it's just a object with $ attribute
                    if (!val.IsNull) return val;
                }

                obj[key] = this.ReadValue(token); // read "," or "}"

                token = _tokenizer.ReadToken();

                if (token.Type == TokenType.Comma)
                {
                    token = _tokenizer.ReadToken(); // read "<key>"
                }
            }

            return obj;
        }

        private BsonArray ReadArray()
        {
            var arr = new BsonArray();

            var token = _tokenizer.ReadToken();

            while (token.Type != TokenType.CloseBracket)
            {
                var value = this.ReadValue(token);

                arr.Add(value);

                token = _tokenizer.ReadToken();

                if (token.Type == TokenType.Comma)
                {
                    token = _tokenizer.ReadToken();
                }
            }

            return arr;
        }

        private BsonValue ReadExtendedDataType(string key, string value)
        {
            BsonValue val;

            switch (key)
            {
                case "$binary": val = new BsonValue(Convert.FromBase64String(value)); break;
                case "$oid": val = new BsonValue(new ObjectId(value)); break;
                case "$guid": val = new BsonValue(new Guid(value)); break;
                case "$date": val = new BsonValue(DateTime.Parse(value).ToLocalTime()); break;
                case "$numberLong": val = new BsonValue(Convert.ToInt64(value)); break;
                case "$numberDecimal": val = new BsonValue(Convert.ToDecimal(value)); break;
                case "$minValue": val = BsonValue.MinValue; break;
                case "$maxValue": val = BsonValue.MaxValue; break;

                default: return BsonValue.Null; // is not a special data type
            }

            _tokenizer.ReadToken().Expect(TokenType.CloseBrace);

            return val;
        }
    }
}