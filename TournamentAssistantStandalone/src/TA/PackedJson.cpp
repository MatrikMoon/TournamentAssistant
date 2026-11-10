#include "TA/PackedJson.hpp"

#include "TA/Tarf.hpp"

#include "beatsaber-hook/shared/rapidjson/include/rapidjson/document.h"

#include <algorithm>
#include <cmath>
#include <cstring>
#include <functional>
#include <string_view>
#include <unordered_map>
#include <unordered_set>

namespace TA::PackedJson {
    namespace {
        using Bytes = std::vector<uint8_t>;
        using Values = std::vector<rapidjson::Value const*>;

        constexpr uint8_t KindNumber = 0, KindString = 1, KindList = 2, KindObject = 3, KindLiteral = 4, KindMixed = 5;
        constexpr uint8_t FormFraction = 0, FormFloat = 1, FormDouble = 2;
        constexpr uint8_t LiteralNull = 0, LiteralFalse = 1, LiteralTrue = 2;
        constexpr uint8_t LayoutPositional = 0, LayoutFlat = 1;

        // Whole numbers below this are exact as doubles, which is all a JSON reader keeps
        constexpr int64_t MaxExactInteger = int64_t(1) << 53;

        // The writer uses the first one that fits every number of a column, readers accept any denominator
        constexpr int Denominators[] = { 1, 2, 3, 4, 5, 6, 8, 10, 12, 16, 20, 24, 32, 40, 48, 64, 96, 100, 128, 192, 256, 1000, 10000, 100000, 1000000 };

        constexpr size_t MaxPositionalListLength = 8;
        constexpr size_t MinGroupedRows = 16;
        constexpr size_t MaxGroupCount = 64;

        void varint(Bytes& out, uint64_t value) {
            while (value >= 0x80) {
                out.push_back(uint8_t(value | 0x80));
                value >>= 7;
            }
            out.push_back(uint8_t(value));
        }

        void zigZag(Bytes& out, int64_t value) { varint(out, (uint64_t(value) << 1) ^ uint64_t(value >> 63)); }

        template <typename T>
        void littleEndian(Bytes& out, T value) {
            uint8_t bytes[sizeof(T)];
            std::memcpy(bytes, &value, sizeof(T));
            out.insert(out.end(), bytes, bytes + sizeof(T));
        }

        std::string_view text(rapidjson::Value const& value) { return { value.GetString(), value.GetStringLength() }; }

        rapidjson::Value const* member(rapidjson::Value const& object, std::string_view key) {
            for (auto const& entry : object.GetObject()) {
                if (text(entry.name) == key) return &entry.value;
            }
            return nullptr;
        }

        uint8_t kindOf(rapidjson::Value const& value) {
            if (value.IsNumber()) return KindNumber;
            if (value.IsString()) return KindString;
            if (value.IsArray()) return KindList;
            if (value.IsObject()) return KindObject;
            return KindLiteral;
        }

        bool isScalar(rapidjson::Value const& value) { return !value.IsArray() && !value.IsObject(); }

        // A number as Beat Saber reads it. Doubles are only needed for whole numbers too large to be exact otherwise
        struct GameNumber {
            enum Type { Integer, Float, Double } type = Integer;
            int64_t integer = 0;
            float single = 0;
            double number = 0;

            static GameNumber from(rapidjson::Value const& value) {
                if (value.IsInt64()) {
                    auto integer = value.GetInt64();
                    if (integer > -MaxExactInteger && integer < MaxExactInteger) return { Integer, integer };
                }
                double number = value.GetDouble();
                if (std::abs(number) < double(MaxExactInteger) && std::floor(number) == number) return { Integer, int64_t(number) };
                if (std::isnan(number) || std::abs(number) >= double(MaxExactInteger)) return { Double, 0, 0, number };
                return { Float, 0, float(number) };
            }

            // Finds the numerator that gives this number back when divided by the denominator
            bool fit(int denominator, int64_t& numerator) const {
                if (type == Integer) {
                    if (std::abs(integer) >= MaxExactInteger / denominator) return false;
                    numerator = integer * denominator;
                    return true;
                }
                if (type == Float) {
                    double scaled = double(single) * denominator;
                    if (std::abs(scaled) >= double(MaxExactInteger)) return false;
                    numerator = int64_t(std::nearbyint(scaled));
                    return float(double(numerator) / denominator) == single;
                }
                return false;
            }
        };

        size_t deflatedLength(Bytes const& data) {
            Bytes compressed;
            return Tarf::deflateRaw(data, compressed) ? compressed.size() : data.size();
        }

        class Packer {
        public:
            explicit Packer(rapidjson::Value const& root) {
                // The most used strings get the smallest indexes
                std::unordered_map<std::string_view, size_t> counts;
                std::vector<std::string_view> firstSeen;
                countStrings(root, counts, firstSeen);
                std::stable_sort(firstSeen.begin(), firstSeen.end(), [&](auto left, auto right) { return counts[left] > counts[right]; });

                varint(out_, firstSeen.size());
                for (auto value : firstSeen) {
                    indexes_.emplace(value, uint32_t(indexes_.size()));
                    varint(out_, value.size());
                    out_.insert(out_.end(), value.begin(), value.end());
                }

                column({ &root });
            }

            Bytes take() { return std::move(out_); }

        private:
            Bytes out_;
            std::unordered_map<std::string_view, uint32_t> indexes_;

            static void countStrings(rapidjson::Value const& value, std::unordered_map<std::string_view, size_t>& counts, std::vector<std::string_view>& firstSeen) {
                auto count = [&](std::string_view string) {
                    if (counts[string]++ == 0) firstSeen.push_back(string);
                };
                if (value.IsObject()) {
                    for (auto const& entry : value.GetObject()) {
                        count(text(entry.name));
                        countStrings(entry.value, counts, firstSeen);
                    }
                } else if (value.IsArray()) {
                    for (auto const& item : value.GetArray()) countStrings(item, counts, firstSeen);
                } else if (value.IsString()) {
                    count(text(value));
                }
            }

            // Writes into a scratch buffer, so the writer can compare ways of storing the same values
            Bytes trial(std::function<void()> const& write) {
                Bytes saved = std::move(out_);
                out_.clear();
                write();
                Bytes result = std::move(out_);
                out_ = std::move(saved);
                return result;
            }

            // Every value of one key, from all the objects that have it
            void column(Values const& values) {
                std::vector<uint8_t> kinds;
                kinds.reserve(values.size());
                for (auto value : values) kinds.push_back(kindOf(*value));

                if (std::any_of(kinds.begin(), kinds.end(), [&](uint8_t kind) { return kind != kinds[0]; })) {
                    out_.push_back(KindMixed);
                    out_.insert(out_.end(), kinds.begin(), kinds.end());
                    for (uint8_t kind = 0; kind < KindMixed; kind++) {
                        Values part;
                        for (size_t i = 0; i < values.size(); i++) {
                            if (kinds[i] == kind) part.push_back(values[i]);
                        }
                        if (!part.empty()) column(part);
                    }
                    return;
                }

                out_.push_back(kinds[0]);
                switch (kinds[0]) {
                    case KindNumber: {
                        std::vector<GameNumber> numbers;
                        numbers.reserve(values.size());
                        for (auto value : values) numbers.push_back(GameNumber::from(*value));
                        writeNumbers(numbers);
                        break;
                    }
                    case KindString:
                        for (auto value : values) varint(out_, indexes_.at(text(*value)));
                        break;
                    case KindList:
                        writeLists(values);
                        break;
                    case KindObject:
                        writeObjects(values);
                        break;
                    default:
                        for (auto value : values) out_.push_back(value->IsBool() ? value->GetBool() ? LiteralTrue : LiteralFalse : LiteralNull);
                        break;
                }
            }

            // Plain or as differences from the previous value, whichever is smaller. Sorted values like times get small differences
            void writeIntegers(std::vector<int64_t> const& values) {
                Bytes plain, delta;
                int64_t previous = 0;
                for (auto value : values) {
                    zigZag(plain, value);
                    zigZag(delta, value - previous);
                    previous = value;
                }
                bool useDelta = delta.size() < plain.size();
                out_.push_back(useDelta ? 1 : 0);
                auto const& chosen = useDelta ? delta : plain;
                out_.insert(out_.end(), chosen.begin(), chosen.end());
            }

            void writeNumbers(std::vector<GameNumber> const& numbers) {
                int denominator = Denominators[0];
                int64_t mostFitting = -1;
                for (int candidate : Denominators) {
                    int64_t fitting = 0, numerator;
                    for (auto const& number : numbers) fitting += number.fit(candidate, numerator);
                    if (fitting > mostFitting) {
                        denominator = candidate;
                        mostFitting = fitting;
                    }
                    if (fitting == int64_t(numbers.size())) break;
                }
                varint(out_, denominator);

                std::vector<uint8_t> forms(numbers.size());
                std::vector<int64_t> numerators;
                Bytes floats, doubles;
                for (size_t i = 0; i < numbers.size(); i++) {
                    int64_t numerator;
                    if (numbers[i].fit(denominator, numerator)) {
                        forms[i] = FormFraction;
                        numerators.push_back(numerator);
                    } else if (numbers[i].type == GameNumber::Float) {
                        forms[i] = FormFloat;
                        littleEndian(floats, numbers[i].single);
                    } else {
                        forms[i] = FormDouble;
                        littleEndian(doubles, numbers[i].type == GameNumber::Integer ? double(numbers[i].integer) : numbers[i].number);
                    }
                }

                if (numerators.size() == numbers.size()) {
                    out_.push_back(0);
                } else {
                    out_.push_back(1);
                    out_.insert(out_.end(), forms.begin(), forms.end());
                }
                writeIntegers(numerators);
                out_.insert(out_.end(), floats.begin(), floats.end());
                out_.insert(out_.end(), doubles.begin(), doubles.end());
            }

            void writeLists(Values const& lists) {
                std::vector<int64_t> lengths;
                size_t longest = 0;
                bool scalars = true;
                for (auto list : lists) {
                    lengths.push_back(list->Size());
                    longest = std::max<size_t>(longest, list->Size());
                    for (auto const& item : list->GetArray()) scalars = scalars && isScalar(item);
                }
                writeIntegers(lengths);
                if (longest == 0) return;

                auto writeFlat = [&]() {
                    out_.push_back(LayoutFlat);
                    Values items;
                    for (auto list : lists) {
                        for (auto const& item : list->GetArray()) items.push_back(&item);
                    }
                    column(items);
                };

                // Short lists like [x, y, z] can get a column per position, which suits values that change independently.
                // Lists that repeat as a whole, like colors, compress better left in order
                if (longest <= MaxPositionalListLength && lists.size() > 1 && scalars) {
                    auto flat = trial(writeFlat);
                    auto positional = trial([&]() {
                        out_.push_back(LayoutPositional);
                        for (size_t position = 0; position < longest; position++) {
                            Values items;
                            for (auto list : lists) {
                                if (list->Size() > position) items.push_back(&(*list)[rapidjson::SizeType(position)]);
                            }
                            column(items);
                        }
                    });
                    auto const& chosen = deflatedLength(positional) < deflatedLength(flat) ? positional : flat;
                    out_.insert(out_.end(), chosen.begin(), chosen.end());
                } else {
                    writeFlat();
                }
            }

            static std::vector<std::string_view> keysOf(Values const& rows) {
                std::vector<std::string_view> keys;
                std::unordered_set<std::string_view> seen;
                for (auto row : rows) {
                    for (auto const& entry : row->GetObject()) {
                        if (seen.insert(text(entry.name)).second) keys.push_back(text(entry.name));
                    }
                }
                return keys;
            }

            static Values groupedOrder(Values rows, std::string_view groupKey) {
                std::stable_sort(rows.begin(), rows.end(), [&](auto left, auto right) {
                    return GameNumber::from(*member(*left, groupKey)).integer < GameNumber::from(*member(*right, groupKey)).integer;
                });
                return rows;
            }

            // Rows can be stored grouped by a small whole number key like the event type. The key is written first in the original order
            // so readers can put the rows back. Lights of one type then sit together, with times and values that repeat
            std::optional<std::string_view> chooseGroupKey(Values const& rows) {
                if (rows.size() < MinGroupedRows) return std::nullopt;

                std::vector<std::string_view> candidates;
                for (auto const& entry : rows[0]->GetObject()) {
                    auto key = text(entry.name);
                    std::unordered_set<int64_t> groups;
                    bool groupable = true;
                    for (auto row : rows) {
                        auto value = member(*row, key);
                        if (!value || !value->IsNumber() || GameNumber::from(*value).type != GameNumber::Integer) {
                            groupable = false;
                            break;
                        }
                        groups.insert(GameNumber::from(*value).integer);
                        if (groups.size() > MaxGroupCount) {
                            groupable = false;
                            break;
                        }
                    }
                    if (groupable && groups.size() >= 2) candidates.push_back(key);
                }
                if (candidates.empty()) return std::nullopt;

                // Only the plain values are compared, nested objects and lists would make every trial as slow as the whole file
                std::vector<std::string_view> scalarKeys;
                for (auto key : keysOf(rows)) {
                    bool scalar = std::all_of(rows.begin(), rows.end(), [&](auto row) {
                        auto value = member(*row, key);
                        return !value || isScalar(*value);
                    });
                    if (scalar) scalarKeys.push_back(key);
                }

                auto trialLength = [&](std::optional<std::string_view> groupKey) {
                    auto ordered = groupKey ? groupedOrder(rows, *groupKey) : rows;
                    return deflatedLength(trial([&]() {
                        for (auto key : scalarKeys) {
                            if (groupKey && key == *groupKey) continue;
                            Values values;
                            for (auto row : ordered) {
                                if (auto value = member(*row, key)) values.push_back(value);
                            }
                            column(values);
                        }
                    }));
                };

                std::optional<std::string_view> best;
                auto bestLength = trialLength(std::nullopt);
                for (auto candidate : candidates) {
                    auto length = trialLength(candidate);
                    if (length < bestLength) {
                        best = candidate;
                        bestLength = length;
                    }
                }
                return best;
            }

            void writeObjects(Values const& rows) {
                auto groupKey = chooseGroupKey(rows);
                if (!groupKey) {
                    out_.push_back(0);
                    writeKeyColumns(rows, std::nullopt);
                    return;
                }

                out_.push_back(1);
                varint(out_, indexes_.at(*groupKey));
                Values groups;
                for (auto row : rows) groups.push_back(member(*row, *groupKey));
                column(groups);
                writeKeyColumns(groupedOrder(rows, *groupKey), groupKey);
            }

            void writeKeyColumns(Values const& rows, std::optional<std::string_view> skippedKey) {
                auto keys = keysOf(rows);
                if (skippedKey) keys.erase(std::remove(keys.begin(), keys.end(), *skippedKey), keys.end());

                varint(out_, keys.size());
                for (auto key : keys) {
                    varint(out_, indexes_.at(key));

                    Values values;
                    Bytes bitmap((rows.size() + 7) / 8);
                    for (size_t i = 0; i < rows.size(); i++) {
                        if (auto value = member(*rows[i], key)) {
                            values.push_back(value);
                            bitmap[i / 8] |= uint8_t(1 << (i % 8));
                        }
                    }

                    if (values.size() == rows.size()) {
                        out_.push_back(0);
                    } else {
                        out_.push_back(1);
                        out_.insert(out_.end(), bitmap.begin(), bitmap.end());
                    }
                    column(values);
                }
            }
        };
    }

    std::optional<std::vector<uint8_t>> pack(std::vector<uint8_t> const& json) {
        size_t start = json.size() >= 3 && json[0] == 0xEF && json[1] == 0xBB && json[2] == 0xBF ? 3 : 0;

        // Full precision, the fast path can be off by a bit, which decides which float some numbers become
        rapidjson::Document document;
        document.Parse<rapidjson::kParseFullPrecisionFlag>(reinterpret_cast<char const*>(json.data() + start), json.size() - start);
        if (document.HasParseError()) return std::nullopt;

        return Packer(document).take();
    }
}
