#pragma once

#include "TA/Models.hpp"

namespace TA::MidPlayModifiers {
    void installHooks();
    void toggle(GameplayModifierCommand modifier);
    void reset();
}
