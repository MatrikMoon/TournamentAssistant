#pragma once

namespace GlobalNamespace { class ScoreController; class ScoringElement; }

#include <cstdint>
#include <vector>

namespace TA::ReplayStreaming {
    // The JSON of the player's selected HitScoreVisualizer profile, empty when there is none
    std::vector<uint8_t> hsvProfile();

    void start(GlobalNamespace::ScoreController* scoreController);
    void tick(GlobalNamespace::ScoreController* scoreController);
    void recordScoring(GlobalNamespace::ScoringElement* scoringElement, int eventType);
    void finish(int completion);
}
