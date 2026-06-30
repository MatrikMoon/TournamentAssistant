#pragma once

#include <cstdint>
#include <optional>
#include <string>
#include <vector>

namespace TA {
    enum class PacketKind {
        None,
        Heartbeat,
        ForwardingPacket,
        Command,
        Push,
        Request,
        Response,
        Event
    };

    enum class PushKind {
        None,
        SongFinished,
        RealtimeScore
    };

    enum class SongCompletionType : int32_t {
        Passed = 0,
        Failed = 1,
        Quit = 2
    };

    enum class CommandKind {
        None,
        ReturnToMenu,
        DelayTestFinish,
        StreamSyncShowImage,
        PlaySong,
        DiscordAuthorize,
        ModifyGameplay,
        ShowColorForStreamSync
    };

    enum class GameplayModifierCommand : int32_t {
        InvertColors = 0,
        InvertHandedness = 1,
        DisableBlueNotes = 2,
        DisableRedNotes = 3
    };

    enum class RequestKind {
        None,
        Connect,
        Join,
        UpdateUser,
        LoadSong,
        PreloadImageForStreamSync,
        ShowPrompt,
        QualifierScores,
        RemainingAttempts,
        SubmitQualifierScore
    };

    enum class ResponseKind {
        None,
        Connect,
        Join,
        LoadSong,
        ShowPrompt,
        PreloadImageForStreamSync,
        LeaderboardEntries,
        RemainingAttempts,
        SubmitQualifierScore
    };

    enum class EventKind {
        None,
        UserAdded,
        UserUpdated,
        UserLeft,
        MatchCreated,
        MatchUpdated,
        MatchDeleted,
        QualifierCreated,
        QualifierUpdated,
        QualifierDeleted,
        TournamentCreated,
        TournamentUpdated,
        TournamentDeleted
    };

    enum class ResponseType : int32_t {
        Fail = 0,
        Success = 1
    };

    enum class PlayState : int32_t {
        InMenu = 0,
        WaitingForCoordinator = 1,
        InGame = 2
    };

    enum class DownloadState : int32_t {
        None = 0,
        Downloading = 1,
        Downloaded = 2,
        DownloadError = 3
    };

    enum GameOptions : int32_t {
        NoFail = 1,
        NoBombs = 2,
        NoArrows = 4,
        NoObstacles = 8,
        SlowSong = 16,
        InstaFail = 32,
        FailOnClash = 64,
        BatteryEnergy = 128,
        FastNotes = 256,
        FastSong = 512,
        DisappearingArrows = 1024,
        GhostNotes = 2048,
        DemoNoFail = 4096,
        DemoNoObstacles = 8192,
        StrictAngles = 16384,
        ProMode = 32768,
        ZenMode = 65536,
        SmallCubes = 131072,
        SuperFastSong = 262144
    };

    struct Characteristic {
        std::string serializedName;
        std::vector<int32_t> difficulties;
    };

    struct Beatmap {
        std::string name;
        std::string levelId;
        Characteristic characteristic;
        int32_t difficulty = 0;
    };

    struct GameplayModifiers {
        int32_t options = 0;
    };

    struct PlayerSpecificSettings {
        float playerHeight = 0.0f;
        float sfxVolume = 0.0f;
        float saberTrailIntensity = 0.0f;
        float noteJumpStartBeatOffset = 0.0f;
        float noteJumpFixedDuration = 0.0f;
        int32_t options = 0;
        int32_t noteJumpDurationTypeSettings = 0;
        int32_t arcVisibilityType = 0;
    };

    struct GameplayParameters {
        Beatmap beatmap;
        PlayerSpecificSettings playerSettings;
        GameplayModifiers gameplayModifiers;
        int32_t attempts = 0;
        bool showScoreboard = false;
        bool disablePause = false;
        bool disableFail = false;
        bool disableScoresaberSubmission = false;
        bool disableCustomNotesOnStream = false;
        bool useSync = false;
        int32_t target = 0;
    };

    struct Map {
        std::string guid;
        GameplayParameters gameplayParameters;
    };

    struct User {
        std::string guid;
        std::string name;
        std::string platformId;
        int32_t clientType = 0;
        std::string teamId;
        PlayState playState = PlayState::InMenu;
        DownloadState downloadState = DownloadState::None;
        std::vector<std::string> modList;
        int64_t streamDelayMs = 0;
        int64_t streamSyncStartMs = 0;
    };

    struct Match {
        std::string guid;
        std::vector<std::string> associatedUsers;
        std::string leader;
        std::optional<Map> selectedMap;
    };

    struct CoreServer {
        std::string name;
        std::string address;
        int32_t port = 0;
        int32_t websocketPort = 0;
    };

    struct Team {
        std::string guid;
        std::string name;
        std::string image;
    };

    struct TournamentSettings {
        std::string tournamentName;
        std::string tournamentImage;
        bool enableTeams = false;
        bool enablePools = false;
        std::vector<Team> teams;
        int32_t scoreUpdateFrequency = 30;
        bool showTournamentButton = true;
        bool showQualifierButton = false;
        bool allowUnauthorizedView = false;
    };

    enum class QualifierLeaderboardSort : int32_t {
        ModifiedScore = 0,
        ModifiedScoreAscending = 1,
        ModifiedScoreTarget = 2,
        NotesMissed = 3,
        NotesMissedAscending = 4,
        NotesMissedTarget = 5,
        BadCuts = 6,
        BadCutsAscending = 7,
        BadCutsTarget = 8,
        MaxCombo = 9,
        MaxComboAscending = 10,
        MaxComboTarget = 11,
        GoodCuts = 12,
        GoodCutsAscending = 13,
        GoodCutsTarget = 14
    };

    struct QualifierEvent {
        std::string guid;
        std::string name;
        std::string image;
        std::vector<Map> qualifierMaps;
        int32_t flags = 0;
        QualifierLeaderboardSort sort = QualifierLeaderboardSort::ModifiedScore;
    };

    struct Tournament {
        std::string guid;
        TournamentSettings settings;
        std::vector<User> users;
        std::vector<Match> matches;
        std::vector<QualifierEvent> qualifiers;
        CoreServer server;
    };

    struct PromptOption {
        std::string label;
        std::string value;
    };

    struct Prompt {
        std::string requestId;
        std::string fromUserId;
        std::string promptId;
        std::string title;
        std::string text;
        int32_t timeout = 0;
        bool showTimer = false;
        bool canClose = false;
        std::vector<PromptOption> options;
    };

    struct LeaderboardEntry {
        std::string eventId;
        std::string mapId;
        std::string platformId;
        std::string username;
        int32_t multipliedScore = 0;
        int32_t modifiedScore = 0;
        int32_t maxPossibleScore = 0;
        double accuracy = 0.0;
        int32_t notesMissed = 0;
        int32_t badCuts = 0;
        int32_t goodCuts = 0;
        int32_t maxCombo = 0;
        bool fullCombo = false;
        bool isPlaceholder = false;
        std::string color = "#ffffff";
    };

    struct State {
        std::vector<Tournament> tournaments;
        std::vector<CoreServer> knownServers;
    };

    struct Command {
        CommandKind kind = CommandKind::None;
        std::string tournamentId;
        std::vector<std::string> forwardTo;
        GameplayParameters playSong;
        std::string discordAuthorize;
        GameplayModifierCommand modifier = GameplayModifierCommand::InvertColors;
        std::string streamSyncColor;
    };

    struct Request {
        RequestKind kind = RequestKind::None;
        std::string tournamentId;
        std::string password;
        std::vector<std::string> modList;
        int32_t clientVersion = 0;
        std::string levelId;
        std::string customHostUrl;
        std::string fileId;
        bool compressed = false;
        std::vector<uint8_t> data;
        std::vector<std::string> forwardTo;
        User user;
        Prompt prompt;
        std::string eventId;
        std::string mapId;
        LeaderboardEntry qualifierScore;
        Map map;
    };

    struct Response {
        ResponseKind kind = ResponseKind::None;
        ResponseType type = ResponseType::Fail;
        std::string respondingToPacketId;
        State state;
        int32_t serverVersion = 0;
        std::string message;
        std::string selfGuid;
        std::string tournamentId;
        std::string levelId;
        std::string fileId;
        std::string promptValue;
        std::string permissionRequired;
        std::string permissionRoles;
        std::string permissionPermissions;
        std::vector<LeaderboardEntry> leaderboardEntries;
        int32_t remainingAttempts = -1;
    };

    struct Event {
        EventKind kind = EventKind::None;
        std::string tournamentId;
        User user;
        Match match;
        QualifierEvent qualifier;
        Tournament tournament;
    };

    struct SongFinished {
        User player;
        Beatmap beatmap;
        SongCompletionType type = SongCompletionType::Quit;
        int32_t score = 0;
        int32_t misses = 0;
        int32_t badCuts = 0;
        int32_t goodCuts = 0;
        float endTime = 0.0f;
        std::string tournamentId;
        std::string matchId;
        int32_t maxScore = 0;
        double accuracy = 0.0;
    };

    struct ScoreTrackerHand {
        int32_t hit = 0;
        int32_t miss = 0;
        int32_t badCut = 0;
        std::vector<float> avgCut;
    };

    struct RealtimeScore {
        std::string userGuid;
        int32_t score = 0;
        int32_t scoreWithModifiers = 0;
        int32_t maxScore = 0;
        int32_t maxScoreWithModifiers = 0;
        int32_t combo = 0;
        float playerHealth = 0.0f;
        double accuracy = 0.0;
        float songPosition = 0.0f;
        int32_t notesMissed = 0;
        int32_t badCuts = 0;
        int32_t bombHits = 0;
        int32_t wallHits = 0;
        int32_t maxCombo = 0;
        ScoreTrackerHand leftHand;
        ScoreTrackerHand rightHand;
    };

    struct Push {
        PushKind kind = PushKind::None;
        SongFinished songFinished;
        RealtimeScore realtimeScore;
    };

    struct Packet {
        std::string token;
        std::string id;
        std::string from;
        PacketKind kind = PacketKind::None;
        bool heartbeat = false;
        Command command;
        Push push;
        Request request;
        Response response;
        Event event;
    };
}
