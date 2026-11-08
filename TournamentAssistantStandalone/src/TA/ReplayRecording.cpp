#include "TA/ReplayRecording.hpp"

#include "TA/Client.hpp"
#include "TA/MidPlayModifiers.hpp"
#include "TA/ReplayStreaming.hpp"
#include "TA/Tarf.hpp"
#include "main.hpp"

#include "GlobalNamespace/AudioTimeSyncController.hpp"
#include "GlobalNamespace/BadCutScoringElement.hpp"
#include "GlobalNamespace/BeatmapObjectManager.hpp"
#include "GlobalNamespace/ColorScheme.hpp"
#include "GlobalNamespace/ComboController.hpp"
#include "GlobalNamespace/CutScoreBuffer.hpp"
#include "GlobalNamespace/EnvironmentInfoSO.hpp"
#include "GlobalNamespace/GameEnergyCounter.hpp"
#include "GlobalNamespace/GameplayCoreInstaller.hpp"
#include "GlobalNamespace/GameplayCoreSceneSetupData.hpp"
#include "GlobalNamespace/GoodCutScoringElement.hpp"
#include "GlobalNamespace/NoteController.hpp"
#include "GlobalNamespace/NoteCutInfo.hpp"
#include "GlobalNamespace/NoteData.hpp"
#include "GlobalNamespace/ObstacleController.hpp"
#include "GlobalNamespace/ObstacleData.hpp"
#include "GlobalNamespace/PlayerHeadAndObstacleInteraction.hpp"
#include "GlobalNamespace/PlayerSpecificSettings.hpp"
#include "GlobalNamespace/PlayerTransforms.hpp"
#include "GlobalNamespace/ScoreController.hpp"
#include "GlobalNamespace/ScoreMultiplierCounter.hpp"
#include "GlobalNamespace/ScoringElement.hpp"
#include "GlobalNamespace/VariableMovementDataProvider.hpp"
#include "UnityEngine/Application.hpp"
#include "UnityEngine/Resources.hpp"
#include "UnityEngine/Time.hpp"

#include "beatsaber-hook/shared/utils/hooking.hpp"
#include "beatsaber-hook/shared/utils/il2cpp-utils.hpp"
#include "scotland2/shared/loader.hpp"
#include "songcore/shared/SongCore.hpp"
#include "songcore/shared/SongLoader/CustomBeatmapLevel.hpp"

#include <algorithm>
#include <cctype>
#include <chrono>
#include <cmath>
#include <filesystem>
#include <fstream>
#include <iterator>
#include <limits>
#include <memory>
#include <optional>
#include <thread>
#include <unordered_map>

namespace TA::ReplayRecording {
    namespace {
        constexpr char const* ReplaysFolder = "/sdcard/ModData/com.beatgames.beatsaber/Mods/TournamentAssistant/Replays";
        constexpr float ComponentLookupIntervalSeconds = 0.25f;

        // Everything known about the map when the song starts. The Map message is only built at the end, once the map files are ready
        struct MapInfo {
            std::string hash;
            std::string levelId;
            std::string characteristic;
            int difficulty = -1;
            std::string songName;
            std::string environment;
            std::string folder;
        };

        struct OpenWall {
            int64_t timeUs = 0;
            Tarf::Message identity;
        };

        bool active = false;
        std::unique_ptr<Tarf::Recorder> recorder;
        Tarf::Message headerWithoutMap;
        MapInfo map;
        std::string tournamentId;
        std::string matchId;
        std::string platformId;
        int64_t recordedAtUnixMs = 0;

        int64_t lastFrameTimeUs = 0;
        bool hasFrame = false;
        int32_t lastScore = 0;
        int32_t lastCombo = 0;
        int32_t lastMultiplier = 0;
        float lastMultiplierProgress = 0;
        float lastEnergy = std::numeric_limits<float>::quiet_NaN();
        MidPlayModifiers::State lastModifiers;
        bool paused = false;
        float pauseStartedAtRealtime = 0;
        int64_t pauseStartedAtUnixMs = 0;
        std::optional<OpenWall> openWall;
        float nextComponentLookupAt = 0;

        // Bad cuts only report their cut info when the note is cut, the scoring element that follows doesn't carry it
        std::unordered_map<GlobalNamespace::NoteData*, GlobalNamespace::NoteCutInfo> cutInfos;

        // Held across scene loads, SafePtrUnity reports false once the gameplay scene is gone
        SafePtrUnity<GlobalNamespace::PlayerTransforms> playerTransforms;
        SafePtrUnity<GlobalNamespace::AudioTimeSyncController> audioTimeSyncController;
        SafePtrUnity<GlobalNamespace::ComboController> comboController;
        SafePtrUnity<GlobalNamespace::GameEnergyCounter> gameEnergyCounter;

        template <typename T> T* firstResource() {
            auto values = UnityEngine::Resources::FindObjectsOfTypeAll<T*>();
            return values && values.size() ? values[0] : nullptr;
        }

        int64_t nowMs() { return std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::system_clock::now().time_since_epoch()).count(); }

        int64_t toTimeUs(float seconds) { return std::llround(double(seconds) * 1'000'000.0); }

        float songTime() { return audioTimeSyncController ? audioTimeSyncController->get_songTime() : 0; }

        // Level ids of custom maps end with the map hash, for example "custom_level_DEE2EF2F..."
        std::string hashFromLevelId(std::string const& levelId) {
            constexpr size_t hashLength = 40;
            if (levelId.size() < hashLength) return "";
            auto hash = levelId.substr(levelId.size() - hashLength);
            if (!std::all_of(hash.begin(), hash.end(), [](unsigned char c) { return std::isxdigit(c); })) return "";
            std::transform(hash.begin(), hash.end(), hash.begin(), ::toupper);
            return hash;
        }

        std::string levelFolder(std::string const& hash) {
            if (hash.empty()) return "";
            auto* level = SongCore::API::Loading::GetLevelByHash(hash);
            auto customLevel = level ? il2cpp_utils::try_cast<SongCore::SongLoader::CustomBeatmapLevel>(level) : std::nullopt;
            return customLevel && customLevel.value() ? std::string(customLevel.value()->get_customLevelPath()) : "";
        }

        // TA's own modifier names, the same ones the PC plugin records
        void addModifiers(Tarf::Message& settings, int32_t options) {
            constexpr char const* names[] = {
                "NoFail", "NoBombs", "NoArrows", "NoObstacles", "SlowSong", "InstaFail", "FailOnClash", "BatteryEnergy", "FastNotes", "FastSong",
                "DisappearingArrows", "GhostNotes", "DemoNoFail", "DemoNoObstacles", "StrictAngles", "ProMode", "ZenMode", "SmallCubes", "SuperFastSong",
            };
            for (size_t bit = 0; bit < std::size(names); bit++) {
                if (options & (1 << bit)) settings.string(1, names[bit]);
            }
        }

        Tarf::Message midPlayModifiers(MidPlayModifiers::State const& state) {
            Tarf::Message message;
            message.boolean(1, state.invertColors).boolean(2, state.invertHandedness).boolean(3, state.disableBlueNotes).boolean(4, state.disableRedNotes);
            return message;
        }

        Tarf::Message colors(GlobalNamespace::ColorScheme* scheme) {
            Tarf::Message message;
            auto color = [&](uint32_t field, UnityEngine::Color value) { message.color(field, value.r, value.g, value.b, value.a); };
            color(1, scheme->saberAColor);
            color(2, scheme->saberBColor);
            color(3, scheme->obstaclesColor);
            color(4, scheme->environmentColor0);
            color(5, scheme->environmentColor1);
            color(6, scheme->environmentColorW);
            color(7, scheme->environmentColor0Boost);
            color(8, scheme->environmentColor1Boost);
            color(9, scheme->environmentColorWBoost);
            message.optionalBoolean(10, scheme->supportsEnvironmentColorBoost);
            return message;
        }

        // Builds every part of the header except the map, see MapInfo
        Tarf::Message createHeader(GameplayParameters const& parameters, std::optional<User> const& user) {
            auto* installer = firstResource<GlobalNamespace::GameplayCoreInstaller>();
            auto* setup = installer ? installer->____sceneSetupData : nullptr;
            auto* playerSettings = setup ? setup->playerSpecificSettings : nullptr;
            auto* movement = firstResource<GlobalNamespace::VariableMovementDataProvider>();

            Tarf::Message header;
            header.message(1, Tarf::Message().integer(1, 1).string(2, VERSION).string(3, std::string("TournamentAssistant Standalone ") + VERSION));
            header.integer(2, recordedAtUnixMs);
            header.message(3, Tarf::Message()
                .string(1, platformId)
                .string(2, user ? user->name : "")
                .string(3, "Quest")
                .string(4, (std::string)UnityEngine::Application::get_version()));

            Tarf::Message settings;
            addModifiers(settings, parameters.gameplayModifiers.options);
            if (playerSettings) {
                settings.boolean(2, playerSettings->get_leftHanded())
                    .floating(3, playerSettings->get_playerHeight())
                    .optionalFloating(5, playerSettings->get_noteJumpStartBeatOffset())
                    .optionalInteger(10, int(playerSettings->environmentEffectsFilterDefaultPreset))
                    .optionalInteger(11, int(playerSettings->environmentEffectsFilterExpertPlusPreset))
                    .optionalBoolean(13, playerSettings->noTextsAndHuds)
                    .optionalFloating(14, playerSettings->saberTrailIntensity)
                    .optionalBoolean(15, playerSettings->hideNoteSpawnEffect)
                    .optionalBoolean(16, playerSettings->arcsHapticFeedback)
                    .optionalInteger(17, int(playerSettings->arcVisibility));
            } else {
                settings.boolean(2, (parameters.playerSettings.options & 1) != 0).floating(3, parameters.playerSettings.playerHeight);
            }
            if (movement) settings.optionalFloating(4, movement->get_jumpDistance());
            if (audioTimeSyncController) settings.optionalFloating(6, audioTimeSyncController->get_timeScale());
            header.message(5, settings);

            if (setup && setup->colorScheme) header.message(6, colors(setup->colorScheme));

            auto hsv = ReplayStreaming::hsvProfile();
            if (!hsv.empty()) header.message(7, Tarf::Message().string(1, std::string(hsv.begin(), hsv.end())).integer(2, 2));

            for (auto const& mod : modloader::get_loaded()) {
                header.message(8, Tarf::Message().string(1, mod.info.id).string(2, mod.info.version));
            }

            header.message(11, Tarf::Message().string(1, tournamentId).string(2, matchId));
            header.message(12, midPlayModifiers(lastModifiers));

            map.environment = setup && setup->targetEnvironmentInfo ? (std::string)setup->targetEnvironmentInfo->____serializedName : "";
            return header;
        }

        void lookUpComponents() {
            if (!playerTransforms) playerTransforms = firstResource<GlobalNamespace::PlayerTransforms>();
            if (!audioTimeSyncController) audioTimeSyncController = firstResource<GlobalNamespace::AudioTimeSyncController>();
            if (!comboController) comboController = firstResource<GlobalNamespace::ComboController>();
            if (!gameEnergyCounter) gameEnergyCounter = firstResource<GlobalNamespace::GameEnergyCounter>();
        }

        Tarf::Pose pose(UnityEngine::Vector3 position, UnityEngine::Quaternion rotation) {
            return Tarf::Pose{ position.x, position.y, position.z, rotation.x, rotation.y, rotation.z, rotation.w };
        }

        void addEvent(uint32_t payloadField, Tarf::Message const& payload) {
            recorder->addEvent(toTimeUs(songTime()), payloadField, payload);
        }

        // A wall stays open until the head leaves it, which is when the energy it cost is known
        void closeOpenWall() {
            auto exitTimeUs = std::max(openWall->timeUs, toTimeUs(songTime()));
            Tarf::Message wall;
            wall.message(1, openWall->identity)
                .floating(2, gameEnergyCounter ? gameEnergyCounter->get_energy() : 0)
                .optionalSignedInteger(3, exitTimeUs);
            recorder->addEvent(openWall->timeUs, Tarf::Fields::EventWall, wall);
            openWall.reset();
        }

        void recordStateChanges(GlobalNamespace::ScoreController* scoreController) {
            auto score = scoreController->get_multipliedScore();
            if (score != lastScore) {
                lastScore = score;
                addEvent(Tarf::Fields::EventScore, Tarf::Message().integer(1, score).optionalInteger(2, scoreController->get_immediateMaxPossibleMultipliedScore()));
            }

            auto combo = comboController ? comboController->__cordl_internal_get__combo() : 0;
            if (combo != lastCombo) {
                lastCombo = combo;
                addEvent(Tarf::Fields::EventCombo, Tarf::Message().integer(1, combo));
            }

            if (auto* counter = scoreController->__cordl_internal_get__scoreMultiplierCounter()) {
                auto multiplier = counter->get_multiplier();
                auto progress = counter->get_normalizedProgress();
                if (multiplier != lastMultiplier || progress != lastMultiplierProgress) {
                    lastMultiplier = multiplier;
                    lastMultiplierProgress = progress;
                    addEvent(Tarf::Fields::EventMultiplier, Tarf::Message().integer(1, multiplier).floating(2, progress));
                }
            }

            auto energy = gameEnergyCounter ? gameEnergyCounter->get_energy() : 0.0f;
            if (!std::isfinite(lastEnergy) || energy != lastEnergy) {
                lastEnergy = energy;
                addEvent(Tarf::Fields::EventEnergy, Tarf::Message().floating(1, energy));
            }

            auto modifiers = MidPlayModifiers::current();
            if (!(modifiers == lastModifiers)) {
                int toggled = modifiers.invertColors != lastModifiers.invertColors ? 1
                    : modifiers.invertHandedness != lastModifiers.invertHandedness ? 2
                    : modifiers.disableBlueNotes != lastModifiers.disableBlueNotes ? 3 : 4;
                lastModifiers = modifiers;
                addEvent(Tarf::Fields::EventMidPlayModifier, Tarf::Message().integer(1, toggled).message(2, midPlayModifiers(modifiers)));
            }
        }

        void recordPause() {
            bool nowPaused = audioTimeSyncController->get_state() == GlobalNamespace::AudioTimeSyncController_State::Paused;
            if (nowPaused == paused) return;
            paused = nowPaused;

            float realtime = UnityEngine::Time::get_realtimeSinceStartup();
            if (paused) {
                pauseStartedAtRealtime = realtime;
                pauseStartedAtUnixMs = nowMs();
                return;
            }
            addEvent(Tarf::Fields::EventPause, Tarf::Message()
                .integer(1, std::llround((realtime - pauseStartedAtRealtime) * 1000.0f))
                .optionalInteger(2, pauseStartedAtUnixMs)
                .optionalInteger(3, nowMs()));
        }

        Tarf::Message vector(UnityEngine::Vector3 value) { return Tarf::Message().floating(1, value.x).floating(2, value.y).floating(3, value.z); }

        Tarf::Message quaternion(UnityEngine::Quaternion value) { return Tarf::Message().floating(1, value.x).floating(2, value.y).floating(3, value.z).floating(4, value.w); }

        // Runs on the background thread, so it only uses what it is given, never the recorder's state
        void writeAndSubmit(Tarf::Bytes const& data, std::string const& fileName, std::string const& tournamentId, std::string const& matchId) {
            std::error_code error;
            std::filesystem::create_directories(ReplaysFolder, error);
            std::ofstream output(std::filesystem::path(ReplaysFolder) / fileName, std::ios::binary);
            output.write(reinterpret_cast<char const*>(data.data()), std::streamsize(data.size()));
            output.close();
            PaperLogger.info("Saved replay '{}' ({} KB)", fileName, data.size() / 1024);

            Client::instance().submitReplay(tournamentId, matchId, fileName, data);
        }
    }

    void start(GlobalNamespace::ScoreController*) {
        active = false;
        if (!Client::instance().replayRecordingEnabled() || !Client::instance().activeSong()) return;

        auto parameters = *Client::instance().activeSong();
        auto user = Client::instance().selfUser();
        auto match = Client::instance().currentMatch();

        recorder = std::make_unique<Tarf::Recorder>();
        tournamentId = Client::instance().selectedTournamentId();
        matchId = match ? match->guid : "";
        platformId = user ? user->platformId : "";
        recordedAtUnixMs = nowMs();
        hasFrame = false;
        lastScore = lastCombo = lastMultiplier = 0;
        lastMultiplierProgress = 0;
        lastEnergy = std::numeric_limits<float>::quiet_NaN();
        lastModifiers = MidPlayModifiers::current();
        paused = false;
        openWall.reset();
        cutInfos.clear();
        nextComponentLookupAt = 0;

        playerTransforms = nullptr;
        audioTimeSyncController = nullptr;
        comboController = nullptr;
        gameEnergyCounter = nullptr;
        lookUpComponents();

        map = MapInfo{};
        map.levelId = parameters.beatmap.levelId;
        map.hash = hashFromLevelId(map.levelId);
        map.characteristic = parameters.beatmap.characteristic.serializedName;
        map.difficulty = parameters.beatmap.difficulty;
        map.songName = parameters.beatmap.name;
        map.folder = levelFolder(map.hash);

        headerWithoutMap = createHeader(parameters, user);
        active = true;
        PaperLogger.info("TA replay recording started levelId='{}' folder='{}'", map.levelId, map.folder);
    }

    void tick(GlobalNamespace::ScoreController* scoreController) {
        if (!active || !scoreController) return;

        float realtime = UnityEngine::Time::get_realtimeSinceStartup();
        if ((!playerTransforms || !audioTimeSyncController || !comboController || !gameEnergyCounter) && realtime >= nextComponentLookupAt) {
            lookUpComponents();
            nextComponentLookupAt = realtime + ComponentLookupIntervalSeconds;
        }
        if (!playerTransforms || !audioTimeSyncController) return;

        // The song time stands still while the game is paused, and those frames would only repeat the same moment
        auto time = audioTimeSyncController->get_songTime();
        auto timeUs = toTimeUs(time);
        if (time >= 0 && (!hasFrame || timeUs != lastFrameTimeUs)) {
            hasFrame = true;
            lastFrameTimeUs = timeUs;
            auto deltaTime = UnityEngine::Time::get_unscaledDeltaTime();
            recorder->addFrame(Tarf::Frame{
                timeUs,
                deltaTime > 0 ? int32_t(std::lround(1.0f / deltaTime)) : 0,
                pose(playerTransforms->get_headPseudoLocalPos(), playerTransforms->get_headPseudoLocalRot()),
                pose(playerTransforms->get_leftHandPseudoLocalPos(), playerTransforms->get_leftHandPseudoLocalRot()),
                pose(playerTransforms->get_rightHandPseudoLocalPos(), playerTransforms->get_rightHandPseudoLocalRot()),
            });
        }

        GlobalNamespace::PlayerHeadAndObstacleInteraction* obstacles = scoreController->__cordl_internal_get__playerHeadAndObstacleInteraction();
        if (openWall && obstacles && !obstacles->get_playerHeadIsInObstacle()) closeOpenWall();

        recordStateChanges(scoreController);
        recordPause();
    }

    void recordScoring(GlobalNamespace::ScoringElement* scoring, int kind) {
        if (!active || !scoring || !scoring->noteData) return;

        auto* data = scoring->noteData;
        Tarf::Message note;
        note.message(1, Tarf::Message()
            .floating(1, data->get_time())
            .integer(2, data->get_lineIndex())
            .integer(3, int(data->get_noteLineLayer()))
            .integer(4, int(data->get_colorType()))
            .integer(5, int(data->get_cutDirection()))
            .optionalInteger(6, int(data->get_scoringType()))
            .optionalInteger(7, int(data->get_gameplayType()))
            .optionalFloating(8, data->get_cutDirectionAngleOffset()));
        note.integer(2, kind);

        std::optional<GlobalNamespace::NoteCutInfo> cut;
        float beforeCutRating = 0, afterCutRating = 0;
        if (auto* good = il2cpp_utils::try_cast<GlobalNamespace::GoodCutScoringElement>(scoring).value_or(nullptr)) {
            auto* buffer = good->cutScoreBuffer;
            cut = buffer->noteCutInfo;
            beforeCutRating = buffer->beforeCutSwingRating;
            afterCutRating = buffer->afterCutSwingRating;
        } else if (auto found = cutInfos.find(data); found != cutInfos.end()) {
            cut = found->second;
        }
        cutInfos.erase(data);

        // A cut is timed when the saber went through the note, which is a little before its scoring finished
        auto timeUs = toTimeUs(songTime());
        if (cut && (kind == 1 || kind == 2 || kind == 4)) {
            note.message(3, Tarf::Message()
                .optionalBoolean(1, cut->speedOK)
                .boolean(2, cut->directionOK)
                .optionalBoolean(3, cut->saberTypeOK)
                .optionalBoolean(4, cut->wasCutTooSoon)
                .floating(5, cut->saberSpeed)
                .message(6, vector(cut->saberDir))
                .integer(7, int(cut->saberType))
                .optionalFloating(8, cut->timeDeviation)
                .floating(9, cut->cutDirDeviation)
                .message(10, vector(cut->cutPoint))
                .message(11, vector(cut->cutNormal))
                .floating(12, cut->cutDistanceToCenter)
                .floating(13, cut->cutAngle)
                .floating(14, beforeCutRating)
                .floating(15, afterCutRating));
            note.message(6, quaternion(cut->worldRotation))
                .message(7, quaternion(cut->inverseWorldRotation))
                .message(8, quaternion(cut->noteRotation))
                .message(9, vector(cut->notePosition));
            timeUs = toTimeUs(data->get_time() - cut->timeDeviation);
        }
        note.optionalFloating(4, UnityEngine::Time::get_timeScale());
        note.optionalFloating(5, audioTimeSyncController ? audioTimeSyncController->get_timeScale() : 1.0f);

        recorder->addEvent(timeUs, Tarf::Fields::EventNote, note);
    }

    void recordObstacleEntered(GlobalNamespace::ObstacleController* obstacle) {
        if (!active || !obstacle || openWall) return;

        auto* data = obstacle->get_obstacleData();
        if (!data) return;
        OpenWall wall;
        wall.timeUs = toTimeUs(songTime());
        wall.identity.floating(1, data->get_time())
            .optionalFloating(2, data->get_duration())
            .optionalInteger(3, data->get_lineIndex())
            .optionalInteger(4, int(data->get_lineLayer()))
            .optionalInteger(5, data->get_width())
            .optionalInteger(6, data->get_height());
        openWall = std::move(wall);
    }

    // Stops recording, then embeds the map, writes the file and submits it on a background thread
    void finish(int completion, int finalScore, float endSongTime) {
        if (!active) return;
        if (openWall) closeOpenWall();
        active = false;
        cutInfos.clear();
        playerTransforms = nullptr;
        audioTimeSyncController = nullptr;
        comboController = nullptr;
        gameEnergyCounter = nullptr;

        if (!recorder || recorder->frameCount() == 0) return;

        Tarf::Message footer;
        footer.integer(1, completion).optionalInteger(3, finalScore);
        if (completion == 2 || completion == 3) footer.optionalSignedInteger(2, toTimeUs(endSongTime));

        auto fileName = Tarf::fileName(platformId, map.hash, map.characteristic, map.difficulty, recordedAtUnixMs);
        std::thread([recorder = std::move(recorder), header = headerWithoutMap, footer, map = map, fileName, tournamentId = tournamentId, matchId = matchId]() mutable {
            auto mapFiles = Tarf::buildMapFiles(map.folder, map.characteristic, map.difficulty);

            Tarf::Message mapMessage;
            mapMessage.string(1, map.hash)
                .string(2, map.levelId)
                .string(3, map.characteristic)
                .integer(4, map.difficulty >= 0 ? map.difficulty + 1 : 0)
                .string(5, map.songName)
                .string(9, map.environment);
            for (auto const& file : mapFiles) mapMessage.message(10, file);
            header.message(4, mapMessage);

            writeAndSubmit(recorder->finish(std::move(header), std::move(footer), !mapFiles.empty()), fileName, tournamentId, matchId);
        }).detach();
    }
}

// Bad cuts only report their cut info here, so it is kept until the note's scoring finishes
MAKE_HOOK_MATCH(TA_BeatmapObjectManager_HandleNoteControllerNoteWasCut, &GlobalNamespace::BeatmapObjectManager::HandleNoteControllerNoteWasCut, void,
    GlobalNamespace::BeatmapObjectManager* self, GlobalNamespace::NoteController* noteController, ByRef<GlobalNamespace::NoteCutInfo> noteCutInfo) {
    TA_BeatmapObjectManager_HandleNoteControllerNoteWasCut(self, noteController, noteCutInfo);
    if (TA::ReplayRecording::active && noteController && noteController->get_noteData()) {
        TA::ReplayRecording::cutInfos[noteController->get_noteData()] = noteCutInfo.heldRef;
    }
}

namespace TA::ReplayRecording {
    void installHooks() {
        INSTALL_HOOK(PaperLogger, TA_BeatmapObjectManager_HandleNoteControllerNoteWasCut);
    }
}
