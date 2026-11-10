#pragma once

#include "TA/Models.hpp"

namespace TA::MidPlayModifiers {
    struct State {
        bool invertColors = false;
        bool invertHandedness = false;
        bool disableBlueNotes = false;
        bool disableRedNotes = false;

        bool operator==(State const& other) const = default;
    };

    void installHooks();
    void toggle(GameplayModifierCommand modifier);
    void reset();
    State current();
}
