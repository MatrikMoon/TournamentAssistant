#include "TA/AntiPause.hpp"

#include "main.hpp"

#include "GlobalNamespace/PauseController.hpp"
#include "GlobalNamespace/PauseMenuManager.hpp"
#include "UnityEngine/GameObject.hpp"
#include "UnityEngine/Resources.hpp"
#include "UnityEngine/UI/Button.hpp"

#include <atomic>

namespace TA::AntiPause {
    namespace {
        bool installed = false;
        std::atomic<bool> pauseAllowed = true;
        std::atomic<bool> continueAllowed = true;
        std::atomic<bool> restartAllowed = true;

        void applyPauseMenuButtons() {
            auto managers = UnityEngine::Resources::FindObjectsOfTypeAll<GlobalNamespace::PauseMenuManager*>();
            for (auto* manager : managers) {
                if (!manager) continue;
                auto continueButton = manager->__cordl_internal_get__continueButton();
                if (continueButton && continueButton->get_gameObject()) {
                    continueButton->get_gameObject()->SetActive(continueAllowed.load());
                }

                auto restartButton = manager->__cordl_internal_get__restartButton();
                if (restartButton && restartButton->get_gameObject()) {
                    restartButton->get_gameObject()->SetActive(restartAllowed.load());
                }
            }
        }
    }
}

MAKE_HOOK_MATCH(TA_PauseController_Pause, &GlobalNamespace::PauseController::Pause, void, GlobalNamespace::PauseController* self) {
    if (TA::AntiPause::allowPause()) {
        PaperLogger.info("Pause allowed");
        TA_PauseController_Pause(self);
    } else {
        PaperLogger.warn("Pause blocked by TournamentAssistant");
    }
}

MAKE_HOOK_MATCH(TA_PauseController_Continue, &GlobalNamespace::PauseController::HandlePauseMenuManagerDidPressContinueButton, void, GlobalNamespace::PauseController* self) {
    if (TA::AntiPause::allowContinueAfterPause()) {
        PaperLogger.info("Continue after pause allowed");
        TA_PauseController_Continue(self);
    } else {
        PaperLogger.warn("Continue after pause blocked by TournamentAssistant");
    }
}

MAKE_HOOK_MATCH(TA_PauseMenuManager_RestartButtonPressed, &GlobalNamespace::PauseMenuManager::RestartButtonPressed, void, GlobalNamespace::PauseMenuManager* self) {
    if (TA::AntiPause::allowRestart()) {
        PaperLogger.info("Restart allowed");
        TA_PauseMenuManager_RestartButtonPressed(self);
    } else {
        PaperLogger.warn("Restart blocked by TournamentAssistant");
    }
}

namespace TA::AntiPause {
    void installHooks() {
        if (installed) return;
        installed = true;
        PaperLogger.info("Installing AntiPause hooks");
        INSTALL_HOOK(PaperLogger, TA_PauseController_Pause);
        INSTALL_HOOK(PaperLogger, TA_PauseController_Continue);
        INSTALL_HOOK(PaperLogger, TA_PauseMenuManager_RestartButtonPressed);
    }

    void reset() {
        PaperLogger.info("AntiPause reset");
        pauseAllowed = true;
        continueAllowed = true;
        restartAllowed = true;
        applyPauseMenuButtons();
    }

    void setAllowPause(bool value) {
        PaperLogger.info("AntiPause setAllowPause {}", value);
        pauseAllowed = value;
    }

    void setAllowContinueAfterPause(bool value) {
        PaperLogger.info("AntiPause setAllowContinueAfterPause {}", value);
        continueAllowed = value;
        applyPauseMenuButtons();
    }

    void setAllowRestart(bool value) {
        PaperLogger.info("AntiPause setAllowRestart {}", value);
        restartAllowed = value;
        applyPauseMenuButtons();
    }

    bool allowPause() {
        return pauseAllowed.load();
    }

    bool allowContinueAfterPause() {
        return continueAllowed.load();
    }

    bool allowRestart() {
        return restartAllowed.load();
    }
}
