#pragma once

#include "TA/Models.hpp"

#include <functional>
#include <string>

namespace TA::Song {
    using DownloadCallback = std::function<void(bool success, std::string message)>;

    struct PlayResult {
        bool completed = false;
        SongCompletionType type = SongCompletionType::Quit;
        int32_t score = 0;
        int32_t misses = 0;
        int32_t badCuts = 0;
        int32_t goodCuts = 0;
        float endTime = 0.0f;
    };

    using PlayFinishedCallback = std::function<void(PlayResult const& result)>;

    struct SongDetails {
        bool loaded = false;
        std::string name;
        std::string songAuthor;
        std::string mapper;
        std::string coverUrl;
        float bpm = 0.0f;
        int32_t duration = 0;
        int32_t notes = 0;
        int32_t bombs = 0;
        int32_t walls = 0;
        float njs = 0.0f;
    };

    SongDetails detailsFor(GameplayParameters const& parameters);
    void requestDetails(GameplayParameters parameters);
    bool isDownloaded(std::string const& levelId);
    void ensureDownloaded(std::string levelId, std::string customHostUrl, DownloadCallback callback);
    void playSong(GameplayParameters parameters, PlayFinishedCallback callback = nullptr, bool reportSongFinished = true);
    void returnToMenu();
}
