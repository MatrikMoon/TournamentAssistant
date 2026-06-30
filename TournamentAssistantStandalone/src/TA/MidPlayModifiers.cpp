#include "TA/MidPlayModifiers.hpp"

#include "main.hpp"

#include "GlobalNamespace/BeatmapObjectSpawnController.hpp"
#include "GlobalNamespace/ColorType.hpp"
#include "GlobalNamespace/GameplayCoreInstaller.hpp"
#include "GlobalNamespace/GameplayCoreSceneSetupData.hpp"
#include "GlobalNamespace/HapticFeedbackManager.hpp"
#include "GlobalNamespace/IReadonlyBeatmapData.hpp"
#include "GlobalNamespace/NoteData.hpp"
#include "GlobalNamespace/ObstacleData.hpp"
#include "GlobalNamespace/Saber.hpp"
#include "GlobalNamespace/SaberManager.hpp"
#include "GlobalNamespace/SaberModelController.hpp"
#include "GlobalNamespace/SaberType.hpp"
#include "GlobalNamespace/SaberTypeObject.hpp"
#include "GlobalNamespace/SetSaberFakeGlowColor.hpp"
#include "GlobalNamespace/SetSaberGlowColor.hpp"
#include "GlobalNamespace/SliderData.hpp"
#include "Libraries/HM/HMLib/VR/HapticPresetSO.hpp"
#include "UnityEngine/Resources.hpp"
#include "UnityEngine/XR/XRNode.hpp"
#include "bsml/shared/BSML/MainThreadScheduler.hpp"

#include <atomic>

namespace TA::MidPlayModifiers {
    namespace {
        std::atomic_bool installed = false;
        std::atomic_bool invertColors = false;
        std::atomic_bool invertHaptics = false;
        std::atomic_bool invertHands = false;
        std::atomic_bool disableBlueNotes = false;
        std::atomic_bool disableRedNotes = false;
        std::atomic_int cachedNumberOfLines = 0;

        int32_t value(GlobalNamespace::ColorType color) {
            return static_cast<int32_t>(color);
        }

        int32_t value(GlobalNamespace::SaberType saberType) {
            return static_cast<int32_t>(saberType);
        }

        int32_t value(UnityEngine::XR::XRNode node) {
            return static_cast<int32_t>(node);
        }

        bool isColor(GlobalNamespace::ColorType color, GlobalNamespace::ColorType target) {
            return value(color) == value(target);
        }

        bool isSaber(GlobalNamespace::SaberType saberType, GlobalNamespace::SaberType target) {
            return value(saberType) == value(target);
        }

        bool isNode(UnityEngine::XR::XRNode node, UnityEngine::XR::XRNode target) {
            return value(node) == value(target);
        }

        GlobalNamespace::ColorType opposite(GlobalNamespace::ColorType color) {
            if (isColor(color, GlobalNamespace::ColorType::ColorA)) return GlobalNamespace::ColorType::ColorB;
            if (isColor(color, GlobalNamespace::ColorType::ColorB)) return GlobalNamespace::ColorType::ColorA;
            return color;
        }

        GlobalNamespace::SaberType opposite(GlobalNamespace::SaberType saberType) {
            if (isSaber(saberType, GlobalNamespace::SaberType::SaberA)) return GlobalNamespace::SaberType::SaberB;
            return GlobalNamespace::SaberType::SaberA;
        }

        UnityEngine::XR::XRNode opposite(UnityEngine::XR::XRNode node) {
            if (isNode(node, UnityEngine::XR::XRNode::LeftHand)) return UnityEngine::XR::XRNode::RightHand;
            if (isNode(node, UnityEngine::XR::XRNode::RightHand)) return UnityEngine::XR::XRNode::LeftHand;
            return node;
        }

        bool shouldSuppress(GlobalNamespace::ColorType color) {
            return (disableBlueNotes.load() && isColor(color, GlobalNamespace::ColorType::ColorB)) ||
                   (disableRedNotes.load() && isColor(color, GlobalNamespace::ColorType::ColorA));
        }

        int32_t numberOfLines() {
            auto cached = cachedNumberOfLines.load();
            if (cached > 0) return cached;

            auto installers = UnityEngine::Resources::FindObjectsOfTypeAll<GlobalNamespace::GameplayCoreInstaller*>();
            for (auto* installer : installers) {
                if (!installer) continue;
                auto* setupData = installer->__cordl_internal_get__sceneSetupData();
                if (!setupData) continue;
                auto* beatmapData = setupData->get_transformedBeatmapData();
                if (!beatmapData) continue;
                auto lines = beatmapData->get_numberOfLines();
                if (lines > 0) {
                    cachedNumberOfLines = lines;
                    PaperLogger.info("Mid-play modifiers detected {} beatmap lines", lines);
                    return lines;
                }
            }

            PaperLogger.warn("Mid-play modifiers could not find beatmap line count; defaulting to 4");
            cachedNumberOfLines = 4;
            return 4;
        }

        void swapGlowColors() {
            auto controllers = UnityEngine::Resources::FindObjectsOfTypeAll<GlobalNamespace::SaberModelController*>();
            for (auto* controller : controllers) {
                if (!controller) continue;

                auto glowColors = controller->__cordl_internal_get__setSaberGlowColors();
                for (auto glowRef : glowColors) {
                    auto* glow = glowRef.ptr();
                    if (!glow) continue;
                    auto nextType = opposite(glow->__cordl_internal_get__saberType());
                    glow->__cordl_internal_set__saberType(nextType);
                    glow->set_saberType(nextType);
                    glow->SetColors();
                }

                auto fakeGlowColors = controller->__cordl_internal_get__setSaberFakeGlowColors();
                for (auto glowRef : fakeGlowColors) {
                    auto* glow = glowRef.ptr();
                    if (!glow) continue;
                    auto nextType = opposite(glow->__cordl_internal_get__saberType());
                    glow->__cordl_internal_set__saberType(nextType);
                    glow->set_saberType(nextType);
                    glow->SetColors();
                }
            }
        }

        void swapSaberColors() {
            try {
                auto managers = UnityEngine::Resources::FindObjectsOfTypeAll<GlobalNamespace::SaberManager*>();
                for (auto* manager : managers) {
                    if (!manager) continue;
                    auto leftSaber = manager->get_leftSaber();
                    auto rightSaber = manager->get_rightSaber();
                    auto* left = leftSaber.ptr();
                    auto* right = rightSaber.ptr();
                    if (!left || !right) continue;

                    auto leftType = left->__cordl_internal_get__saberType();
                    auto rightType = right->__cordl_internal_get__saberType();
                    left->__cordl_internal_set__saberType(rightType);
                    right->__cordl_internal_set__saberType(leftType);
                    break;
                }

                swapGlowColors();
                PaperLogger.info("Mid-play modifier saber colors swapped");
            } catch (...) {
                PaperLogger.warn("Mid-play modifier saber color swap failed; continuing with note/haptic inversion");
            }
        }

        void applyToggle(GameplayModifierCommand modifier) {
            switch (modifier) {
                case GameplayModifierCommand::InvertColors: {
                    auto enabled = !invertColors.load();
                    invertColors = enabled;
                    invertHaptics = enabled;
                    swapSaberColors();
                    PaperLogger.info("Mid-play modifier InvertColors set to {}", enabled);
                    break;
                }
                case GameplayModifierCommand::InvertHandedness: {
                    auto enabled = !invertHands.load();
                    invertHands = enabled;
                    cachedNumberOfLines = 0;
                    PaperLogger.info("Mid-play modifier InvertHandedness set to {}", enabled);
                    break;
                }
                case GameplayModifierCommand::DisableBlueNotes: {
                    auto enabled = !disableBlueNotes.load();
                    disableBlueNotes = enabled;
                    PaperLogger.info("Mid-play modifier DisableBlueNotes set to {}", enabled);
                    break;
                }
                case GameplayModifierCommand::DisableRedNotes: {
                    auto enabled = !disableRedNotes.load();
                    disableRedNotes = enabled;
                    PaperLogger.info("Mid-play modifier DisableRedNotes set to {}", enabled);
                    break;
                }
                default:
                    PaperLogger.warn("Unknown ModifyGameplay modifier={}", int(modifier));
                    break;
            }
        }

        void resetOnMainThread() {
            auto colorsWereInverted = invertColors.exchange(false);
            invertHaptics = false;
            invertHands = false;
            disableBlueNotes = false;
            disableRedNotes = false;
            cachedNumberOfLines = 0;
            if (colorsWereInverted) swapSaberColors();
            PaperLogger.info("Mid-play modifiers reset");
        }
    }
}

MAKE_HOOK_MATCH(
    TA_BeatmapObjectSpawnController_HandleNoteDataCallback,
    &GlobalNamespace::BeatmapObjectSpawnController::HandleNoteDataCallback,
    void,
    GlobalNamespace::BeatmapObjectSpawnController* self,
    GlobalNamespace::NoteData* noteData
) {
    if (noteData) {
        auto color = noteData->get_colorType();
        if (TA::MidPlayModifiers::shouldSuppress(color)) return;
        if (TA::MidPlayModifiers::invertColors.load()) noteData->set_colorType(TA::MidPlayModifiers::opposite(color));
        if (TA::MidPlayModifiers::invertHands.load()) noteData->Mirror(TA::MidPlayModifiers::numberOfLines());
    }

    TA_BeatmapObjectSpawnController_HandleNoteDataCallback(self, noteData);
}

MAKE_HOOK_MATCH(
    TA_BeatmapObjectSpawnController_HandleObstacleDataCallback,
    &GlobalNamespace::BeatmapObjectSpawnController::HandleObstacleDataCallback,
    void,
    GlobalNamespace::BeatmapObjectSpawnController* self,
    GlobalNamespace::ObstacleData* obstacleData
) {
    if (obstacleData && TA::MidPlayModifiers::invertHands.load()) {
        obstacleData->Mirror(TA::MidPlayModifiers::numberOfLines());
    }

    TA_BeatmapObjectSpawnController_HandleObstacleDataCallback(self, obstacleData);
}

MAKE_HOOK_MATCH(
    TA_BeatmapObjectSpawnController_HandleSliderDataCallback,
    &GlobalNamespace::BeatmapObjectSpawnController::HandleSliderDataCallback,
    void,
    GlobalNamespace::BeatmapObjectSpawnController* self,
    GlobalNamespace::SliderData* sliderNoteData
) {
    if (sliderNoteData) {
        auto color = sliderNoteData->get_colorType();
        if (TA::MidPlayModifiers::shouldSuppress(color)) return;
        if (TA::MidPlayModifiers::invertColors.load()) sliderNoteData->set_colorType(TA::MidPlayModifiers::opposite(color));
        if (TA::MidPlayModifiers::invertHands.load()) sliderNoteData->Mirror(TA::MidPlayModifiers::numberOfLines());
    }

    TA_BeatmapObjectSpawnController_HandleSliderDataCallback(self, sliderNoteData);
}

MAKE_HOOK_MATCH(
    TA_HapticFeedbackManager_PlayHapticFeedback,
    &GlobalNamespace::HapticFeedbackManager::PlayHapticFeedback,
    void,
    GlobalNamespace::HapticFeedbackManager* self,
    UnityEngine::XR::XRNode node,
    Libraries::HM::HMLib::VR::HapticPresetSO* hapticPreset
) {
    if (TA::MidPlayModifiers::invertHaptics.load()) {
        node = TA::MidPlayModifiers::opposite(node);
    }

    TA_HapticFeedbackManager_PlayHapticFeedback(self, node, hapticPreset);
}

namespace TA::MidPlayModifiers {
    void installHooks() {
        if (installed.exchange(true)) return;
        PaperLogger.info("Installing mid-play modifier hooks");
        INSTALL_HOOK(PaperLogger, TA_BeatmapObjectSpawnController_HandleNoteDataCallback);
        INSTALL_HOOK(PaperLogger, TA_BeatmapObjectSpawnController_HandleObstacleDataCallback);
        INSTALL_HOOK(PaperLogger, TA_BeatmapObjectSpawnController_HandleSliderDataCallback);
        INSTALL_HOOK(PaperLogger, TA_HapticFeedbackManager_PlayHapticFeedback);
    }

    void toggle(GameplayModifierCommand modifier) {
        BSML::MainThreadScheduler::Schedule([modifier] {
            applyToggle(modifier);
        });
    }

    void reset() {
        BSML::MainThreadScheduler::Schedule([] {
            resetOnMainThread();
        });
    }
}
