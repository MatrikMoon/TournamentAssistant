#pragma once

namespace GlobalNamespace { class ScoreController; class ScoringElement; }

namespace TA::ReplayStreaming {
    void start(GlobalNamespace::ScoreController* scoreController);
    void tick(GlobalNamespace::ScoreController* scoreController);
    void recordScoring(GlobalNamespace::ScoringElement* scoringElement, int eventType);
    void finish(int completion);
}
