#include "TA/Song.hpp"

#include "TA/AntiPause.hpp"
#include "TA/Client.hpp"
#include "TA/MidPlayModifiers.hpp"
#include "TA/RealtimeScore.hpp"
#include "TA/StreamSync.hpp"
#include "main.hpp"

#include "GlobalNamespace/BeatmapCharacteristicSO.hpp"
#include "GlobalNamespace/BeatmapDifficulty.hpp"
#include "GlobalNamespace/BeatmapKey.hpp"
#include "GlobalNamespace/BeatmapLevel.hpp"
#include "GlobalNamespace/ColorScheme.hpp"
#include "GlobalNamespace/EnvironmentsListModel.hpp"
#include "GlobalNamespace/IBeatmapLevelData.hpp"
#include "GlobalNamespace/GameplayModifiers.hpp"
#include "GlobalNamespace/LevelCompletionResults.hpp"
#include "GlobalNamespace/MenuTransitionsHelper.hpp"
#include "GlobalNamespace/PlayerData.hpp"
#include "GlobalNamespace/PlayerDataModel.hpp"
#include "GlobalNamespace/OverrideEnvironmentSettings.hpp"
#include "GlobalNamespace/PlayerSpecificSettings.hpp"
#include "GlobalNamespace/PracticeSettings.hpp"
#include "GlobalNamespace/PrepareLevelCompletionResults.hpp"
#include "GlobalNamespace/RecordingToolManager.hpp"
#include "GlobalNamespace/StandardLevelScenesTransitionSetupDataSO.hpp"
#include "GlobalNamespace/NoteJumpDurationTypeSettings.hpp"
#include "GlobalNamespace/ArcVisibilityType.hpp"
#include "GlobalNamespace/EnvironmentEffectsFilterPreset.hpp"
#include "System/Action_2.hpp"
#include "System/Nullable_1.hpp"
#include "UnityEngine/Resources.hpp"

#include "beatsaber-hook/shared/utils/il2cpp-utils.hpp"
#include "beatsaber-hook/shared/config/rapidjson-utils.hpp"
#include "bsml/shared/Helpers/getters.hpp"
#include "bsml/shared/BSML/MainThreadScheduler.hpp"
#include "custom-types/shared/delegate.hpp"
#include "libcurl/shared/curl.h"
#include "metacore/shared/game.hpp"
#include "songcore/shared/SongCore.hpp"
#include "songcore/shared/SongLoader/CustomBeatmapLevel.hpp"
#include "zip.h"

#include <algorithm>
#include <cctype>
#include <filesystem>
#include <map>
#include <mutex>
#include <string_view>
#include <thread>

namespace TA::Song {
    namespace {
        std::string toLower(std::string value) {
            std::transform(value.begin(), value.end(), value.begin(), [](unsigned char c) { return char(std::tolower(c)); });
            return value;
        }

        std::string toUpper(std::string value) {
            std::transform(value.begin(), value.end(), value.begin(), [](unsigned char c) { return char(std::toupper(c)); });
            return value;
        }

        std::string beatSaverDifficultyName(int32_t difficulty) {
            switch (difficulty) {
                case 0: return "Easy";
                case 1: return "Normal";
                case 2: return "Hard";
                case 3: return "Expert";
                case 4: return "ExpertPlus";
                case 5: return "Hard";
                case 7: return "Expert";
                case 9: return "ExpertPlus";
                default: return std::to_string(difficulty);
            }
        }

        std::string hashFromLevelId(std::string levelId) {
            constexpr std::string_view prefix = "custom_level_";
            auto lower = toLower(levelId);
            if (lower.starts_with(prefix)) return lower.substr(prefix.size());
            return lower;
        }

        std::string detailsKey(GameplayParameters const& parameters) {
            return hashFromLevelId(parameters.beatmap.levelId) + ":" +
                   parameters.beatmap.characteristic.serializedName + ":" +
                   std::to_string(parameters.beatmap.difficulty);
        }

        std::mutex detailsMutex;
        std::map<std::string, SongDetails> detailsCache;

        size_t curlWrite(void* contents, size_t size, size_t nmemb, void* userdata) {
            auto* out = static_cast<std::string*>(userdata);
            out->append(static_cast<char*>(contents), size * nmemb);
            return size * nmemb;
        }

        long getUrl(std::string const& url, std::string& data) {
            PaperLogger.info("HTTP GET {}", url);
            static std::once_flag curlInitFlag;
            std::call_once(curlInitFlag, [] {
                PaperLogger.info("Initializing curl");
                curl_global_init(CURL_GLOBAL_DEFAULT);
            });

            auto* curl = curl_easy_init();
            if (!curl) {
                PaperLogger.error("curl_easy_init failed for {}", url);
                return 0;
            }
            curl_easy_setopt(curl, CURLOPT_URL, url.c_str());
            curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 1L);
            curl_easy_setopt(curl, CURLOPT_TIMEOUT, 90L);
            curl_easy_setopt(curl, CURLOPT_SSL_VERIFYPEER, false);
            curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, curlWrite);
            curl_easy_setopt(curl, CURLOPT_WRITEDATA, &data);
            curl_easy_setopt(curl, CURLOPT_USERAGENT, "TournamentAssistant Standalone");
            auto result = curl_easy_perform(curl);
            long httpCode = 0;
            curl_easy_getinfo(curl, CURLINFO_RESPONSE_CODE, &httpCode);
            if (result != CURLE_OK) {
                PaperLogger.warn("curl failed for {}: {}", url, curl_easy_strerror(result));
            }
            PaperLogger.info("HTTP GET completed url={} status={} bytes={}", url, httpCode, data.size());
            curl_easy_cleanup(curl);
            return httpCode;
        }

        std::string beatSaverDownloadUrl(std::string const& hash, std::string const& customHostUrl) {
            PaperLogger.info("Resolving BeatSaver download URL hash='{}' customHost='{}'", hash, customHostUrl);
            if (!customHostUrl.empty()) {
                return customHostUrl + toUpper(hash) + ".zip";
            }

            std::string response;
            auto status = getUrl("https://api.beatsaver.com/maps/hash/" + hash, response);
            if (status != 200) {
                PaperLogger.warn("BeatSaver metadata lookup failed for hash='{}', using CDN fallback", hash);
                return "https://cdn.beatsaver.com/" + hash + ".zip";
            }

            rapidjson::Document document;
            document.Parse(response.data());
            if (document.HasParseError() || !document.IsObject() || !document.HasMember("versions")) {
                PaperLogger.warn("BeatSaver metadata parse failed for hash='{}', using CDN fallback", hash);
                return "https://cdn.beatsaver.com/" + hash + ".zip";
            }

            auto versions = document["versions"].GetArray();
            if (versions.Empty() || !versions[0].HasMember("downloadURL")) {
                PaperLogger.warn("BeatSaver metadata missing downloadURL for hash='{}', using CDN fallback", hash);
                return "https://cdn.beatsaver.com/" + hash + ".zip";
            }

            auto url = std::string(versions[0]["downloadURL"].GetString());
            PaperLogger.info("BeatSaver download URL resolved {}", url);
            return url;
        }

        void parseBeatSaverDetails(GameplayParameters const& parameters, std::string const& response, SongDetails& details) {
            PaperLogger.info("Parsing BeatSaver details levelId='{}' responseBytes={}", parameters.beatmap.levelId, response.size());
            rapidjson::Document document;
            document.Parse(response.data());
            if (document.HasParseError() || !document.IsObject()) {
                PaperLogger.warn("BeatSaver details parse failed for '{}'", parameters.beatmap.levelId);
                return;
            }

            if (document.HasMember("metadata") && document["metadata"].IsObject()) {
                auto const& metadata = document["metadata"];
                if (metadata.HasMember("songName")) details.name = metadata["songName"].GetString();
                if (metadata.HasMember("songAuthorName")) details.songAuthor = metadata["songAuthorName"].GetString();
                if (metadata.HasMember("levelAuthorName")) details.mapper = metadata["levelAuthorName"].GetString();
                if (metadata.HasMember("bpm")) details.bpm = metadata["bpm"].GetFloat();
                if (metadata.HasMember("duration")) details.duration = metadata["duration"].GetInt();
            }

            if (document.HasMember("versions") && document["versions"].IsArray() && !document["versions"].GetArray().Empty()) {
                auto const& version = document["versions"].GetArray()[0];
                if (version.HasMember("coverURL")) details.coverUrl = version["coverURL"].GetString();
                if (version.HasMember("diffs") && version["diffs"].IsArray()) {
                    auto selectedCharacteristic = parameters.beatmap.characteristic.serializedName.empty() ? "Standard" : parameters.beatmap.characteristic.serializedName;
                    auto selectedDifficulty = beatSaverDifficultyName(parameters.beatmap.difficulty);
                    for (auto const& diff : version["diffs"].GetArray()) {
                        if (!diff.IsObject()) continue;
                        auto characteristicMatches = !diff.HasMember("characteristic") || selectedCharacteristic == diff["characteristic"].GetString();
                        auto difficultyMatches = !diff.HasMember("difficulty") || selectedDifficulty == diff["difficulty"].GetString();
                        if (!characteristicMatches || !difficultyMatches) continue;
                        if (diff.HasMember("notes")) details.notes = diff["notes"].GetInt();
                        if (diff.HasMember("bombs")) details.bombs = diff["bombs"].GetInt();
                        if (diff.HasMember("obstacles")) details.walls = diff["obstacles"].GetInt();
                        if (diff.HasMember("njs")) details.njs = diff["njs"].GetFloat();
                        break;
                    }
                }
            }

            if (details.name.empty()) details.name = parameters.beatmap.name;
            details.loaded = true;
            PaperLogger.info("Parsed song details name='{}' author='{}' mapper='{}' notes={} bombs={} walls={}", details.name, details.songAuthor, details.mapper, details.notes, details.bombs, details.walls);
        }

        GlobalNamespace::BeatmapLevel* getLevelById(std::string const& levelId) {
            auto hash = hashFromLevelId(levelId);
            auto* level = SongCore::API::Loading::GetLevelByHash(hash);
            PaperLogger.info("GetLevelByHash levelId='{}' hash='{}' found={}", levelId, hash, level != nullptr);
            return level;
        }

        GlobalNamespace::IBeatmapLevelData* getCustomBeatmapLevelData(GlobalNamespace::BeatmapLevel* level) {
            auto customLevel = il2cpp_utils::try_cast<SongCore::SongLoader::CustomBeatmapLevel>(level);
            if (customLevel == std::nullopt || !customLevel.value()) {
                PaperLogger.info("Level is not a SongCore CustomBeatmapLevel, level={}", static_cast<void*>(level));
                return nullptr;
            }

            auto* levelData = customLevel.value()->get_beatmapLevelData();
            PaperLogger.info(
                "SongCore CustomBeatmapLevel detected level='{}' levelData={}",
                (std::string)level->levelID,
                static_cast<void*>(levelData)
            );
            return levelData;
        }

        GlobalNamespace::EnvironmentsListModel* getEnvironmentsListModel() {
            auto* container = BSML::Helpers::GetDiContainer();
            if (!container) {
                PaperLogger.warn("BSML menu DiContainer is null, cannot resolve EnvironmentsListModel");
                return nullptr;
            }

            auto* environmentsListModel = container->TryResolve<GlobalNamespace::EnvironmentsListModel*>();
            PaperLogger.info("Resolved EnvironmentsListModel={}", static_cast<void*>(environmentsListModel));
            return environmentsListModel;
        }

        GlobalNamespace::BeatmapCharacteristicSO* findCharacteristic(std::string serializedName) {
            if (serializedName.empty()) serializedName = "Standard";
            PaperLogger.info("Finding characteristic '{}'", serializedName);
            auto characteristics = UnityEngine::Resources::FindObjectsOfTypeAll<GlobalNamespace::BeatmapCharacteristicSO*>();
            PaperLogger.info("Found {} characteristic resources", characteristics.size());
            for (auto characteristic : characteristics) {
                if (!characteristic) continue;
                if ((std::string)characteristic->get_serializedName() == serializedName) return characteristic;
            }
            for (auto characteristic : characteristics) {
                if (characteristic) return characteristic;
            }
            return nullptr;
        }

        bool hasDifficulty(GlobalNamespace::BeatmapLevel* level, GlobalNamespace::BeatmapCharacteristicSO* characteristic, int32_t difficulty) {
            if (!level || !characteristic) return false;
            auto basicData = level->GetDifficultyBeatmapData(characteristic, GlobalNamespace::BeatmapDifficulty(difficulty));
            PaperLogger.info("Validated difficulty level='{}' characteristic='{}' difficulty={} exists={}",
                (std::string)level->levelID,
                (std::string)characteristic->get_serializedName(),
                difficulty,
                basicData != nullptr
            );
            return basicData != nullptr;
        }

        enum PlayerOptions : int32_t {
            LeftHanded = 1,
            StaticLights = 2,
            NoHud = 4,
            AdvancedHud = 8,
            ReduceDebris = 16,
            AutoPlayerHeight = 32,
            NoFailEffects = 64,
            AutoRestart = 128,
            HideNoteSpawnEffect = 256,
            AdaptiveSfx = 512,
            ArcsHapticFeedback = 1024
        };

        bool hasPlayerOption(int32_t options, PlayerOptions option) {
            return (options & int32_t(option)) != 0;
        }

        GlobalNamespace::PlayerSpecificSettings* makePlayerSettings(GameplayParameters const& parameters) {
            auto playerDataModels = UnityEngine::Resources::FindObjectsOfTypeAll<GlobalNamespace::PlayerDataModel*>();
            if (parameters.playerSettings.options == 0 && playerDataModels.size() > 0 && playerDataModels[0] && playerDataModels[0]->get_playerData()) {
                auto* existing = playerDataModels[0]->get_playerData()->get_playerSpecificSettings();
                if (existing) {
                    PaperLogger.info("Using current player-specific settings");
                    return existing;
                }
            }

            auto const& settings = parameters.playerSettings;
            auto effects = hasPlayerOption(settings.options, PlayerOptions::StaticLights)
                ? GlobalNamespace::EnvironmentEffectsFilterPreset::NoEffects
                : GlobalNamespace::EnvironmentEffectsFilterPreset::AllEffects;

            PaperLogger.info("Creating forced player settings options={} height={} sfx={} trail={} njdType={} njsOffset={} fixedDuration={} arcVisibility={}",
                settings.options,
                settings.playerHeight,
                settings.sfxVolume,
                settings.saberTrailIntensity,
                settings.noteJumpDurationTypeSettings,
                settings.noteJumpStartBeatOffset,
                settings.noteJumpFixedDuration,
                settings.arcVisibilityType
            );

            return GlobalNamespace::PlayerSpecificSettings::New_ctor(
                hasPlayerOption(settings.options, PlayerOptions::LeftHanded),
                settings.playerHeight,
                hasPlayerOption(settings.options, PlayerOptions::AutoPlayerHeight),
                settings.sfxVolume,
                hasPlayerOption(settings.options, PlayerOptions::ReduceDebris),
                hasPlayerOption(settings.options, PlayerOptions::NoHud),
                hasPlayerOption(settings.options, PlayerOptions::NoFailEffects),
                hasPlayerOption(settings.options, PlayerOptions::AdvancedHud),
                hasPlayerOption(settings.options, PlayerOptions::AutoRestart),
                settings.saberTrailIntensity,
                GlobalNamespace::NoteJumpDurationTypeSettings(settings.noteJumpDurationTypeSettings),
                settings.noteJumpFixedDuration,
                settings.noteJumpStartBeatOffset,
                hasPlayerOption(settings.options, PlayerOptions::HideNoteSpawnEffect),
                hasPlayerOption(settings.options, PlayerOptions::AdaptiveSfx),
                hasPlayerOption(settings.options, PlayerOptions::ArcsHapticFeedback),
                GlobalNamespace::ArcVisibilityType(settings.arcVisibilityType),
                effects,
                effects,
                1.0f
            );
        }

        GlobalNamespace::GameplayModifiers* makeGameplayModifiers(GameplayParameters const& parameters) {
            auto options = parameters.gameplayModifiers.options;
            bool noFail = parameters.disableFail || ((options & GameOptions::NoFail) != 0) || ((options & GameOptions::DemoNoFail) != 0);
            PaperLogger.info("Creating gameplay modifiers options={} disableFail={} noFail={}", int(options), parameters.disableFail, noFail);
            auto energyType = (options & GameOptions::BatteryEnergy) != 0
                ? GlobalNamespace::GameplayModifiers_EnergyType::Battery
                : GlobalNamespace::GameplayModifiers_EnergyType::Bar;
            auto obstacleType = (options & GameOptions::NoObstacles) != 0 || (options & GameOptions::DemoNoObstacles) != 0
                ? GlobalNamespace::GameplayModifiers_EnabledObstacleType::NoObstacles
                : GlobalNamespace::GameplayModifiers_EnabledObstacleType::All;
            auto songSpeed = GlobalNamespace::GameplayModifiers_SongSpeed::Normal;
            if ((options & GameOptions::SlowSong) != 0) songSpeed = GlobalNamespace::GameplayModifiers_SongSpeed::Slower;
            if ((options & GameOptions::FastSong) != 0) songSpeed = GlobalNamespace::GameplayModifiers_SongSpeed::Faster;
            if ((options & GameOptions::SuperFastSong) != 0) songSpeed = GlobalNamespace::GameplayModifiers_SongSpeed::SuperFast;

            return GlobalNamespace::GameplayModifiers::New_ctor(
                energyType,
                noFail,
                (options & GameOptions::InstaFail) != 0,
                (options & GameOptions::FailOnClash) != 0,
                obstacleType,
                (options & GameOptions::NoBombs) != 0,
                (options & GameOptions::FastNotes) != 0,
                (options & GameOptions::StrictAngles) != 0,
                (options & GameOptions::DisappearingArrows) != 0,
                songSpeed,
                (options & GameOptions::NoArrows) != 0,
                (options & GameOptions::GhostNotes) != 0,
                (options & GameOptions::ProMode) != 0,
                (options & GameOptions::ZenMode) != 0,
                (options & GameOptions::SmallCubes) != 0
            );
        }

        void startLevel(GameplayParameters parameters, PlayFinishedCallback callback, bool reportSongFinished) {
            PaperLogger.info("startLevel levelId='{}' characteristic='{}' difficulty={} disablePause={} attempts={}", parameters.beatmap.levelId, parameters.beatmap.characteristic.serializedName, parameters.beatmap.difficulty, parameters.disablePause, parameters.attempts);
            auto* level = getLevelById(parameters.beatmap.levelId);
            if (!level) {
                PaperLogger.warn("Cannot start {}, level not loaded", parameters.beatmap.levelId);
                if (callback) callback(PlayResult{.completed = false});
                return;
            }

            auto* characteristic = findCharacteristic(parameters.beatmap.characteristic.serializedName);
            if (!characteristic) {
                PaperLogger.warn("Cannot start {}, characteristic not found", parameters.beatmap.levelId);
                if (callback) callback(PlayResult{.completed = false});
                return;
            }
            if (!hasDifficulty(level, characteristic, parameters.beatmap.difficulty)) {
                PaperLogger.warn("Cannot start {}, difficulty {} was not found for characteristic '{}'", parameters.beatmap.levelId, parameters.beatmap.difficulty, parameters.beatmap.characteristic.serializedName);
                if (callback) callback(PlayResult{.completed = false});
                return;
            }

            GlobalNamespace::BeatmapKey key;
            key._ctor(level->levelID, characteristic, GlobalNamespace::BeatmapDifficulty(parameters.beatmap.difficulty));

            auto helpers = UnityEngine::Resources::FindObjectsOfTypeAll<GlobalNamespace::MenuTransitionsHelper*>();
            PaperLogger.info("Found {} MenuTransitionsHelper resources", helpers.size());
            if (helpers.size() == 0 || !helpers[0]) {
                PaperLogger.warn("MenuTransitionsHelper was not found");
                if (callback) callback(PlayResult{.completed = false});
                return;
            }

            auto* modifiers = makeGameplayModifiers(parameters);
            auto* playerSettings = makePlayerSettings(parameters);
            auto* beatmapLevelData = getCustomBeatmapLevelData(level);
            auto* environmentsListModel = getEnvironmentsListModel();
            auto recordingToolData = System::Nullable_1<GlobalNamespace::RecordingToolManager_SetupData>();
            AntiPause::reset();
            if (parameters.useSync) {
                PaperLogger.info("Streamsync enabled: level will pause after GameCore is ready");
            }
            if (parameters.disablePause) {
                PaperLogger.info("Applying anti-pause: pause and continue disabled");
                AntiPause::setAllowPause(false);
                AntiPause::setAllowContinueAfterPause(false);
            }
            if (parameters.disableScoresaberSubmission) {
                PaperLogger.info("Disabling score submission for this TournamentAssistant song");
                MetaCore::Game::DisableScoreSubmissionOnce("TAStandalone");
            }
            if (parameters.attempts > 0) {
                PaperLogger.info("Applying anti-pause: restart disabled due to limited attempts={}", parameters.attempts);
                AntiPause::setAllowRestart(false);
            }
            auto levelFinishedCallback = custom_types::MakeDelegate<
                System::Action_2<UnityW<GlobalNamespace::StandardLevelScenesTransitionSetupDataSO>, GlobalNamespace::LevelCompletionResults*>*
            >((std::function<void(UnityW<GlobalNamespace::StandardLevelScenesTransitionSetupDataSO>, GlobalNamespace::LevelCompletionResults*)>)
                [parameters, callback, reportSongFinished](UnityW<GlobalNamespace::StandardLevelScenesTransitionSetupDataSO>, GlobalNamespace::LevelCompletionResults* results) {
                    PaperLogger.info("Level finished callback levelId='{}' results={}", parameters.beatmap.levelId, static_cast<void*>(results));
                    AntiPause::reset();
                    MidPlayModifiers::reset();
                    PlayResult playResult;
                    playResult.completed = true;

                    if (results) {
                        auto state = results->__cordl_internal_get_levelEndStateType();
                        auto action = results->__cordl_internal_get_levelEndAction();
                        if (action == GlobalNamespace::LevelCompletionResults_LevelEndAction::Quit) {
                            playResult.type = SongCompletionType::Quit;
                        } else if (state == GlobalNamespace::LevelCompletionResults_LevelEndStateType::Failed) {
                            playResult.type = SongCompletionType::Failed;
                        } else {
                            playResult.type = SongCompletionType::Passed;
                        }
                        playResult.score = results->__cordl_internal_get_modifiedScore();
                        playResult.misses = results->__cordl_internal_get_missedCount();
                        playResult.badCuts = results->__cordl_internal_get_badCutsCount();
                        playResult.goodCuts = results->__cordl_internal_get_goodCutsCount();
                        playResult.endTime = results->__cordl_internal_get_endSongTime();
                    }

                    if (reportSongFinished) {
                        Client::instance().sendSongFinished(parameters, playResult.type, playResult.score, playResult.misses, playResult.badCuts, playResult.goodCuts, playResult.endTime);
                    } else {
                        PaperLogger.info("Song finished reporting suppressed for levelId='{}' type={}", parameters.beatmap.levelId, int(playResult.type));
                    }
                    if (callback) callback(playResult);
                });

            if (beatmapLevelData) {
                PaperLogger.info("Calling MenuTransitionsHelper::StartStandardLevel with explicit SongCore beatmapLevelData={}", static_cast<void*>(beatmapLevelData));
                helpers[0]->StartStandardLevel(
                    "Solo",
                    ByRef<GlobalNamespace::BeatmapKey>(&key),
                    level,
                    beatmapLevelData,
                    nullptr,
                    nullptr,
                    false,
                    nullptr,
                    modifiers,
                    playerSettings,
                    nullptr,
                    environmentsListModel,
                    "Menu",
                    false,
                    false,
                    nullptr,
                    nullptr,
                    levelFinishedCallback,
                    nullptr,
                    recordingToolData
                );
            } else {
                PaperLogger.info("Calling MenuTransitionsHelper::StartStandardLevel without explicit beatmapLevelData");
                helpers[0]->StartStandardLevel(
                    "Solo",
                    ByRef<GlobalNamespace::BeatmapKey>(&key),
                    level,
                    nullptr,
                    nullptr,
                    false,
                    nullptr,
                    modifiers,
                    playerSettings,
                    nullptr,
                    environmentsListModel,
                    "Menu",
                    false,
                    false,
                    nullptr,
                    nullptr,
                    levelFinishedCallback,
                    nullptr,
                    recordingToolData
                );
            }
            PaperLogger.info("StartStandardLevel returned");
            if (parameters.useSync) {
                TA::StreamSync::begin();
            }
        }
    }

    SongDetails detailsFor(GameplayParameters const& parameters) {
        auto key = detailsKey(parameters);
        std::scoped_lock lock(detailsMutex);
        auto it = detailsCache.find(key);
        if (it != detailsCache.end()) return it->second;

        SongDetails fallback;
        fallback.name = parameters.beatmap.name;
        return fallback;
    }

    void requestDetails(GameplayParameters parameters) {
        auto key = detailsKey(parameters);
        PaperLogger.info("requestDetails key='{}' levelId='{}'", key, parameters.beatmap.levelId);
        {
            std::scoped_lock lock(detailsMutex);
            if (detailsCache.contains(key)) {
                PaperLogger.info("requestDetails cache hit/pending key='{}' loaded={}", key, detailsCache[key].loaded);
                return;
            }
            SongDetails pending;
            pending.name = parameters.beatmap.name;
            detailsCache[key] = pending;
        }

        std::thread([parameters = std::move(parameters), key] {
            PaperLogger.info("requestDetails worker started key='{}'", key);
            auto hash = hashFromLevelId(parameters.beatmap.levelId);
            std::string response;
            auto status = getUrl("https://api.beatsaver.com/maps/hash/" + hash, response);
            SongDetails details;
            details.name = parameters.beatmap.name;
            if (status == 200) parseBeatSaverDetails(parameters, response, details);
            {
                std::scoped_lock lock(detailsMutex);
                detailsCache[key] = details;
            }
            PaperLogger.info("requestDetails worker finished key='{}' status={} loaded={}", key, status, details.loaded);
        }).detach();
    }

    bool isDownloaded(std::string const& levelId) {
        return getLevelById(levelId) != nullptr;
    }

    void ensureDownloaded(std::string levelId, std::string customHostUrl, DownloadCallback callback) {
        PaperLogger.info("ensureDownloaded levelId='{}' customHost='{}'", levelId, customHostUrl);
        if (getLevelById(levelId)) {
            PaperLogger.info("ensureDownloaded already installed '{}'", levelId);
            if (callback) callback(true, "");
            return;
        }

        std::thread([levelId = std::move(levelId), customHostUrl = std::move(customHostUrl), callback = std::move(callback)] {
            PaperLogger.info("ensureDownloaded worker started levelId='{}'", levelId);
            auto hash = hashFromLevelId(levelId);
            auto url = beatSaverDownloadUrl(hash, customHostUrl);
            std::string zipBytes;
            auto status = getUrl(url, zipBytes);
            if (status != 200 || zipBytes.empty()) {
                PaperLogger.error("Download failed levelId='{}' url='{}' status={} bytes={}", levelId, url, status, zipBytes.size());
                if (callback) callback(false, "Download failed");
                return;
            }

            auto targetFolder = std::string(SongCore::API::Loading::GetPreferredCustomLevelPath()) + "/" + hash;
            PaperLogger.info("Extracting song levelId='{}' bytes={} target='{}'", levelId, zipBytes.size(), targetFolder);
            std::filesystem::create_directories(targetFolder);
            int args = 0;
            auto zipStatus = zip_stream_extract(zipBytes.data(), zipBytes.length(), targetFolder.c_str(), +[](const char*, void*) -> int { return 0; }, &args);
            if (zipStatus != 0) {
                PaperLogger.error("Zip extract failed levelId='{}' status={}", levelId, zipStatus);
                if (callback) callback(false, "Zip extract failed");
                return;
            }

            BSML::MainThreadScheduler::Schedule([levelId, callback] {
                PaperLogger.info("Refreshing SongCore after download '{}'", levelId);
                SongCore::API::Loading::RefreshSongs(false);
                SongCore::API::Loading::RefreshLevelPacks();
                BSML::MainThreadScheduler::ScheduleAfterTime(5.0f, [levelId, callback] {
                    PaperLogger.info("Checking SongCore refresh result '{}'", levelId);
                    if (getLevelById(levelId)) {
                        if (callback) callback(true, "");
                    } else if (callback) {
                        callback(false, "Song refresh failed");
                    }
                });
            });
        }).detach();
    }

    void playSong(GameplayParameters parameters, PlayFinishedCallback callback, bool reportSongFinished) {
        PaperLogger.info("playSong levelId='{}' reportSongFinished={}", parameters.beatmap.levelId, reportSongFinished);
        ensureDownloaded(parameters.beatmap.levelId, "", [parameters = std::move(parameters), callback = std::move(callback), reportSongFinished](bool success, std::string) {
            PaperLogger.info("playSong ensureDownloaded callback levelId='{}' success={}", parameters.beatmap.levelId, success);
            if (!success) {
                if (callback) callback(PlayResult{.completed = false});
                return;
            }
            BSML::MainThreadScheduler::Schedule([parameters, callback, reportSongFinished] {
                PaperLogger.info("playSong scheduled startLevel levelId='{}'", parameters.beatmap.levelId);
                startLevel(parameters, callback, reportSongFinished);
            });
        });
    }

    void returnToMenu() {
        PaperLogger.info("returnToMenu requested");
        BSML::MainThreadScheduler::Schedule([] {
            PaperLogger.info("returnToMenu scheduled body entered");
            TA::StreamSync::clearImmediate();
            AntiPause::reset();
            MidPlayModifiers::reset();
            RealtimeScoreHooks::resetCounters();
            auto preparers = UnityEngine::Resources::FindObjectsOfTypeAll<GlobalNamespace::PrepareLevelCompletionResults*>();
            PaperLogger.info("returnToMenu preparers={}", preparers.size());
            if (preparers.size() == 0 || !preparers[0]) return;
            auto* results = preparers[0]->FillLevelCompletionResults(
                GlobalNamespace::LevelCompletionResults_LevelEndStateType::Incomplete,
                GlobalNamespace::LevelCompletionResults_LevelEndAction::Quit
            );
            if (!results) {
                PaperLogger.warn("returnToMenu FillLevelCompletionResults returned null");
                return;
            }

            auto transitions = UnityEngine::Resources::FindObjectsOfTypeAll<GlobalNamespace::StandardLevelScenesTransitionSetupDataSO*>();
            PaperLogger.info("returnToMenu transitions={}", transitions.size());
            if (transitions.size() > 0 && transitions[0]) transitions[0]->Finish(results);
        });
    }
}
