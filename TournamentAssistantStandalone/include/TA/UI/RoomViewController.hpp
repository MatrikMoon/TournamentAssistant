#pragma once

#include "TA/Models.hpp"

#include "UnityEngine/Transform.hpp"

#include <functional>
#include <optional>
#include <string>

namespace TA::UI {
    class RoomViewController {
    public:
        using TextRenderer = std::function<void(UnityEngine::Transform*, std::string const&, float)>;
        using SongDetailsRenderer = std::function<void(UnityEngine::Transform*, TA::GameplayParameters const&)>;
        using MapLabelProvider = std::function<std::string(TA::Map const&)>;
        using ModifiersProvider = std::function<std::string(TA::GameplayParameters const&)>;
        using EnsureDownloaded = std::function<void(TA::Map const&)>;

        static void Render(
            UnityEngine::Transform* parent,
            std::optional<TA::Match> const& match,
            std::optional<TA::Map> const& selectedMap,
            TextRenderer text,
            MapLabelProvider mapLabel,
            SongDetailsRenderer songDetails,
            ModifiersProvider modifiers,
            EnsureDownloaded ensureDownloaded
        );
    };
}
