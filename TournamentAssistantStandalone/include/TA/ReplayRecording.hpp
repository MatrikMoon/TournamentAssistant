#pragma once

namespace GlobalNamespace { class ScoreController; class ScoringElement; class ObstacleController; }

// Records the song being played as a .tarf replay when the tournament has replay recording on.
// The replay is saved to Mods/TournamentAssistant/Replays and submitted to the server once the song ends
namespace TA::ReplayRecording {
    void installHooks();
    void start(GlobalNamespace::ScoreController* scoreController);
    void tick(GlobalNamespace::ScoreController* scoreController);
    // kind is a TARF NoteEventKind: 1 good cut, 2 bad cut, 3 miss, 4 bomb
    void recordScoring(GlobalNamespace::ScoringElement* scoringElement, int kind);
    void recordObstacleEntered(GlobalNamespace::ObstacleController* obstacle);
    // completion is a TARF Completion: 1 passed, 2 failed, 3 quit
    void finish(int completion, int finalScore, float endSongTime);
}
