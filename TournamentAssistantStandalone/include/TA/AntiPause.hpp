#pragma once

namespace TA::AntiPause {
    void installHooks();
    void reset();

    void setAllowPause(bool value);
    void setAllowContinueAfterPause(bool value);
    void setAllowRestart(bool value);

    bool allowPause();
    bool allowContinueAfterPause();
    bool allowRestart();
}
