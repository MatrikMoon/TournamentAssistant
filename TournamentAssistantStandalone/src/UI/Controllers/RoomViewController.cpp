#include "TA/UI/RoomViewController.hpp"

#include "main.hpp"

void TA::UI::RoomViewController::Render(
    UnityEngine::Transform* parent,
    std::optional<TA::Match> const& match,
    std::optional<TA::Map> const& selectedMap,
    TextRenderer text,
    MapLabelProvider mapLabel,
    SongDetailsRenderer songDetails,
    ModifiersProvider modifiers,
    EnsureDownloaded ensureDownloaded
) {
    PaperLogger.info("RoomViewController rendering match={} selectedMap={}", match.has_value(), selectedMap.has_value());
    if (!parent) {
        PaperLogger.error("RoomViewController parent is null");
        return;
    }

    if (!text) {
        PaperLogger.error("RoomViewController text renderer is null");
        return;
    }

    if (!match) {
        text(parent, "Joined Tournament", 4.4f);
        text(parent, "Waiting for coordinator to create a match...", 3.8f);
        return;
    }

    if (!selectedMap) {
        text(parent, "Match Created", 4.4f);
        text(parent, "Waiting for coordinator to select song", 3.8f);
        return;
    }

    if (ensureDownloaded) ensureDownloaded(*selectedMap);
    text(parent, "Match Created. Song selected", 3.8f);
    if (mapLabel) text(parent, mapLabel(*selectedMap), 4.0f);
    text(parent, selectedMap->gameplayParameters.beatmap.levelId, 3.0f);
    if (songDetails) songDetails(parent, selectedMap->gameplayParameters);
    text(parent, "Coordinator settings", 3.5f);
    if (modifiers) text(parent, modifiers(selectedMap->gameplayParameters), 3.2f);
    auto playerOptions = selectedMap->gameplayParameters.playerSettings.options;
    if (playerOptions == 0) {
        text(parent, "Player options: personal defaults", 3.2f);
    } else {
        text(parent, "Player options: forced for this map", 3.2f);
    }
    text(
        parent,
        selectedMap->gameplayParameters.disablePause ? "Pause: disabled by coordinator" : "Pause: allowed",
        3.2f
    );
    text(parent, "Download will begin automatically if this map is not installed.", 3.0f);
}
