#pragma once

#include "TA/Models.hpp"

#include "UnityEngine/Transform.hpp"

#include <functional>

namespace TA::UI {
    class TournamentModeViewController {
    public:
        using ImageRenderer = std::function<bool(UnityEngine::Transform*, TA::Tournament const&, float)>;
        using Action = std::function<void()>;

        static void Render(
            UnityEngine::Transform* parent,
            TA::Tournament const& tournament,
            ImageRenderer imageRenderer,
            Action onTournament,
            Action onQualifiers,
            Action onBack
        );
    };
}
