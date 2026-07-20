#pragma once

#include "TA/Models.hpp"

#include "UnityEngine/Sprite.hpp"
#include "UnityEngine/Transform.hpp"

#include <functional>
#include <string>

namespace TA::UI {
    class TournamentListViewController {
    public:
        using SpriteProvider = std::function<UnityEngine::Sprite*(TA::Tournament const&)>;
        using SelectCallback = std::function<void(std::string const&)>;

        static void Render(
            UnityEngine::Transform* parent,
            std::vector<TA::Tournament> const& tournaments,
            SpriteProvider spriteProvider,
            SelectCallback onSelected
        );
    };
}
