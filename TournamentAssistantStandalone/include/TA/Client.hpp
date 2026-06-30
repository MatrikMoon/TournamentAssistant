#pragma once

#include "TA/Models.hpp"

#include "libcurl/shared/curl.h"

#include <atomic>
#include <condition_variable>
#include <functional>
#include <future>
#include <map>
#include <mutex>
#include <optional>
#include <string>
#include <thread>
#include <tuple>
#include <vector>

namespace TA {
    class Client {
    public:
        using UiCallback = std::function<void()>;

        static Client& instance();

        void setUiCallback(UiCallback callback);
        void connect();
        void disconnect();
        void joinTournament(std::string tournamentId);

        State state() const;
        std::string status() const;
        std::string selectedTournamentId() const;
        std::string selfGuid() const;
        bool connected() const;
        bool inTournament() const;
        std::optional<Match> currentMatch() const;
        std::optional<Map> selectedMatchMap() const;
        std::optional<Prompt> activePrompt() const;
        std::optional<GameplayParameters> activeSong() const;
        std::vector<LeaderboardEntry> leaderboard(std::string const& eventId, std::string const& mapId) const;
        int32_t remainingAttempts(std::string const& eventId, std::string const& mapId) const;
        int32_t scoreUpdateFrequency() const;

        void setLocalPlayState(PlayState playState);
        void setLocalDownloadState(DownloadState downloadState);
        void setLocalTeam(std::string teamId);
        void clearTransientGameplayState(std::string reason);
        std::optional<User> selfUser() const;

        void dismissPrompt();
        void sendPromptResponse(Prompt prompt, std::string value);
        void requestLeaderboard(std::string tournamentId, std::string eventId, std::string mapId);
        void requestRemainingAttempts(std::string tournamentId, std::string eventId, std::string mapId);
        void playQualifierMap(std::string tournamentId, std::string eventId, Map map);
        void practiceQualifierMap(Map map);
        void ensureMapDownloaded(Map map);

        void sendLoadSongResponse(std::string const& requester, std::string const& requestId, std::string const& levelId, bool success, std::string message);
        void sendPreloadImageResponse(std::string const& requester, std::string const& requestId, std::string const& fileId, bool success, std::string message);
        void sendRealtimeScore(RealtimeScore score);
        void sendSongFinished(GameplayParameters const& parameters, SongCompletionType type, int32_t score, int32_t misses, int32_t badCuts, int32_t goodCuts, float endTime, int32_t maxScore = 0, double accuracy = 0.0);

    private:
        Client() = default;
        ~Client();

        void workerConnect();
        void receiveLoop();
        void heartbeatLoop();
        void handlePacket(Packet packet);
        void applyEvent(Event const& event);
        void notifyUi();
        void setStatus(std::string value);
        void failPendingResponses(std::string message);

        std::future<Response> sendRequest(Request request);
        void sendPacket(Packet packet);
        void sendUserUpdate(User const& user);
        void submitQualifierScore(Map const& map, LeaderboardEntry score);
        Response submitQualifierScoreBlocking(Map const& map, LeaderboardEntry score);

        Tournament* findTournamentLocked(std::string const& tournamentId);
        User* findSelfLocked();
        static bool isSuccess(Response const& response);
        static std::string qualifierKey(std::string const& eventId, std::string const& mapId);

        mutable std::mutex mutex_;
        mutable std::mutex ioMutex_;
        UiCallback uiCallback_;
        State state_;
        std::string status_ = "Not connected";
        std::string selectedTournamentId_;
        std::string selfGuid_;
        std::optional<Prompt> activePrompt_;
        std::optional<GameplayParameters> activeSong_;
        std::optional<std::tuple<std::string, std::string, std::string, Map>> activeQualifier_;
        std::map<std::string, std::vector<LeaderboardEntry>> leaderboards_;
        std::map<std::string, int32_t> remainingAttempts_;
        std::map<std::string, bool> downloadRequests_;
        std::map<std::string, DownloadState> downloadStatesByLevel_;
        std::map<std::string, std::promise<Response>> pendingResponses_;

        int socketFd_ = -1;
        CURL* curl_ = nullptr;
        std::atomic<bool> connected_{false};
        std::atomic<bool> connecting_{false};
        std::atomic<bool> stop_{false};
        std::thread connectThread_;
        std::thread receiveThread_;
        std::thread heartbeatThread_;
    };
}
