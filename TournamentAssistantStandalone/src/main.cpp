#include "main.hpp"

#include "TA/AntiPause.hpp"
#include "TA/Client.hpp"
#include "TA/MidPlayModifiers.hpp"
#include "TA/RealtimeScore.hpp"
#include "TA/TournamentViewController.hpp"

#include "bsml/shared/BSML.hpp"
#include "custom-types/shared/register.hpp"
#include "scotland2/shared/modloader.h"

static modloader::ModInfo modInfo{MOD_ID, VERSION, 0};

// Called at the early stages of game loading
MOD_EXTERN_FUNC void setup(CModInfo* info) noexcept {
  *info = modInfo.to_c();

  // File logging
  Paper::Logger::RegisterFileContextId(PaperLogger.tag);

  PaperLogger.info("Completed setup!");
}

// Called later on in the game loading - a good time to install function hooks
MOD_EXTERN_FUNC void late_load() noexcept {
  il2cpp_functions::Init();

  PaperLogger.info("Installing hooks...");
  TA::AntiPause::installHooks();
  TA::MidPlayModifiers::installHooks();
  TA::RealtimeScoreHooks::installHooks();
  custom_types::Register::AutoRegister();
  BSML::Init();
  BSML::Register::RegisterMainMenu<TA::TournamentViewController*>("TournamentAssistant", "TournamentAssistant", "Open TournamentAssistant");

  PaperLogger.info("Installed all hooks!");
}
