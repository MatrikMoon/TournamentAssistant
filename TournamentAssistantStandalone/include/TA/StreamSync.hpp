#pragma once

#include <cstdint>
#include <string>
#include <vector>

namespace TA::StreamSync {
    void begin();
    void finish();
    void clear();
    void clearImmediate();
    void showColor(std::string color);
    void setImage(std::vector<uint8_t> bytes);
    void showImage(bool show);
}
