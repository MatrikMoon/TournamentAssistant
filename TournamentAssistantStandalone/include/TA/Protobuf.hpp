#pragma once

#include "TA/Models.hpp"

#include <cstdint>
#include <optional>
#include <string>
#include <vector>

namespace TA::Proto {
    std::vector<uint8_t> wrapPacket(Packet const& packet);
    std::optional<Packet> unwrapPacket(std::vector<uint8_t> const& payload);

    std::vector<uint8_t> encodePacket(Packet const& packet);
    std::optional<Packet> decodePacket(std::vector<uint8_t> const& payload);

    std::string makePacketId();
}
