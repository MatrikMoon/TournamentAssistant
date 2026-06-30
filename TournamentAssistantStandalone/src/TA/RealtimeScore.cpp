#include "TA/RealtimeScore.hpp"

#include "TA/Client.hpp"
#include "main.hpp"

#include "GlobalNamespace/AudioTimeSyncController.hpp"
#include "GlobalNamespace/BadCutScoringElement.hpp"
#include "GlobalNamespace/ComboController.hpp"
#include "GlobalNamespace/ColorType.hpp"
#include "GlobalNamespace/GameEnergyCounter.hpp"
#include "GlobalNamespace/MissScoringElement.hpp"
#include "GlobalNamespace/NoteData.hpp"
#include "GlobalNamespace/ObstacleController.hpp"
#include "GlobalNamespace/PlayerHeadAndObstacleInteraction.hpp"
#include "GlobalNamespace/ScoreController.hpp"
#include "GlobalNamespace/ScoringElement.hpp"
#include "System/Action_1.hpp"
#include "UnityEngine/Resources.hpp"

#include "beatsaber-hook/shared/utils/il2cpp-utils.hpp"
#include "custom-types/shared/delegate.hpp"

#include <algorithm>

namespace TA::RealtimeScoreHooks {
    bool installed = false;
    int frameCounter = 0;
    TA::RealtimeScore lastScore;
    int32_t notesMissed = 0;
    int32_t badCuts = 0;
    int32_t bombHits = 0;
    int32_t wallHits = 0;
    System::Action_1<GlobalNamespace::ScoringElement*>* scoringFinishedDelegate = nullptr;
    System::Action_1<UnityW<GlobalNamespace::ObstacleController>>* obstacleDelegate = nullptr;

    template <typename T>
    T* firstResource() {
        auto items = UnityEngine::Resources::FindObjectsOfTypeAll<T*>();
        return items.size() > 0 ? items[0] : nullptr;
    }

    bool changed(TA::RealtimeScore const& left, TA::RealtimeScore const& right) {
        return left.score != right.score ||
               left.notesMissed != right.notesMissed ||
               left.badCuts != right.badCuts ||
               left.bombHits != right.bombHits ||
               left.wallHits != right.wallHits ||
               left.maxCombo != right.maxCombo;
    }

    void resetCounters() {
        PaperLogger.info("Resetting realtime score counters");
        frameCounter = 0;
        lastScore = TA::RealtimeScore{};
        notesMissed = 0;
        badCuts = 0;
        bombHits = 0;
        wallHits = 0;
    }

    bool isBomb(GlobalNamespace::ScoringElement* scoringElement) {
        if (!scoringElement || !scoringElement->noteData) return false;
        auto* noteData = scoringElement->noteData;
        return noteData->get_gameplayType() == GlobalNamespace::NoteData_GameplayType::Bomb ||
               noteData->get_colorType() == GlobalNamespace::ColorType::None;
    }

    void onScoringFinished(GlobalNamespace::ScoringElement* scoringElement) {
        if (!scoringElement) return;
        auto bomb = isBomb(scoringElement);
        if (il2cpp_utils::try_cast<GlobalNamespace::MissScoringElement>(scoringElement).has_value()) {
            if (!bomb) ++notesMissed;
        } else if (il2cpp_utils::try_cast<GlobalNamespace::BadCutScoringElement>(scoringElement).has_value()) {
            if (bomb) ++bombHits;
            else ++badCuts;
        }
        PaperLogger.info("Realtime scoring counters miss={} badCuts={} bombs={} walls={}", notesMissed, badCuts, bombHits, wallHits);
    }

    void onObstacleEntered(UnityW<GlobalNamespace::ObstacleController>) {
        ++wallHits;
        PaperLogger.info("Realtime wall hit count={}", wallHits);
    }
}

MAKE_HOOK_MATCH(TA_ScoreController_Start, &GlobalNamespace::ScoreController::Start, void, GlobalNamespace::ScoreController* self) {
    TA_ScoreController_Start(self);
    TA::RealtimeScoreHooks::resetCounters();
    if (!self) return;

    TA::RealtimeScoreHooks::scoringFinishedDelegate = custom_types::MakeDelegate<
        System::Action_1<GlobalNamespace::ScoringElement*>*
    >((std::function<void(GlobalNamespace::ScoringElement*)>)TA::RealtimeScoreHooks::onScoringFinished);
    self->add_scoringForNoteFinishedEvent(TA::RealtimeScoreHooks::scoringFinishedDelegate);

    auto obstacleInteraction = self->__cordl_internal_get__playerHeadAndObstacleInteraction();
    if (obstacleInteraction) {
        TA::RealtimeScoreHooks::obstacleDelegate = custom_types::MakeDelegate<
            System::Action_1<UnityW<GlobalNamespace::ObstacleController>>*
        >((std::function<void(UnityW<GlobalNamespace::ObstacleController>)>)TA::RealtimeScoreHooks::onObstacleEntered);
        obstacleInteraction->add_headDidEnterObstacleEvent(TA::RealtimeScoreHooks::obstacleDelegate);
    } else {
        PaperLogger.warn("ScoreController has no PlayerHeadAndObstacleInteraction for wall hit tracking");
    }
}

MAKE_HOOK_MATCH(TA_ScoreController_LateUpdate, &GlobalNamespace::ScoreController::LateUpdate, void, GlobalNamespace::ScoreController* self) {
    TA_ScoreController_LateUpdate(self);
    if (!TA::Client::instance().activeSong()) return;

    auto frequency = std::max(1, TA::Client::instance().scoreUpdateFrequency());
    if (++TA::RealtimeScoreHooks::frameCounter < frequency) return;
    TA::RealtimeScoreHooks::frameCounter = 0;

    auto* comboController = TA::RealtimeScoreHooks::firstResource<GlobalNamespace::ComboController>();
    auto* energyCounter = TA::RealtimeScoreHooks::firstResource<GlobalNamespace::GameEnergyCounter>();
    auto* audioTime = TA::RealtimeScoreHooks::firstResource<GlobalNamespace::AudioTimeSyncController>();

    TA::RealtimeScore score;
    score.userGuid = TA::Client::instance().selfGuid();
    score.score = self->get_multipliedScore();
    score.scoreWithModifiers = self->get_modifiedScore();
    score.maxScore = self->get_immediateMaxPossibleMultipliedScore();
    score.maxScoreWithModifiers = self->get_immediateMaxPossibleModifiedScore();
    score.combo = comboController ? comboController->__cordl_internal_get__combo() : 0;
    score.maxCombo = score.combo > TA::RealtimeScoreHooks::lastScore.maxCombo ? score.combo : TA::RealtimeScoreHooks::lastScore.maxCombo;
    score.playerHealth = energyCounter ? energyCounter->get_energy() : 0.0f;
    score.songPosition = audioTime ? audioTime->get_songTime() : 0.0f;
    score.accuracy = score.maxScoreWithModifiers > 0 ? double(score.scoreWithModifiers) / double(score.maxScoreWithModifiers) : 0.0;
    score.notesMissed = TA::RealtimeScoreHooks::notesMissed;
    score.badCuts = TA::RealtimeScoreHooks::badCuts;
    score.bombHits = TA::RealtimeScoreHooks::bombHits;
    score.wallHits = TA::RealtimeScoreHooks::wallHits;

    if (TA::RealtimeScoreHooks::changed(score, TA::RealtimeScoreHooks::lastScore)) {
        PaperLogger.info("Realtime score changed score={} modified={} combo={} maxCombo={} miss={} badCuts={} bombs={} walls={} health={} songTime={}", score.score, score.scoreWithModifiers, score.combo, score.maxCombo, score.notesMissed, score.badCuts, score.bombHits, score.wallHits, score.playerHealth, score.songPosition);
        TA::RealtimeScoreHooks::lastScore = score;
        TA::Client::instance().sendRealtimeScore(score);
    }
}

namespace TA::RealtimeScoreHooks {
    void installHooks() {
        if (installed) return;
        installed = true;
        PaperLogger.info("Installing realtime score hook");
        INSTALL_HOOK(PaperLogger, TA_ScoreController_Start);
        INSTALL_HOOK(PaperLogger, TA_ScoreController_LateUpdate);
    }
}
