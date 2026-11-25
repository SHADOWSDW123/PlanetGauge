using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PlanetGauge
{
    internal enum PlanetGaugeJsonLimitKind
    {
        None = 0,
        InputTooLarge = 1,
        DepthExceeded = 2,
        StringTooLong = 3,
        PropertyNameTooLong = 4,
        TooManyObjectProperties = 5,
        TooManyArrayElements = 6,
        TooManyTotalValues = 7
    }

    internal sealed class PlanetGaugeJsonLimitException : FormatException
    {
        internal PlanetGaugeJsonLimitException(PlanetGaugeJsonLimitKind limitKind, string message)
            : base(message)
        {
            LimitKind = limitKind;
        }

        internal PlanetGaugeJsonLimitKind LimitKind { get; private set; }
    }

    /// <summary>
    /// 외부 JSON 라이브러리 의존성 없이 .plgg 파일을 직렬화·역직렬화하는 RFC 8259 JSON 처리기다.
    /// 역직렬화 결과는 Dictionary&lt;string, object&gt;, List&lt;object&gt;, string, bool, long, double, null로 구성된다.
    /// </summary>
    internal static class PlanetGaugeJson
    {
        internal const int MaxInputCharacters = 4 * 1024 * 1024;
        internal const int MaxDepth = 64;
        internal const int MaxStringLength = 65536;
        internal const int MaxPropertyNameLength = 128;
        internal const int MaxObjectProperties = 256;
        internal const int MaxArrayElements = 20000;
        internal const int MaxTotalValues = 600000;
        private const int MaxErrorSnippetLength = 32;

        internal static string Serialize(object value)
        {
            StringBuilder builder = new StringBuilder(1024);
            WriteValue(builder, value, 0);
            builder.Append('\n');
            return builder.ToString();
        }

        internal static object Deserialize(string json)
        {
            if (json == null)
            {
                throw new FormatException("JSON input is null.");
            }

            if (json.Length > MaxInputCharacters)
            {
                throw new PlanetGaugeJsonLimitException(
                    PlanetGaugeJsonLimitKind.InputTooLarge,
                    "JSON input exceeds maximum allowed length (" + MaxInputCharacters + " characters).");
            }

            Parser parser = new Parser(json);
            return parser.ParseRoot();
        }

        private static void WriteValue(StringBuilder builder, object value, int depth)
        {
            if (depth > MaxDepth)
            {
                throw new InvalidOperationException("JSON nesting depth exceeded maximum limit.");
            }

            if (value == null)
            {
                builder.Append("null");
                return;
            }

            string text = value as string;
            if (text != null)
            {
                WriteString(builder, text);
                return;
            }

            if (value is bool)
            {
                builder.Append((bool)value ? "true" : "false");
                return;
            }

            if (value is int)
            {
                builder.Append(((int)value).ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (value is long)
            {
                builder.Append(((long)value).ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (value is float)
            {
                float f = (float)value;
                if (float.IsNaN(f) || float.IsInfinity(f))
                {
                    throw new ArgumentException("Cannot serialize non-finite float value to JSON.");
                }

                builder.Append(f.ToString("R", CultureInfo.InvariantCulture));
                return;
            }

            if (value is double)
            {
                double d = (double)value;
                if (double.IsNaN(d) || double.IsInfinity(d))
                {
                    throw new ArgumentException("Cannot serialize non-finite double value to JSON.");
                }

                builder.Append(d.ToString("R", CultureInfo.InvariantCulture));
                return;
            }

            if (value is decimal)
            {
                builder.Append(((decimal)value).ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (value is short || value is byte || value is sbyte || value is ushort || value is uint || value is ulong)
            {
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            IDictionary<string, object> genericDict = value as IDictionary<string, object>;
            if (genericDict != null)
            {
                WriteObject(builder, genericDict, depth);
                return;
            }

            IDictionary dictionary = value as IDictionary;
            if (dictionary != null)
            {
                WriteNonGenericObject(builder, dictionary, depth);
                return;
            }

            IList list = value as IList;
            if (list != null)
            {
                WriteArray(builder, list, depth);
                return;
            }

            Enum enumValue = value as Enum;
            if (enumValue != null)
            {
                WriteString(builder, enumValue.ToString());
                return;
            }

            WriteString(builder, Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        private static void WriteObject(
            StringBuilder builder,
            IDictionary<string, object> dict,
            int depth)
        {
            if (dict.Count == 0)
            {
                builder.Append("{}");
                return;
            }

            builder.Append("{\n");
            bool first = true;
            foreach (KeyValuePair<string, object> pair in dict)
            {
                if (!first)
                {
                    builder.Append(",\n");
                }

                first = false;
                AppendIndent(builder, depth + 1);
                WriteString(builder, pair.Key ?? string.Empty);
                builder.Append(": ");
                WriteValue(builder, pair.Value, depth + 1);
            }

            builder.Append('\n');
            AppendIndent(builder, depth);
            builder.Append('}');
        }

        private static void WriteNonGenericObject(
            StringBuilder builder,
            IDictionary dict,
            int depth)
        {
            if (dict.Count == 0)
            {
                builder.Append("{}");
                return;
            }

            builder.Append("{\n");
            bool first = true;
            foreach (DictionaryEntry entry in dict)
            {
                if (!first)
                {
                    builder.Append(",\n");
                }

                first = false;
                AppendIndent(builder, depth + 1);
                WriteString(builder, Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty);
                builder.Append(": ");
                WriteValue(builder, entry.Value, depth + 1);
            }

            builder.Append('\n');
            AppendIndent(builder, depth);
            builder.Append('}');
        }

        private static void WriteArray(StringBuilder builder, IList list, int depth)
        {
            if (list.Count == 0)
            {
                builder.Append("[]");
                return;
            }

            builder.Append("[\n");
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(",\n");
                }

                AppendIndent(builder, depth + 1);
                WriteValue(builder, list[i], depth + 1);
            }

            builder.Append('\n');
            AppendIndent(builder, depth);
            builder.Append(']');
        }

        private static void AppendIndent(StringBuilder builder, int depth)
        {
            for (int i = 0; i < depth; i++)
            {
                builder.Append("  ");
            }
        }

        private static void WriteString(StringBuilder builder, string value)
        {
            builder.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                switch (ch)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (ch < 0x20)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(ch);
                        }
                        break;
                }
            }

            builder.Append('"');
        }

        private sealed class Parser
        {
            private readonly string text;
            private int index;
            private int totalValues;

            internal Parser(string text)
            {
                this.text = text;
                index = 0;
                totalValues = 0;
                if (this.text.Length > 0 && this.text[0] == '\uFEFF')
                {
                    index = 1;
                }
            }

            internal object ParseRoot()
            {
                SkipWhitespace();
                if (index >= text.Length)
                {
                    throw new FormatException("Empty JSON document.");
                }

                object value = ParseValue(0);
                SkipWhitespace();
                if (index < text.Length)
                {
                    throw new FormatException("Unexpected trailing characters at position " + index + ".");
                }

                return value;
            }

            private object ParseValue(int depth)
            {
                if (depth > MaxDepth)
                {
                    throw new PlanetGaugeJsonLimitException(
                        PlanetGaugeJsonLimitKind.DepthExceeded,
                        "JSON nesting depth exceeded maximum limit.");
                }

                totalValues++;
                if (totalValues > MaxTotalValues)
                {
                    throw new PlanetGaugeJsonLimitException(
                        PlanetGaugeJsonLimitKind.TooManyTotalValues,
                        "JSON value count exceeded maximum limit (" + MaxTotalValues + ").");
                }

                SkipWhitespace();
                if (index >= text.Length)
                {
                    throw new FormatException("Unexpected end of JSON input.");
                }

                char ch = text[index];
                if (ch == '{')
                {
                    return ParseObject(depth + 1);
                }

                if (ch == '[')
                {
                    return ParseArray(depth + 1);
                }

                if (ch == '"')
                {
                    return ParseString(MaxStringLength, PlanetGaugeJsonLimitKind.StringTooLong);
                }

                if (ch == 't')
                {
                    ExpectLiteral("true");
                    return true;
                }

                if (ch == 'f')
                {
                    ExpectLiteral("false");
                    return false;
                }

                if (ch == 'n')
                {
                    ExpectLiteral("null");
                    return null;
                }

                if (ch == '-' || (ch >= '0' && ch <= '9'))
                {
                    return ParseNumber();
                }

                throw new FormatException("Unexpected character " + DescribeCharacter(ch) + " at position " + index + ".");
            }

            private Dictionary<string, object> ParseObject(int depth)
            {
                index++; // '{'
                Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.Ordinal);
                SkipWhitespace();
                if (index < text.Length && text[index] == '}')
                {
                    index++;
                    return result;
                }

                int propertyCount = 0;
                while (true)
                {
                    propertyCount++;
                    if (propertyCount > MaxObjectProperties)
                    {
                        throw new PlanetGaugeJsonLimitException(
                            PlanetGaugeJsonLimitKind.TooManyObjectProperties,
                            "JSON object exceeded maximum allowed property count (" + MaxObjectProperties + ").");
                    }

                    SkipWhitespace();
                    if (index >= text.Length || text[index] != '"')
                    {
                        throw new FormatException("Expected property name string at position " + index + ".");
                    }

                    string key = ParseString(MaxPropertyNameLength, PlanetGaugeJsonLimitKind.PropertyNameTooLong);
                    SkipWhitespace();
                    if (index >= text.Length || text[index] != ':')
                    {
                        throw new FormatException("Expected ':' after property name at position " + index + ".");
                    }

                    index++;
                    object value = ParseValue(depth);
                    result[key] = value;

                    SkipWhitespace();
                    if (index >= text.Length)
                    {
                        throw new FormatException("Unterminated JSON object.");
                    }

                    char delimiter = text[index];
                    if (delimiter == '}')
                    {
                        index++;
                        return result;
                    }

                    if (delimiter != ',')
                    {
                        throw new FormatException("Expected ',' or '}' in JSON object at position " + index + ".");
                    }

                    index++;
                }
            }

            private List<object> ParseArray(int depth)
            {
                index++; // '['
                List<object> result = new List<object>();
                SkipWhitespace();
                if (index < text.Length && text[index] == ']')
                {
                    index++;
                    return result;
                }

                while (true)
                {
                    if (result.Count >= MaxArrayElements)
                    {
                        throw new PlanetGaugeJsonLimitException(
                            PlanetGaugeJsonLimitKind.TooManyArrayElements,
                            "JSON array exceeded maximum allowed element count (" + MaxArrayElements + ").");
                    }

                    result.Add(ParseValue(depth));
                    SkipWhitespace();
                    if (index >= text.Length)
                    {
                        throw new FormatException("Unterminated JSON array.");
                    }

                    char delimiter = text[index];
                    if (delimiter == ']')
                    {
                        index++;
                        return result;
                    }

                    if (delimiter != ',')
                    {
                        throw new FormatException("Expected ',' or ']' in JSON array at position " + index + ".");
                    }

                    index++;
                }
            }

            private string ParseString(int maxLength, PlanetGaugeJsonLimitKind limitKind)
            {
                index++; // '"'
                StringBuilder builder = new StringBuilder();
                while (index < text.Length)
                {
                    char ch = text[index++];
                    if (ch == '"')
                    {
                        return builder.ToString();
                    }

                    if (ch < 0x20)
                    {
                        throw new FormatException("Unescaped control character in JSON string at position " + (index - 1) + ".");
                    }

                    if (ch != '\\')
                    {
                        AppendBoundedChar(builder, ch, maxLength, limitKind);
                        continue;
                    }

                    if (index >= text.Length)
                    {
                        throw new FormatException("Unterminated escape sequence in JSON string.");
                    }

                    char escaped = text[index++];
                    switch (escaped)
                    {
                        case '"': AppendBoundedChar(builder, '"', maxLength, limitKind); break;
                        case '\\': AppendBoundedChar(builder, '\\', maxLength, limitKind); break;
                        case '/': AppendBoundedChar(builder, '/', maxLength, limitKind); break;
                        case 'b': AppendBoundedChar(builder, '\b', maxLength, limitKind); break;
                        case 'f': AppendBoundedChar(builder, '\f', maxLength, limitKind); break;
                        case 'n': AppendBoundedChar(builder, '\n', maxLength, limitKind); break;
                        case 'r': AppendBoundedChar(builder, '\r', maxLength, limitKind); break;
                        case 't': AppendBoundedChar(builder, '\t', maxLength, limitKind); break;
                        case 'u':
                            if (index + 4 > text.Length)
                            {
                                throw new FormatException("Incomplete \\u escape sequence at position " + index + ".");
                            }

                            string hex = text.Substring(index, 4);
                            ushort codeUnit;
                            if (!ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out codeUnit))
                            {
                                throw new FormatException("Invalid \\u escape sequence at position " + index + ".");
                            }

                            index += 4;
                            AppendBoundedChar(builder, (char)codeUnit, maxLength, limitKind);
                            break;
                        default:
                            throw new FormatException("Invalid escape sequence at position " + (index - 1) + ".");
                    }
                }

                throw new FormatException("Unterminated JSON string.");
            }

            private static void AppendBoundedChar(
                StringBuilder builder,
                char ch,
                int maxLength,
                PlanetGaugeJsonLimitKind limitKind)
            {
                if (builder.Length >= maxLength)
                {
                    throw new PlanetGaugeJsonLimitException(
                        limitKind,
                        "JSON string exceeded maximum allowed length (" + maxLength + " characters).");
                }

                builder.Append(ch);
            }

            private object ParseNumber()
            {
                int start = index;
                if (text[index] == '-')
                {
                    index++;
                }

                if (index >= text.Length)
                {
                    throw new FormatException("Invalid number at position " + start + ".");
                }

                if (text[index] == '0')
                {
                    index++;
                }
                else if (text[index] >= '1' && text[index] <= '9')
                {
                    while (index < text.Length && text[index] >= '0' && text[index] <= '9')
                    {
                        index++;
                    }
                }
                else
                {
                    throw new FormatException("Invalid number at position " + start + ".");
                }

                bool isFloating = false;
                if (index < text.Length && text[index] == '.')
                {
                    isFloating = true;
                    index++;
                    int fractionStart = index;
                    while (index < text.Length && text[index] >= '0' && text[index] <= '9')
                    {
                        index++;
                    }

                    if (index == fractionStart)
                    {
                        throw new FormatException("Expected digit after decimal point at position " + start + ".");
                    }
                }

                if (index < text.Length && (text[index] == 'e' || text[index] == 'E'))
                {
                    isFloating = true;
                    index++;
                    if (index < text.Length && (text[index] == '+' || text[index] == '-'))
                    {
                        index++;
                    }

                    int expStart = index;
                    while (index < text.Length && text[index] >= '0' && text[index] <= '9')
                    {
                        index++;
                    }

                    if (index == expStart)
                    {
                        throw new FormatException("Expected exponent digits at position " + start + ".");
                    }
                }

                int length = index - start;
                string raw = text.Substring(start, length);
                if (!isFloating)
                {
                    long intVal;
                    if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out intVal))
                    {
                        return intVal;
                    }
                }

                double doubleVal;
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out doubleVal)
                    || double.IsNaN(doubleVal)
                    || double.IsInfinity(doubleVal))
                {
                    string snippet = raw.Length > MaxErrorSnippetLength
                        ? raw.Substring(0, MaxErrorSnippetLength) + "..."
                        : raw;
                    throw new FormatException("Numeric value out of range at position " + start + ": " + snippet);
                }

                return doubleVal;
            }

            private static string DescribeCharacter(char ch)
            {
                if (ch < 0x20 || ch == '<' || ch == '>' || char.IsControl(ch))
                {
                    return "U+" + ((int)ch).ToString("X4", CultureInfo.InvariantCulture);
                }

                return "'" + ch + "'";
            }

            private void ExpectLiteral(string literal)
            {
                if (index + literal.Length > text.Length
                    || string.Compare(text, index, literal, 0, literal.Length, StringComparison.Ordinal) != 0)
                {
                    throw new FormatException("Unexpected token at position " + index + ".");
                }

                index += literal.Length;
            }

            private void SkipWhitespace()
            {
                while (index < text.Length)
                {
                    char ch = text[index];
                    if (ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n')
                    {
                        index++;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
    }
}
