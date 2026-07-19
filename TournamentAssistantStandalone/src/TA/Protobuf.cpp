#include "TA/Protobuf.hpp"

#include "TA/Constants.hpp"

#include <algorithm>
#include <array>
#include <cstring>
#include <random>
#include <sstream>

namespace TA::Proto {
    namespace {
        constexpr uint32_t kWireVarint = 0;
        constexpr uint32_t kWireFixed64 = 1;
        constexpr uint32_t kWireLength = 2;
        constexpr uint32_t kWireFixed32 = 5;

        struct Reader {
            uint8_t const* data;
            size_t size;
            size_t pos = 0;

            explicit Reader(std::vector<uint8_t> const& bytes) : data(bytes.data()), size(bytes.size()) {}
            Reader(uint8_t const* bytes, size_t length) : data(bytes), size(length) {}

            bool eof() const { return pos >= size; }

            bool readVarint(uint64_t& value) {
                value = 0;
                int shift = 0;
                while (pos < size && shift <= 63) {
                    uint8_t b = data[pos++];
                    value |= uint64_t(b & 0x7f) << shift;
                    if ((b & 0x80) == 0) return true;
                    shift += 7;
                }
                return false;
            }

            bool next(uint32_t& field, uint32_t& wire) {
                uint64_t tag = 0;
                if (!readVarint(tag)) return false;
                field = uint32_t(tag >> 3);
                wire = uint32_t(tag & 0x07);
                return field != 0;
            }

            bool readBytes(std::vector<uint8_t>& bytes) {
                uint64_t length = 0;
                if (!readVarint(length) || pos + length > size) return false;
                bytes.assign(data + pos, data + pos + length);
                pos += length;
                return true;
            }

            bool readString(std::string& value) {
                std::vector<uint8_t> bytes;
                if (!readBytes(bytes)) return false;
                value.assign(reinterpret_cast<char const*>(bytes.data()), bytes.size());
                return true;
            }

            bool readFloat(float& value) {
                if (pos + sizeof(float) > size) return false;
                std::memcpy(&value, data + pos, sizeof(float));
                pos += sizeof(float);
                return true;
            }

            bool readDouble(double& value) {
                if (pos + sizeof(double) > size) return false;
                std::memcpy(&value, data + pos, sizeof(double));
                pos += sizeof(double);
                return true;
            }

            bool skip(uint32_t wire) {
                uint64_t scratch = 0;
                switch (wire) {
                    case kWireVarint:
                        return readVarint(scratch);
                    case kWireFixed64:
                        if (pos + 8 > size) return false;
                        pos += 8;
                        return true;
                    case kWireLength:
                        if (!readVarint(scratch) || pos + scratch > size) return false;
                        pos += scratch;
                        return true;
                    case kWireFixed32:
                        if (pos + 4 > size) return false;
                        pos += 4;
                        return true;
                    default:
                        return false;
                }
            }
        };

        using Bytes = std::vector<uint8_t>;

        void writeVarint(Bytes& out, uint64_t value) {
            while (value >= 0x80) {
                out.push_back(uint8_t(value | 0x80));
                value >>= 7;
            }
            out.push_back(uint8_t(value));
        }

        void writeTag(Bytes& out, uint32_t field, uint32_t wire) {
            writeVarint(out, (uint64_t(field) << 3) | wire);
        }

        void writeInt(Bytes& out, uint32_t field, int64_t value) {
            if (value == 0) return;
            writeTag(out, field, kWireVarint);
            writeVarint(out, uint64_t(value));
        }

        void writeBool(Bytes& out, uint32_t field, bool value) {
            if (!value) return;
            writeTag(out, field, kWireVarint);
            writeVarint(out, 1);
        }

        void writeString(Bytes& out, uint32_t field, std::string const& value) {
            if (value.empty()) return;
            writeTag(out, field, kWireLength);
            writeVarint(out, value.size());
            out.insert(out.end(), value.begin(), value.end());
        }

        void writeBytes(Bytes& out, uint32_t field, Bytes const& value) {
            if (value.empty()) return;
            writeTag(out, field, kWireLength);
            writeVarint(out, value.size());
            out.insert(out.end(), value.begin(), value.end());
        }

        void writeFloat(Bytes& out, uint32_t field, float value) {
            if (value == 0.0f) return;
            writeTag(out, field, kWireFixed32);
            auto const* ptr = reinterpret_cast<uint8_t const*>(&value);
            out.insert(out.end(), ptr, ptr + sizeof(float));
        }

        void writeDouble(Bytes& out, uint32_t field, double value) {
            if (value == 0.0) return;
            writeTag(out, field, kWireFixed64);
            auto const* ptr = reinterpret_cast<uint8_t const*>(&value);
            out.insert(out.end(), ptr, ptr + sizeof(double));
        }

        Bytes encodeCharacteristic(Characteristic const& characteristic);
        Bytes encodeBeatmap(Beatmap const& beatmap);
        Bytes encodeGameplayModifiers(GameplayModifiers const& modifiers);
        Bytes encodePlayerSpecificSettings(PlayerSpecificSettings const& settings);
        Bytes encodeGameplayParameters(GameplayParameters const& parameters);
        Bytes encodeMap(Map const& map);
        Bytes encodeUser(User const& user);
        Bytes encodeLeaderboardEntry(LeaderboardEntry const& entry);
        Bytes encodeScoreTrackerHand(ScoreTrackerHand const& hand);
        Bytes encodeRealtimeScore(RealtimeScore const& score);
        Bytes encodeRequest(Request const& request);
        Bytes encodeResponse(Response const& response);
        Bytes encodePush(Push const& push);

        void decodeCharacteristic(Reader& reader, Characteristic& characteristic);
        void decodeBeatmap(Reader& reader, Beatmap& beatmap);
        void decodeGameplayModifiers(Reader& reader, GameplayModifiers& modifiers);
        void decodePlayerSpecificSettings(Reader& reader, PlayerSpecificSettings& settings);
        void decodeGameplayParameters(Reader& reader, GameplayParameters& parameters);
        void decodeMap(Reader& reader, Map& map);
        void decodeUser(Reader& reader, User& user);
        void decodeMatch(Reader& reader, Match& match);
        void decodeQualifierEvent(Reader& reader, QualifierEvent& qualifier);
        void decodeLeaderboardEntry(Reader& reader, LeaderboardEntry& entry);
        void decodeTeam(Reader& reader, Team& team);
        void decodeTournament(Reader& reader, Tournament& tournament);
        void decodeState(Reader& reader, State& state);

        Bytes encodeCharacteristic(Characteristic const& characteristic) {
            Bytes out;
            writeString(out, 1, characteristic.serializedName);
            for (int32_t difficulty : characteristic.difficulties) writeInt(out, 2, difficulty);
            return out;
        }

        Bytes encodeBeatmap(Beatmap const& beatmap) {
            Bytes out;
            writeString(out, 1, beatmap.name);
            writeString(out, 2, beatmap.levelId);
            writeBytes(out, 3, encodeCharacteristic(beatmap.characteristic));
            writeInt(out, 4, beatmap.difficulty);
            return out;
        }

        Bytes encodeGameplayModifiers(GameplayModifiers const& modifiers) {
            Bytes out;
            writeInt(out, 1, modifiers.options);
            return out;
        }

        Bytes encodePlayerSpecificSettings(PlayerSpecificSettings const& settings) {
            Bytes out;
            writeFloat(out, 1, settings.playerHeight);
            writeFloat(out, 2, settings.sfxVolume);
            writeFloat(out, 3, settings.saberTrailIntensity);
            writeFloat(out, 4, settings.noteJumpStartBeatOffset);
            writeFloat(out, 5, settings.noteJumpFixedDuration);
            writeInt(out, 6, settings.options);
            writeInt(out, 7, settings.noteJumpDurationTypeSettings);
            writeInt(out, 8, settings.arcVisibilityType);
            return out;
        }

        Bytes encodeGameplayParameters(GameplayParameters const& parameters) {
            Bytes out;
            writeBytes(out, 1, encodeBeatmap(parameters.beatmap));
            writeBytes(out, 2, encodePlayerSpecificSettings(parameters.playerSettings));
            writeBytes(out, 3, encodeGameplayModifiers(parameters.gameplayModifiers));
            writeInt(out, 4, parameters.attempts);
            writeBool(out, 5, parameters.showScoreboard);
            writeBool(out, 6, parameters.disablePause);
            writeBool(out, 7, parameters.disableFail);
            writeBool(out, 8, parameters.disableScoresaberSubmission);
            writeBool(out, 9, parameters.disableCustomNotesOnStream);
            writeBool(out, 10, parameters.useSync);
            writeInt(out, 11, parameters.target);
            return out;
        }

        Bytes encodeMap(Map const& map) {
            Bytes out;
            writeString(out, 1, map.guid);
            writeBytes(out, 2, encodeGameplayParameters(map.gameplayParameters));
            return out;
        }

        Bytes encodeUser(User const& user) {
            Bytes out;
            writeString(out, 1, user.guid);
            writeString(out, 2, user.name);
            writeString(out, 3, user.platformId);
            writeInt(out, 4, user.clientType);
            writeString(out, 5, user.teamId);
            writeInt(out, 6, static_cast<int32_t>(user.playState));
            writeInt(out, 7, static_cast<int32_t>(user.downloadState));
            for (auto const& mod : user.modList) writeString(out, 8, mod);
            writeInt(out, 10, user.streamDelayMs);
            writeInt(out, 11, user.streamSyncStartMs);
            return out;
        }

        Bytes encodeLeaderboardEntry(LeaderboardEntry const& entry) {
            Bytes out;
            writeString(out, 1, entry.eventId);
            writeString(out, 2, entry.mapId);
            writeString(out, 3, entry.platformId);
            writeString(out, 4, entry.username);
            writeInt(out, 5, entry.multipliedScore);
            writeInt(out, 6, entry.modifiedScore);
            writeInt(out, 7, entry.maxPossibleScore);
            writeDouble(out, 8, entry.accuracy);
            writeInt(out, 9, entry.notesMissed);
            writeInt(out, 10, entry.badCuts);
            writeInt(out, 11, entry.goodCuts);
            writeInt(out, 12, entry.maxCombo);
            writeBool(out, 13, entry.fullCombo);
            writeBool(out, 14, entry.isPlaceholder);
            writeString(out, 15, entry.color);
            return out;
        }

        Bytes encodeScoreTrackerHand(ScoreTrackerHand const& hand) {
            Bytes out;
            writeInt(out, 1, hand.hit);
            writeInt(out, 2, hand.miss);
            writeInt(out, 3, hand.badCut);
            for (float value : hand.avgCut) writeFloat(out, 4, value);
            return out;
        }

        Bytes encodeRealtimeScore(RealtimeScore const& score) {
            Bytes out;
            writeString(out, 1, score.userGuid);
            writeInt(out, 2, score.score);
            writeInt(out, 3, score.scoreWithModifiers);
            writeInt(out, 4, score.maxScore);
            writeInt(out, 5, score.maxScoreWithModifiers);
            writeInt(out, 6, score.combo);
            writeFloat(out, 7, score.playerHealth);
            writeDouble(out, 8, score.accuracy);
            writeFloat(out, 9, score.songPosition);
            writeInt(out, 10, score.notesMissed);
            writeInt(out, 11, score.badCuts);
            writeInt(out, 12, score.bombHits);
            writeInt(out, 13, score.wallHits);
            writeInt(out, 14, score.maxCombo);
            writeBytes(out, 15, encodeScoreTrackerHand(score.leftHand));
            writeBytes(out, 16, encodeScoreTrackerHand(score.rightHand));
            return out;
        }

        Bytes encodeRequest(Request const& request) {
            Bytes out;
            switch (request.kind) {
                case RequestKind::Connect: {
                    Bytes connect;
                    writeInt(connect, 1, request.clientVersion);
                    writeBytes(out, 36, connect);
                    break;
                }
                case RequestKind::Join: {
                    Bytes join;
                    writeString(join, 1, request.tournamentId);
                    writeString(join, 2, request.password);
                    for (auto const& mod : request.modList) writeString(join, 3, mod);
                    writeBytes(out, 37, join);
                    break;
                }
                case RequestKind::UpdateUser: {
                    Bytes update;
                    writeString(update, 1, request.tournamentId);
                    writeBytes(update, 2, encodeUser(request.user));
                    writeBytes(out, 1, update);
                    break;
                }
                case RequestKind::QualifierScores: {
                    Bytes scores;
                    writeString(scores, 1, request.tournamentId);
                    writeString(scores, 2, request.eventId);
                    writeString(scores, 3, request.mapId);
                    writeBytes(out, 38, scores);
                    break;
                }
                case RequestKind::SubmitQualifierScore: {
                    Bytes submit;
                    writeString(submit, 1, request.tournamentId);
                    writeBytes(submit, 2, encodeLeaderboardEntry(request.qualifierScore));
                    writeBytes(submit, 3, encodeGameplayParameters(request.map.gameplayParameters));
                    writeBytes(out, 39, submit);
                    break;
                }
                case RequestKind::RemainingAttempts: {
                    Bytes attempts;
                    writeString(attempts, 1, request.tournamentId);
                    writeString(attempts, 2, request.eventId);
                    writeString(attempts, 3, request.mapId);
                    writeBytes(out, 43, attempts);
                    break;
                }
                default:
                    break;
            }
            return out;
        }

        Bytes encodeResponse(Response const& response) {
            Bytes out;
            writeInt(out, 1, static_cast<int32_t>(response.type));
            writeString(out, 2, response.respondingToPacketId);
            if (response.kind == ResponseKind::LoadSong) {
                Bytes loadSong;
                writeString(loadSong, 1, response.levelId);
                writeString(loadSong, 2, response.message);
                writeBytes(out, 17, loadSong);
            } else if (response.kind == ResponseKind::PreloadImageForStreamSync) {
                Bytes preload;
                writeString(preload, 1, response.fileId);
                writeBytes(out, 18, preload);
            } else if (response.kind == ResponseKind::ShowPrompt) {
                Bytes showPrompt;
                writeString(showPrompt, 1, response.promptValue);
                writeBytes(out, 19, showPrompt);
            }
            return out;
        }

        Bytes encodeSongFinished(SongFinished const& songFinished) {
            Bytes out;
            writeBytes(out, 1, encodeUser(songFinished.player));
            writeBytes(out, 2, encodeBeatmap(songFinished.beatmap));
            writeInt(out, 3, static_cast<int32_t>(songFinished.type));
            writeInt(out, 4, songFinished.score);
            writeInt(out, 5, songFinished.misses);
            writeInt(out, 6, songFinished.badCuts);
            writeInt(out, 7, songFinished.goodCuts);
            writeFloat(out, 8, songFinished.endTime);
            writeString(out, 9, songFinished.tournamentId);
            writeString(out, 10, songFinished.matchId);
            writeInt(out, 11, songFinished.maxScore);
            writeDouble(out, 12, songFinished.accuracy);
            return out;
        }

        Bytes encodePush(Push const& push) {
            Bytes out;
            if (push.kind == PushKind::SongFinished) {
                writeBytes(out, 2, encodeSongFinished(push.songFinished));
            } else if (push.kind == PushKind::RealtimeScore) {
                writeBytes(out, 1, encodeRealtimeScore(push.realtimeScore));
            }
            return out;
        }

        Bytes encodeForwardingPacket(std::vector<std::string> const& recipients, Packet const& inner) {
            Bytes out;
            for (auto const& recipient : recipients) writeString(out, 1, recipient);
            writeBytes(out, 2, encodePacket(inner));
            return out;
        }

        void decodeCharacteristic(Reader& reader, Characteristic& characteristic) {
            uint32_t field = 0, wire = 0;
            while (!reader.eof() && reader.next(field, wire)) {
                uint64_t value = 0;
                switch (field) {
                    case 1:
                        if (wire == kWireLength) reader.readString(characteristic.serializedName);
                        else reader.skip(wire);
                        break;
                    case 2:
                        if (wire == kWireVarint && reader.readVarint(value)) characteristic.difficulties.push_back(int32_t(value));
                        else reader.skip(wire);
                        break;
                    default:
                        reader.skip(wire);
                        break;
                }
            }
        }

        void decodeBeatmap(Reader& reader, Beatmap& beatmap) {
            uint32_t field = 0, wire = 0;
            while (!reader.eof() && reader.next(field, wire)) {
                uint64_t value = 0;
                std::vector<uint8_t> bytes;
                switch (field) {
                    case 1:
                        if (wire == kWireLength) reader.readString(beatmap.name);
                        else reader.skip(wire);
                        break;
                    case 2:
                        if (wire == kWireLength) reader.readString(beatmap.levelId);
                        else reader.skip(wire);
                        break;
                    case 3:
                        if (wire == kWireLength && reader.readBytes(bytes)) {
                            Reader sub(bytes);
                            decodeCharacteristic(sub, beatmap.characteristic);
                        } else reader.skip(wire);
                        break;
                    case 4:
                        if (wire == kWireVarint && reader.readVarint(value)) beatmap.difficulty = int32_t(value);
                        else reader.skip(wire);
                        break;
                    default:
                        reader.skip(wire);
                        break;
                }
            }
        }

        void decodeGameplayModifiers(Reader& reader, GameplayModifiers& modifiers) {
            uint32_t field = 0, wire = 0;
            while (!reader.eof() && reader.next(field, wire)) {
                uint64_t value = 0;
                if (field == 1 && wire == kWireVarint && reader.readVarint(value)) modifiers.options = int32_t(value);
                else reader.skip(wire);
            }
        }

        void decodePlayerSpecificSettings(Reader& reader, PlayerSpecificSettings& settings) {
            uint32_t field = 0, wire = 0;
            while (!reader.eof() && reader.next(field, wire)) {
                uint64_t value = 0;
                switch (field) {
                    case 1:
                        if (wire == kWireFixed32) reader.readFloat(settings.playerHeight); else reader.skip(wire);
                        break;
                    case 2:
                        if (wire == kWireFixed32) reader.readFloat(settings.sfxVolume); else reader.skip(wire);
                        break;
                    case 3:
                        if (wire == kWireFixed32) reader.readFloat(settings.saberTrailIntensity); else reader.skip(wire);
                        break;
                    case 4:
                        if (wire == kWireFixed32) reader.readFloat(settings.noteJumpStartBeatOffset); else reader.skip(wire);
                        break;
                    case 5:
                        if (wire == kWireFixed32) reader.readFloat(settings.noteJumpFixedDuration); else reader.skip(wire);
                        break;
                    case 6:
                        if (wire == kWireVarint && reader.readVarint(value)) settings.options = int32_t(value); else reader.skip(wire);
                        break;
                    case 7:
                        if (wire == kWireVarint && reader.readVarint(value)) settings.noteJumpDurationTypeSettings = int32_t(value); else reader.skip(wire);
                        break;
                    case 8:
                        if (wire == kWireVarint && reader.readVarint(value)) settings.arcVisibilityType = int32_t(value); else reader.skip(wire);
                        break;
                    default:
                        reader.skip(wire);
                        break;
                }
            }
        }

        void decodeGameplayParameters(Reader& reader, GameplayParameters& parameters) {
            uint32_t field = 0, wire = 0;
            while (!reader.eof() && reader.next(field, wire)) {
                uint64_t value = 0;
                std::vector<uint8_t> bytes;
                switch (field) {
                    case 1:
                        if (wire == kWireLength && reader.readBytes(bytes)) { Reader sub(bytes); decodeBeatmap(sub, parameters.beatmap); }
                        else reader.skip(wire);
                        break;
                    case 2:
                        if (wire == kWireLength && reader.readBytes(bytes)) { Reader sub(bytes); decodePlayerSpecificSettings(sub, parameters.playerSettings); }
                        else reader.skip(wire);
                        break;
                    case 3:
                        if (wire == kWireLength && reader.readBytes(bytes)) { Reader sub(bytes); decodeGameplayModifiers(sub, parameters.gameplayModifiers); }
                        else reader.skip(wire);
                        break;
                    case 4:
                        if (wire == kWireVarint && reader.readVarint(value)) parameters.attempts = int32_t(value); else reader.skip(wire);
                        break;
                    case 5:
                        if (wire == kWireVarint && reader.readVarint(value)) parameters.showScoreboard = value != 0; else reader.skip(wire);
                        break;
                    case 6:
                        if (wire == kWireVarint && reader.readVarint(value)) parameters.disablePause = value != 0; else reader.skip(wire);
                        break;
                    case 7:
                        if (wire == kWireVarint && reader.readVarint(value)) parameters.disableFail = value != 0; else reader.skip(wire);
                        break;
                    case 8:
                        if (wire == kWireVarint && reader.readVarint(value)) parameters.disableScoresaberSubmission = value != 0; else reader.skip(wire);
                        break;
                    case 9:
                        if (wire == kWireVarint && reader.readVarint(value)) parameters.disableCustomNotesOnStream = value != 0; else reader.skip(wire);
                        break;
                    case 10:
                        if (wire == kWireVarint && reader.readVarint(value)) parameters.useSync = value != 0; else reader.skip(wire);
                        break;
                    case 11:
                        if (wire == kWireVarint && reader.readVarint(value)) parameters.target = int32_t(value); else reader.skip(wire);
                        break;
                    default:
                        reader.skip(wire);
                        break;
                }
            }
        }

        void decodeMap(Reader& reader, Map& map) {
            uint32_t field = 0, wire = 0;
            while (!reader.eof() && reader.next(field, wire)) {
                std::vector<uint8_t> bytes;
                switch (field) {
                    case 1:
                        if (wire == kWireLength) reader.readString(map.guid); else reader.skip(wire);
                        break;
                    case 2:
                        if (wire == kWireLength && reader.readBytes(bytes)) { Reader sub(bytes); decodeGameplayParameters(sub, map.gameplayParameters); }
                        else reader.skip(wire);
                        break;
                    default:
                        reader.skip(wire);
                        break;
                }
            }
        }

        void decodeUser(Reader& reader, User& user) {
            uint32_t field = 0, wire = 0;
            while (!reader.eof() && reader.next(field, wire)) {
                uint64_t value = 0;
                switch (field) {
                    case 1:
                        if (wire == kWireLength) reader.readString(user.guid); else reader.skip(wire);
                        break;
                    case 2:
                        if (wire == kWireLength) reader.readString(user.name); else reader.skip(wire);
                        break;
                    case 3:
                        if (wire == kWireLength) reader.readString(user.platformId); else reader.skip(wire);
                        break;
                    case 4:
                        if (wire == kWireVarint && reader.readVarint(value)) user.clientType = int32_t(value); else reader.skip(wire);
                        break;
                    case 5:
                        if (wire == kWireLength) reader.readString(user.teamId); else reader.skip(wire);
                        break;
                    case 6:
                        if (wire == kWireVarint && reader.readVarint(value)) user.playState = PlayState(int32_t(value)); else reader.skip(wire);
                        break;
                    case 7:
                        if (wire == kWireVarint && reader.readVarint(value)) user.downloadState = DownloadState(int32_t(value)); else reader.skip(wire);
                        break;
                    case 8: {
                        std::string mod;
                        if (wire == kWireLength && reader.readString(mod)) user.modList.push_back(mod);
                        else reader.skip(wire);
                        break;
                    }
                    case 10:
                        if (wire == kWireVarint && reader.readVarint(value)) user.streamDelayMs = int64_t(value); else reader.skip(wire);
                        break;
                    case 11:
                        if (wire == kWireVarint && reader.readVarint(value)) user.streamSyncStartMs = int64_t(value); else reader.skip(wire);
                        break;
                    default:
                        reader.skip(wire);
                        break;
                }
            }
        }

        void decodeMatch(Reader& reader, Match& match) {
            uint32_t field = 0, wire = 0;
            while (!reader.eof() && reader.next(field, wire)) {
                std::vector<uint8_t> bytes;
                std::string text;
                switch (field) {
                    case 1:
                        if (wire == kWireLength) reader.readString(match.guid); else reader.skip(wire);
                        break;
                    case 2:
                        if (wire == kWireLength && reader.readString(text)) match.associatedUsers.push_back(text); else reader.skip(wire);
                        break;
                    case 3:
                        if (wire == kWireLength) reader.readString(match.leader); else reader.skip(wire);
                        break;
                    case 4:
                        if (wire == kWireLength && reader.readBytes(bytes)) {
                            Map map;
                            Reader sub(bytes);
                            decodeMap(sub, map);
                            match.selectedMap = map;
                        } else reader.skip(wire);
                        break;
                    default:
                        reader.skip(wire);
                        break;
                }
            }
        }

        void decodeQualifierEvent(Reader& reader, QualifierEvent& qualifier) {
            uint32_t field = 0, wire = 0;
            while (!reader.eof() && reader.next(field, wire)) {
                uint64_t value = 0;
                std::vector<uint8_t> bytes;
                switch (field) {
                    case 1:
                        if (wire == kWireLength) reader.readString(qualifier.guid); else reader.skip(wire);
                        break;
                    case 2:
                        if (wire == kWireLength) reader.readString(qualifier.name); else reader.skip(wire);
                        break;
                    case 3:
                        if (wire == kWireLength) reader.readString(qualifier.image); else reader.skip(wire);
                        break;
                    case 5:
                        if (wire == kWireLength && reader.readBytes(bytes)) {
                            Map map;
                            Reader mapReader(bytes);
                            decodeMap(mapReader, map);
                            qualifier.qualifierMaps.push_back(map);
                        } else reader.skip(wire);
                        break;
                    case 6:
                        if (wire == kWireVarint && reader.readVarint(value)) qualifier.flags = int32_t(value); else reader.skip(wire);
                        break;
                    case 7:
                        if (wire == kWireVarint && reader.readVarint(value)) qualifier.sort = QualifierLeaderboardSort(int32_t(value)); else reader.skip(wire);
                        break;
                    default:
                        reader.skip(wire);
                        break;
                }
            }
        }

        void decodeLeaderboardEntry(Reader& reader, LeaderboardEntry& entry) {
            uint32_t field = 0, wire = 0;
            while (!reader.eof() && reader.next(field, wire)) {
                uint64_t value = 0;
                switch (field) {
                    case 1:
                        if (wire == kWireLength) reader.readString(entry.eventId); else reader.skip(wire);
                        break;
                    case 2:
                        if (wire == kWireLength) reader.readString(entry.mapId); else reader.skip(wire);
                        break;
                    case 3:
                        if (wire == kWireLength) reader.readString(entry.platformId); else reader.skip(wire);
                        break;
                    case 4:
                        if (wire == kWireLength) reader.readString(entry.username); else reader.skip(wire);
                        break;
                    case 5:
                        if (wire == kWireVarint && reader.readVarint(value)) entry.multipliedScore = int32_t(value); else reader.skip(wire);
                        break;
                    case 6:
                        if (wire == kWireVarint && reader.readVarint(value)) entry.modifiedScore = int32_t(value); else reader.skip(wire);
                        break;
                    case 7:
                        if (wire == kWireVarint && reader.readVarint(value)) entry.maxPossibleScore = int32_t(value); else reader.skip(wire);
                        break;
                    case 8:
                        if (wire == kWireFixed64) reader.readDouble(entry.accuracy); else reader.skip(wire);
                        break;
                    case 9:
                        if (wire == kWireVarint && reader.readVarint(value)) entry.notesMissed = int32_t(value); else reader.skip(wire);
                        break;
                    case 10:
                        if (wire == kWireVarint && reader.readVarint(value)) entry.badCuts = int32_t(value); else reader.skip(wire);
                        break;
                    case 11:
                        if (wire == kWireVarint && reader.readVarint(value)) entry.goodCuts = int32_t(value); else reader.skip(wire);
                        break;
                    case 12:
                        if (wire == kWireVarint && reader.readVarint(value)) entry.maxCombo = int32_t(value); else reader.skip(wire);
                        break;
                    case 13:
                        if (wire == kWireVarint && reader.readVarint(value)) entry.fullCombo = value != 0; else reader.skip(wire);
                        break;
                    case 14:
                        if (wire == kWireVarint && reader.readVarint(value)) entry.isPlaceholder = value != 0; else reader.skip(wire);
                        break;
                    case 15:
                        if (wire == kWireLength) reader.readString(entry.color); else reader.skip(wire);
                        break;
                    default:
                        reader.skip(wire);
                        break;
                }
            }
        }

        void decodeCoreServer(Reader& reader, CoreServer& server) {
            uint32_t field = 0, wire = 0;
            while (!reader.eof() && reader.next(field, wire)) {
                uint64_t value = 0;
                switch (field) {
                    case 1:
                        if (wire == kWireLength) reader.readString(server.name); else reader.skip(wire);
                        break;
                    case 2:
                        if (wire == kWireLength) reader.readString(server.address); else reader.skip(wire);
                        break;
                    case 3:
                        if (wire == kWireVarint && reader.readVarint(value)) server.port = int32_t(value); else reader.skip(wire);
                        break;
                    case 4:
                        if (wire == kWireVarint && reader.readVarint(value)) server.websocketPort = int32_t(value); else reader.skip(wire);
                        break;
                    default:
                        reader.skip(wire);
                        break;
                }
            }
        }

        void decodeTournamentSettings(Reader& reader, TournamentSettings& settings) {
            uint32_t field = 0, wire = 0;
            while (!reader.eof() && reader.next(field, wire)) {
                uint64_t value = 0;
                std::vector<uint8_t> bytes;
                switch (field) {
                    case 1:
                        if (wire == kWireLength) reader.readString(settings.tournamentName); else reader.skip(wire);
                        break;
                    case 2:
                        if (wire == kWireLength) reader.readString(settings.tournamentImage); else reader.skip(wire);
                        break;
                    case 3:
                        if (wire == kWireVarint && reader.readVarint(value)) settings.enableTeams = value != 0; else reader.skip(wire);
                        break;
                    case 4:
                        if (wire == kWireVarint && reader.readVarint(value)) settings.enablePools = value != 0; else reader.skip(wire);
                        break;
                    case 5: {
                        if (wire == kWireLength && reader.readBytes(bytes)) {
                            Team team;
                            Reader sub(bytes);
                            decodeTeam(sub, team);
                            settings.teams.push_back(team);
                        } else reader.skip(wire);
                        break;
                    }
                    case 6:
                        if (wire == kWireVarint && reader.readVarint(value)) settings.scoreUpdateFrequency = int32_t(value); else reader.skip(wire);
                        break;
                    case 9:
                        if (wire == kWireVarint && reader.readVarint(value)) settings.showTournamentButton = value != 0; else reader.skip(wire);
                        break;
                    case 10:
                        if (wire == kWireVarint && reader.readVarint(value)) settings.showQualifierButton = value != 0; else reader.skip(wire);
                        break;
                    case 11:
                        if (wire == kWireVarint && reader.readVarint(value)) settings.allowUnauthorizedView = value != 0; else reader.skip(wire);
                        break;
                    default:
                        reader.skip(wire);
                        break;
                }
            }
            if (settings.scoreUpdateFrequency <= 0) settings.scoreUpdateFrequency = 30;
        }

        void decodeTeam(Reader& reader, Team& team) {
            uint32_t field = 0, wire = 0;
            while (!reader.eof() && reader.next(field, wire)) {
                switch (field) {
                    case 1:
                        if (wire == kWireLength) reader.readString(team.guid); else reader.skip(wire);
                        break;
                    case 2:
                        if (wire == kWireLength) reader.readString(team.name); else reader.skip(wire);
                        break;
                    case 3:
                        if (wire == kWireLength) reader.readString(team.image); else reader.skip(wire);
                        break;
                    default:
                        reader.skip(wire);
                        break;
                }
            }
        }

        void decodeTournament(Reader& reader, Tournament& tournament) {
            uint32_t field = 0, wire = 0;
            while (!reader.eof() && reader.next(field, wire)) {
                std::vector<uint8_t> bytes;
                switch (field) {
                    case 1:
                        if (wire == kWireLength) reader.readString(tournament.guid); else reader.skip(wire);
                        break;
                    case 2:
                        if (wire == kWireLength && reader.readBytes(bytes)) { Reader sub(bytes); decodeTournamentSettings(sub, tournament.settings); }
                        else reader.skip(wire);
                        break;
                    case 3: {
                        if (wire == kWireLength && reader.readBytes(bytes)) {
                            User user;
                            Reader sub(bytes);
                            decodeUser(sub, user);
                            tournament.users.push_back(user);
                        } else reader.skip(wire);
                        break;
                    }
                    case 4: {
                        if (wire == kWireLength && reader.readBytes(bytes)) {
                            Match match;
                            Reader sub(bytes);
                            decodeMatch(sub, match);
                            tournament.matches.push_back(match);
                        } else reader.skip(wire);
                        break;
                    }
                    case 5: {
                        if (wire == kWireLength && reader.readBytes(bytes)) {
                            QualifierEvent qualifier;
                            Reader sub(bytes);
                            decodeQualifierEvent(sub, qualifier);
                            tournament.qualifiers.push_back(qualifier);
                        } else reader.skip(wire);
                        break;
                    }
                    case 6:
                        if (wire == kWireLength && reader.readBytes(bytes)) { Reader sub(bytes); decodeCoreServer(sub, tournament.server); }
                        else reader.skip(wire);
                        break;
                    default:
                        reader.skip(wire);
                        break;
                }
            }
        }

        void decodeState(Reader& reader, State& state) {
            uint32_t field = 0, wire = 0;
            while (!reader.eof() && reader.next(field, wire)) {
                std::vector<uint8_t> bytes;
                if (field == 1 && wire == kWireLength && reader.readBytes(bytes)) {
                    Tournament tournament;
                    Reader sub(bytes);
                    decodeTournament(sub, tournament);
                    state.tournaments.push_back(tournament);
                } else if (field == 2 && wire == kWireLength && reader.readBytes(bytes)) {
                    CoreServer server;
                    Reader sub(bytes);
                    decodeCoreServer(sub, server);
                    state.knownServers.push_back(server);
                } else {
                    reader.skip(wire);
                }
            }
        }

        void decodeCommand(Reader& reader, Command& command) {
            uint32_t field = 0, wire = 0;
            while (!reader.eof() && reader.next(field, wire)) {
                std::vector<uint8_t> bytes;
                std::string text;
                switch (field) {
                    case 2:
                        command.kind = CommandKind::ReturnToMenu;
                        reader.skip(wire);
                        break;
                    case 3:
                        command.kind = CommandKind::DelayTestFinish;
                        reader.skip(wire);
                        break;
                    case 4:
                        command.kind = CommandKind::StreamSyncShowImage;
                        reader.skip(wire);
                        break;
                    case 6:
                        command.kind = CommandKind::PlaySong;
                        if (wire == kWireLength && reader.readBytes(bytes)) {
                            Reader play(bytes);
                            uint32_t subField = 0, subWire = 0;
                            while (!play.eof() && play.next(subField, subWire)) {
                                std::vector<uint8_t> params;
                                if (subField == 1 && subWire == kWireLength && play.readBytes(params)) {
                                    Reader paramReader(params);
                                    decodeGameplayParameters(paramReader, command.playSong);
                                } else {
                                    play.skip(subWire);
                                }
                            }
                        } else reader.skip(wire);
                        break;
                    case 9:
                        command.kind = CommandKind::DiscordAuthorize;
                        if (wire == kWireLength) reader.readString(command.discordAuthorize); else reader.skip(wire);
                        break;
                    case 10:
                        command.kind = CommandKind::ModifyGameplay;
                        if (wire == kWireLength && reader.readBytes(bytes)) {
                            Reader modify(bytes);
                            uint32_t subField = 0, subWire = 0;
                            while (!modify.eof() && modify.next(subField, subWire)) {
                                uint64_t value = 0;
                                if (subField == 1 && subWire == kWireVarint && modify.readVarint(value)) {
                                    command.modifier = GameplayModifierCommand(int32_t(value));
                                } else {
                                    modify.skip(subWire);
                                }
                            }
                        } else reader.skip(wire);
                        break;
                    case 11:
                        if (wire == kWireLength) reader.readString(command.tournamentId); else reader.skip(wire);
                        break;
                    case 12:
                        if (wire == kWireLength && reader.readString(text)) command.forwardTo.push_back(text); else reader.skip(wire);
                        break;
                    case 13:
                        command.kind = CommandKind::ShowColorForStreamSync;
                        if (wire == kWireLength && reader.readBytes(bytes)) {
                            Reader color(bytes);
                            uint32_t subField = 0, subWire = 0;
                            while (!color.eof() && color.next(subField, subWire)) {
                                if (subField == 1 && subWire == kWireLength) {
                                    color.readString(command.streamSyncColor);
                                } else {
                                    color.skip(subWire);
                                }
                            }
                        } else reader.skip(wire);
                        break;
                    default:
                        reader.skip(wire);
                        break;
                }
            }
        }

        void decodeRequest(Reader& reader, Request& request) {
            uint32_t field = 0, wire = 0;
            while (!reader.eof() && reader.next(field, wire)) {
                std::vector<uint8_t> bytes;
                if (field == 40 && wire == kWireLength && reader.readBytes(bytes)) {
                    request.kind = RequestKind::LoadSong;
                    Reader load(bytes);
                    uint32_t subField = 0, subWire = 0;
                    while (!load.eof() && load.next(subField, subWire)) {
                        std::string text;
                        switch (subField) {
                            case 1:
                                if (subWire == kWireLength) load.readString(request.levelId); else load.skip(subWire);
                                break;
                            case 2:
                                if (subWire == kWireLength) load.readString(request.customHostUrl); else load.skip(subWire);
                                break;
                            case 3:
                                if (subWire == kWireLength) load.readString(request.tournamentId); else load.skip(subWire);
                                break;
                            case 4:
                                if (subWire == kWireLength && load.readString(text)) request.forwardTo.push_back(text); else load.skip(subWire);
                                break;
                            default:
                                load.skip(subWire);
                                break;
                        }
                    }
                } else if (field == 41 && wire == kWireLength && reader.readBytes(bytes)) {
                    request.kind = RequestKind::PreloadImageForStreamSync;
                    Reader preload(bytes);
                    uint32_t subField = 0, subWire = 0;
                    while (!preload.eof() && preload.next(subField, subWire)) {
                        uint64_t value = 0;
                        std::string text;
                        switch (subField) {
                            case 1:
                                if (subWire == kWireLength) preload.readString(request.fileId); else preload.skip(subWire);
                                break;
                            case 2:
                                if (subWire == kWireVarint && preload.readVarint(value)) request.compressed = value != 0; else preload.skip(subWire);
                                break;
                            case 3:
                                if (subWire == kWireLength) preload.readBytes(request.data); else preload.skip(subWire);
                                break;
                            case 4:
                                if (subWire == kWireLength) preload.readString(request.tournamentId); else preload.skip(subWire);
                                break;
                            case 5:
                                if (subWire == kWireLength && preload.readString(text)) request.forwardTo.push_back(text); else preload.skip(subWire);
                                break;
                            default:
                                preload.skip(subWire);
                                break;
                        }
                    }
                } else if (field == 42 && wire == kWireLength && reader.readBytes(bytes)) {
                    request.kind = RequestKind::ShowPrompt;
                    Reader prompt(bytes);
                    uint32_t subField = 0, subWire = 0;
                    while (!prompt.eof() && prompt.next(subField, subWire)) {
                        std::vector<uint8_t> optionBytes;
                        std::string text;
                        uint64_t value = 0;
                        switch (subField) {
                            case 1:
                                if (subWire == kWireLength) prompt.readString(request.prompt.promptId); else prompt.skip(subWire);
                                break;
                            case 2:
                                if (subWire == kWireLength) prompt.readString(request.prompt.title); else prompt.skip(subWire);
                                break;
                            case 3:
                                if (subWire == kWireLength) prompt.readString(request.prompt.text); else prompt.skip(subWire);
                                break;
                            case 4:
                                if (subWire == kWireVarint && prompt.readVarint(value)) request.prompt.timeout = int32_t(value); else prompt.skip(subWire);
                                break;
                            case 5:
                                if (subWire == kWireVarint && prompt.readVarint(value)) request.prompt.showTimer = value != 0; else prompt.skip(subWire);
                                break;
                            case 6:
                                if (subWire == kWireVarint && prompt.readVarint(value)) request.prompt.canClose = value != 0; else prompt.skip(subWire);
                                break;
                            case 7:
                                if (subWire == kWireLength && prompt.readBytes(optionBytes)) {
                                    PromptOption option;
                                    Reader optionReader(optionBytes);
                                    uint32_t optionField = 0, optionWire = 0;
                                    while (!optionReader.eof() && optionReader.next(optionField, optionWire)) {
                                        if (optionField == 1 && optionWire == kWireLength) optionReader.readString(option.label);
                                        else if (optionField == 2 && optionWire == kWireLength) optionReader.readString(option.value);
                                        else optionReader.skip(optionWire);
                                    }
                                    request.prompt.options.push_back(option);
                                } else prompt.skip(subWire);
                                break;
                            case 8:
                                if (subWire == kWireLength) prompt.readString(request.tournamentId); else prompt.skip(subWire);
                                break;
                            case 9:
                                if (subWire == kWireLength && prompt.readString(text)) request.forwardTo.push_back(text); else prompt.skip(subWire);
                                break;
                            default:
                                prompt.skip(subWire);
                                break;
                        }
                    }
                } else {
                    reader.skip(wire);
                }
            }
        }

        void decodeResponse(Reader& reader, Response& response) {
            uint32_t field = 0, wire = 0;
            while (!reader.eof() && reader.next(field, wire)) {
                uint64_t value = 0;
                std::vector<uint8_t> bytes;
                switch (field) {
                    case 1:
                        if (wire == kWireVarint && reader.readVarint(value)) response.type = ResponseType(int32_t(value)); else reader.skip(wire);
                        break;
                    case 2:
                        if (wire == kWireLength) reader.readString(response.respondingToPacketId); else reader.skip(wire);
                        break;
                    case 14:
                        response.kind = ResponseKind::Connect;
                        if (wire == kWireLength && reader.readBytes(bytes)) {
                            Reader connect(bytes);
                            uint32_t subField = 0, subWire = 0;
                            while (!connect.eof() && connect.next(subField, subWire)) {
                                if (subField == 1 && subWire == kWireLength) {
                                    std::vector<uint8_t> stateBytes;
                                    connect.readBytes(stateBytes);
                                    Reader stateReader(stateBytes);
                                    decodeState(stateReader, response.state);
                                } else if (subField == 2 && subWire == kWireVarint && connect.readVarint(value)) {
                                    response.serverVersion = int32_t(value);
                                } else if (subField == 3 && subWire == kWireLength) {
                                    connect.readString(response.message);
                                } else {
                                    connect.skip(subWire);
                                }
                            }
                        } else reader.skip(wire);
                        break;
                    case 15:
                        response.kind = ResponseKind::Join;
                        if (wire == kWireLength && reader.readBytes(bytes)) {
                            Reader join(bytes);
                            uint32_t subField = 0, subWire = 0;
                            while (!join.eof() && join.next(subField, subWire)) {
                                if (subField == 1 && subWire == kWireLength) {
                                    std::vector<uint8_t> stateBytes;
                                    join.readBytes(stateBytes);
                                    Reader stateReader(stateBytes);
                                    decodeState(stateReader, response.state);
                                } else if (subField == 2 && subWire == kWireLength) {
                                    join.readString(response.selfGuid);
                                } else if (subField == 3 && subWire == kWireLength) {
                                    join.readString(response.tournamentId);
                                } else if (subField == 4 && subWire == kWireLength) {
                                    join.readString(response.message);
                                } else {
                                    join.skip(subWire);
                                }
                            }
                        } else reader.skip(wire);
                        break;
                    case 16:
                        response.kind = ResponseKind::LeaderboardEntries;
                        if (wire == kWireLength && reader.readBytes(bytes)) {
                            Reader leaderboard(bytes);
                            uint32_t subField = 0, subWire = 0;
                            while (!leaderboard.eof() && leaderboard.next(subField, subWire)) {
                                std::vector<uint8_t> scoreBytes;
                                if (subField == 1 && subWire == kWireLength && leaderboard.readBytes(scoreBytes)) {
                                    LeaderboardEntry entry;
                                    Reader scoreReader(scoreBytes);
                                    decodeLeaderboardEntry(scoreReader, entry);
                                    response.leaderboardEntries.push_back(entry);
                                } else {
                                    leaderboard.skip(subWire);
                                }
                            }
                        } else reader.skip(wire);
                        break;
                    case 17:
                        response.kind = ResponseKind::LoadSong;
                        if (wire == kWireLength && reader.readBytes(bytes)) {
                            Reader load(bytes);
                            uint32_t subField = 0, subWire = 0;
                            while (!load.eof() && load.next(subField, subWire)) {
                                if (subField == 1 && subWire == kWireLength) load.readString(response.levelId);
                                else if (subField == 2 && subWire == kWireLength) load.readString(response.message);
                                else load.skip(subWire);
                            }
                        } else reader.skip(wire);
                        break;
                    case 18:
                        response.kind = ResponseKind::PreloadImageForStreamSync;
                        if (wire == kWireLength && reader.readBytes(bytes)) {
                            Reader preload(bytes);
                            uint32_t subField = 0, subWire = 0;
                            while (!preload.eof() && preload.next(subField, subWire)) {
                                if (subField == 1 && subWire == kWireLength) preload.readString(response.fileId);
                                else preload.skip(subWire);
                            }
                        } else reader.skip(wire);
                        break;
                    case 19:
                        response.kind = ResponseKind::ShowPrompt;
                        if (wire == kWireLength && reader.readBytes(bytes)) {
                            Reader prompt(bytes);
                            uint32_t subField = 0, subWire = 0;
                            while (!prompt.eof() && prompt.next(subField, subWire)) {
                                if (subField == 1 && subWire == kWireLength) prompt.readString(response.promptValue);
                                else prompt.skip(subWire);
                            }
                        } else reader.skip(wire);
                        break;
                    case 20:
                        response.kind = ResponseKind::RemainingAttempts;
                        if (wire == kWireLength && reader.readBytes(bytes)) {
                            Reader attempts(bytes);
                            uint32_t subField = 0, subWire = 0;
                            while (!attempts.eof() && attempts.next(subField, subWire)) {
                                if (subField == 1 && subWire == kWireVarint && attempts.readVarint(value)) {
                                    response.remainingAttempts = int32_t(value);
                                } else {
                                    attempts.skip(subWire);
                                }
                            }
                        } else reader.skip(wire);
                        break;
                    case 30:
                        if (wire == kWireLength && reader.readBytes(bytes)) {
                            Reader permission(bytes);
                            uint32_t subField = 0, subWire = 0;
                            while (!permission.eof() && permission.next(subField, subWire)) {
                                if (subField == 1 && subWire == kWireLength) permission.readString(response.permissionRequired);
                                else if (subField == 2 && subWire == kWireLength) permission.readString(response.permissionRoles);
                                else if (subField == 3 && subWire == kWireLength) permission.readString(response.permissionPermissions);
                                else permission.skip(subWire);
                            }
                            if (!response.permissionRequired.empty()) {
                                response.message = "Missing permission: " + response.permissionRequired;
                            }
                        } else reader.skip(wire);
                        break;
                    default:
                        reader.skip(wire);
                        break;
                }
            }
        }

        void decodeEvent(Reader& reader, Event& event) {
            uint32_t field = 0, wire = 0;
            while (!reader.eof() && reader.next(field, wire)) {
                std::vector<uint8_t> bytes;
                if (wire != kWireLength || !reader.readBytes(bytes)) {
                    reader.skip(wire);
                    continue;
                }

                Reader wrapper(bytes);
                auto readTournamentAndUser = [&](User& user) {
                    uint32_t subField = 0, subWire = 0;
                    while (!wrapper.eof() && wrapper.next(subField, subWire)) {
                        std::vector<uint8_t> subBytes;
                        if (subField == 1 && subWire == kWireLength) wrapper.readString(event.tournamentId);
                        else if (subField == 2 && subWire == kWireLength && wrapper.readBytes(subBytes)) {
                            Reader userReader(subBytes);
                            decodeUser(userReader, user);
                        } else wrapper.skip(subWire);
                    }
                };

                auto readTournamentAndMatch = [&](Match& match) {
                    uint32_t subField = 0, subWire = 0;
                    while (!wrapper.eof() && wrapper.next(subField, subWire)) {
                        std::vector<uint8_t> subBytes;
                        if (subField == 1 && subWire == kWireLength) wrapper.readString(event.tournamentId);
                        else if (subField == 2 && subWire == kWireLength && wrapper.readBytes(subBytes)) {
                            Reader matchReader(subBytes);
                            decodeMatch(matchReader, match);
                        } else wrapper.skip(subWire);
                    }
                };

                auto readTournamentAndQualifier = [&](QualifierEvent& qualifier) {
                    uint32_t subField = 0, subWire = 0;
                    while (!wrapper.eof() && wrapper.next(subField, subWire)) {
                        std::vector<uint8_t> subBytes;
                        if (subField == 1 && subWire == kWireLength) wrapper.readString(event.tournamentId);
                        else if (subField == 2 && subWire == kWireLength && wrapper.readBytes(subBytes)) {
                            Reader qualifierReader(subBytes);
                            decodeQualifierEvent(qualifierReader, qualifier);
                        } else wrapper.skip(subWire);
                    }
                };

                switch (field) {
                    case 1:
                        event.kind = EventKind::UserAdded;
                        readTournamentAndUser(event.user);
                        break;
                    case 2:
                        event.kind = EventKind::UserUpdated;
                        readTournamentAndUser(event.user);
                        break;
                    case 3:
                        event.kind = EventKind::UserLeft;
                        readTournamentAndUser(event.user);
                        break;
                    case 6:
                        event.kind = EventKind::MatchCreated;
                        readTournamentAndMatch(event.match);
                        break;
                    case 7:
                        event.kind = EventKind::MatchUpdated;
                        readTournamentAndMatch(event.match);
                        break;
                    case 8:
                        event.kind = EventKind::MatchDeleted;
                        readTournamentAndMatch(event.match);
                        break;
                    case 9:
                        event.kind = EventKind::QualifierCreated;
                        readTournamentAndQualifier(event.qualifier);
                        break;
                    case 10:
                        event.kind = EventKind::QualifierUpdated;
                        readTournamentAndQualifier(event.qualifier);
                        break;
                    case 11:
                        event.kind = EventKind::QualifierDeleted;
                        readTournamentAndQualifier(event.qualifier);
                        break;
                    case 12:
                        event.kind = EventKind::TournamentCreated;
                        while (!wrapper.eof()) {
                            uint32_t subField = 0, subWire = 0;
                            std::vector<uint8_t> subBytes;
                            if (!wrapper.next(subField, subWire)) break;
                            if (subField == 1 && subWire == kWireLength && wrapper.readBytes(subBytes)) {
                                Reader tournamentReader(subBytes);
                                decodeTournament(tournamentReader, event.tournament);
                            } else wrapper.skip(subWire);
                        }
                        break;
                    case 13:
                        event.kind = EventKind::TournamentUpdated;
                        while (!wrapper.eof()) {
                            uint32_t subField = 0, subWire = 0;
                            std::vector<uint8_t> subBytes;
                            if (!wrapper.next(subField, subWire)) break;
                            if (subField == 1 && subWire == kWireLength && wrapper.readBytes(subBytes)) {
                                Reader tournamentReader(subBytes);
                                decodeTournament(tournamentReader, event.tournament);
                            } else wrapper.skip(subWire);
                        }
                        break;
                    case 14:
                        event.kind = EventKind::TournamentDeleted;
                        while (!wrapper.eof()) {
                            uint32_t subField = 0, subWire = 0;
                            std::vector<uint8_t> subBytes;
                            if (!wrapper.next(subField, subWire)) break;
                            if (subField == 1 && subWire == kWireLength && wrapper.readBytes(subBytes)) {
                                Reader tournamentReader(subBytes);
                                decodeTournament(tournamentReader, event.tournament);
                            } else wrapper.skip(subWire);
                        }
                        break;
                    default:
                        break;
                }
            }
        }
    }

    std::string makePacketId() {
        static std::mt19937_64 rng(std::random_device{}());
        std::array<uint8_t, 16> bytes{};
        for (auto& byte : bytes) byte = uint8_t(rng() & 0xff);
        bytes[6] = uint8_t((bytes[6] & 0x0f) | 0x40);
        bytes[8] = uint8_t((bytes[8] & 0x3f) | 0x80);

        std::ostringstream stream;
        stream << std::hex;
        for (size_t i = 0; i < bytes.size(); ++i) {
            if (i == 4 || i == 6 || i == 8 || i == 10) stream << '-';
            stream.width(2);
            stream.fill('0');
            stream << int(bytes[i]);
        }
        return stream.str();
    }

    Bytes encodePacket(Packet const& packet) {
        Bytes out;
        writeString(out, 1, packet.token);
        writeString(out, 2, packet.id);
        writeString(out, 3, packet.from);

        switch (packet.kind) {
            case PacketKind::Heartbeat:
                writeBool(out, 11, true);
                break;
            case PacketKind::Request:
                writeBytes(out, 8, encodeRequest(packet.request));
                break;
            case PacketKind::Push:
                writeBytes(out, 7, encodePush(packet.push));
                break;
            case PacketKind::Response:
                writeBytes(out, 9, encodeResponse(packet.response));
                break;
            case PacketKind::ForwardingPacket:
                if (packet.push.kind != PushKind::None) {
                    writeBytes(out, 5, encodeForwardingPacket(packet.request.forwardTo, Packet{
                        .token = packet.token,
                        .id = packet.id,
                        .from = packet.from,
                        .kind = PacketKind::Push,
                        .push = packet.push
                    }));
                } else {
                    writeBytes(out, 5, encodeForwardingPacket(packet.request.forwardTo, Packet{
                        .token = packet.token,
                        .id = packet.id,
                        .from = packet.from,
                        .kind = PacketKind::Response,
                        .response = packet.response
                    }));
                }
                break;
            default:
                break;
        }

        return out;
    }

    std::optional<Packet> decodePacket(std::vector<uint8_t> const& payload) {
        Packet packet;
        Reader reader(payload);
        uint32_t field = 0, wire = 0;
        while (!reader.eof() && reader.next(field, wire)) {
            std::vector<uint8_t> bytes;
            uint64_t value = 0;
            switch (field) {
                case 1:
                    if (wire == kWireLength) reader.readString(packet.token); else reader.skip(wire);
                    break;
                case 2:
                    if (wire == kWireLength) reader.readString(packet.id); else reader.skip(wire);
                    break;
                case 3:
                    if (wire == kWireLength) reader.readString(packet.from); else reader.skip(wire);
                    break;
                case 6:
                    packet.kind = PacketKind::Command;
                    if (wire == kWireLength && reader.readBytes(bytes)) { Reader sub(bytes); decodeCommand(sub, packet.command); }
                    else reader.skip(wire);
                    break;
                case 8:
                    packet.kind = PacketKind::Request;
                    if (wire == kWireLength && reader.readBytes(bytes)) { Reader sub(bytes); decodeRequest(sub, packet.request); }
                    else reader.skip(wire);
                    break;
                case 9:
                    packet.kind = PacketKind::Response;
                    if (wire == kWireLength && reader.readBytes(bytes)) { Reader sub(bytes); decodeResponse(sub, packet.response); }
                    else reader.skip(wire);
                    break;
                case 10:
                    packet.kind = PacketKind::Event;
                    if (wire == kWireLength && reader.readBytes(bytes)) { Reader sub(bytes); decodeEvent(sub, packet.event); }
                    else reader.skip(wire);
                    break;
                case 11:
                    packet.kind = PacketKind::Heartbeat;
                    if (wire == kWireVarint && reader.readVarint(value)) packet.heartbeat = value != 0;
                    else reader.skip(wire);
                    break;
                default:
                    reader.skip(wire);
                    break;
            }
        }
        return packet;
    }

    std::vector<uint8_t> wrapPacket(Packet const& packet) {
        auto payload = encodePacket(packet);
        std::vector<uint8_t> out{'m', 'o', 'o', 'n'};
        uint32_t size = uint32_t(payload.size());
        out.push_back(uint8_t(size & 0xff));
        out.push_back(uint8_t((size >> 8) & 0xff));
        out.push_back(uint8_t((size >> 16) & 0xff));
        out.push_back(uint8_t((size >> 24) & 0xff));
        out.insert(out.end(), payload.begin(), payload.end());
        return out;
    }

    std::optional<Packet> unwrapPacket(std::vector<uint8_t> const& payload) {
        return decodePacket(payload);
    }
}
