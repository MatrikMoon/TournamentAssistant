#pragma once

#include <cstdint>
namespace TA {
    // Quest standalone connects to the configured TA server after fetching a
    // short-lived BeatKhana game token from the local ScoreSaber proof file.
    constexpr const char* kServerHost = "server.tournamentassistant.net";
    constexpr int kServerPort = 8675;
    constexpr int kMasterApiPort = 8678;
    constexpr int kClientVersion = 1240;
    constexpr const char* kBeatKhanaGameAuthUrl = "https://api.beatkhana.com/game/requestAuthToken";
    constexpr const char* kLatestStandaloneVersionUrl = "https://api.beatkhana.com/game/ta/standalone/latest-version";
    constexpr const char* kDownloadUrl = "https://download.tournamentassistant.net";

    constexpr const char* kModList[] = {
        "TournamentAssistant Standalone",
        "SongCore"
    };
}
