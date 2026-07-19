#include "TA/ReplayStreaming.hpp"

#include "TA/Client.hpp"
#include "main.hpp"

#include "GlobalNamespace/AudioTimeSyncController.hpp"
#include "GlobalNamespace/ColorManager.hpp"
#include "GlobalNamespace/ColorType.hpp"
#include "GlobalNamespace/ComboController.hpp"
#include "GlobalNamespace/GameEnergyCounter.hpp"
#include "GlobalNamespace/NoteData.hpp"
#include "GlobalNamespace/PlayerTransforms.hpp"
#include "GlobalNamespace/ScoreController.hpp"
#include "GlobalNamespace/ScoringElement.hpp"
#include "GlobalNamespace/VariableMovementDataProvider.hpp"
#include "UnityEngine/Application.hpp"
#include "UnityEngine/Resources.hpp"
#include "UnityEngine/Time.hpp"

#include <algorithm>
#include <chrono>
#include <cmath>
#include <cstring>
#include <limits>
#include <random>
#include <string>
#include <vector>

namespace TA::ReplayStreaming {
    using Bytes = std::vector<uint8_t>;
    constexpr uint32_t Varint = 0, Fixed32 = 5, Length = 2;
    constexpr float PoseIntervalSeconds = 1.0f / 60.0f;
    constexpr float MaxChunkAgeSeconds = 1.0f;
    constexpr float ComponentLookupIntervalSeconds = 0.25f;
    constexpr size_t MaxEventsPerChunk = 220;
    std::string streamId;
    std::string platformId;
    uint64_t sequence = 1, chunkCount = 0;
    uint64_t totalFrames = 0, totalNotes = 0, totalScores = 0, totalCombos = 0, totalEnergies = 0;
    size_t eventCount = 0;
    float minTime = 0, maxTime = 0, batchStartedAt = 0, nextPoseAt = 0, nextComponentLookupAt = 0;
    int lastScore = std::numeric_limits<int>::min();
    int lastCombo = std::numeric_limits<int>::min();
    float lastEnergy = std::numeric_limits<float>::quiet_NaN();
    bool active = false;
    Bytes batch;
    Bytes frameScratch;
    Bytes poseScratch;
    Bytes vectorScratch;
    Bytes quaternionScratch;
    Bytes eventScratch;
    Bytes nestedScratch;
    Bytes cursorScratch;
    Bytes countsScratch;
    Bytes chunkScratch;
    GlobalNamespace::PlayerTransforms* playerTransforms = nullptr;
    GlobalNamespace::AudioTimeSyncController* audioTimeSyncController = nullptr;
    GlobalNamespace::ComboController* comboController = nullptr;
    GlobalNamespace::GameEnergyCounter* gameEnergyCounter = nullptr;

    template <typename T> T* firstResource() {
        auto values = UnityEngine::Resources::FindObjectsOfTypeAll<T*>();
        return values && values.size() ? values[0] : nullptr;
    }
    void varint(Bytes& out, uint64_t value) { while (value >= 0x80) { out.push_back(uint8_t(value | 0x80)); value >>= 7; } out.push_back(uint8_t(value)); }
    void tag(Bytes& out, uint32_t field, uint32_t wire) { varint(out, (uint64_t(field) << 3) | wire); }
    void integer(Bytes& out, uint32_t field, int64_t value) { if (!value) return; tag(out, field, Varint); varint(out, uint64_t(value)); }
    void boolean(Bytes& out, uint32_t field, bool value) { if (value) { tag(out, field, Varint); varint(out, 1); } }
    void floating(Bytes& out, uint32_t field, float value) { if (value == 0 || !std::isfinite(value)) return; tag(out, field, Fixed32); auto p = reinterpret_cast<uint8_t*>(&value); out.insert(out.end(), p, p + 4); }
    void string(Bytes& out, uint32_t field, std::string const& value) { if (value.empty()) return; tag(out, field, Length); varint(out, value.size()); out.insert(out.end(), value.begin(), value.end()); }
    void message(Bytes& out, uint32_t field, Bytes const& value) { if (value.empty()) return; tag(out, field, Length); varint(out, value.size()); out.insert(out.end(), value.begin(), value.end()); }
    int64_t nowMs() { return std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::system_clock::now().time_since_epoch()).count(); }
    std::string id() { static std::mt19937_64 rng(std::random_device{}()); return std::to_string(rng()) + std::to_string(rng()); }
    std::string hash(std::string value) { constexpr char prefix[] = "custom_level_"; if (value.rfind(prefix, 0) == 0) value.erase(0, sizeof(prefix) - 1); std::transform(value.begin(), value.end(), value.begin(), ::toupper); return value; }
    int replayDifficulty(int difficulty) { constexpr int ratings[] = { 1, 3, 5, 7, 9 }; return difficulty >= 0 && difficulty < 5 ? ratings[difficulty] : difficulty; }
    std::string replayDifficultyName(int difficulty) { constexpr char const* names[] = { "Easy", "Normal", "Hard", "Expert", "ExpertPlus" }; return difficulty >= 0 && difficulty < 5 ? names[difficulty] : ""; }

    void prepareBuffers() {
        batch.reserve(65536);
        frameScratch.reserve(192);
        poseScratch.reserve(64);
        vectorScratch.reserve(24);
        quaternionScratch.reserve(32);
        eventScratch.reserve(128);
        nestedScratch.reserve(64);
        cursorScratch.reserve(32);
        countsScratch.reserve(32);
        chunkScratch.reserve(65536);
    }

    void encodeVector(Bytes& out, UnityEngine::Vector3 value) { out.clear(); floating(out, 1, value.x); floating(out, 2, value.y); floating(out, 3, value.z); }
    void encodeQuaternion(Bytes& out, UnityEngine::Quaternion value) { out.clear(); floating(out, 1, value.x); floating(out, 2, value.y); floating(out, 3, value.z); floating(out, 4, value.w); }
    void appendPose(Bytes& out, uint32_t field, UnityEngine::Vector3 position, UnityEngine::Quaternion rotation) {
        poseScratch.clear();
        encodeVector(vectorScratch, position); message(poseScratch, 1, vectorScratch);
        encodeQuaternion(quaternionScratch, rotation); message(poseScratch, 2, quaternionScratch);
        message(out, field, poseScratch);
    }
    void encodeColor(Bytes& out, UnityEngine::Color value) { out.clear(); floating(out, 1, value.r); floating(out, 2, value.g); floating(out, 3, value.b); floating(out, 4, value.a); }
    void encodeCursor(Bytes& out, float time) { out.clear(); integer(out, 1, sequence++); integer(out, 2, std::llround(time * 1000)); integer(out, 4, nowMs()); }
    void encodeCounts(Bytes& out) { out.clear(); integer(out, 1, totalFrames); integer(out, 3, totalNotes); integer(out, 4, totalScores); integer(out, 5, totalCombos); integer(out, 7, totalEnergies); }
    void mark(float songTime, float realTime) {
        if (eventCount == 0) { minTime = songTime; batchStartedAt = realTime; }
        maxTime = std::max(maxTime, songTime);
    }
    void sendBody(uint32_t field, Bytes const& body) {
        Bytes packet;
        packet.reserve(body.size() + streamId.size() + platformId.size() + 16);
        string(packet, 1, streamId);
        string(packet, 3, platformId);
        message(packet, field, body);
        Client::instance().sendReplayStream(std::move(packet));
    }

    void flush() {
        if (!active || eventCount == 0) return;
        floating(batch, 8, minTime); floating(batch, 9, maxTime);
        chunkScratch.clear();
        encodeCursor(cursorScratch, maxTime); message(chunkScratch, 1, cursorScratch);
        message(chunkScratch, 2, batch);
        encodeCounts(countsScratch); message(chunkScratch, 3, countsScratch);
        sendBody(11, chunkScratch); ++chunkCount;
        batch.clear(); eventCount = 0; minTime = maxTime;
    }

    void start(GlobalNamespace::ScoreController*) {
        if (!Client::instance().replayStreamingEnabled() || !Client::instance().activeSong()) { active = false; return; }
        auto parameters = *Client::instance().activeSong();
        auto user = Client::instance().selfUser();
        prepareBuffers();
        streamId = "ta-quest-" + id();
        platformId = user ? user->platformId : "";
        sequence = 1; chunkCount = 0; eventCount = 0; minTime = maxTime = 0; active = true;
        totalFrames = totalNotes = totalScores = totalCombos = totalEnergies = 0;
        batch.clear();
        lastScore = lastCombo = std::numeric_limits<int>::min();
        lastEnergy = std::numeric_limits<float>::quiet_NaN();
        nextPoseAt = nextComponentLookupAt = 0;
        playerTransforms = firstResource<GlobalNamespace::PlayerTransforms>();
        audioTimeSyncController = firstResource<GlobalNamespace::AudioTimeSyncController>();
        comboController = firstResource<GlobalNamespace::ComboController>();
        gameEnergyCounter = firstResource<GlobalNamespace::GameEnergyCounter>();

        Bytes player; string(player, 1, user ? user->platformId : ""); integer(player, 2, 3); string(player, 3, (std::string)UnityEngine::Application::get_version()); string(player, 4, VERSION);
        auto difficulty = replayDifficulty(parameters.beatmap.difficulty);
        auto difficultyName = replayDifficultyName(parameters.beatmap.difficulty);
        Bytes beatmap; string(beatmap, 1, hash(parameters.beatmap.levelId)); string(beatmap, 2, parameters.beatmap.levelId); integer(beatmap, 3, difficulty); string(beatmap, 4, difficultyName); string(beatmap, 5, parameters.beatmap.characteristic.serializedName);
        auto* movement = firstResource<GlobalNamespace::VariableMovementDataProvider>();
        auto* colors = firstResource<GlobalNamespace::ColorManager>();
        Bytes metadata; string(metadata, 1, "ta-live-1"); string(metadata, 2, parameters.beatmap.levelId); integer(metadata, 3, difficulty); string(metadata, 4, parameters.beatmap.characteristic.serializedName); floating(metadata, 7, parameters.playerSettings.noteJumpStartBeatOffset); boolean(metadata, 8, (parameters.playerSettings.options & 1) != 0); floating(metadata, 9, parameters.playerSettings.playerHeight > 0 ? parameters.playerSettings.playerHeight : 1.7f); string(metadata, 13, (std::string)UnityEngine::Application::get_version()); string(metadata, 14, VERSION); string(metadata, 15, "Quest"); floating(metadata, 16, 1); floating(metadata, 17, movement ? movement->get_jumpDistance() : 0);
        if (colors) {
            encodeColor(eventScratch, colors->ColorForType(GlobalNamespace::ColorType::ColorA)); message(metadata, 18, eventScratch);
            encodeColor(eventScratch, colors->ColorForType(GlobalNamespace::ColorType::ColorB)); message(metadata, 19, eventScratch);
        }
        Bytes start; integer(start, 1, 1); message(start, 2, player); message(start, 3, beatmap); integer(start, 9, nowMs()); string(start, 11, id()); message(start, 13, metadata);
        sendBody(10, start);
        PaperLogger.info("TA live replay started stream='{}'", streamId);
    }

    void tick(GlobalNamespace::ScoreController* scoreController) {
        if (!active || !scoreController) return;
        float realTime = UnityEngine::Time::get_realtimeSinceStartup();
        if ((!playerTransforms || !audioTimeSyncController || !comboController || !gameEnergyCounter) && realTime >= nextComponentLookupAt) {
            if (!playerTransforms) playerTransforms = firstResource<GlobalNamespace::PlayerTransforms>();
            if (!audioTimeSyncController) audioTimeSyncController = firstResource<GlobalNamespace::AudioTimeSyncController>();
            if (!comboController) comboController = firstResource<GlobalNamespace::ComboController>();
            if (!gameEnergyCounter) gameEnergyCounter = firstResource<GlobalNamespace::GameEnergyCounter>();
            nextComponentLookupAt = realTime + ComponentLookupIntervalSeconds;
        }
        if (!playerTransforms || !audioTimeSyncController) return;
        if (nextPoseAt == 0) nextPoseAt = realTime;
        if (realTime < nextPoseAt) return;
        nextPoseAt += PoseIntervalSeconds;
        if (nextPoseAt < realTime) nextPoseAt = realTime + PoseIntervalSeconds;
        float time = audioTimeSyncController->get_songTime(); if (time < 0) return;
        mark(time, realTime);
        frameScratch.clear();
        appendPose(frameScratch, 1, playerTransforms->get_headPseudoLocalPos(), playerTransforms->get_headPseudoLocalRot());
        appendPose(frameScratch, 2, playerTransforms->get_leftHandPseudoLocalPos(), playerTransforms->get_leftHandPseudoLocalRot());
        appendPose(frameScratch, 3, playerTransforms->get_rightHandPseudoLocalPos(), playerTransforms->get_rightHandPseudoLocalRot());
        integer(frameScratch, 4, 60); floating(frameScratch, 5, time); message(batch, 1, frameScratch);
        ++totalFrames; ++eventCount;

        auto score = scoreController->get_modifiedScore();
        if (score != lastScore) {
            lastScore = score; eventScratch.clear(); integer(eventScratch, 1, score); floating(eventScratch, 2, time); integer(eventScratch, 3, scoreController->get_immediateMaxPossibleModifiedScore()); message(batch, 4, eventScratch); ++totalScores; ++eventCount;
        }
        auto combo = comboController ? comboController->__cordl_internal_get__combo() : 0;
        if (combo != lastCombo) {
            lastCombo = combo; eventScratch.clear(); integer(eventScratch, 1, combo); floating(eventScratch, 2, time); message(batch, 5, eventScratch); ++totalCombos; ++eventCount;
        }
        auto energy = gameEnergyCounter ? gameEnergyCounter->get_energy() : 0.0f;
        if (!std::isfinite(lastEnergy) || std::abs(energy - lastEnergy) >= 0.0025f) {
            lastEnergy = energy; eventScratch.clear(); floating(eventScratch, 1, energy); floating(eventScratch, 2, time); message(batch, 7, eventScratch); ++totalEnergies; ++eventCount;
        }
        if (eventCount >= MaxEventsPerChunk || realTime - batchStartedAt >= MaxChunkAgeSeconds) flush();
    }

    void recordScoring(GlobalNamespace::ScoringElement* scoring, int eventType) {
        if (!active || !scoring || !scoring->noteData) return;
        auto* data = scoring->noteData;
        auto time = data->get_time();
        mark(time, UnityEngine::Time::get_realtimeSinceStartup());
        nestedScratch.clear(); floating(nestedScratch, 1, time); integer(nestedScratch, 2, int(data->get_noteLineLayer())); integer(nestedScratch, 3, data->get_lineIndex()); integer(nestedScratch, 4, int(data->get_colorType())); integer(nestedScratch, 5, int(data->get_cutDirection())); integer(nestedScratch, 6, int(data->get_gameplayType())); integer(nestedScratch, 7, int(data->get_scoringType())); floating(nestedScratch, 8, data->get_cutDirectionAngleOffset());
        eventScratch.clear(); message(eventScratch, 1, nestedScratch); integer(eventScratch, 2, eventType); floating(eventScratch, 14, time); floating(eventScratch, 15, UnityEngine::Time::get_timeScale()); floating(eventScratch, 16, 1); message(batch, 3, eventScratch);
        ++totalNotes; ++eventCount;
        if (eventCount >= MaxEventsPerChunk) flush();
    }

    void finish(int completion) {
        if (!active) return; flush(); Bytes end; encodeCursor(cursorScratch, maxTime); message(end, 1, cursorScratch); integer(end, 2, completion); integer(end, 5, chunkCount); encodeCounts(countsScratch); message(end, 6, countsScratch); sendBody(13, end); active = false;
        playerTransforms = nullptr; audioTimeSyncController = nullptr; comboController = nullptr; gameEnergyCounter = nullptr;
        PaperLogger.info("TA live replay ended stream='{}' chunks={}", streamId, chunkCount);
    }
}
