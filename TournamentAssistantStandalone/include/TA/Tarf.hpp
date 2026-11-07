#pragma once

#include <cstdint>
#include <string>
#include <utility>
#include <vector>

// Writes .tarf replays, the format described in TARF/README.md and TARF/tarf.proto of the TournamentAssistant repository
namespace TA::Tarf {
    using Bytes = std::vector<uint8_t>;

    // Field numbers used by the recorder, named after tarf.proto
    namespace Fields {
        constexpr uint32_t EventNote = 2, EventWall = 3, EventHeight = 4, EventPause = 5, EventScore = 6, EventCombo = 7, EventMultiplier = 8, EventEnergy = 9, EventMidPlayModifier = 10;
    }

    // A protobuf message being built field by field. Zero values are skipped like protobuf does,
    // the optional variants always write the field because proto3 optional fields care about presence
    class Message {
    public:
        Bytes bytes;

        Message& integer(uint32_t field, int64_t value);
        Message& optionalInteger(uint32_t field, int64_t value);
        Message& signedInteger(uint32_t field, int64_t value);
        Message& optionalSignedInteger(uint32_t field, int64_t value);
        Message& boolean(uint32_t field, bool value);
        Message& optionalBoolean(uint32_t field, bool value);
        Message& floating(uint32_t field, float value);
        Message& optionalFloating(uint32_t field, float value);
        Message& string(uint32_t field, std::string const& value);
        Message& raw(uint32_t field, Bytes const& value);
        Message& message(uint32_t field, Message const& value);
        Message& vector(uint32_t field, float x, float y, float z);
        Message& quaternion(uint32_t field, float x, float y, float z, float w);
        Message& color(uint32_t field, float r, float g, float b, float a);
    };

    struct Pose {
        float x = 0, y = 0, z = 0;
        float rx = 0, ry = 0, rz = 0, rw = 1;
    };

    struct Frame {
        int64_t timeUs = 0;
        int32_t fps = 0;
        Pose head, left, right;
    };

    // Builds a .tarf file while a song is being played. Frames are packed 60 at a time and events are kept encoded,
    // so memory only grows with the size of the finished file. The header is written last so it can hold the map zip
    class Recorder {
    public:
        static constexpr uint32_t PositionStepNm = 10000;
        static constexpr uint32_t RotationStepNano = 1000;
        static constexpr uint32_t AnchorInterval = 60;

        void addFrame(Frame const& frame);
        // Events don't need to arrive in time order, a note is only known once its swing has finished
        void addEvent(int64_t timeUs, uint32_t payloadField, Message const& payload);
        // Adds the motion encoding to the header and the frame and event counts to the footer
        Bytes finish(Message header, Message footer, bool headerHasMapArchive);

        size_t frameCount() const { return frameCount_; }

    private:
        void writeBlock();

        Bytes body_;
        std::vector<Frame> blockFrames_;
        std::vector<std::pair<int64_t, Bytes>> pendingEvents_;
        size_t frameCount_ = 0;
        size_t eventCount_ = 0;
    };

    // The map zip embedded in a replay, with only the files needed to play one difficulty:
    // Info.dat, the difficulty and its lightshow, the BPM data, the cover image and the song. Empty when the difficulty isn't found
    Bytes buildMapArchive(std::string const& folder, std::string const& characteristic, int difficulty);

    // userPlatformId-mapHash-mapCharacteristic-mapDifficulty-unixTimestamp.tarf
    std::string fileName(std::string const& platformId, std::string const& mapHash, std::string const& characteristic, int difficulty, int64_t recordedAtUnixMs);

    // Beat Saber numbers difficulties from Easy = 0 to ExpertPlus = 4
    std::string difficultyName(int difficulty);
}
