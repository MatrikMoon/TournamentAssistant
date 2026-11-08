#include "TA/Tarf.hpp"

#include "TA/PackedJson.hpp"

#include "beatsaber-hook/shared/rapidjson/include/rapidjson/document.h"

#include <zlib.h>

#include <algorithm>
#include <array>
#include <cctype>
#include <cmath>
#include <cstdlib>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <map>

namespace TA::Tarf {
    namespace {
        constexpr uint32_t WireVarint = 0, WireLength = 2, WireFixed32 = 5;
        constexpr uint8_t RecordHeader = 1, RecordMotionBlock = 2, RecordEventBatch = 3, RecordFooter = 4;
        constexpr int PoseValueCount = 7;
        constexpr int ValuesPerFrame = 2 + 3 * PoseValueCount;
        constexpr double NanoUnitsPerUnit = 1'000'000'000.0;
        using Values = std::array<int64_t, ValuesPerFrame>;

        void varint(Bytes& out, uint64_t value) {
            while (value >= 0x80) {
                out.push_back(uint8_t(value | 0x80));
                value >>= 7;
            }
            out.push_back(uint8_t(value));
        }

        // The same ZigZag mapping protobuf uses for sint64
        void zigZag(Bytes& out, int64_t value) { varint(out, (uint64_t(value) << 1) ^ uint64_t(value >> 63)); }

        void tag(Bytes& out, uint32_t field, uint32_t wire) { varint(out, (uint64_t(field) << 3) | wire); }

        void littleEndian(Bytes& out, uint32_t value, int byteCount) {
            for (int i = 0; i < byteCount; i++) out.push_back(uint8_t(value >> (8 * i)));
        }

        // The decoder only needs the steps to be multiplied back, so this rounds the same way as the C# writer apart from exact ties
        void quantizePose(Pose const& pose, int64_t* values, int64_t const* previous) {
            constexpr double positionStep = Recorder::PositionStepNm / NanoUnitsPerUnit;
            constexpr double rotationStep = Recorder::RotationStepNano / NanoUnitsPerUnit;
            values[0] = std::llround(pose.x / positionStep);
            values[1] = std::llround(pose.y / positionStep);
            values[2] = std::llround(pose.z / positionStep);

            // q and -q are the same rotation. Keeping the sign consistent with the previous frame avoids huge deltas when the game flips it
            float sign = 1;
            if (previous) {
                double agreement = pose.rx * double(previous[3]) + pose.ry * double(previous[4]) + pose.rz * double(previous[5]) + pose.rw * double(previous[6]);
                if (agreement < 0) sign = -1;
            }
            values[3] = std::llround(sign * pose.rx / rotationStep);
            values[4] = std::llround(sign * pose.ry / rotationStep);
            values[5] = std::llround(sign * pose.rz / rotationStep);
            values[6] = std::llround(sign * pose.rw / rotationStep);
        }

        void quantize(Frame const& frame, Values& values, Values const* previous) {
            values[0] = frame.timeUs;
            values[1] = frame.fps;
            quantizePose(frame.head, &values[2], previous ? &(*previous)[2] : nullptr);
            quantizePose(frame.left, &values[2 + PoseValueCount], previous ? &(*previous)[2 + PoseValueCount] : nullptr);
            quantizePose(frame.right, &values[2 + 2 * PoseValueCount], previous ? &(*previous)[2 + 2 * PoseValueCount] : nullptr);
        }

        // Each value is stored as its difference from a prediction: 0 for the anchor frame, the previous value for the second frame,
        // and the previous value moving at the same speed as in the last two frames after that
        Bytes packFrames(std::vector<Frame> const& frames) {
            Bytes packed;
            packed.reserve(frames.size() * ValuesPerFrame * 2);
            Values current{}, previous{}, beforePrevious{};
            for (size_t i = 0; i < frames.size(); i++) {
                quantize(frames[i], current, i > 0 ? &previous : nullptr);
                for (int value = 0; value < ValuesPerFrame; value++) {
                    int64_t prediction = i == 0 ? 0 : i == 1 ? previous[value] : 2 * previous[value] - beforePrevious[value];
                    zigZag(packed, current[value] - prediction);
                }
                beforePrevious = previous;
                previous = current;
            }
            return packed;
        }

        // Deflate is only kept when it makes the record smaller
        void writeRecord(Bytes& out, uint8_t type, Bytes const& payload, bool tryDeflate = true) {
            Bytes compressed;
            bool useDeflate = tryDeflate && !payload.empty() && deflateRaw(payload, compressed) && compressed.size() < payload.size();
            Bytes const& stored = useDeflate ? compressed : payload;

            out.push_back(type);
            out.push_back(useDeflate ? 1 : 0);
            littleEndian(out, 0, 2);
            littleEndian(out, uint32_t(stored.size()), 4);
            littleEndian(out, uint32_t(payload.size()), 4);
            littleEndian(out, uint32_t(crc32(crc32(0L, Z_NULL, 0), payload.data(), uInt(payload.size()))), 4);
            out.insert(out.end(), stored.begin(), stored.end());
        }

        std::string lowercase(std::string value) {
            std::transform(value.begin(), value.end(), value.begin(), [](unsigned char c) { return std::tolower(c); });
            return value;
        }

        bool equalsIgnoringCase(rapidjson::Value const& value, std::string const& expected) {
            return value.IsString() && lowercase(value.GetString()) == lowercase(expected);
        }

        std::string stringMember(rapidjson::Value const& object, char const* name) {
            auto member = object.FindMember(name);
            return member != object.MemberEnd() && member->value.IsString() ? member->value.GetString() : "";
        }

        Bytes readFile(std::filesystem::path const& path) {
            std::ifstream input(path, std::ios::binary);
            return Bytes((std::istreambuf_iterator<char>(input)), std::istreambuf_iterator<char>());
        }

        // Returns the files to keep besides Info.dat, or nothing when the difficulty isn't in the Info.dat
        std::vector<std::string> wantedFiles(rapidjson::Document const& info, std::string const& characteristic, std::string const& difficulty) {
            auto audio = info.FindMember("audio");
            if (audio != info.MemberEnd() && audio->value.IsObject()) {
                // Version 4 Info.dat
                auto beatmaps = info.FindMember("difficultyBeatmaps");
                if (beatmaps == info.MemberEnd() || !beatmaps->value.IsArray()) return {};
                for (auto const& beatmap : beatmaps->value.GetArray()) {
                    if (!beatmap.IsObject() || !beatmap.HasMember("characteristic") || !beatmap.HasMember("difficulty")) continue;
                    if (!equalsIgnoringCase(beatmap["characteristic"], characteristic) || !equalsIgnoringCase(beatmap["difficulty"], difficulty)) continue;
                    return {
                        stringMember(beatmap, "beatmapDataFilename"),
                        stringMember(beatmap, "lightshowDataFilename"),
                        stringMember(audio->value, "songFilename"),
                        stringMember(audio->value, "audioDataFilename"),
                        stringMember(info, "coverImageFilename"),
                    };
                }
                return {};
            }

            // Version 2 and 3 Info.dat
            auto sets = info.FindMember("_difficultyBeatmapSets");
            if (sets == info.MemberEnd() || !sets->value.IsArray()) return {};
            for (auto const& set : sets->value.GetArray()) {
                if (!set.IsObject() || !set.HasMember("_beatmapCharacteristicName") || !equalsIgnoringCase(set["_beatmapCharacteristicName"], characteristic)) continue;
                auto beatmaps = set.FindMember("_difficultyBeatmaps");
                if (beatmaps == set.MemberEnd() || !beatmaps->value.IsArray()) continue;
                for (auto const& beatmap : beatmaps->value.GetArray()) {
                    if (!beatmap.IsObject() || !beatmap.HasMember("_difficulty") || !equalsIgnoringCase(beatmap["_difficulty"], difficulty)) continue;
                    return {
                        stringMember(beatmap, "_beatmapFilename"),
                        stringMember(info, "_songFilename"),
                        stringMember(info, "_coverImageFilename"),
                        // Not listed in the Info.dat, but read by mods and viewers when it is there
                        "BPMInfo.dat",
                    };
                }
            }
            return {};
        }

        constexpr uint32_t MapFileStored = 1, MapFileDeflate = 2, MapFilePackedJson = 3;

        // Larger files are only deflated, parsing them would take hundreds of megabytes on the headset
        constexpr size_t MaxPackedJsonSize = 32 * 1024 * 1024;

        bool hasJsonExtension(std::string const& name) {
            auto extension = lowercase(std::filesystem::path(name).extension().string());
            return extension == ".dat" || extension == ".json";
        }

        // Uses whichever encoding is smallest. Map JSON is packed into columns when that beats deflating its text
        Message encodeMapFile(std::string const& name, Bytes const& data) {
            uint32_t encoding = MapFileStored;
            Bytes const* stored = &data;

            Bytes deflated;
            if (deflateRaw(data, deflated) && deflated.size() < stored->size()) {
                encoding = MapFileDeflate;
                stored = &deflated;
            }

            Bytes packedDeflated;
            if (hasJsonExtension(name) && data.size() <= MaxPackedJsonSize) {
                auto packed = PackedJson::pack(data);
                if (packed && deflateRaw(*packed, packedDeflated) && packedDeflated.size() < stored->size()) {
                    encoding = MapFilePackedJson;
                    stored = &packedDeflated;
                }
            }

            Message file;
            file.string(1, name).integer(2, encoding).raw(3, *stored);
            return file;
        }
    }

    bool deflateRaw(Bytes const& input, Bytes& output) {
        z_stream stream{};
        if (deflateInit2(&stream, Z_DEFAULT_COMPRESSION, Z_DEFLATED, -15, 8, Z_DEFAULT_STRATEGY) != Z_OK) return false;
        output.resize(deflateBound(&stream, input.size()));
        stream.next_in = const_cast<Bytef*>(input.data());
        stream.avail_in = uInt(input.size());
        stream.next_out = output.data();
        stream.avail_out = uInt(output.size());
        auto result = deflate(&stream, Z_FINISH);
        output.resize(stream.total_out);
        deflateEnd(&stream);
        return result == Z_STREAM_END;
    }

    Message& Message::integer(uint32_t field, int64_t value) {
        if (value != 0) optionalInteger(field, value);
        return *this;
    }

    // Negative values are written as 64 bit two's complement, like protobuf does for int32 and int64
    Message& Message::optionalInteger(uint32_t field, int64_t value) {
        tag(bytes, field, WireVarint);
        varint(bytes, uint64_t(value));
        return *this;
    }

    Message& Message::signedInteger(uint32_t field, int64_t value) {
        if (value != 0) optionalSignedInteger(field, value);
        return *this;
    }

    Message& Message::optionalSignedInteger(uint32_t field, int64_t value) {
        tag(bytes, field, WireVarint);
        zigZag(bytes, value);
        return *this;
    }

    Message& Message::boolean(uint32_t field, bool value) {
        if (value) optionalBoolean(field, value);
        return *this;
    }

    Message& Message::optionalBoolean(uint32_t field, bool value) {
        tag(bytes, field, WireVarint);
        bytes.push_back(value ? 1 : 0);
        return *this;
    }

    Message& Message::floating(uint32_t field, float value) {
        if (value != 0 && std::isfinite(value)) optionalFloating(field, value);
        return *this;
    }

    Message& Message::optionalFloating(uint32_t field, float value) {
        if (!std::isfinite(value)) value = 0;
        uint32_t bits;
        std::memcpy(&bits, &value, sizeof(bits));
        tag(bytes, field, WireFixed32);
        littleEndian(bytes, bits, 4);
        return *this;
    }

    Message& Message::string(uint32_t field, std::string const& value) {
        if (value.empty()) return *this;
        tag(bytes, field, WireLength);
        varint(bytes, value.size());
        bytes.insert(bytes.end(), value.begin(), value.end());
        return *this;
    }

    Message& Message::raw(uint32_t field, Bytes const& value) {
        tag(bytes, field, WireLength);
        varint(bytes, value.size());
        bytes.insert(bytes.end(), value.begin(), value.end());
        return *this;
    }

    Message& Message::message(uint32_t field, Message const& value) { return raw(field, value.bytes); }

    Message& Message::vector(uint32_t field, float x, float y, float z) {
        return message(field, Message().floating(1, x).floating(2, y).floating(3, z));
    }

    Message& Message::quaternion(uint32_t field, float x, float y, float z, float w) {
        return message(field, Message().floating(1, x).floating(2, y).floating(3, z).floating(4, w));
    }

    Message& Message::color(uint32_t field, float r, float g, float b, float a) {
        return message(field, Message().floating(1, r).floating(2, g).floating(3, b).floating(4, a));
    }

    void Recorder::addFrame(Frame const& frame) {
        if (blockFrames_.capacity() < AnchorInterval) blockFrames_.reserve(AnchorInterval);
        blockFrames_.push_back(frame);
        frameCount_++;
        if (blockFrames_.size() == AnchorInterval) writeBlock();
    }

    void Recorder::addEvent(int64_t timeUs, uint32_t payloadField, Message const& payload) {
        Message event;
        event.signedInteger(1, timeUs).message(payloadField, payload);
        pendingEvents_.emplace_back(timeUs, std::move(event.bytes));
        eventCount_++;
    }

    // Writes the frames collected so far as one motion block, followed by the events that came in meanwhile
    void Recorder::writeBlock() {
        if (!blockFrames_.empty()) {
            Message block;
            block.integer(1, int64_t(frameCount_ - blockFrames_.size()))
                .integer(2, int64_t(blockFrames_.size()))
                .raw(3, packFrames(blockFrames_));
            writeRecord(body_, RecordMotionBlock, block.bytes);
            blockFrames_.clear();
        }

        if (!pendingEvents_.empty()) {
            std::stable_sort(pendingEvents_.begin(), pendingEvents_.end(), [](auto const& left, auto const& right) { return left.first < right.first; });
            Message batch;
            for (auto const& event : pendingEvents_) batch.raw(1, event.second);
            writeRecord(body_, RecordEventBatch, batch.bytes);
            pendingEvents_.clear();
        }
    }

    Bytes Recorder::finish(Message header, Message footer, bool headerHasMapFiles) {
        writeBlock();

        header.message(10, Message().integer(1, PositionStepNm).integer(2, RotationStepNano).integer(3, AnchorInterval));
        footer.integer(4, int64_t(frameCount_)).integer(5, int64_t(eventCount_));

        Bytes file;
        file.reserve(body_.size() + header.bytes.size() + 64);
        file.insert(file.end(), { 'T', 'A', 'R', 'F' });
        littleEndian(file, 1, 2);
        littleEndian(file, 0, 2);
        // Embedded map files are already compressed, deflating them again only costs time
        writeRecord(file, RecordHeader, header.bytes, !headerHasMapFiles);
        file.insert(file.end(), body_.begin(), body_.end());
        writeRecord(file, RecordFooter, footer.bytes);
        return file;
    }

    std::vector<Message> buildMapFiles(std::string const& folder, std::string const& characteristic, int difficulty) {
        std::error_code error;
        if (folder.empty() || !std::filesystem::is_directory(folder, error)) return {};

        // Quest's file system cares about case, map Info.dat files don't always agree with the files they name
        std::map<std::string, std::filesystem::path> files;
        for (auto const& entry : std::filesystem::directory_iterator(folder, error)) {
            if (entry.is_regular_file(error)) files[lowercase(entry.path().filename().string())] = entry.path();
        }

        auto info = files.find("info.dat");
        if (info == files.end()) return {};
        auto infoBytes = readFile(info->second);

        rapidjson::Document document;
        document.Parse(reinterpret_cast<char const*>(infoBytes.data()), infoBytes.size());
        if (document.HasParseError() || !document.IsObject()) return {};

        auto wanted = wantedFiles(document, characteristic, difficultyName(difficulty));
        if (wanted.empty()) return {};

        std::vector<Message> mapFiles;
        std::vector<std::string> added;
        auto add = [&](std::filesystem::path const& path) {
            auto name = path.filename().string();
            if (std::find(added.begin(), added.end(), lowercase(name)) != added.end()) return;
            added.push_back(lowercase(name));
            mapFiles.push_back(encodeMapFile(name, readFile(path)));
        };

        add(info->second);
        for (auto const& name : wanted) {
            auto file = files.find(lowercase(name));
            if (!name.empty() && file != files.end()) add(file->second);
        }
        return mapFiles;
    }

    std::string difficultyName(int difficulty) {
        constexpr char const* names[] = { "Easy", "Normal", "Hard", "Expert", "ExpertPlus" };
        return difficulty >= 0 && difficulty < 5 ? names[difficulty] : "";
    }

    std::string fileName(std::string const& platformId, std::string const& mapHash, std::string const& characteristic, int difficulty, int64_t recordedAtUnixMs) {
        // A fixed list, so every platform names the same replay the same way
        constexpr char replaced[] = "-/\\:*?\"<>|";
        auto clean = [&](std::string value) {
            for (auto& c : value) {
                if (static_cast<unsigned char>(c) < ' ' || std::strchr(replaced, c)) c = '_';
            }
            return value;
        };
        return clean(platformId) + "-" + clean(mapHash) + "-" + clean(characteristic) + "-" + clean(difficultyName(difficulty)) + "-" + std::to_string(recordedAtUnixMs / 1000) + ".tarf";
    }
}
