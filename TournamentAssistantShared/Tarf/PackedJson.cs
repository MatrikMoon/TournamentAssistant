using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TournamentAssistantShared.Tarf
{
    // Packs JSON map files into columns before they are deflated, see "Map files" in TARF/README.md.
    // The values of one key from every object in a list are stored together, so note times, event types and colors each sit next to their own kind.
    // Numbers keep the precision Beat Saber reads them with: whole numbers stay exact and everything else becomes a float
    public static class PackedJson
    {
        private const byte KindNumber = 0, KindString = 1, KindList = 2, KindObject = 3, KindLiteral = 4, KindMixed = 5;
        private const byte FormFraction = 0, FormFloat = 1, FormDouble = 2;
        private const byte LiteralNull = 0, LiteralFalse = 1, LiteralTrue = 2;
        private const byte LayoutPositional = 0, LayoutFlat = 1;

        // Whole numbers below this are exact as doubles, which is all a JSON reader keeps
        private const long MaxExactInteger = 1L << 53;

        // The writer uses the first one that fits every number of a column, readers accept any denominator
        private static readonly int[] Denominators = { 1, 2, 3, 4, 5, 6, 8, 10, 12, 16, 20, 24, 32, 40, 48, 64, 96, 100, 128, 192, 256, 1000, 10000, 100000, 1000000 };

        private const int MaxPositionalListLength = 8;
        private const int MinGroupedRows = 16;
        private const int MaxGroupCount = 64;

        // A corrupted count shouldn't make a reader allocate billions of values
        private const long MaxUnpackedValues = 64L * 1024 * 1024;

        // Parses map JSON without the conversions Newtonsoft does by default, like turning date-like strings into dates
        public static JToken Parse(byte[] json)
        {
            var text = Encoding.UTF8.GetString(json).TrimStart('﻿');
            using var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Double };
            var root = JToken.ReadFrom(reader);
            if (reader.Read()) throw new JsonReaderException("Unexpected content after the JSON value");
            return root;
        }

        public static byte[] Pack(JToken root) => new Packer(root).ToArray();

        public static JToken Unpack(byte[] data) => new Unpacker(data).Root;

        private enum NumberType { Integer, Float, Double }

        // A number as Beat Saber reads it. Doubles are only needed for whole numbers too large to be exact otherwise
        private readonly struct GameNumber
        {
            public readonly NumberType Type;
            public readonly long Integer;
            public readonly float Float;
            public readonly double Double;

            private GameNumber(NumberType type, long integer, float single, double number)
            {
                Type = type;
                Integer = integer;
                Float = single;
                Double = number;
            }

            public static GameNumber From(JValue value)
            {
                if (value.Value is long integer && integer > -MaxExactInteger && integer < MaxExactInteger) return new GameNumber(NumberType.Integer, integer, 0, 0);

                var number = (double)value;
                if (Math.Abs(number) < MaxExactInteger && Math.Floor(number) == number) return new GameNumber(NumberType.Integer, (long)number, 0, 0);
                if (double.IsNaN(number) || Math.Abs(number) >= MaxExactInteger) return new GameNumber(NumberType.Double, 0, 0, number);
                return new GameNumber(NumberType.Float, 0, (float)number, 0);
            }

            // Finds the numerator that gives this number back when divided by the denominator
            public bool TryFit(int denominator, out long numerator)
            {
                numerator = 0;
                if (Type == NumberType.Integer)
                {
                    if (Math.Abs(Integer) >= MaxExactInteger / denominator) return false;
                    numerator = Integer * denominator;
                    return true;
                }
                if (Type == NumberType.Float)
                {
                    var scaled = (double)Float * denominator;
                    if (Math.Abs(scaled) >= MaxExactInteger) return false;
                    numerator = (long)Math.Round(scaled);
                    return (float)(numerator / (double)denominator) == Float;
                }
                return false;
            }
        }

        private static byte KindOf(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Integer:
                case JTokenType.Float:
                    return KindNumber;
                case JTokenType.String:
                    return KindString;
                case JTokenType.Array:
                    return KindList;
                case JTokenType.Object:
                    return KindObject;
                case JTokenType.Null:
                case JTokenType.Undefined:
                case JTokenType.Boolean:
                    return KindLiteral;
                default:
                    throw new InvalidDataException($"{token.Type} values can't be packed");
            }
        }

        private static bool IsScalar(JToken token) => token.Type != JTokenType.Array && token.Type != JTokenType.Object;

        private class Packer
        {
            private readonly Dictionary<string, int> _stringIndexes = new Dictionary<string, int>();
            private MemoryStream _output = new MemoryStream();

            public Packer(JToken root)
            {
                // The most used strings get the smallest indexes
                var counts = new Dictionary<string, int>();
                var firstSeen = new List<string>();
                CountStrings(root, counts, firstSeen);
                var strings = firstSeen.OrderByDescending(x => counts[x]).ToList();

                WriteUnsigned(strings.Count);
                foreach (var value in strings)
                {
                    _stringIndexes[value] = _stringIndexes.Count;
                    var bytes = Encoding.UTF8.GetBytes(value);
                    WriteUnsigned(bytes.Length);
                    _output.Write(bytes, 0, bytes.Length);
                }

                WriteColumn(new List<JToken> { root });
            }

            public byte[] ToArray() => _output.ToArray();

            private static void CountStrings(JToken token, Dictionary<string, int> counts, List<string> firstSeen)
            {
                void Count(string value)
                {
                    counts.TryGetValue(value, out var count);
                    if (count == 0) firstSeen.Add(value);
                    counts[value] = count + 1;
                }

                if (token is JObject jObject)
                {
                    foreach (var property in jObject.Properties())
                    {
                        Count(property.Name);
                        CountStrings(property.Value, counts, firstSeen);
                    }
                }
                else if (token is JArray jArray)
                {
                    foreach (var item in jArray) CountStrings(item, counts, firstSeen);
                }
                else if (token.Type == JTokenType.String)
                {
                    Count((string)token);
                }
            }

            private void WriteUnsigned(long value) => VarInt.WriteUnsigned(_output, (ulong)value);

            // Writes into a scratch buffer, so the writer can compare ways of storing the same values
            private byte[] Trial(Action write)
            {
                var output = _output;
                _output = new MemoryStream();
                write();
                var result = _output.ToArray();
                _output = output;
                return result;
            }

            private static int DeflatedLength(byte[] data) => TarfContainer.Deflate(data).Length;

            // Every value of one key, from all the objects that have it
            private void WriteColumn(List<JToken> values)
            {
                var kinds = values.Select(KindOf).ToArray();
                if (kinds.Distinct().Count() > 1)
                {
                    _output.WriteByte(KindMixed);
                    _output.Write(kinds, 0, kinds.Length);
                    for (byte kind = 0; kind < KindMixed; kind++)
                    {
                        var part = values.Where((_, i) => kinds[i] == kind).ToList();
                        if (part.Count > 0) WriteColumn(part);
                    }
                    return;
                }

                _output.WriteByte(kinds[0]);
                switch (kinds[0])
                {
                    case KindNumber:
                        WriteNumbers(values.Select(x => GameNumber.From((JValue)x)).ToList());
                        break;
                    case KindString:
                        foreach (var value in values) WriteUnsigned(_stringIndexes[(string)value]);
                        break;
                    case KindList:
                        WriteLists(values.Cast<JArray>().ToList());
                        break;
                    case KindObject:
                        WriteObjects(values.Cast<JObject>().ToList());
                        break;
                    default:
                        foreach (var value in values) _output.WriteByte(value.Type == JTokenType.Boolean ? (bool)value ? LiteralTrue : LiteralFalse : LiteralNull);
                        break;
                }
            }

            // Plain or as differences from the previous value, whichever is smaller. Sorted values like times get small differences
            private void WriteIntegers(IReadOnlyList<long> values)
            {
                using var plain = new MemoryStream();
                using var delta = new MemoryStream();
                long previous = 0;
                foreach (var value in values)
                {
                    VarInt.WriteSigned(plain, value);
                    VarInt.WriteSigned(delta, value - previous);
                    previous = value;
                }

                var useDelta = delta.Length < plain.Length;
                _output.WriteByte(useDelta ? (byte)1 : (byte)0);
                (useDelta ? delta : plain).WriteTo(_output);
            }

            private void WriteNumbers(List<GameNumber> numbers)
            {
                var denominator = Denominators[0];
                var mostFitting = -1;
                foreach (var candidate in Denominators)
                {
                    var fitting = numbers.Count(x => x.TryFit(candidate, out _));
                    if (fitting > mostFitting)
                    {
                        denominator = candidate;
                        mostFitting = fitting;
                    }
                    if (fitting == numbers.Count) break;
                }
                WriteUnsigned(denominator);

                var forms = new byte[numbers.Count];
                var numerators = new List<long>();
                var floats = new List<float>();
                var doubles = new List<double>();
                for (var i = 0; i < numbers.Count; i++)
                {
                    if (numbers[i].TryFit(denominator, out var numerator))
                    {
                        forms[i] = FormFraction;
                        numerators.Add(numerator);
                    }
                    else if (numbers[i].Type == NumberType.Float)
                    {
                        forms[i] = FormFloat;
                        floats.Add(numbers[i].Float);
                    }
                    else
                    {
                        forms[i] = FormDouble;
                        doubles.Add(numbers[i].Type == NumberType.Integer ? numbers[i].Integer : numbers[i].Double);
                    }
                }

                if (numerators.Count == numbers.Count) _output.WriteByte(0);
                else
                {
                    _output.WriteByte(1);
                    _output.Write(forms, 0, forms.Length);
                }

                WriteIntegers(numerators);
                foreach (var value in floats) _output.Write(BitConverter.GetBytes(value), 0, 4);
                foreach (var value in doubles) _output.Write(BitConverter.GetBytes(value), 0, 8);
            }

            private void WriteLists(List<JArray> lists)
            {
                WriteIntegers(lists.Select(x => (long)x.Count).ToList());
                if (lists.All(x => x.Count == 0)) return;

                void WriteFlat()
                {
                    _output.WriteByte(LayoutFlat);
                    WriteColumn(lists.SelectMany(x => x).ToList());
                }

                // Short lists like [x, y, z] can get a column per position, which suits values that change independently.
                // Lists that repeat as a whole, like colors, compress better left in order
                var longest = lists.Max(x => x.Count);
                if (longest <= MaxPositionalListLength && lists.Count > 1 && lists.All(x => x.All(IsScalar)))
                {
                    var flat = Trial(WriteFlat);
                    var positional = Trial(() =>
                    {
                        _output.WriteByte(LayoutPositional);
                        for (var i = 0; i < longest; i++) WriteColumn(lists.Where(x => x.Count > i).Select(x => x[i]).ToList());
                    });
                    var chosen = DeflatedLength(positional) < DeflatedLength(flat) ? positional : flat;
                    _output.Write(chosen, 0, chosen.Length);
                }
                else
                {
                    WriteFlat();
                }
            }

            private void WriteObjects(List<JObject> rows)
            {
                var groupKey = ChooseGroupKey(rows);
                if (groupKey == null)
                {
                    _output.WriteByte(0);
                    WriteKeyColumns(rows, null);
                    return;
                }

                _output.WriteByte(1);
                WriteUnsigned(_stringIndexes[groupKey]);
                WriteColumn(rows.Select(x => x[groupKey]).ToList());
                WriteKeyColumns(GroupedOrder(rows, groupKey), groupKey);
            }

            // Rows can be stored grouped by a small whole number key like the event type. The key is written first in the original order
            // so readers can put the rows back. Lights of one type then sit together, with times and values that repeat
            private string ChooseGroupKey(List<JObject> rows)
            {
                if (rows.Count < MinGroupedRows) return null;

                var candidates = rows[0].Properties().Select(x => x.Name).Where(key =>
                {
                    var values = new HashSet<long>();
                    foreach (var row in rows)
                    {
                        if (!(row[key] is JValue value) || KindOf(value) != KindNumber) return false;
                        var number = GameNumber.From(value);
                        if (number.Type != NumberType.Integer) return false;
                        values.Add(number.Integer);
                        if (values.Count > MaxGroupCount) return false;
                    }
                    return values.Count >= 2;
                }).ToList();
                if (candidates.Count == 0) return null;

                // Only the plain values are compared, nested objects and lists would make every trial as slow as the whole file
                var scalarKeys = KeysOf(rows).Where(key => rows.All(x => x[key] == null || IsScalar(x[key]))).ToList();
                int TrialLength(string groupKey)
                {
                    var ordered = groupKey == null ? rows : GroupedOrder(rows, groupKey);
                    return DeflatedLength(Trial(() =>
                    {
                        foreach (var key in scalarKeys.Where(x => x != groupKey)) WriteColumn(ordered.Where(x => x[key] != null).Select(x => x[key]).ToList());
                    }));
                }

                string best = null;
                var bestLength = TrialLength(null);
                foreach (var candidate in candidates)
                {
                    var length = TrialLength(candidate);
                    if (length < bestLength)
                    {
                        best = candidate;
                        bestLength = length;
                    }
                }
                return best;
            }

            private static List<JObject> GroupedOrder(List<JObject> rows, string groupKey) => rows.OrderBy(x => GameNumber.From((JValue)x[groupKey]).Integer).ToList();

            private static List<string> KeysOf(List<JObject> rows)
            {
                var keys = new List<string>();
                var seen = new HashSet<string>();
                foreach (var row in rows)
                {
                    foreach (var property in row.Properties())
                    {
                        if (seen.Add(property.Name)) keys.Add(property.Name);
                    }
                }
                return keys;
            }

            private void WriteKeyColumns(List<JObject> rows, string skippedKey)
            {
                var keys = KeysOf(rows).Where(x => x != skippedKey).ToList();
                WriteUnsigned(keys.Count);
                foreach (var key in keys)
                {
                    WriteUnsigned(_stringIndexes[key]);

                    var present = rows.Select(x => x[key] != null).ToArray();
                    if (present.All(x => x)) _output.WriteByte(0);
                    else
                    {
                        _output.WriteByte(1);
                        var bitmap = new byte[(rows.Count + 7) / 8];
                        for (var i = 0; i < present.Length; i++)
                        {
                            if (present[i]) bitmap[i / 8] |= (byte)(1 << (i % 8));
                        }
                        _output.Write(bitmap, 0, bitmap.Length);
                    }

                    WriteColumn(rows.Where(x => x[key] != null).Select(x => x[key]).ToList());
                }
            }
        }

        private class Unpacker
        {
            private readonly byte[] _data;
            private readonly string[] _strings;
            private int _offset;
            private long _valueCount;

            public JToken Root { get; }

            public Unpacker(byte[] data)
            {
                _data = data;
                _strings = new string[ReadCount()];
                for (var i = 0; i < _strings.Length; i++)
                {
                    var length = ReadCount();
                    _strings[i] = Encoding.UTF8.GetString(ReadBytes(length));
                }

                Root = ReadColumn(1)[0];
                if (_offset != _data.Length) throw new InvalidDataException("Packed JSON has data after its last value");
            }

            private byte ReadByte()
            {
                if (_offset >= _data.Length) throw new InvalidDataException("Packed JSON is truncated");
                return _data[_offset++];
            }

            private byte[] ReadBytes(int count)
            {
                if (count > _data.Length - _offset) throw new InvalidDataException("Packed JSON is truncated");
                var bytes = new byte[count];
                Array.Copy(_data, _offset, bytes, 0, count);
                _offset += count;
                return bytes;
            }

            private int ReadCount()
            {
                var value = VarInt.ReadUnsigned(_data, ref _offset);
                if (value > int.MaxValue) throw new InvalidDataException("Packed JSON count is too large");
                return (int)value;
            }

            private string ReadString()
            {
                var index = ReadCount();
                if (index >= _strings.Length) throw new InvalidDataException("Packed JSON string index is out of range");
                return _strings[index];
            }

            private long[] ReadIntegers(int count)
            {
                var delta = ReadByte() == 1;
                var values = new long[count];
                long previous = 0;
                for (var i = 0; i < count; i++)
                {
                    var value = VarInt.ReadSigned(_data, ref _offset);
                    if (delta) value += previous;
                    values[i] = previous = value;
                }
                return values;
            }

            private JToken[] ReadColumn(int count)
            {
                _valueCount += count;
                if (_valueCount > MaxUnpackedValues) throw new InvalidDataException("Packed JSON has too many values");

                var kind = ReadByte();
                switch (kind)
                {
                    case KindNumber:
                        return ReadNumbers(count);
                    case KindString:
                        return Enumerable.Range(0, count).Select(_ => (JToken)new JValue(ReadString())).ToArray();
                    case KindList:
                        return ReadLists(count);
                    case KindObject:
                        return ReadObjects(count);
                    case KindLiteral:
                        return ReadBytes(count).Select(ToLiteral).ToArray();
                    case KindMixed:
                        var kinds = ReadBytes(count);
                        var parts = new Queue<JToken>[KindMixed];
                        for (byte part = 0; part < KindMixed; part++)
                        {
                            var partCount = kinds.Count(x => x == part);
                            if (partCount > 0) parts[part] = new Queue<JToken>(ReadColumn(partCount));
                        }
                        return kinds.Select(x => x < KindMixed ? parts[x].Dequeue() : throw new InvalidDataException("Unknown packed JSON kind")).ToArray();
                    default:
                        throw new InvalidDataException($"Unknown packed JSON kind {kind}");
                }
            }

            private static JToken ToLiteral(byte literal)
            {
                switch (literal)
                {
                    case LiteralNull: return JValue.CreateNull();
                    case LiteralFalse: return new JValue(false);
                    case LiteralTrue: return new JValue(true);
                    default: throw new InvalidDataException($"Unknown packed JSON literal {literal}");
                }
            }

            private JToken[] ReadNumbers(int count)
            {
                var denominator = ReadCount();
                if (denominator == 0) throw new InvalidDataException("Packed JSON denominator is 0");

                var forms = ReadByte() == 1 ? ReadBytes(count) : new byte[count];
                var numerators = ReadIntegers(forms.Count(x => x == FormFraction));
                var floats = ReadBytes(4 * forms.Count(x => x == FormFloat));
                var doubles = ReadBytes(8 * forms.Count(x => x == FormDouble));

                var values = new JToken[count];
                int nextNumerator = 0, nextFloat = 0, nextDouble = 0;
                for (var i = 0; i < count; i++)
                {
                    switch (forms[i])
                    {
                        case FormFraction:
                            var numerator = numerators[nextNumerator++];
                            values[i] = numerator % denominator == 0 ? new JValue(numerator / denominator) : new JValue((float)(numerator / (double)denominator));
                            break;
                        case FormFloat:
                            values[i] = new JValue(BitConverter.ToSingle(floats, 4 * nextFloat++));
                            break;
                        case FormDouble:
                            values[i] = new JValue(BitConverter.ToDouble(doubles, 8 * nextDouble++));
                            break;
                        default:
                            throw new InvalidDataException($"Unknown packed JSON number form {forms[i]}");
                    }
                }
                return values;
            }

            private JToken[] ReadLists(int count)
            {
                var lengths = ReadIntegers(count);
                var lists = new JArray[count];
                long total = 0;
                for (var i = 0; i < count; i++)
                {
                    if (lengths[i] < 0 || lengths[i] > int.MaxValue) throw new InvalidDataException("Packed JSON list length is out of range");
                    lists[i] = new JArray();
                    total += lengths[i];
                }
                if (total == 0) return lists;
                if (total > MaxUnpackedValues) throw new InvalidDataException("Packed JSON has too many values");

                var layout = ReadByte();
                if (layout == LayoutFlat)
                {
                    var items = ReadColumn((int)total);
                    var next = 0;
                    for (var i = 0; i < count; i++)
                    {
                        for (var j = 0; j < lengths[i]; j++) lists[i].Add(items[next++]);
                    }
                }
                else if (layout == LayoutPositional)
                {
                    var longest = lengths.Max();
                    for (var position = 0; position < longest; position++)
                    {
                        var targets = lists.Where((_, i) => lengths[i] > position).ToList();
                        var items = ReadColumn(targets.Count);
                        for (var i = 0; i < targets.Count; i++) targets[i].Add(items[i]);
                    }
                }
                else
                {
                    throw new InvalidDataException($"Unknown packed JSON list layout {layout}");
                }
                return lists;
            }

            private JToken[] ReadObjects(int count)
            {
                var rows = new JObject[count];
                for (var i = 0; i < count; i++) rows[i] = new JObject();

                // Grouped rows are stored in the order of their group value, the original order is kept for rows with the same value
                var stored = rows;
                if (ReadByte() == 1)
                {
                    var groupKey = ReadString();
                    var groups = ReadColumn(count);
                    for (var i = 0; i < count; i++) rows[i][groupKey] = groups[i];
                    stored = Enumerable.Range(0, count).OrderBy(i => (double)groups[i]).Select(i => rows[i]).ToArray();
                }

                var keyCount = ReadCount();
                for (var k = 0; k < keyCount; k++)
                {
                    var key = ReadString();
                    var present = stored;
                    if (ReadByte() == 1)
                    {
                        var bitmap = ReadBytes((count + 7) / 8);
                        present = stored.Where((_, i) => (bitmap[i / 8] >> (i % 8) & 1) == 1).ToArray();
                    }

                    var values = ReadColumn(present.Length);
                    for (var i = 0; i < present.Length; i++) present[i][key] = values[i];
                }
                return rows;
            }
        }
    }
}
