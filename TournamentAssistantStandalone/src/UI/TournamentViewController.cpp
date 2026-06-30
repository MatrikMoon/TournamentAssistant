#include "TA/TournamentViewController.hpp"

#include "TA/Client.hpp"
#include "TA/Constants.hpp"
#include "TA/Song.hpp"
#include "TA/UI/RoomViewController.hpp"
#include "TA/UI/TournamentListViewController.hpp"
#include "TA/UI/TournamentModeViewController.hpp"
#include "main.hpp"

#include "HMUI/ScreenSystem.hpp"
#include "HMUI/FlowCoordinator.hpp"
#include "HMUI/ViewController.hpp"
#include "HMUI/Touchable.hpp"
#include "GlobalNamespace/GameplayModifierToggle.hpp"
#include "GlobalNamespace/GameplayModifiersPanelController.hpp"
#include "GlobalNamespace/GameplaySetupViewController.hpp"
#include "GlobalNamespace/PlayerSettingsPanelController.hpp"
#include "TMPro/TextAlignmentOptions.hpp"
#include "TMPro/TextOverflowModes.hpp"
#include "UnityEngine/GameObject.hpp"
#include "UnityEngine/RectOffset.hpp"
#include "UnityEngine/Object.hpp"
#include "UnityEngine/RectTransform.hpp"
#include "UnityEngine/Resources.hpp"
#include "UnityEngine/Sprite.hpp"
#include "UnityEngine/TextAnchor.hpp"
#include "UnityEngine/Transform.hpp"
#include "UnityEngine/UI/Button.hpp"
#include "UnityEngine/UI/HorizontalLayoutGroup.hpp"
#include "UnityEngine/UI/LayoutElement.hpp"
#include "UnityEngine/UI/VerticalLayoutGroup.hpp"

#include "bsml/shared/BSML/MainThreadScheduler.hpp"
#include "bsml/shared/BSML/Components/CustomListTableData.hpp"
#include "bsml/shared/BSML-Lite.hpp"
#include "bsml/shared/BSML-Lite/Creation/Lists.hpp"
#include "libcurl/shared/curl.h"
#include "paper2_scotland2/shared/string_convert.hpp"

#include <algorithm>
#include <cstdint>
#include <map>
#include <mutex>
#include <sstream>
#include <thread>
#include <vector>

using namespace BSML::Lite;

DEFINE_TYPE(TA, TournamentViewController);

namespace {
    enum class ViewMode {
        Tournaments,
        Tournament,
        TeamSelection,
        Qualifiers,
        QualifierMaps,
        MapDetail
    };

    ViewMode viewMode = ViewMode::Tournaments;
    std::string selectedQualifierId;
    std::string selectedMapId;
    std::string lastRequestedMapKey;
    std::string pendingTournamentId;
    std::string joiningTournamentId;
    ViewMode postJoinMode = ViewMode::Tournament;
    TA::TournamentViewController* activeController = nullptr;
    bool controllerInHierarchy = false;
    std::mutex imageMutex;
    std::map<std::string, UnityEngine::Sprite*> tournamentSprites;
    std::map<std::string, bool> tournamentImageRequests;
    std::map<std::string, UnityEngine::Sprite*> qualifierSprites;
    std::map<std::string, bool> qualifierImageRequests;
    std::map<std::string, UnityEngine::Sprite*> songCoverSprites;
    std::map<std::string, bool> songCoverRequests;
    bool pendingPreservedResumeRefresh = false;
    HMUI::FlowCoordinator* roomSidePanelFlowCoordinator = nullptr;
    GlobalNamespace::GameplayModifiersPanelController* roomModifiersPanelController = nullptr;
    bool roomSidePanelVisible = false;
    bool roomModifierTogglesHidden = false;

    TMPro::TextMeshProUGUI* addText(UnityEngine::Transform* parent, std::string const& value, float size);
    void clearRoomSidePanel();

    size_t curlWrite(void* contents, size_t size, size_t nmemb, void* userdata) {
        auto* out = static_cast<std::string*>(userdata);
        out->append(static_cast<char*>(contents), size * nmemb);
        return size * nmemb;
    }

    void refreshView() {
        PaperLogger.info(
            "UI refresh requested, activeController={} inHierarchy={}",
            static_cast<void*>(activeController),
            controllerInHierarchy
        );
        if (activeController && controllerInHierarchy) activeController->Refresh();
    }

    void resetUiState() {
        PaperLogger.info("Resetting TournamentAssistant UI state");
        clearRoomSidePanel();
        pendingTournamentId.clear();
        joiningTournamentId.clear();
        selectedQualifierId.clear();
        selectedMapId.clear();
        lastRequestedMapKey.clear();
        viewMode = ViewMode::Tournaments;
        postJoinMode = ViewMode::Tournament;
        pendingPreservedResumeRefresh = false;
    }

    HMUI::FlowCoordinator* owningFlowCoordinator(TA::TournamentViewController* controller) {
        if (!controller) return nullptr;

        auto coordinators = UnityEngine::Resources::FindObjectsOfTypeAll<HMUI::FlowCoordinator*>();
        for (auto* coordinator : coordinators) {
            if (!coordinator) continue;
            auto top = coordinator->get_topViewController();
            if (top.ptr() == static_cast<HMUI::ViewController*>(controller)) return coordinator;

            auto* mainControllers = coordinator->__cordl_internal_get__mainScreenViewControllers();
            if (!mainControllers) continue;
            auto count = mainControllers->get_Count();
            for (int32_t i = 0; i < count; ++i) {
                if (mainControllers->get_Item(i).ptr() == static_cast<HMUI::ViewController*>(controller)) return coordinator;
            }
        }

        PaperLogger.warn("No owning FlowCoordinator found for TournamentAssistant controller={}", static_cast<void*>(controller));
        return nullptr;
    }

    GlobalNamespace::GameplaySetupViewController* findGameplaySetupViewController() {
        auto controllers = UnityEngine::Resources::FindObjectsOfTypeAll<GlobalNamespace::GameplaySetupViewController*>();
        PaperLogger.info("Found {} GameplaySetupViewController instances", controllers.size());
        for (auto* controller : controllers) {
            if (controller) return controller;
        }
        return nullptr;
    }

    void setRoomModifierTogglesActive(GlobalNamespace::GameplayModifiersPanelController* controller, bool active) {
        if (!controller) return;
        auto toggles = controller->__cordl_internal_get__gameplayModifierToggles();
        PaperLogger.info("Setting room modifier toggles active={} count={}", active, toggles.size());
        for (auto toggleRef : toggles) {
            auto* toggle = toggleRef.ptr();
            if (!toggle) continue;
            auto name = Paper::StringConvert::from_utf16(toggle->get_name());
            if (!active && name == "ProMode") continue;
            if (auto* object = toggle->get_gameObject().ptr()) object->SetActive(active);
        }
    }

    void hideRoomSidePanelModifiers(GlobalNamespace::GameplayModifiersPanelController* controller) {
        if (!controller || roomModifierTogglesHidden) return;
        setRoomModifierTogglesActive(controller, false);
        roomModifierTogglesHidden = true;
    }

    void restoreRoomSidePanelModifiers() {
        if (!roomModifierTogglesHidden) return;
        setRoomModifierTogglesActive(roomModifiersPanelController, true);
        roomModifierTogglesHidden = false;
    }

    void clearRoomSidePanel() {
        if (!roomSidePanelVisible && !roomModifierTogglesHidden) return;

        PaperLogger.info("Clearing TournamentAssistant room side panel");
        if (roomSidePanelFlowCoordinator) {
            roomSidePanelFlowCoordinator->SetLeftScreenViewController(nullptr, HMUI::ViewController::AnimationType::None);
        }
        restoreRoomSidePanelModifiers();
        roomSidePanelVisible = false;
        roomSidePanelFlowCoordinator = nullptr;
        roomModifiersPanelController = nullptr;
    }

    void showRoomSidePanel(TA::TournamentViewController* controller, TA::Map const& map) {
        if (!controller) return;
        auto* flowCoordinator = owningFlowCoordinator(controller);
        if (!flowCoordinator) return;

        auto* gameplaySetup = findGameplaySetupViewController();
        if (!gameplaySetup) {
            PaperLogger.warn("Cannot show room side panel because GameplaySetupViewController was not found");
            return;
        }

        auto* modifiers = gameplaySetup->__cordl_internal_get__gameplayModifiersPanelController().ptr();
        if (!modifiers) {
            auto allModifiers = UnityEngine::Resources::FindObjectsOfTypeAll<GlobalNamespace::GameplayModifiersPanelController*>();
            for (auto* candidate : allModifiers) {
                if (candidate) {
                    modifiers = candidate;
                    break;
                }
            }
        }

        if (roomSidePanelVisible && roomSidePanelFlowCoordinator == flowCoordinator && roomModifiersPanelController == modifiers) {
            hideRoomSidePanelModifiers(modifiers);
            return;
        }

        clearRoomSidePanel();
        PaperLogger.info(
            "Showing TournamentAssistant room side panel for map='{}' levelId='{}'",
            map.guid,
            map.gameplayParameters.beatmap.levelId
        );
        gameplaySetup->Setup(
            true,
            true,
            true,
            false,
            GlobalNamespace::PlayerSettingsPanelController::PlayerSettingsPanelLayout::Singleplayer
        );
        flowCoordinator->SetLeftScreenViewController(gameplaySetup, HMUI::ViewController::AnimationType::In);
        roomSidePanelFlowCoordinator = flowCoordinator;
        roomModifiersPanelController = modifiers;
        roomSidePanelVisible = true;
        hideRoomSidePanelModifiers(modifiers);
    }

    void setLayoutSize(UnityEngine::GameObject* object, float width, float height) {
        if (!object) return;
        auto* layout = object->GetComponent<UnityEngine::UI::LayoutElement*>();
        if (!layout) layout = object->AddComponent<UnityEngine::UI::LayoutElement*>();
        if (layout) {
            layout->set_preferredWidth(width);
            layout->set_preferredHeight(height);
            layout->set_minWidth(width);
            layout->set_minHeight(height);
            layout->set_flexibleWidth(0.0f);
            layout->set_flexibleHeight(0.0f);
        }
    }

    void sizeButton(UnityEngine::UI::Button* button, float width, float height, float textSize) {
        if (!button) return;
        setLayoutSize(button->get_gameObject(), width, height);
        SetButtonTextSize(button, textSize);
        ToggleButtonWordWrapping(button, true);
    }

    std::string tournamentDetails(TA::Tournament const& tournament) {
        if (!tournament.server.name.empty()) return tournament.server.name;
        if (!tournament.server.address.empty()) {
            return tournament.server.address + ":" + std::to_string(tournament.server.port);
        }
        return std::to_string(tournament.users.size()) + " players, " +
               std::to_string(tournament.matches.size()) + " matches, " +
               std::to_string(tournament.qualifiers.size()) + " qualifiers";
    }

    std::string tournamentImageUrl(std::string const& imageId) {
        if (imageId.starts_with("http://") || imageId.starts_with("https://")) return imageId;
        return "https://" + std::string(TA::kServerHost) + ":" + std::to_string(TA::kMasterApiPort) + "/api/file/" + imageId;
    }

    long getUrl(std::string const& url, std::string& data) {
        PaperLogger.info("Image HTTP GET {}", url);
        static std::once_flag curlInitFlag;
        std::call_once(curlInitFlag, [] {
            PaperLogger.info("Initializing curl for UI images");
            curl_global_init(CURL_GLOBAL_DEFAULT);
        });

        auto* curl = curl_easy_init();
        if (!curl) {
            PaperLogger.error("curl_easy_init failed for image {}", url);
            return 0;
        }
        curl_easy_setopt(curl, CURLOPT_URL, url.c_str());
        curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 1L);
        curl_easy_setopt(curl, CURLOPT_TIMEOUT, 45L);
        curl_easy_setopt(curl, CURLOPT_SSL_VERIFYPEER, false);
        curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, curlWrite);
        curl_easy_setopt(curl, CURLOPT_WRITEDATA, &data);
        curl_easy_setopt(curl, CURLOPT_USERAGENT, "TournamentAssistant Standalone");
        auto result = curl_easy_perform(curl);
        long httpCode = 0;
        curl_easy_getinfo(curl, CURLINFO_RESPONSE_CODE, &httpCode);
        if (result != CURLE_OK) {
            PaperLogger.warn("Image curl failed for {}: {}", url, curl_easy_strerror(result));
        }
        PaperLogger.info("Image HTTP GET completed url={} status={} bytes={}", url, httpCode, data.size());
        curl_easy_cleanup(curl);
        return httpCode;
    }

    void requestTournamentImage(std::string imageId) {
        if (imageId.empty()) return;
        {
            std::scoped_lock lock(imageMutex);
            if (tournamentSprites.contains(imageId) || tournamentImageRequests[imageId]) return;
            tournamentImageRequests[imageId] = true;
        }

        std::thread([imageId = std::move(imageId)] {
            auto url = tournamentImageUrl(imageId);
            std::string data;
            auto status = getUrl(url, data);
            if (status != 200 || data.empty()) {
                PaperLogger.warn("Tournament image download failed id='{}' status={} bytes={}", imageId, status, data.size());
                {
                    std::scoped_lock lock(imageMutex);
                    tournamentImageRequests[imageId] = false;
                }
                return;
            }

            std::vector<uint8_t> bytes(data.begin(), data.end());
            BSML::MainThreadScheduler::Schedule([imageId, bytes] {
                PaperLogger.info("Creating tournament sprite id='{}' bytes={}", imageId, bytes.size());
                auto* sprite = VectorToSprite(bytes);
                {
                    std::scoped_lock lock(imageMutex);
                    if (sprite) tournamentSprites[imageId] = sprite;
                    tournamentImageRequests[imageId] = false;
                }
                refreshView();
            });
        }).detach();
    }

    void requestSongCover(std::string url) {
        if (url.empty()) return;
        {
            std::scoped_lock lock(imageMutex);
            if (songCoverSprites.contains(url) || songCoverRequests[url]) return;
            songCoverRequests[url] = true;
        }

        std::thread([url = std::move(url)] {
            std::string data;
            auto status = getUrl(url, data);
            if (status != 200 || data.empty()) {
                PaperLogger.warn("Song cover download failed url='{}' status={} bytes={}", url, status, data.size());
                {
                    std::scoped_lock lock(imageMutex);
                    songCoverRequests[url] = false;
                }
                return;
            }

            std::vector<uint8_t> bytes(data.begin(), data.end());
            BSML::MainThreadScheduler::Schedule([url, bytes] {
                PaperLogger.info("Creating song cover sprite url='{}' bytes={}", url, bytes.size());
                auto* sprite = VectorToSprite(bytes);
                {
                    std::scoped_lock lock(imageMutex);
                    if (sprite) songCoverSprites[url] = sprite;
                    songCoverRequests[url] = false;
                }
                refreshView();
            });
        }).detach();
    }

    void requestQualifierImage(std::string imageId) {
        if (imageId.empty()) return;
        {
            std::scoped_lock lock(imageMutex);
            if (qualifierSprites.contains(imageId) || qualifierImageRequests[imageId]) return;
            qualifierImageRequests[imageId] = true;
        }

        std::thread([imageId = std::move(imageId)] {
            auto url = tournamentImageUrl(imageId);
            std::string data;
            auto status = getUrl(url, data);
            if (status != 200 || data.empty()) {
                PaperLogger.warn("Qualifier image download failed id='{}' status={} bytes={}", imageId, status, data.size());
                {
                    std::scoped_lock lock(imageMutex);
                    qualifierImageRequests[imageId] = false;
                }
                return;
            }

            std::vector<uint8_t> bytes(data.begin(), data.end());
            BSML::MainThreadScheduler::Schedule([imageId, bytes] {
                PaperLogger.info("Creating qualifier sprite id='{}' bytes={}", imageId, bytes.size());
                auto* sprite = VectorToSprite(bytes);
                {
                    std::scoped_lock lock(imageMutex);
                    if (sprite) qualifierSprites[imageId] = sprite;
                    qualifierImageRequests[imageId] = false;
                }
                refreshView();
            });
        }).detach();
    }

    bool renderTournamentImage(UnityEngine::Transform* parent, TA::Tournament const& tournament, float size) {
        auto const& imageId = tournament.settings.tournamentImage;
        if (imageId.empty()) return false;

        UnityEngine::Sprite* sprite = nullptr;
        bool pending = false;
        {
            std::scoped_lock lock(imageMutex);
            auto it = tournamentSprites.find(imageId);
            if (it != tournamentSprites.end()) sprite = it->second;
            pending = tournamentImageRequests[imageId];
        }

        if (sprite) {
            auto* image = CreateImage(parent, sprite, {0, 0}, {size, size});
            if (image) setLayoutSize(image->get_gameObject(), size, size);
            return true;
        }

        if (!pending) requestTournamentImage(imageId);
        addText(parent, "Image loading...", 2.4f);
        return false;
    }

    UnityEngine::Sprite* tournamentSprite(TA::Tournament const& tournament) {
        auto const& imageId = tournament.settings.tournamentImage;
        if (imageId.empty()) return nullptr;

        UnityEngine::Sprite* sprite = nullptr;
        bool pending = false;
        {
            std::scoped_lock lock(imageMutex);
            auto it = tournamentSprites.find(imageId);
            if (it != tournamentSprites.end()) sprite = it->second;
            pending = tournamentImageRequests[imageId];
        }

        if (!sprite && !pending) requestTournamentImage(imageId);
        return sprite;
    }

    bool renderSongCover(UnityEngine::Transform* parent, std::string const& url, float size) {
        if (url.empty()) return false;

        UnityEngine::Sprite* sprite = nullptr;
        bool pending = false;
        {
            std::scoped_lock lock(imageMutex);
            auto it = songCoverSprites.find(url);
            if (it != songCoverSprites.end()) sprite = it->second;
            pending = songCoverRequests[url];
        }

        if (sprite) {
            auto* image = CreateImage(parent, sprite, {0, 0}, {size, size});
            if (image) setLayoutSize(image->get_gameObject(), size, size);
            return true;
        }

        if (!pending) requestSongCover(url);
        return false;
    }

    UnityEngine::Sprite* qualifierSprite(TA::QualifierEvent const& qualifier) {
        if (qualifier.image.empty()) return nullptr;
        UnityEngine::Sprite* sprite = nullptr;
        bool pending = false;
        {
            std::scoped_lock lock(imageMutex);
            auto it = qualifierSprites.find(qualifier.image);
            if (it != qualifierSprites.end()) sprite = it->second;
            pending = qualifierImageRequests[qualifier.image];
        }
        if (!sprite && !pending) requestQualifierImage(qualifier.image);
        return sprite;
    }

    UnityEngine::Sprite* mapSprite(TA::Map const& map) {
        TA::Song::requestDetails(map.gameplayParameters);
        auto details = TA::Song::detailsFor(map.gameplayParameters);
        if (details.coverUrl.empty()) return nullptr;

        UnityEngine::Sprite* sprite = nullptr;
        bool pending = false;
        {
            std::scoped_lock lock(imageMutex);
            auto it = songCoverSprites.find(details.coverUrl);
            if (it != songCoverSprites.end()) sprite = it->second;
            pending = songCoverRequests[details.coverUrl];
        }
        if (!sprite && !pending) requestSongCover(details.coverUrl);
        return sprite;
    }

    std::string modifiersLabel(TA::GameplayParameters const& parameters) {
        std::vector<std::string> labels;
        auto options = parameters.gameplayModifiers.options;
        if (parameters.disableFail || (options & TA::GameOptions::NoFail) != 0 || (options & TA::GameOptions::DemoNoFail) != 0) labels.emplace_back("No Fail");
        if ((options & TA::GameOptions::NoBombs) != 0) labels.emplace_back("No Bombs");
        if ((options & TA::GameOptions::NoArrows) != 0) labels.emplace_back("No Arrows");
        if ((options & TA::GameOptions::NoObstacles) != 0 || (options & TA::GameOptions::DemoNoObstacles) != 0) labels.emplace_back("No Walls");
        if ((options & TA::GameOptions::GhostNotes) != 0) labels.emplace_back("Ghost Notes");
        if ((options & TA::GameOptions::DisappearingArrows) != 0) labels.emplace_back("Disappearing Arrows");
        if ((options & TA::GameOptions::SlowSong) != 0) labels.emplace_back("Slower Song");
        if ((options & TA::GameOptions::FastSong) != 0) labels.emplace_back("Faster Song");
        if ((options & TA::GameOptions::SuperFastSong) != 0) labels.emplace_back("Super Fast Song");
        if (labels.empty()) return "Modifiers: none";

        std::string result = "Modifiers: ";
        for (size_t i = 0; i < labels.size(); ++i) {
            if (i > 0) result += ", ";
            result += labels[i];
        }
        return result;
    }

    std::string playerOptionsLabel(TA::GameplayParameters const& parameters) {
        std::vector<std::string> labels;
        auto options = parameters.playerSettings.options;
        if ((options & 1) != 0) labels.emplace_back("Left handed");
        if ((options & 2) != 0) labels.emplace_back("Static lights");
        if ((options & 4) != 0) labels.emplace_back("No HUD");
        if ((options & 8) != 0) labels.emplace_back("Advanced HUD");
        if ((options & 16) != 0) labels.emplace_back("Reduce debris");
        if ((options & 256) != 0) labels.emplace_back("Hide note spawn effect");
        if (labels.empty()) return "Player options: personal defaults";

        std::string result = "Player options: ";
        for (size_t i = 0; i < labels.size(); ++i) {
            if (i > 0) result += ", ";
            result += labels[i];
        }
        return result;
    }

    std::string tournamentName(TA::Tournament const& tournament) {
        if (!tournament.settings.tournamentName.empty()) return tournament.settings.tournamentName;
        if (!tournament.guid.empty()) return tournament.guid;
        return "Tournament";
    }

    std::string qualifierName(TA::QualifierEvent const& qualifier) {
        if (!qualifier.name.empty()) return qualifier.name;
        if (!qualifier.guid.empty()) return qualifier.guid;
        return "Qualifier";
    }

    std::string songName(TA::GameplayParameters const& parameters) {
        if (!parameters.beatmap.name.empty()) return parameters.beatmap.name;
        if (!parameters.beatmap.levelId.empty()) return parameters.beatmap.levelId;
        return "Song";
    }

        std::string difficultyName(int32_t difficulty) {
            switch (difficulty) {
            case 0: return "Easy";
            case 1: return "Normal";
            case 2: return "Hard";
            case 3: return "Expert";
            case 4: return "Expert+";
            case 5: return "Hard";
            case 7: return "Expert";
            case 9: return "Expert+";
            default: return std::to_string(difficulty);
            }
        }

        float defaultNjs(int32_t difficulty) {
            switch (difficulty) {
            case 0: return 10.0f;
            case 1: return 10.0f;
            case 2: return 12.0f;
            case 3: return 15.0f;
            case 4: return 19.0f;
            case 5: return 12.0f;
            case 7: return 15.0f;
            case 9: return 19.0f;
            default: return 0.0f;
            }
        }

    std::string mapLabel(TA::Map const& map) {
        auto const& parameters = map.gameplayParameters;
        auto characteristic = parameters.beatmap.characteristic.serializedName.empty() ? "Standard" : parameters.beatmap.characteristic.serializedName;
        return songName(parameters) + " - " + characteristic + " " + difficultyName(parameters.beatmap.difficulty);
    }

    std::string qualifierMapKey(std::string const& eventId, std::string const& mapId) {
        return eventId + ":" + mapId;
    }

    bool qualifierDisablesScoreSubmission(TA::QualifierEvent const& qualifier) {
        return (qualifier.flags & 2) != 0;
    }

    void clearChildren(UnityEngine::Transform* transform) {
        if (!transform) {
            PaperLogger.warn("clearChildren called with null transform");
            return;
        }
        PaperLogger.info("Clearing {} UI children", transform->get_childCount());
        for (int i = transform->get_childCount() - 1; i >= 0; --i) {
            UnityEngine::Object::Destroy(transform->GetChild(i)->get_gameObject());
        }
    }

    TMPro::TextMeshProUGUI* addTextWithWidth(
        UnityEngine::Transform* parent,
        std::string const& value,
        float size,
        float width,
        float height,
        TMPro::TextAlignmentOptions alignment = TMPro::TextAlignmentOptions::Center
    ) {
        PaperLogger.info("Adding UI text: {}", value);
        auto* text = CreateText(parent, StringW(value), size, {0.0f, 0.0f}, {width, height});
        if (!text) {
            PaperLogger.error("CreateText returned null for '{}'", value);
            return nullptr;
        }
        text->set_fontSize(size);
        text->set_alignment(alignment);
        text->set_enableWordWrapping(true);
        text->set_overflowMode(TMPro::TextOverflowModes::Overflow);
        auto* layout = text->GetComponent<UnityEngine::UI::LayoutElement*>();
        if (!layout) layout = text->get_gameObject()->AddComponent<UnityEngine::UI::LayoutElement*>();
        if (layout) {
            layout->set_preferredWidth(width);
            layout->set_minWidth(width);
            layout->set_preferredHeight(height);
            layout->set_minHeight(height);
            layout->set_flexibleWidth(0.0f);
            layout->set_flexibleHeight(0.0f);
        }
        if (auto rect = text->get_transform().cast<UnityEngine::RectTransform>()) {
            rect->set_sizeDelta({width, height});
        }
        return text;
    }

    TMPro::TextMeshProUGUI* addText(UnityEngine::Transform* parent, std::string const& value, float size = 3.6f) {
        auto height = value.size() > 48 ? 10.0f : 6.0f;
        return addTextWithWidth(parent, value, size, 96.0f, height);
    }

    TA::Tournament const* selectedTournament(TA::State const& state, std::string const& selected) {
        auto tournament = std::find_if(state.tournaments.begin(), state.tournaments.end(), [&](TA::Tournament const& item) {
            return item.guid == selected;
        });
        return tournament == state.tournaments.end() ? nullptr : &*tournament;
    }

    TA::QualifierEvent const* findQualifier(TA::Tournament const& tournament, std::string const& qualifierId) {
        auto qualifier = std::find_if(tournament.qualifiers.begin(), tournament.qualifiers.end(), [&](TA::QualifierEvent const& item) {
            return item.guid == qualifierId;
        });
        return qualifier == tournament.qualifiers.end() ? nullptr : &*qualifier;
    }

    TA::Map const* findMap(TA::QualifierEvent const& qualifier, std::string const& mapId) {
        auto map = std::find_if(qualifier.qualifierMaps.begin(), qualifier.qualifierMaps.end(), [&](TA::Map const& item) {
            return item.guid == mapId;
        });
        return map == qualifier.qualifierMaps.end() ? nullptr : &*map;
    }

    void renderQualifierEventList(UnityEngine::Transform* parent, TA::Tournament const& tournament) {
        PaperLogger.info("Rendering native qualifier event list count={}", tournament.qualifiers.size());
        if (tournament.qualifiers.empty()) {
            addText(parent, "No qualifiers available");
            return;
        }

        std::vector<std::string> ids;
        ids.reserve(tournament.qualifiers.size());
        for (auto const& qualifier : tournament.qualifiers) ids.emplace_back(qualifier.guid);

        auto* list = CreateScrollableList(parent, {0.0f, 0.0f}, {78.0f, 52.0f}, [ids, tournament](int row) {
            if (row < 0 || static_cast<size_t>(row) >= ids.size()) return;
            auto id = ids[static_cast<size_t>(row)];
            auto const* qualifier = findQualifier(tournament, id);
            PaperLogger.info("Qualifier event selected '{}'", id);
            selectedQualifierId = id;
            selectedMapId.clear();
            if (qualifier && !qualifier->qualifierMaps.empty()) selectedMapId = qualifier->qualifierMaps.front().guid;
            if (qualifier) {
                for (auto const& map : qualifier->qualifierMaps) TA::Client::instance().ensureMapDownloaded(map);
            }
            viewMode = ViewMode::QualifierMaps;
            lastRequestedMapKey.clear();
            refreshView();
        });

        if (!list) {
            addText(parent, "Qualifier list failed to render");
            return;
        }
        setLayoutSize(list->get_gameObject(), 78.0f, 52.0f);
        if (auto rect = list->get_transform().cast<UnityEngine::RectTransform>()) rect->set_sizeDelta({78.0f, 52.0f});
        list->cellSize = 10.0f;
        list->expandCell = true;
        list->listStyle = BSML::CustomListTableData::ListStyle::List;

        auto data = ListW<BSML::CustomCellInfo*>::New();
        data->EnsureCapacity(tournament.qualifiers.size());
        for (auto const& qualifier : tournament.qualifiers) {
            data->Add(BSML::CustomCellInfo::construct(
                StringW(qualifierName(qualifier)),
                StringW(std::to_string(qualifier.qualifierMaps.size()) + " maps"),
                qualifierSprite(qualifier)
            ));
        }
        list->data = data;
        if (list->tableView) {
            auto* tableView = list->tableView;
            BSML::MainThreadScheduler::Schedule([tableView] {
                if (!tableView) return;
                tableView->ReloadData();
                tableView->ClearSelection();
            });
        }
    }

    void renderTeamSelection(UnityEngine::Transform* parent, TA::Tournament const& tournament) {
        PaperLogger.info("Rendering team selection teams={}", tournament.settings.teams.size());
        addText(parent, "Select Team", 4.6f);
        if (tournament.settings.teams.empty()) {
            addText(parent, "No teams available", 3.8f);
            CreateUIButton(parent, "Continue", [] {
                PaperLogger.info("No teams available, continuing to tournament room");
                viewMode = ViewMode::Tournament;
                refreshView();
            });
            return;
        }

        std::vector<TA::Team> teams = tournament.settings.teams;
        auto* list = CreateScrollableList(parent, {0.0f, 0.0f}, {78.0f, 48.0f}, [teams](int row) {
            if (row < 0 || static_cast<size_t>(row) >= teams.size()) return;
            auto const& team = teams[static_cast<size_t>(row)];
            PaperLogger.info("Team selected guid='{}' name='{}'", team.guid, team.name);
            TA::Client::instance().setLocalTeam(team.guid);
            viewMode = ViewMode::Tournament;
            refreshView();
        });
        if (!list) {
            addText(parent, "Team list failed to render", 3.8f);
            return;
        }
        setLayoutSize(list->get_gameObject(), 78.0f, 48.0f);
        if (auto rect = list->get_transform().cast<UnityEngine::RectTransform>()) rect->set_sizeDelta({78.0f, 48.0f});
        list->cellSize = 10.0f;
        list->expandCell = true;
        list->listStyle = BSML::CustomListTableData::ListStyle::List;

        auto data = ListW<BSML::CustomCellInfo*>::New();
        data->EnsureCapacity(teams.size());
        for (auto const& team : teams) {
            auto name = team.name.empty() ? team.guid : team.name;
            data->Add(BSML::CustomCellInfo::construct(StringW(name), StringW(team.guid), nullptr));
        }
        list->data = data;
        if (list->tableView) {
            auto* tableView = list->tableView;
            BSML::MainThreadScheduler::Schedule([tableView] {
                if (!tableView) return;
                tableView->ReloadData();
                tableView->ClearSelection();
            });
        }

        CreateUIButton(parent, "Back", [] {
            PaperLogger.info("Back from team selection pressed");
            pendingTournamentId = TA::Client::instance().selectedTournamentId();
            viewMode = ViewMode::Tournaments;
            refreshView();
        });
    }

    void renderPrompt(UnityEngine::Transform* parent, TA::Prompt const& prompt) {
        PaperLogger.info("Rendering prompt title='{}' options={} canClose={}", prompt.title, prompt.options.size(), prompt.canClose);
        addText(parent, prompt.title.empty() ? "Prompt" : "<color=#ffdd88>" + prompt.title + "</color>", 4.2f);
        if (!prompt.text.empty()) addText(parent, prompt.text, 3.4f);
        for (auto const& option : prompt.options) {
            auto captured = prompt;
            auto value = option.value;
            CreateUIButton(parent, option.label.empty() ? value : option.label, [captured, value] {
                TA::Client::instance().sendPromptResponse(captured, value);
                refreshView();
            });
        }
        if (prompt.canClose) {
            auto captured = prompt;
            CreateUIButton(parent, "Close", [captured] {
                TA::Client::instance().sendPromptResponse(captured, "");
                refreshView();
            });
        }
    }

    void renderActiveSong(UnityEngine::Transform* parent) {
        auto activeSong = TA::Client::instance().activeSong();
        if (!activeSong) return;
        PaperLogger.info("Rendering active song '{}'", songName(*activeSong));
        addText(parent, "<color=#88ccff>Loaded song: " + songName(*activeSong) + "</color>", 3.8f);
    }

    void renderSongDetails(UnityEngine::Transform* parent, TA::GameplayParameters const& parameters, float width = 100.0f) {
        PaperLogger.info("Rendering song details for levelId='{}'", parameters.beatmap.levelId);
        TA::Song::requestDetails(parameters);
        auto details = TA::Song::detailsFor(parameters);

        auto characteristic = parameters.beatmap.characteristic.serializedName.empty() ? "Standard" : parameters.beatmap.characteristic.serializedName;
        auto difficulty = difficultyName(parameters.beatmap.difficulty);

        auto* row = CreateHorizontalLayoutGroup(parent);
        if (!row) {
            PaperLogger.warn("Song details row layout failed, falling back to text");
            addText(parent, "Name: " + (details.name.empty() ? songName(parameters) : details.name), 3.2f);
            return;
        }
        row->set_spacing(2.0f);
        row->set_childAlignment(UnityEngine::TextAnchor::UpperCenter);
        row->set_childControlHeight(true);
        row->set_childControlWidth(true);
        row->set_childForceExpandHeight(false);
        row->set_childForceExpandWidth(false);
        auto compact = width < 70.0f;
        auto coverSize = compact ? 14.0f : 24.0f;
        auto textWidth = std::max(24.0f, width - coverSize - 4.0f);
        setLayoutSize(row->get_gameObject(), width, 30.0f);

        bool coverRendered = renderSongCover(row->get_rectTransform(), details.coverUrl, coverSize);
        if (!coverRendered) {
            auto* placeholder = CreateVerticalLayoutGroup(row->get_rectTransform());
            if (placeholder) {
                setLayoutSize(placeholder->get_gameObject(), coverSize, coverSize);
                addTextWithWidth(placeholder->get_rectTransform(), details.coverUrl.empty() ? "Cover" : "Loading cover", compact ? 1.8f : 2.2f, coverSize, 8.0f);
            }
        }

        auto* column = CreateVerticalLayoutGroup(row->get_rectTransform());
        if (!column) return;
        column->set_spacing(0.6f);
        column->set_childAlignment(UnityEngine::TextAnchor::UpperLeft);
        column->set_childControlHeight(true);
        column->set_childControlWidth(true);
        column->set_childForceExpandHeight(false);
        column->set_childForceExpandWidth(false);
        setLayoutSize(column->get_gameObject(), textWidth, 30.0f);

        auto name = details.name.empty() ? songName(parameters) : details.name;
        addTextWithWidth(column->get_rectTransform(), name, compact ? 2.5f : 3.4f, textWidth, 5.2f, TMPro::TextAlignmentOptions::Left);
        addTextWithWidth(column->get_rectTransform(), "Author: " + (details.songAuthor.empty() ? "Unknown" : details.songAuthor), compact ? 2.1f : 2.8f, textWidth, 4.4f, TMPro::TextAlignmentOptions::Left);
        addTextWithWidth(column->get_rectTransform(), "Mapper: " + (details.mapper.empty() ? "Unknown" : details.mapper), compact ? 2.1f : 2.8f, textWidth, 4.4f, TMPro::TextAlignmentOptions::Left);
        addTextWithWidth(column->get_rectTransform(), characteristic + " - " + difficulty, compact ? 2.1f : 2.8f, textWidth, 4.4f, TMPro::TextAlignmentOptions::Left);

        auto njs = details.njs > 0.0f ? details.njs : defaultNjs(parameters.beatmap.difficulty);
        std::ostringstream statLine;
        statLine << "BPM " << (details.bpm > 0.0f ? std::to_string(int(details.bpm)) : "?")
                 << "   Duration " << (details.duration > 0 ? std::to_string(details.duration) + "s" : "?")
                 << "   NJS " << (njs > 0.0f ? std::to_string(njs) : "?");
        addTextWithWidth(column->get_rectTransform(), statLine.str(), compact ? 1.9f : 2.6f, textWidth, 4.4f, TMPro::TextAlignmentOptions::Left);

        std::ostringstream objectLine;
        objectLine << "Notes " << details.notes << "   Bombs " << details.bombs << "   Walls " << details.walls;
        addTextWithWidth(column->get_rectTransform(), objectLine.str(), compact ? 1.9f : 2.6f, textWidth, 4.4f, TMPro::TextAlignmentOptions::Left);

        if (!details.loaded) {
            addTextWithWidth(column->get_rectTransform(), "Song details loading...", compact ? 1.9f : 2.4f, textWidth, 4.4f, TMPro::TextAlignmentOptions::Left);
            BSML::MainThreadScheduler::ScheduleAfterTime(1.5f, [] { refreshView(); });
        }
    }

    void renderLeaderboard(UnityEngine::Transform* parent, TA::QualifierEvent const& qualifier, TA::Map const& map, float width = 96.0f) {
        auto scores = TA::Client::instance().leaderboard(qualifier.guid, map.guid);
        PaperLogger.info("Rendering leaderboard qualifier='{}' map='{}' scores={}", qualifier.guid, map.guid, scores.size());
        std::sort(scores.begin(), scores.end(), [&](TA::LeaderboardEntry const& left, TA::LeaderboardEntry const& right) {
            bool ascending = qualifier.sort == TA::QualifierLeaderboardSort::ModifiedScoreAscending ||
                             qualifier.sort == TA::QualifierLeaderboardSort::NotesMissedAscending ||
                             qualifier.sort == TA::QualifierLeaderboardSort::BadCutsAscending ||
                             qualifier.sort == TA::QualifierLeaderboardSort::MaxComboAscending ||
                             qualifier.sort == TA::QualifierLeaderboardSort::GoodCutsAscending;
            return ascending ? left.modifiedScore < right.modifiedScore : left.modifiedScore > right.modifiedScore;
        });

        addTextWithWidth(parent, "Leaderboard", width < 40.0f ? 2.7f : 4.0f, width, 5.5f);
        if (scores.empty()) {
            addTextWithWidth(parent, "No scores yet", width < 40.0f ? 2.2f : 3.3f, width, 5.0f);
            return;
        }

        int row = 1;
        for (auto const& score : scores) {
            if (row > 10) break;
            std::ostringstream line;
            line << row << ". " << (score.username.empty() ? "Player" : score.username) << " - " << score.modifiedScore;
            if (score.isPlaceholder) line << " (playing)";
            addTextWithWidth(parent, line.str(), width < 40.0f ? 2.0f : 3.2f, width, 4.6f);
            ++row;
        }
    }

    void setActive(UnityEngine::GameObject* object, bool active) {
        if (object) object->SetActive(active);
    }

    void setGlobalBackButtonInteractivity(bool enable) {
        auto systems = UnityEngine::Resources::FindObjectsOfTypeAll<HMUI::ScreenSystem*>();
        PaperLogger.info("Setting global back button interactivity={} screenSystems={}", enable, systems.size());
        for (auto* system : systems) {
            if (!system) continue;
            auto button = system->__cordl_internal_get__backButton();
            if (button) button->set_interactable(enable);
        }
    }

    void syncGlobalBackButtonFromClient() {
        auto hasMatch = TA::Client::instance().currentMatch().has_value();
        PaperLogger.info("Syncing global back button from client hasMatch={}", hasMatch);
        setGlobalBackButtonInteractivity(!hasMatch);
    }

    void resumePreservedControllerWhenBackInMenu(TA::TournamentViewController* controller) {
        auto& client = TA::Client::instance();
        if (!client.inTournament() || client.activeSong().has_value()) return;
        if (pendingPreservedResumeRefresh) {
            PaperLogger.info("Preserved controller resume refresh is already pending");
            return;
        }

        pendingPreservedResumeRefresh = true;
        PaperLogger.info("Scheduling preserved TournamentAssistant controller resume refresh");
        BSML::MainThreadScheduler::ScheduleAfterTime(0.75f, [controller] {
            pendingPreservedResumeRefresh = false;
            if (activeController != controller) {
                PaperLogger.warn("Skipping preserved resume refresh for stale controller={}", static_cast<void*>(controller));
                return;
            }
            if (!TA::Client::instance().inTournament() || TA::Client::instance().activeSong().has_value()) {
                PaperLogger.info("Skipping preserved resume refresh because client is no longer in a menu tournament state");
                syncGlobalBackButtonFromClient();
                return;
            }

            PaperLogger.info("Resuming preserved TournamentAssistant controller after gameplay/menu return");
            controllerInHierarchy = true;
            syncGlobalBackButtonFromClient();
            controller->Refresh();
        });
    }

    void sizeScrollable(UnityEngine::GameObject* scroll, float width, float height, UnityEngine::Vector2 anchoredPosition) {
        if (!scroll) return;
        PaperLogger.info("Sizing scroll view width={} height={} x={} y={}", width, height, anchoredPosition.x, anchoredPosition.y);
        if (auto scrollRect = scroll->get_transform().cast<UnityEngine::RectTransform>()) {
            scrollRect->set_anchorMin({0.5f, 0.5f});
            scrollRect->set_anchorMax({0.5f, 0.5f});
            scrollRect->set_pivot({0.5f, 0.5f});
            scrollRect->set_sizeDelta({width, height});
            scrollRect->set_anchoredPosition(anchoredPosition);
        }
        if (auto* scrollLayoutElement = scroll->GetComponent<UnityEngine::UI::LayoutElement*>()) {
            scrollLayoutElement->set_preferredWidth(width);
            scrollLayoutElement->set_preferredHeight(height);
            scrollLayoutElement->set_minWidth(width);
            scrollLayoutElement->set_minHeight(height);
        }
    }

    void showLoadingChrome(TMPro::TextMeshProUGUI* title, TMPro::TextMeshProUGUI* status, UnityEngine::UI::Button* reconnect, UnityEngine::GameObject* detail) {
        setActive(detail, true);
        setActive(title ? title->get_gameObject() : nullptr, true);
        setActive(status ? status->get_gameObject() : nullptr, true);
        setActive(reconnect ? reconnect->get_gameObject() : nullptr, true);
    }

    void showContentOnly(TMPro::TextMeshProUGUI* title, TMPro::TextMeshProUGUI* status, UnityEngine::UI::Button* reconnect, UnityEngine::GameObject* detail) {
        setActive(detail, true);
        setActive(title ? title->get_gameObject() : nullptr, false);
        setActive(status ? status->get_gameObject() : nullptr, false);
        setActive(reconnect ? reconnect->get_gameObject() : nullptr, false);
    }
}

void TA::TournamentViewController::DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling) {
    PaperLogger.info(
        "TournamentViewController::DidActivate firstActivation={} addedToHierarchy={} screenSystemEnabling={} this={}",
        firstActivation,
        addedToHierarchy,
        screenSystemEnabling,
        static_cast<void*>(this)
    );
    if (!addedToHierarchy) {
        PaperLogger.info("DidActivate ignored because controller was not added to hierarchy");
        return;
    }
    controllerInHierarchy = true;
    pendingPreservedResumeRefresh = false;

    if (!firstActivation) {
        PaperLogger.info("Reactivating existing TournamentAssistant controller");
        TA::Client::instance().setUiCallback([this] {
            PaperLogger.info(
                "Client UI callback entered for reactivated controller={} inHierarchy={}",
                static_cast<void*>(this),
                controllerInHierarchy
            );
            if (activeController != this) {
                PaperLogger.warn("Ignoring stale UI callback for inactive controller={}", static_cast<void*>(this));
                return;
            }
            if (!controllerInHierarchy) {
                PaperLogger.warn("TournamentAssistant controller is not in hierarchy; syncing global state without immediate render");
                syncGlobalBackButtonFromClient();
                resumePreservedControllerWhenBackInMenu(this);
                return;
            }
            Refresh();
        });
        activeController = this;
        auto& client = TA::Client::instance();
        if (client.inTournament() || client.currentMatch().has_value() || client.activeSong().has_value()) {
            PaperLogger.info("Preserving existing TournamentAssistant session on reactivation");
        } else {
            resetUiState();
            client.disconnect();
            client.connect();
        }
        Refresh();
        return;
    }

    this->get_gameObject()->AddComponent<HMUI::Touchable*>();

    PaperLogger.info("Creating root layout");
    rootLayout = CreateVerticalLayoutGroup(rectTransform);
    if (!rootLayout) {
        PaperLogger.error("CreateVerticalLayoutGroup(root) returned null");
        return;
    }
    rootLayout->set_spacing(2.0f);
    rootLayout->set_padding(UnityEngine::RectOffset::New_ctor(4, 4, 3, 3));
    rootLayout->set_childAlignment(UnityEngine::TextAnchor::UpperCenter);
    rootLayout->set_childControlHeight(true);
    rootLayout->set_childControlWidth(true);
    rootLayout->set_childForceExpandHeight(false);
    rootLayout->set_childForceExpandWidth(false);

    PaperLogger.info("Creating title text");
    titleText = CreateText(rootLayout->get_rectTransform(), "TournamentAssistant");
    if (!titleText) {
        PaperLogger.error("CreateText(title) returned null");
        return;
    }
    titleText->set_fontSize(6.0f);
    titleText->set_alignment(TMPro::TextAlignmentOptions::Center);

    PaperLogger.info("Creating status text");
    statusText = CreateText(rootLayout->get_rectTransform(), "");
    if (!statusText) {
        PaperLogger.error("CreateText(status) returned null");
        return;
    }
    statusText->set_fontSize(3.6f);
    statusText->set_alignment(TMPro::TextAlignmentOptions::Center);
    if (auto* statusLayout = statusText->GetComponent<UnityEngine::UI::LayoutElement*>()) {
        statusLayout->set_preferredHeight(8.0f);
    } else {
        PaperLogger.warn("Status text has no LayoutElement");
    }

    PaperLogger.info("Creating reconnect button");
    reconnectButton = CreateUIButton(rootLayout->get_rectTransform(), "Reconnect", [] {
        PaperLogger.info("Reconnect button pressed");
        TA::Client::instance().disconnect();
        TA::Client::instance().connect();
    });

    PaperLogger.info("Creating wide content layout");
    listLayout = CreateVerticalLayoutGroup(rectTransform);
    if (!listLayout) {
        PaperLogger.error("CreateVerticalLayoutGroup(content) returned null");
        return;
    }
    detailScroll = listLayout->get_gameObject();
    detailScroll->SetActive(false);
    sizeScrollable(detailScroll, 112.0f, 60.0f, {0.0f, -4.0f});
    listLayout->set_spacing(1.2f);
    listLayout->set_childAlignment(UnityEngine::TextAnchor::UpperCenter);
    listLayout->set_childForceExpandHeight(false);
    listLayout->set_childControlHeight(true);
    listLayout->set_childForceExpandWidth(false);
    listLayout->set_childControlWidth(true);
    setLayoutSize(listLayout->get_gameObject(), 108.0f, 60.0f);

    TA::Client::instance().setUiCallback([this] {
        PaperLogger.info(
            "Client UI callback entered for controller={} inHierarchy={}",
            static_cast<void*>(this),
            controllerInHierarchy
        );
        if (activeController != this) {
            PaperLogger.warn("Ignoring stale UI callback for inactive controller={}", static_cast<void*>(this));
            return;
        }
        if (!controllerInHierarchy) {
            PaperLogger.warn("TournamentAssistant controller is not in hierarchy; syncing global state without immediate render");
            syncGlobalBackButtonFromClient();
            resumePreservedControllerWhenBackInMenu(this);
            return;
        }
        Refresh();
    });
    activeController = this;
    PaperLogger.info("Active controller assigned, connecting client");
    TA::Client::instance().connect();
    PaperLogger.info("Initial UI refresh");
    Refresh();
}

void TA::TournamentViewController::DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling) {
    PaperLogger.info(
        "TournamentViewController::DidDeactivate removedFromHierarchy={} screenSystemDisabling={} this={}",
        removedFromHierarchy,
        screenSystemDisabling,
        static_cast<void*>(this)
    );
    controllerInHierarchy = false;
    auto& client = TA::Client::instance();
    auto preserveSession = client.activeSong().has_value() || client.currentMatch().has_value() || (screenSystemDisabling && client.inTournament());
    if (preserveSession) {
        PaperLogger.info(
            "TournamentAssistant deactivated during active tournament/gameplay; preserving socket and state activeSong={} currentMatch={} inTournament={}",
            client.activeSong().has_value(),
            client.currentMatch().has_value(),
            client.inTournament()
        );
        return;
    }

    if (removedFromHierarchy) {
        TA::Client::instance().setUiCallback(nullptr);
        if (activeController == this) activeController = nullptr;
        setGlobalBackButtonInteractivity(true);
        clearRoomSidePanel();
        resetUiState();
        TA::Client::instance().disconnect();
    } else if (activeController == this) {
        PaperLogger.info("TournamentAssistant deactivated without removal; disconnecting for fresh reopen");
        TA::Client::instance().setUiCallback(nullptr);
        activeController = nullptr;
        setGlobalBackButtonInteractivity(true);
        clearRoomSidePanel();
        resetUiState();
        TA::Client::instance().disconnect();
    }
}

void TA::TournamentViewController::Refresh() {
    PaperLogger.info(
        "Refresh entered statusText={} listLayout={} viewMode={}",
        static_cast<void*>(statusText),
        static_cast<void*>(listLayout),
        static_cast<int>(viewMode)
    );
    if (!statusText || !listLayout) {
        PaperLogger.warn("Refresh aborted because UI fields are not initialized");
        return;
    }

    auto& client = TA::Client::instance();
    auto status = client.status();
    PaperLogger.info("Refresh status='{}'", status);
    statusText->set_text(status);

    clearChildren(listLayout->get_transform());

    if (auto prompt = client.activePrompt()) {
        PaperLogger.info("Refresh rendering active prompt");
        renderPrompt(listLayout->get_rectTransform(), *prompt);
        addText(listLayout->get_rectTransform(), " ", 1.0f);
    }

    if (client.inTournament()) {
        showContentOnly(titleText, statusText, reconnectButton, detailScroll);
        joiningTournamentId.clear();
        auto state = client.state();
        auto selected = client.selectedTournamentId();
        auto const* tournament = selectedTournament(state, selected);
        PaperLogger.info("Refresh in tournament selected='{}' found={} tournaments={}", selected, tournament != nullptr, state.tournaments.size());

        renderActiveSong(listLayout->get_rectTransform());

        if (!tournament) {
            addText(listLayout->get_rectTransform(), "Waiting for tournament state");
            return;
        }

        if (auto match = client.currentMatch()) {
            setGlobalBackButtonInteractivity(false);
            if (viewMode != ViewMode::Tournament) {
                PaperLogger.info("Forcing tournament view because an active match exists");
                viewMode = ViewMode::Tournament;
                selectedQualifierId.clear();
                selectedMapId.clear();
            }
        } else if (viewMode == ViewMode::Tournaments) {
            setGlobalBackButtonInteractivity(true);
            viewMode = postJoinMode;
            if (viewMode == ViewMode::TeamSelection && (!tournament->settings.enableTeams || tournament->settings.teams.empty())) viewMode = ViewMode::Tournament;
            if (viewMode == ViewMode::Qualifiers && !tournament->settings.showQualifierButton) viewMode = ViewMode::Tournament;
        } else {
            setGlobalBackButtonInteractivity(true);
        }

        if (viewMode == ViewMode::TeamSelection) {
            if (!tournament->settings.enableTeams || tournament->settings.teams.empty()) {
                PaperLogger.info("Team selection requested but no teams are available; falling back to room");
                viewMode = ViewMode::Tournament;
            } else {
                renderTeamSelection(listLayout->get_rectTransform(), *tournament);
                return;
            }
        }

        if (viewMode == ViewMode::Tournament) {
            auto match = client.currentMatch();
            auto selectedMap = client.selectedMatchMap();
            PaperLogger.info("Rendering tournament view match={} selectedMap={}", match.has_value(), selectedMap.has_value());
            if (match.has_value() && selectedMap.has_value()) {
                showRoomSidePanel(this, *selectedMap);
            } else {
                clearRoomSidePanel();
            }
            TA::UI::RoomViewController::Render(
                listLayout->get_rectTransform(),
                match,
                selectedMap,
                [](UnityEngine::Transform* parent, std::string const& value, float size) {
                    addTextWithWidth(parent, value, size, 104.0f, value.size() > 44 ? 8.0f : 5.8f);
                },
                [](TA::Map const& map) {
                    return mapLabel(map);
                },
                [](UnityEngine::Transform* parent, TA::GameplayParameters const& parameters) {
                    renderSongDetails(parent, parameters);
                },
                [](TA::GameplayParameters const& parameters) {
                    return modifiersLabel(parameters);
                },
                [](TA::Map const& map) {
                    TA::Client::instance().ensureMapDownloaded(map);
                }
            );
            return;
        }

        if (viewMode == ViewMode::Qualifiers) {
            clearRoomSidePanel();
            PaperLogger.info("Rendering qualifiers view count={}", tournament->qualifiers.size());
            CreateUIButton(listLayout->get_rectTransform(), "Back", [] {
                PaperLogger.info("Back to tournament mode selection pressed");
                pendingTournamentId = TA::Client::instance().selectedTournamentId();
                viewMode = ViewMode::Tournaments;
                refreshView();
            });
            renderQualifierEventList(listLayout->get_rectTransform(), *tournament);
            return;
        }

        auto const* qualifier = findQualifier(*tournament, selectedQualifierId);
        clearRoomSidePanel();
        if (!qualifier) {
            PaperLogger.warn("Selected qualifier '{}' no longer exists", selectedQualifierId);
            viewMode = ViewMode::Qualifiers;
            Refresh();
            return;
        }

        if (viewMode == ViewMode::QualifierMaps) {
            PaperLogger.info("Rendering qualifier maps for '{}' count={}", qualifier->guid, qualifier->qualifierMaps.size());
            CreateUIButton(listLayout->get_rectTransform(), "Back", [] {
                PaperLogger.info("Back to qualifiers pressed");
                viewMode = ViewMode::Qualifiers;
                refreshView();
            });
            addText(listLayout->get_rectTransform(), qualifierName(*qualifier), 4.2f);
            if (qualifier->qualifierMaps.empty()) {
                addText(listLayout->get_rectTransform(), "No qualifier maps available");
                return;
            }

            if (selectedMapId.empty()) selectedMapId = qualifier->qualifierMaps.front().guid;

            auto* row = CreateHorizontalLayoutGroup(listLayout->get_rectTransform());
            if (!row) return;
            row->set_spacing(2.0f);
            row->set_childAlignment(UnityEngine::TextAnchor::UpperCenter);
            row->set_childControlHeight(true);
            row->set_childControlWidth(true);
            row->set_childForceExpandHeight(false);
            row->set_childForceExpandWidth(false);
            setLayoutSize(row->get_gameObject(), 110.0f, 50.0f);

            auto* mapColumn = CreateVerticalLayoutGroup(row->get_rectTransform());
            auto* detailColumn = CreateVerticalLayoutGroup(row->get_rectTransform());
            auto* leaderboardColumn = CreateVerticalLayoutGroup(row->get_rectTransform());
            if (!mapColumn || !detailColumn || !leaderboardColumn) return;
            mapColumn->set_spacing(0.8f);
            mapColumn->set_childAlignment(UnityEngine::TextAnchor::UpperCenter);
            mapColumn->set_childControlHeight(true);
            mapColumn->set_childControlWidth(true);
            mapColumn->set_childForceExpandHeight(false);
            mapColumn->set_childForceExpandWidth(false);
            setLayoutSize(mapColumn->get_gameObject(), 32.0f, 50.0f);
            detailColumn->set_spacing(0.8f);
            detailColumn->set_childAlignment(UnityEngine::TextAnchor::UpperCenter);
            detailColumn->set_childControlHeight(true);
            detailColumn->set_childControlWidth(true);
            detailColumn->set_childForceExpandHeight(false);
            detailColumn->set_childForceExpandWidth(false);
            setLayoutSize(detailColumn->get_gameObject(), 50.0f, 50.0f);
            leaderboardColumn->set_spacing(0.6f);
            leaderboardColumn->set_childAlignment(UnityEngine::TextAnchor::UpperCenter);
            leaderboardColumn->set_childControlHeight(true);
            leaderboardColumn->set_childControlWidth(true);
            leaderboardColumn->set_childForceExpandHeight(false);
            leaderboardColumn->set_childForceExpandWidth(false);
            setLayoutSize(leaderboardColumn->get_gameObject(), 28.0f, 50.0f);

            std::vector<std::string> mapIds;
            mapIds.reserve(qualifier->qualifierMaps.size());
            for (auto const& map : qualifier->qualifierMaps) mapIds.emplace_back(map.guid);

            auto* mapList = CreateScrollableList(mapColumn->get_rectTransform(), {0.0f, 0.0f}, {32.0f, 48.0f}, [selected, eventId = qualifier->guid, mapIds](int row) {
                if (row < 0 || static_cast<size_t>(row) >= mapIds.size()) return;
                auto mapId = mapIds[static_cast<size_t>(row)];
                PaperLogger.info("Qualifier map selected tournament='{}' event='{}' map='{}'", selected, eventId, mapId);
                selectedMapId = mapId;
                lastRequestedMapKey.clear();
                TA::Client::instance().requestLeaderboard(selected, eventId, mapId);
                TA::Client::instance().requestRemainingAttempts(selected, eventId, mapId);
                refreshView();
            });
            if (!mapList) {
                addTextWithWidth(mapColumn->get_rectTransform(), "Map list failed", 2.4f, 30.0f, 6.0f);
            } else {
                setLayoutSize(mapList->get_gameObject(), 32.0f, 48.0f);
                if (auto rect = mapList->get_transform().cast<UnityEngine::RectTransform>()) rect->set_sizeDelta({32.0f, 48.0f});
                mapList->cellSize = 9.0f;
                mapList->expandCell = true;
                mapList->listStyle = BSML::CustomListTableData::ListStyle::List;

                auto data = ListW<BSML::CustomCellInfo*>::New();
                data->EnsureCapacity(qualifier->qualifierMaps.size());
                for (auto const& map : qualifier->qualifierMaps) {
                    TA::Song::requestDetails(map.gameplayParameters);
                    auto details = TA::Song::detailsFor(map.gameplayParameters);
                    auto title = details.name.empty() ? songName(map.gameplayParameters) : details.name;
                    data->Add(BSML::CustomCellInfo::construct(
                        StringW(title),
                        StringW(map.gameplayParameters.beatmap.characteristic.serializedName + " - " + difficultyName(map.gameplayParameters.beatmap.difficulty)),
                        mapSprite(map)
                    ));
                }
                mapList->data = data;
                if (mapList->tableView) {
                    auto* tableView = mapList->tableView;
                    BSML::MainThreadScheduler::Schedule([tableView] {
                        if (!tableView) return;
                        tableView->ReloadData();
                        tableView->ClearSelection();
                    });
                }
            }

            auto const* selectedQualifierMap = findMap(*qualifier, selectedMapId);
            if (!selectedQualifierMap) selectedQualifierMap = &qualifier->qualifierMaps.front();
            auto key = qualifierMapKey(qualifier->guid, selectedQualifierMap->guid);
            if (lastRequestedMapKey != key) {
                lastRequestedMapKey = key;
                client.requestLeaderboard(selected, qualifier->guid, selectedQualifierMap->guid);
                client.requestRemainingAttempts(selected, qualifier->guid, selectedQualifierMap->guid);
            }
            auto displayParameters = selectedQualifierMap->gameplayParameters;
            if (qualifierDisablesScoreSubmission(*qualifier)) displayParameters.disableScoresaberSubmission = true;
            renderSongDetails(detailColumn->get_rectTransform(), displayParameters, 50.0f);
            auto remaining = client.remainingAttempts(qualifier->guid, selectedQualifierMap->guid);
            if (selectedQualifierMap->gameplayParameters.attempts > 0) {
                addTextWithWidth(detailColumn->get_rectTransform(), remaining < 0 ? "Remaining attempts: loading" : "Remaining attempts: " + std::to_string(remaining), 3.7f, 48.0f, 7.0f);
            } else {
                addTextWithWidth(detailColumn->get_rectTransform(), "Attempts: unlimited", 3.7f, 48.0f, 7.0f);
            }

            auto capturedPracticeMap = *selectedQualifierMap;
            if (qualifierDisablesScoreSubmission(*qualifier)) capturedPracticeMap.gameplayParameters.disableScoresaberSubmission = true;
            auto* practice = CreateUIButton(detailColumn->get_rectTransform(), "Practice", [capturedPracticeMap] {
                PaperLogger.info("Qualifier practice pressed map='{}'", capturedPracticeMap.guid);
                TA::Client::instance().practiceQualifierMap(capturedPracticeMap);
            });
            sizeButton(practice, 30.0f, 6.5f, 3.0f);

            if (remaining != 0) {
                auto tournamentId = selected;
                auto eventId = qualifier->guid;
                auto capturedMap = *selectedQualifierMap;
                if (qualifierDisablesScoreSubmission(*qualifier)) capturedMap.gameplayParameters.disableScoresaberSubmission = true;
                auto* play = CreateUIButton(detailColumn->get_rectTransform(), "Play", [tournamentId, eventId, capturedMap] {
                    PaperLogger.info("Qualifier play pressed tournament='{}' event='{}' map='{}'", tournamentId, eventId, capturedMap.guid);
                    TA::Client::instance().playQualifierMap(tournamentId, eventId, capturedMap);
                    refreshView();
                });
                sizeButton(play, 34.0f, 7.0f, 3.2f);
            }
            renderLeaderboard(leaderboardColumn->get_rectTransform(), *qualifier, *selectedQualifierMap, 28.0f);
            return;
        }

        auto const* map = findMap(*qualifier, selectedMapId);
        if (!map) {
            PaperLogger.warn("Selected map '{}' no longer exists", selectedMapId);
            viewMode = ViewMode::QualifierMaps;
            Refresh();
            return;
        }

        CreateUIButton(listLayout->get_rectTransform(), "Back", [] {
            PaperLogger.info("Back to qualifier maps pressed");
            viewMode = ViewMode::QualifierMaps;
            refreshView();
        });
        addText(listLayout->get_rectTransform(), mapLabel(*map), 4.0f);
        addText(listLayout->get_rectTransform(), map->gameplayParameters.beatmap.levelId, 3.0f);

        auto key = qualifierMapKey(qualifier->guid, map->guid);
        if (lastRequestedMapKey != key) {
            lastRequestedMapKey = key;
            client.requestLeaderboard(selected, qualifier->guid, map->guid);
            client.requestRemainingAttempts(selected, qualifier->guid, map->guid);
        }

        auto remaining = client.remainingAttempts(qualifier->guid, map->guid);
        if (map->gameplayParameters.attempts > 0) {
            addText(listLayout->get_rectTransform(), remaining < 0 ? "Remaining attempts: loading" : "Remaining attempts: " + std::to_string(remaining), 3.5f);
        } else {
            addText(listLayout->get_rectTransform(), "Attempts: unlimited", 3.5f);
        }

        if (remaining != 0) {
            auto tournamentId = selected;
            auto eventId = qualifier->guid;
            auto capturedMap = *map;
            if (qualifierDisablesScoreSubmission(*qualifier)) capturedMap.gameplayParameters.disableScoresaberSubmission = true;
            CreateUIButton(listLayout->get_rectTransform(), "Play", [tournamentId, eventId, capturedMap] {
                PaperLogger.info("Qualifier play pressed tournament='{}' event='{}' map='{}'", tournamentId, eventId, capturedMap.guid);
                TA::Client::instance().playQualifierMap(tournamentId, eventId, capturedMap);
                refreshView();
            });
        }

        auto tournamentId = selected;
        auto eventId = qualifier->guid;
        auto mapId = map->guid;
        CreateUIButton(listLayout->get_rectTransform(), "Refresh Scores", [tournamentId, eventId, mapId] {
            PaperLogger.info("Refresh scores pressed tournament='{}' event='{}' map='{}'", tournamentId, eventId, mapId);
            TA::Client::instance().requestLeaderboard(tournamentId, eventId, mapId);
            TA::Client::instance().requestRemainingAttempts(tournamentId, eventId, mapId);
        });
        renderLeaderboard(listLayout->get_rectTransform(), *qualifier, *map);
        return;
    }

    viewMode = ViewMode::Tournaments;
    setGlobalBackButtonInteractivity(true);
    clearRoomSidePanel();

    auto state = client.state();
    PaperLogger.info("Rendering tournament list count={} pending='{}' connected={}", state.tournaments.size(), pendingTournamentId, client.connected());
    if (state.tournaments.empty()) {
        if (client.connected()) {
            showContentOnly(titleText, statusText, reconnectButton, detailScroll);
            addText(listLayout->get_rectTransform(), "No tournaments available", 4.0f);
        } else {
            showLoadingChrome(titleText, statusText, reconnectButton, detailScroll);
            addText(listLayout->get_rectTransform(), "Connecting...", 4.0f);
        }
        return;
    }

    if (!joiningTournamentId.empty()) {
        showContentOnly(titleText, statusText, reconnectButton, detailScroll);
        addText(listLayout->get_rectTransform(), "Joining tournament...", 4.2f);
        addText(listLayout->get_rectTransform(), status, 3.2f);
        if (status != "Joining tournament...") {
            CreateUIButton(listLayout->get_rectTransform(), "Back", [] {
                PaperLogger.info("Back from joining state pressed");
                joiningTournamentId.clear();
                refreshView();
            });
        }
        return;
    }

    if (!pendingTournamentId.empty()) {
        auto const* tournament = selectedTournament(state, pendingTournamentId);
        if (!tournament) {
            pendingTournamentId.clear();
        } else {
            showContentOnly(titleText, statusText, reconnectButton, detailScroll);
            auto id = tournament->guid;
            TA::UI::TournamentModeViewController::Render(
                listLayout->get_rectTransform(),
                *tournament,
                [](UnityEngine::Transform* parent, TA::Tournament const& item, float size) {
                    return renderTournamentImage(parent, item, size);
                },
                [id] {
                    PaperLogger.info("Tournament join button pressed '{}'", id);
                    auto state = TA::Client::instance().state();
                    auto const* tournament = selectedTournament(state, id);
                    postJoinMode = tournament && tournament->settings.enableTeams && !tournament->settings.teams.empty()
                        ? ViewMode::TeamSelection
                        : ViewMode::Tournament;
                    joiningTournamentId = id;
                    TA::Client::instance().joinTournament(id);
                    refreshView();
                },
                [id] {
                    PaperLogger.info("Qualifier join button pressed '{}'", id);
                    postJoinMode = ViewMode::Qualifiers;
                    joiningTournamentId = id;
                    TA::Client::instance().joinTournament(id);
                    refreshView();
                },
                [] {
                    PaperLogger.info("Back to tournament list pressed");
                    pendingTournamentId.clear();
                    refreshView();
                }
            );
            return;
        }
    }

    showContentOnly(titleText, statusText, reconnectButton, detailScroll);
    TA::UI::TournamentListViewController::Render(
        listLayout->get_rectTransform(),
        state.tournaments,
        [](TA::Tournament const& item) {
            return tournamentSprite(item);
        },
        [](std::string const& id) {
            pendingTournamentId = id;
            refreshView();
        }
    );
}
