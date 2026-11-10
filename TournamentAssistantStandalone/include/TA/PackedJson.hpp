#pragma once

#include <cstdint>
#include <optional>
#include <vector>

// Packs JSON map files into columns before they are deflated, the format described under "Map files" in TARF/README.md
// of the TournamentAssistant repository. Numbers keep the precision Beat Saber reads them with:
// whole numbers stay exact and everything else becomes a float
namespace TA::PackedJson {
    // Empty when the data isn't JSON
    std::optional<std::vector<uint8_t>> pack(std::vector<uint8_t> const& json);
}
