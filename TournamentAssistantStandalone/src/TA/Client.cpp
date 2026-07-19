#include "TA/Client.hpp"

#include "TA/Constants.hpp"
#include "TA/AntiPause.hpp"
#include "TA/MidPlayModifiers.hpp"
#include "TA/Protobuf.hpp"
#include "TA/Song.hpp"
#include "TA/StreamSync.hpp"
#include "main.hpp"

#include "bsml/shared/BSML/MainThreadScheduler.hpp"

#include <algorithm>
#include <array>
#include <chrono>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <set>
#include <sstream>
#include <thread>

namespace TA {
    namespace {
        constexpr size_t kHeaderSize = 8;
        constexpr char kMagic[] = {'m', 'o', 'o', 'n'};
        constexpr char const* kScoreSaberAuthPath = "/sdcard/ModData/com.beatgames.beatsaber/Mods/ScoreSaber/scoresaber_DO_NOT_SHARE.scary";

        std::once_flag curlInitFlag;
        std::mutex authTokenMutex;
        std::string beatKhanaGameToken;
        std::string beatKhanaPlatformId;
        std::string beatKhanaPlatformUsername;

        void ensureCurlInitialized() {
            std::call_once(curlInitFlag, [] {
                PaperLogger.info("Initializing curl for TA TLS socket");
                curl_global_init(CURL_GLOBAL_DEFAULT);
            });
        }

        bool sendAll(CURL*& curl, std::mutex& ioMutex, std::atomic<bool> const& stop, std::vector<uint8_t> const& bytes) {
            size_t sent = 0;
            while (sent < bytes.size()) {
                size_t bytesSent = 0;
                CURLcode result;
                {
                    std::scoped_lock lock(ioMutex);
                    if (!curl) return false;
                    result = curl_easy_send(curl, bytes.data() + sent, bytes.size() - sent, &bytesSent);
                }

                if (result == CURLE_AGAIN) {
                    if (stop) return false;
                    std::this_thread::sleep_for(std::chrono::milliseconds(10));
                    continue;
                }

                if (result != CURLE_OK) {
                    PaperLogger.error("curl_easy_send failed: {}", curl_easy_strerror(result));
                    return false;
                }

                if (bytesSent == 0) {
                    PaperLogger.warn("curl_easy_send wrote 0 bytes");
                    return false;
                }

                sent += bytesSent;
            }
            return true;
        }

        uint32_t readLittleEndian32(std::vector<uint8_t> const& bytes, size_t offset) {
            return uint32_t(bytes[offset]) |
                   (uint32_t(bytes[offset + 1]) << 8) |
                   (uint32_t(bytes[offset + 2]) << 16) |
                   (uint32_t(bytes[offset + 3]) << 24);
        }

        bool startsWithMagic(std::vector<uint8_t> const& bytes) {
            return bytes.size() >= 4 && std::equal(std::begin(kMagic), std::end(kMagic), bytes.begin());
        }

        size_t curlWrite(void* contents, size_t size, size_t nmemb, void* userdata) {
            auto* out = static_cast<std::string*>(userdata);
            out->append(static_cast<char*>(contents), size * nmemb);
            return size * nmemb;
        }

        std::string jsonEscape(std::string const& input) {
            std::ostringstream out;
            for (char ch : input) {
                switch (ch) {
                    case '\\': out << "\\\\"; break;
                    case '"': out << "\\\""; break;
                    case '\n': out << "\\n"; break;
                    case '\r': out << "\\r"; break;
                    case '\t': out << "\\t"; break;
                    default: out << ch; break;
                }
            }
            return out.str();
        }

        std::string readTextFile(std::string const& path) {
            std::ifstream file(path);
            if (!file) return {};
            std::ostringstream data;
            data << file.rdbuf();
            return data.str();
        }

        std::string extractJsonString(std::string const& json, std::string const& key) {
            auto marker = "\"" + key + "\"";
            auto keyPos = json.find(marker);
            if (keyPos == std::string::npos) return {};
            auto colon = json.find(':', keyPos + marker.size());
            if (colon == std::string::npos) return {};
            auto start = json.find('"', colon + 1);
            if (start == std::string::npos) return {};
            std::string value;
            bool escaping = false;
            for (size_t i = start + 1; i < json.size(); ++i) {
                char ch = json[i];
                if (escaping) {
                    value.push_back(ch);
                    escaping = false;
                    continue;
                }
                if (ch == '\\') {
                    escaping = true;
                    continue;
                }
                if (ch == '"') return value;
                value.push_back(ch);
            }
            return {};
        }

        int base64Value(char ch) {
            if (ch >= 'A' && ch <= 'Z') return ch - 'A';
            if (ch >= 'a' && ch <= 'z') return ch - 'a' + 26;
            if (ch >= '0' && ch <= '9') return ch - '0' + 52;
            if (ch == '+') return 62;
            if (ch == '/') return 63;
            return -1;
        }

        std::string decodeBase64Url(std::string input) {
            for (auto& ch : input) {
                if (ch == '-') ch = '+';
                else if (ch == '_') ch = '/';
            }

            std::string out;
            int value = 0;
            int bits = -8;
            for (char ch : input) {
                if (ch == '=') break;
                auto decoded = base64Value(ch);
                if (decoded < 0) return {};
                value = (value << 6) + decoded;
                bits += 6;
                if (bits >= 0) {
                    out.push_back(char((value >> bits) & 0xff));
                    bits -= 8;
                }
            }
            return out;
        }

        std::string jwtPayloadJson(std::string const& token) {
            auto firstDot = token.find('.');
            if (firstDot == std::string::npos) return {};
            auto secondDot = token.find('.', firstDot + 1);
            if (secondDot == std::string::npos) return {};
            return decodeBase64Url(token.substr(firstDot + 1, secondDot - firstDot - 1));
        }

        void trimInPlace(std::string& value) {
            auto isTrim = [](unsigned char ch) {
                return ch == ' ' || ch == '\n' || ch == '\r' || ch == '\t';
            };
            value.erase(value.begin(), std::find_if(value.begin(), value.end(), [&](unsigned char ch) { return !isTrim(ch); }));
            value.erase(std::find_if(value.rbegin(), value.rend(), [&](unsigned char ch) { return !isTrim(ch); }).base(), value.end());
        }

        bool fetchPlainText(std::string const& url, long timeoutSeconds, std::string& responseBody) {
            ensureCurlInitialized();
            responseBody.clear();

            auto* curl = curl_easy_init();
            if (!curl) {
                PaperLogger.warn("Plain text fetch failed for '{}' because curl_easy_init failed", url);
                return false;
            }

            curl_easy_setopt(curl, CURLOPT_URL, url.c_str());
            curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 1L);
            curl_easy_setopt(curl, CURLOPT_TIMEOUT, timeoutSeconds);
            curl_easy_setopt(curl, CURLOPT_SSL_VERIFYPEER, 0L);
            curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, curlWrite);
            curl_easy_setopt(curl, CURLOPT_WRITEDATA, &responseBody);
            curl_easy_setopt(curl, CURLOPT_USERAGENT, "TournamentAssistant Standalone");

            auto result = curl_easy_perform(curl);
            long httpCode = 0;
            curl_easy_getinfo(curl, CURLINFO_RESPONSE_CODE, &httpCode);
            curl_easy_cleanup(curl);

            if (result != CURLE_OK || httpCode < 200 || httpCode >= 300) {
                PaperLogger.warn("Plain text fetch failed url='{}' curl='{}' http={} body='{}'", url, curl_easy_strerror(result), httpCode, responseBody);
                return false;
            }

            trimInPlace(responseBody);
            return !responseBody.empty();
        }

        bool standaloneVersionAllowed(std::string& latestVersion) {
            if (!fetchPlainText(kLatestStandaloneVersionUrl, 5L, latestVersion)) {
                PaperLogger.warn("Standalone latest-version check failed; allowing auth to continue");
                latestVersion.clear();
                return true;
            }

            auto currentVersion = std::string(VERSION);
            if (latestVersion == currentVersion) {
                PaperLogger.info("Standalone version check passed current='{}' latest='{}'", currentVersion, latestVersion);
                return true;
            }

            PaperLogger.warn("Standalone version check failed current='{}' latest='{}'", currentVersion, latestVersion);
            return false;
        }

        std::string tokenForPackets() {
            std::scoped_lock lock(authTokenMutex);
            return beatKhanaGameToken;
        }

        std::pair<std::string, std::string> authIdentityForScores() {
            std::scoped_lock lock(authTokenMutex);
            return {beatKhanaPlatformId, beatKhanaPlatformUsername.empty() ? beatKhanaPlatformId : beatKhanaPlatformUsername};
        }

        void clearBeatKhanaGameToken() {
            std::scoped_lock lock(authTokenMutex);
            beatKhanaGameToken.clear();
            beatKhanaPlatformId.clear();
            beatKhanaPlatformUsername.clear();
        }

        bool refreshBeatKhanaGameToken() {
            ensureCurlInitialized();
            clearBeatKhanaGameToken();

            PaperLogger.info("Reading ScoreSaber auth proof from '{}'", kScoreSaberAuthPath);
            auto authFile = readTextFile(kScoreSaberAuthPath);
            std::string proofPlayerId;
            std::string proofNonce;
            auto separator = authFile.find(':');
            if (separator != std::string::npos) {
                proofNonce = authFile.substr(0, separator);
                proofPlayerId = authFile.substr(separator + 1);
                trimInPlace(proofNonce);
                trimInPlace(proofPlayerId);
            }
            PaperLogger.info("ScoreSaber proof parse result hasFile={} hasNonce={} hasPlayerId={} playerId='{}'", !authFile.empty(), !proofNonce.empty(), !proofPlayerId.empty(), proofPlayerId);
            if (proofPlayerId.empty() || proofNonce.empty()) {
                PaperLogger.error("BeatKhana game auth cannot continue because ScoreSaber proof data is missing or malformed");
                return false;
            }

            std::ostringstream body;
            body << "{"
                 << "\"platform\":\"scoresaber\","
                 << "\"platformId\":\"" << jsonEscape(proofPlayerId) << "\","
                 << "\"platformUsername\":\"" << jsonEscape(proofPlayerId) << "\","
                 << "\"proofProvider\":\"scoresaber\","
                 << "\"proofPlayerId\":\"" << jsonEscape(proofPlayerId) << "\","
                 << "\"proofNonce\":\"" << jsonEscape(proofNonce) << "\"";
            body << "}";

            std::string responseBody;
            auto* curl = curl_easy_init();
            if (!curl) {
                PaperLogger.error("BeatKhana game auth failed because curl_easy_init failed");
                return false;
            }

            struct curl_slist* headers = nullptr;
            headers = curl_slist_append(headers, "Content-Type: application/json");
            curl_easy_setopt(curl, CURLOPT_URL, kBeatKhanaGameAuthUrl);
            curl_easy_setopt(curl, CURLOPT_POST, 1L);
            auto postBody = body.str();
            curl_easy_setopt(curl, CURLOPT_POSTFIELDS, postBody.c_str());
            curl_easy_setopt(curl, CURLOPT_POSTFIELDSIZE, long(postBody.size()));
            curl_easy_setopt(curl, CURLOPT_HTTPHEADER, headers);
            curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 1L);
            curl_easy_setopt(curl, CURLOPT_TIMEOUT, 10L);
            curl_easy_setopt(curl, CURLOPT_SSL_VERIFYPEER, 0L);
            curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, curlWrite);
            curl_easy_setopt(curl, CURLOPT_WRITEDATA, &responseBody);
            curl_easy_setopt(curl, CURLOPT_USERAGENT, "TournamentAssistant Standalone");

            PaperLogger.info("BeatKhana game auth POST endpoint='{}' body='{}'", kBeatKhanaGameAuthUrl, postBody);
            auto result = curl_easy_perform(curl);
            long httpCode = 0;
            curl_easy_getinfo(curl, CURLINFO_RESPONSE_CODE, &httpCode);
            curl_slist_free_all(headers);
            curl_easy_cleanup(curl);
            PaperLogger.info("BeatKhana game auth response curl='{}' http={} body='{}'", curl_easy_strerror(result), httpCode, responseBody);

            if (result != CURLE_OK || httpCode < 200 || httpCode >= 300) {
                PaperLogger.error("BeatKhana game auth failed curl={} http={} body='{}'", curl_easy_strerror(result), httpCode, responseBody);
                return false;
            }

            auto token = extractJsonString(responseBody, "token");
            if (token.empty()) {
                PaperLogger.error("BeatKhana game auth response did not include token body='{}'", responseBody);
                return false;
            }

            auto tokenPayload = jwtPayloadJson(token);
            auto tokenPlatformId = extractJsonString(tokenPayload, "platformId");
            if (tokenPlatformId.empty()) tokenPlatformId = extractJsonString(tokenPayload, "ta:platform_id");
            auto tokenUsername = extractJsonString(tokenPayload, "username");
            if (tokenUsername.empty()) tokenUsername = extractJsonString(tokenPayload, "platformUsername");
            if (tokenUsername.empty()) tokenUsername = tokenPlatformId.empty() ? proofPlayerId : tokenPlatformId;

            if (tokenPayload.empty()) {
                PaperLogger.warn("BeatKhana game auth token payload could not be decoded; falling back to proof player id for score identity");
            } else if (tokenPlatformId.empty()) {
                PaperLogger.warn("BeatKhana game auth token did not include platformId/ta:platform_id; falling back to proof player id for score identity");
            }

            {
                std::scoped_lock lock(authTokenMutex);
                beatKhanaGameToken = token;
                beatKhanaPlatformId = tokenPlatformId.empty() ? proofPlayerId : tokenPlatformId;
                beatKhanaPlatformUsername = tokenUsername;
            }
            PaperLogger.info(
                "BeatKhana game auth token='{}' proofPlayerId='{}' scorePlatformId='{}' scoreUsername='{}'",
                token,
                proofPlayerId,
                tokenPlatformId.empty() ? proofPlayerId : tokenPlatformId,
                tokenUsername
            );
            return true;
        }

        std::string fileName(char const* path) {
            if (!path) return {};
            return std::filesystem::path(path).filename().string();
        }

        std::vector<std::string> collectModList() {
            std::vector<std::string> mods;
            std::set<std::string> seen;

            auto add = [&](std::string value) {
                if (value.empty() || seen.find(value) != seen.end()) return;
                seen.insert(value);
                mods.push_back(std::move(value));
            };

            for (auto const& fallback : kModList) add(std::string("configured:") + fallback);

            CLoadResults loadResults = modloader_get_all();
            PaperLogger.info("Collecting loaded mod list from modloader results={}", loadResults.size);
            for (size_t i = 0; i < loadResults.size; ++i) {
                auto& result = loadResults.array[i];
                switch (result.result) {
                    case CLoadResultEnum::MatchType_Loaded: {
                        auto name = result.loaded.info.id && result.loaded.info.id[0]
                            ? std::string(result.loaded.info.id)
                            : fileName(result.loaded.path);
                        auto version = result.loaded.info.version ? std::string(result.loaded.info.version) : std::string();
                        add(version.empty() ? "loaded:" + name : "loaded:" + name + "@" + version);
                        break;
                    }
                    case CLoadResultEnum::LoadResult_Failed: {
                        auto name = fileName(result.failed.path);
                        auto reason = result.failed.failure ? std::string(result.failed.failure) : std::string("unknown");
                        add("failed:" + name + ":" + reason);
                        break;
                    }
                    default:
                        break;
                }
            }

            PaperLogger.info("Collected {} mod list entries", mods.size());
            for (auto const& mod : mods) PaperLogger.info("TA mod entry '{}'", mod);
            return mods;
        }
    }

    Client::~Client() {
        disconnect();
    }

    Client& Client::instance() {
        static Client client;
        return client;
    }

    void Client::setUiCallback(UiCallback callback) {
        PaperLogger.info("Client::setUiCallback hasCallback={}", bool(callback));
        std::scoped_lock lock(mutex_);
        uiCallback_ = std::move(callback);
    }

    void Client::connect() {
        PaperLogger.info("Client::connect requested connected={} connecting={} socketFd={}", connected_.load(), connecting_.load(), socketFd_);
        if (connected_ || connecting_) {
            PaperLogger.info("Client::connect ignored because connection is already active or pending");
            return;
        }
        stop_ = false;
        connecting_ = true;
        setStatus("Connecting to TournamentAssistant...");
        if (connectThread_.joinable()) connectThread_.join();
        if (receiveThread_.joinable()) receiveThread_.join();
        if (heartbeatThread_.joinable()) heartbeatThread_.join();
        connectThread_ = std::thread([this] { workerConnect(); });
    }

    void Client::disconnect() {
        PaperLogger.info("Client::disconnect requested socketFd={} connected={} connecting={}", socketFd_, connected_.load(), connecting_.load());
        stop_ = true;
        connected_ = false;
        connecting_ = false;
        {
            std::scoped_lock lock(ioMutex_);
            if (curl_) {
                PaperLogger.info("Cleaning up curl TLS socket");
                curl_easy_cleanup(curl_);
                curl_ = nullptr;
            }
            socketFd_ = -1;
        }
        failPendingResponses("Disconnected");
        if (connectThread_.joinable()) connectThread_.join();
        if (receiveThread_.joinable()) receiveThread_.join();
        if (heartbeatThread_.joinable()) heartbeatThread_.join();
        {
            std::scoped_lock lock(mutex_);
            PaperLogger.info("Clearing TA client session state after disconnect");
            state_ = State{};
            selectedTournamentId_.clear();
            selfGuid_.clear();
            activePrompt_.reset();
            activeSong_.reset();
            activeQualifier_.reset();
            leaderboards_.clear();
            remainingAttempts_.clear();
            downloadRequests_.clear();
            downloadStatesByLevel_.clear();
        }
        clearBeatKhanaGameToken();
        setStatus("Disconnected");
        PaperLogger.info("Client::disconnect completed");
    }

    bool Client::connected() const {
        return connected_;
    }

    bool Client::inTournament() const {
        std::scoped_lock lock(mutex_);
        return !selectedTournamentId_.empty();
    }

    std::optional<Match> Client::currentMatch() const {
        std::scoped_lock lock(mutex_);
        auto tournament = std::find_if(state_.tournaments.begin(), state_.tournaments.end(), [&](Tournament const& item) {
            return item.guid == selectedTournamentId_;
        });
        if (tournament == state_.tournaments.end()) return std::nullopt;

        auto match = std::find_if(tournament->matches.begin(), tournament->matches.end(), [&](Match const& item) {
            return std::find(item.associatedUsers.begin(), item.associatedUsers.end(), selfGuid_) != item.associatedUsers.end();
        });
        if (match == tournament->matches.end()) return std::nullopt;
        return *match;
    }

    std::optional<Map> Client::selectedMatchMap() const {
        auto match = currentMatch();
        if (!match || !match->selectedMap) return std::nullopt;
        return match->selectedMap;
    }

    State Client::state() const {
        std::scoped_lock lock(mutex_);
        return state_;
    }

    std::string Client::status() const {
        std::scoped_lock lock(mutex_);
        return status_;
    }

    std::string Client::selectedTournamentId() const {
        std::scoped_lock lock(mutex_);
        return selectedTournamentId_;
    }

    std::string Client::selfGuid() const {
        std::scoped_lock lock(mutex_);
        return selfGuid_;
    }

    std::optional<User> Client::selfUser() const {
        std::scoped_lock lock(mutex_);
        auto tournament = std::find_if(state_.tournaments.begin(), state_.tournaments.end(), [&](Tournament const& item) {
            return item.guid == selectedTournamentId_;
        });
        if (tournament == state_.tournaments.end()) return std::nullopt;

        auto user = std::find_if(tournament->users.begin(), tournament->users.end(), [&](User const& item) {
            return item.guid == selfGuid_;
        });
        if (user == tournament->users.end()) return std::nullopt;
        return *user;
    }

    bool Client::isSuccess(Response const& response) {
        return response.type == ResponseType::Success;
    }

    std::string Client::qualifierKey(std::string const& eventId, std::string const& mapId) {
        return eventId + ":" + mapId;
    }

    std::optional<Prompt> Client::activePrompt() const {
        std::scoped_lock lock(mutex_);
        return activePrompt_;
    }

    std::optional<GameplayParameters> Client::activeSong() const {
        std::scoped_lock lock(mutex_);
        return activeSong_;
    }

    std::vector<LeaderboardEntry> Client::leaderboard(std::string const& eventId, std::string const& mapId) const {
        std::scoped_lock lock(mutex_);
        auto it = leaderboards_.find(qualifierKey(eventId, mapId));
        return it == leaderboards_.end() ? std::vector<LeaderboardEntry>{} : it->second;
    }

    int32_t Client::remainingAttempts(std::string const& eventId, std::string const& mapId) const {
        std::scoped_lock lock(mutex_);
        auto it = remainingAttempts_.find(qualifierKey(eventId, mapId));
        return it == remainingAttempts_.end() ? -1 : it->second;
    }

    int32_t Client::scoreUpdateFrequency() const {
        std::scoped_lock lock(mutex_);
        auto const* tournament = const_cast<Client*>(this)->findTournamentLocked(selectedTournamentId_);
        if (!tournament || tournament->settings.scoreUpdateFrequency <= 0) return 30;
        return tournament->settings.scoreUpdateFrequency;
    }

    Tournament* Client::findTournamentLocked(std::string const& tournamentId) {
        auto tournament = std::find_if(state_.tournaments.begin(), state_.tournaments.end(), [&](Tournament const& item) {
            return item.guid == tournamentId;
        });
        return tournament == state_.tournaments.end() ? nullptr : &*tournament;
    }

    User* Client::findSelfLocked() {
        if (selectedTournamentId_.empty() || selfGuid_.empty()) return nullptr;
        auto* tournament = findTournamentLocked(selectedTournamentId_);
        if (!tournament) return nullptr;
        auto user = std::find_if(tournament->users.begin(), tournament->users.end(), [&](User const& item) {
            return item.guid == selfGuid_;
        });
        return user == tournament->users.end() ? nullptr : &*user;
    }

    void Client::workerConnect() {
        PaperLogger.info("workerConnect started host='{}' port={}", kServerHost, kServerPort);
        ensureCurlInitialized();
        setStatus("Checking TournamentAssistant version...");
        std::string latestVersion;
        if (!standaloneVersionAllowed(latestVersion)) {
            PaperLogger.warn("workerConnect aborting because standalone version is out of date latest='{}'", latestVersion);
            connecting_ = false;
            connected_ = false;
            setStatus(std::string("Version out of date, please install the newest version at ") + kDownloadUrl);
            return;
        }
        setStatus("Authenticating with BeatKhana...");
        if (!refreshBeatKhanaGameToken()) {
            PaperLogger.error("workerConnect aborting because BeatKhana game auth failed");
            connecting_ = false;
            connected_ = false;
            setStatus("BeatKhana authentication failed");
            return;
        }
        PaperLogger.info("BeatKhana game auth completed; continuing to TA socket connection");

        auto* curl = curl_easy_init();
        if (!curl) {
            PaperLogger.error("curl_easy_init failed for TA socket");
            connecting_ = false;
            setStatus("Failed to initialize TLS socket");
            return;
        }

        auto url = std::string("https://") + kServerHost + ":" + std::to_string(kServerPort) + "/";
        curl_easy_setopt(curl, CURLOPT_URL, url.c_str());
        curl_easy_setopt(curl, CURLOPT_CONNECT_ONLY, 1L);
        curl_easy_setopt(curl, CURLOPT_NOSIGNAL, 1L);
        curl_easy_setopt(curl, CURLOPT_CONNECTTIMEOUT, 30L);
        curl_easy_setopt(curl, CURLOPT_TIMEOUT, 30L);
        curl_easy_setopt(curl, CURLOPT_TCP_NODELAY, 1L);
        curl_easy_setopt(curl, CURLOPT_USERAGENT, "TournamentAssistant Standalone");
        curl_easy_setopt(curl, CURLOPT_SSL_VERIFYPEER, 0L);
        curl_easy_setopt(curl, CURLOPT_SSL_VERIFYHOST, 0L);

        PaperLogger.info("Opening TLS connect-only stream to {}", url);
        auto connectResult = curl_easy_perform(curl);
        if (connectResult != CURLE_OK) {
            PaperLogger.error("TLS connect failed: {}", curl_easy_strerror(connectResult));
            curl_easy_cleanup(curl);
            connecting_ = false;
            setStatus(std::string("Failed to connect: ") + curl_easy_strerror(connectResult));
            return;
        }

        curl_socket_t activeSocket = CURL_SOCKET_BAD;
        curl_easy_getinfo(curl, CURLINFO_ACTIVESOCKET, &activeSocket);
        socketFd_ = int(activeSocket);
        {
            std::scoped_lock lock(ioMutex_);
            curl_ = curl;
        }
        PaperLogger.info("TLS connect succeeded activeSocket={}", socketFd_);

        connected_ = true;
        PaperLogger.info("Starting receive and heartbeat threads");
        receiveThread_ = std::thread([this] { receiveLoop(); });
        heartbeatThread_ = std::thread([this] { heartbeatLoop(); });

        Request connectRequest;
        connectRequest.kind = RequestKind::Connect;
        connectRequest.clientVersion = kClientVersion;

        PaperLogger.info("Sending connect request clientVersion={}", connectRequest.clientVersion);
        auto responseFuture = sendRequest(connectRequest);
        if (responseFuture.wait_for(std::chrono::seconds(30)) != std::future_status::ready) {
            PaperLogger.error("Connect request timed out");
            setStatus("Server timed out");
            connected_ = false;
            connecting_ = false;
            {
                std::scoped_lock lock(ioMutex_);
                if (curl_) {
                    curl_easy_cleanup(curl_);
                    curl_ = nullptr;
                }
                socketFd_ = -1;
            }
            return;
        }

        auto response = responseFuture.get();
        if (stop_) {
            PaperLogger.info("Connect response unblocked after stop; workerConnect exiting");
            connected_ = false;
            connecting_ = false;
            return;
        }
        PaperLogger.info("Connect response type={} message='{}' tournaments={} selfGuid='{}'", int(response.type), response.message, response.state.tournaments.size(), response.selfGuid);
        for (auto const& tournament : response.state.tournaments) {
            PaperLogger.info(
                "Connect tournament guid='{}' name='{}' image='{}' server='{}:{}' showTournament={} showQualifier={} users={} matches={} qualifiers={}",
                tournament.guid,
                tournament.settings.tournamentName,
                tournament.settings.tournamentImage,
                tournament.server.address,
                tournament.server.port,
                tournament.settings.showTournamentButton,
                tournament.settings.showQualifierButton,
                tournament.users.size(),
                tournament.matches.size(),
                tournament.qualifiers.size()
            );
        }
        if (!isSuccess(response)) {
            setStatus(response.message.empty() ? "Server rejected connection" : response.message);
            connected_ = false;
            connecting_ = false;
            {
                std::scoped_lock lock(ioMutex_);
                if (curl_) {
                    curl_easy_cleanup(curl_);
                    curl_ = nullptr;
                }
                socketFd_ = -1;
            }
            return;
        }

        {
            std::scoped_lock lock(mutex_);
            state_ = response.state;
            status_ = "Connected. Select a tournament.";
        }
        connecting_ = false;
        PaperLogger.info("workerConnect completed successfully");
        notifyUi();
    }

    std::future<Response> Client::sendRequest(Request request) {
        PaperLogger.info("sendRequest kind={} tournament='{}' event='{}' map='{}'", int(request.kind), request.tournamentId, request.eventId, request.mapId);
        Packet packet;
        packet.kind = PacketKind::Request;
        packet.id = Proto::makePacketId();
        packet.from = selfGuid();
        packet.token = tokenForPackets();
        packet.request = std::move(request);

        std::promise<Response> promise;
        auto future = promise.get_future();
        {
            std::scoped_lock lock(mutex_);
            pendingResponses_.emplace(packet.id, std::move(promise));
        }

        sendPacket(packet);
        return future;
    }

    void Client::sendPacket(Packet packet) {
        if (!connected_) {
            PaperLogger.warn("sendPacket ignored because client is disconnected kind={}", int(packet.kind));
            return;
        }
        if (packet.id.empty()) packet.id = Proto::makePacketId();
        packet.token = tokenForPackets();
        if (packet.from.empty()) packet.from = selfGuid();
        if (packet.token.empty()) {
            PaperLogger.error("sendPacket has empty auth token kind={} id='{}'", int(packet.kind), packet.id);
        } else {
            PaperLogger.info("sendPacket auth token='{}'", packet.token);
        }

        auto bytes = Proto::wrapPacket(packet);
        PaperLogger.info("sendPacket kind={} id='{}' bytes={} requestKind={} pushKind={}", int(packet.kind), packet.id, bytes.size(), int(packet.request.kind), int(packet.push.kind));
        if (!sendAll(curl_, ioMutex_, stop_, bytes)) {
            PaperLogger.error("Socket send failed for packet id='{}'", packet.id);
            setStatus("Socket send failed");
            connected_ = false;
        }
    }

    void Client::receiveLoop() {
        PaperLogger.info("receiveLoop started");
        std::vector<uint8_t> accumulated;
        std::array<uint8_t, 8192> buffer{};

        while (connected_ && !stop_) {
            size_t bytesRead = 0;
            CURLcode recvResult;
            {
                std::scoped_lock lock(ioMutex_);
                if (!curl_) break;
                recvResult = curl_easy_recv(curl_, buffer.data(), buffer.size(), &bytesRead);
            }

            if (recvResult == CURLE_AGAIN) {
                std::this_thread::sleep_for(std::chrono::milliseconds(20));
                continue;
            }

            if (recvResult != CURLE_OK) {
                PaperLogger.warn("receiveLoop curl_easy_recv ended result={} '{}'", int(recvResult), curl_easy_strerror(recvResult));
                break;
            }

            if (bytesRead == 0) {
                PaperLogger.warn("receiveLoop curl_easy_recv returned 0 bytes");
                break;
            }
            PaperLogger.info("receiveLoop read {} bytes", bytesRead);
            accumulated.insert(accumulated.end(), buffer.begin(), buffer.begin() + bytesRead);

            while (accumulated.size() >= kHeaderSize) {
                while (accumulated.size() >= 4 && !startsWithMagic(accumulated)) {
                    accumulated.erase(accumulated.begin());
                }
                if (accumulated.size() < kHeaderSize) break;

                uint32_t payloadSize = readLittleEndian32(accumulated, 4);
                PaperLogger.info("receiveLoop payloadSize={} accumulated={}", payloadSize, accumulated.size());
                if (accumulated.size() < kHeaderSize + payloadSize) break;

                std::vector<uint8_t> payload(accumulated.begin() + kHeaderSize, accumulated.begin() + kHeaderSize + payloadSize);
                accumulated.erase(accumulated.begin(), accumulated.begin() + kHeaderSize + payloadSize);

                auto packet = Proto::unwrapPacket(payload);
                if (packet) {
                    PaperLogger.info("receiveLoop decoded packet kind={} id='{}'", int(packet->kind), packet->id);
                    handlePacket(*packet);
                } else {
                    PaperLogger.error("receiveLoop failed to decode packet payloadSize={}", payloadSize);
                }
            }
        }

        connected_ = false;
        connecting_ = false;
        failPendingResponses("Server disconnected");
        if (!stop_) setStatus("Server disconnected");
        PaperLogger.info("receiveLoop stopped stop={}", stop_.load());
    }

    void Client::heartbeatLoop() {
        PaperLogger.info("heartbeatLoop started");
        while (connected_ && !stop_) {
            for (int i = 0; i < 100 && connected_ && !stop_; ++i) {
                std::this_thread::sleep_for(std::chrono::milliseconds(100));
            }
            if (!connected_ || stop_) break;
            Packet heartbeat;
            heartbeat.kind = PacketKind::Heartbeat;
            heartbeat.heartbeat = true;
            PaperLogger.info("Sending heartbeat");
            sendPacket(heartbeat);
        }
        PaperLogger.info("heartbeatLoop stopped");
    }

    void Client::handlePacket(Packet packet) {
        PaperLogger.info("handlePacket kind={} id='{}' from='{}'", int(packet.kind), packet.id, packet.from);
        if (packet.kind == PacketKind::Response) {
            PaperLogger.info(
                "Handling response kind={} respondingTo='{}' type={} message='{}' permissionRequired='{}' roles='{}' permissions='{}'",
                int(packet.response.kind),
                packet.response.respondingToPacketId,
                int(packet.response.type),
                packet.response.message,
                packet.response.permissionRequired,
                packet.response.permissionRoles,
                packet.response.permissionPermissions
            );
            std::promise<Response> promise;
            bool found = false;
            {
                std::scoped_lock lock(mutex_);
                auto it = pendingResponses_.find(packet.response.respondingToPacketId);
                if (it != pendingResponses_.end()) {
                    promise = std::move(it->second);
                    pendingResponses_.erase(it);
                    found = true;
                }
            }
            if (!found) PaperLogger.warn("No pending response found for '{}'", packet.response.respondingToPacketId);
            if (found) promise.set_value(packet.response);
            return;
        }

        if (packet.kind == PacketKind::Event) {
            PaperLogger.info("Handling event kind={} tournament='{}'", int(packet.event.kind), packet.event.tournamentId);
            applyEvent(packet.event);
            return;
        }

        if (packet.kind == PacketKind::Command) {
            PaperLogger.info(
                "Handling command kind={} tournament='{}' discordAuthorize='{}' streamSyncColor='{}' modifier={} forwards={}",
                int(packet.command.kind),
                packet.command.tournamentId,
                packet.command.discordAuthorize,
                packet.command.streamSyncColor,
                int(packet.command.modifier),
                packet.command.forwardTo.size()
            );
            if (packet.command.kind == CommandKind::ReturnToMenu) {
                TA::StreamSync::clear();
                Song::returnToMenu();
                clearTransientGameplayState("Coordinator returned player to menu");
            } else if (packet.command.kind == CommandKind::DelayTestFinish) {
                PaperLogger.info("Streamsync delay test finished; resuming synced play");
                TA::StreamSync::finish();
            } else if (packet.command.kind == CommandKind::StreamSyncShowImage) {
                PaperLogger.info("Streamsync show-image command received");
                TA::StreamSync::showImage(true);
            } else if (packet.command.kind == CommandKind::ShowColorForStreamSync) {
                PaperLogger.info("Streamsync show-color command received color='{}'", packet.command.streamSyncColor);
                TA::StreamSync::showColor(packet.command.streamSyncColor);
            } else if (packet.command.kind == CommandKind::ModifyGameplay) {
                PaperLogger.info("ModifyGameplay command received modifier={}", int(packet.command.modifier));
                TA::MidPlayModifiers::toggle(packet.command.modifier);
            } else if (packet.command.kind == CommandKind::PlaySong) {
                setLocalPlayState(PlayState::InGame);
                {
                    std::scoped_lock lock(mutex_);
                    activeSong_ = packet.command.playSong;
                }
                notifyUi();
                Song::playSong(packet.command.playSong, [this](TA::Song::PlayResult const&) {
                    {
                        std::scoped_lock lock(mutex_);
                        activeSong_.reset();
                    }
                    setLocalPlayState(PlayState::InMenu);
                    notifyUi();
                });
            } else if (packet.command.kind == CommandKind::DiscordAuthorize) {
                PaperLogger.warn("Server requested Discord authorization; BeatKhana game token was not accepted. Payload='{}'", packet.command.discordAuthorize);
                connected_ = false;
                connecting_ = false;
                failPendingResponses("Authorization required");
                setStatus("Server requested Discord authorization. BeatKhana token was not accepted.");
            }
            return;
        }

        if (packet.kind == PacketKind::Request) {
            PaperLogger.info("Handling request kind={} from='{}' id='{}'", int(packet.request.kind), packet.from, packet.id);
            if (packet.request.kind == RequestKind::LoadSong) {
                PaperLogger.info("LoadSong request levelId='{}' customHost='{}'", packet.request.levelId, packet.request.customHostUrl);
                setLocalDownloadState(DownloadState::Downloading);
                auto requester = packet.from;
                auto requestId = packet.id;
                auto levelId = packet.request.levelId;
                Song::ensureDownloaded(levelId, packet.request.customHostUrl, [this, requester, requestId, levelId](bool success, std::string message) {
                    setLocalDownloadState(success ? DownloadState::Downloaded : DownloadState::DownloadError);
                    sendLoadSongResponse(requester, requestId, levelId, success, message);
                });
            } else if (packet.request.kind == RequestKind::PreloadImageForStreamSync) {
                PaperLogger.info(
                    "PreloadImageForStreamSync request fileId='{}' compressed={} bytes={}",
                    packet.request.fileId,
                    packet.request.compressed,
                    packet.request.data.size()
                );
                auto requester = packet.from;
                auto requestId = packet.id;
                auto fileId = packet.request.fileId;
                if (packet.request.compressed) {
                    PaperLogger.warn("Compressed streamsync image payloads are not supported on Quest yet; storing raw bytes anyway");
                }
                TA::StreamSync::setImage(packet.request.data);
                sendPreloadImageResponse(requester, requestId, fileId, true, "");
            } else if (packet.request.kind == RequestKind::ShowPrompt) {
                PaperLogger.info("ShowPrompt request title='{}' options={}", packet.request.prompt.title, packet.request.prompt.options.size());
                {
                    std::scoped_lock lock(mutex_);
                    packet.request.prompt.requestId = packet.id;
                    packet.request.prompt.fromUserId = packet.from;
                    activePrompt_ = packet.request.prompt;
                }
                notifyUi();
            }
        }
    }

    void Client::applyEvent(Event const& event) {
        PaperLogger.info("applyEvent kind={} tournament='{}'", int(event.kind), event.tournamentId);
        std::optional<User> userUpdate;
        {
            std::scoped_lock lock(mutex_);
            switch (event.kind) {
                case EventKind::TournamentCreated:
                    PaperLogger.info("TournamentCreated guid='{}'", event.tournament.guid);
                    state_.tournaments.push_back(event.tournament);
                    break;
                case EventKind::TournamentUpdated: {
                    PaperLogger.info("TournamentUpdated guid='{}'", event.tournament.guid);
                    auto* tournament = findTournamentLocked(event.tournament.guid);
                    if (tournament) *tournament = event.tournament;
                    else state_.tournaments.push_back(event.tournament);
                    break;
                }
                case EventKind::TournamentDeleted:
                    PaperLogger.info("TournamentDeleted guid='{}'", event.tournament.guid);
                    std::erase_if(state_.tournaments, [&](Tournament const& item) { return item.guid == event.tournament.guid; });
                    break;
                case EventKind::UserAdded:
                case EventKind::UserUpdated: {
                    auto* tournament = findTournamentLocked(event.tournamentId);
                    if (!tournament) break;
                    PaperLogger.info("User event user='{}' tournament='{}'", event.user.guid, event.tournamentId);
                    auto user = std::find_if(tournament->users.begin(), tournament->users.end(), [&](User const& item) {
                        return item.guid == event.user.guid;
                    });
                    if (user == tournament->users.end()) tournament->users.push_back(event.user);
                    else *user = event.user;
                    break;
                }
                case EventKind::UserLeft: {
                    auto* tournament = findTournamentLocked(event.tournamentId);
                    if (tournament) std::erase_if(tournament->users, [&](User const& item) { return item.guid == event.user.guid; });
                    break;
                }
                case EventKind::MatchCreated:
                case EventKind::MatchUpdated: {
                    auto* tournament = findTournamentLocked(event.tournamentId);
                    if (!tournament) break;
                    PaperLogger.info("Match event match='{}' tournament='{}' associatedUsers={}", event.match.guid, event.tournamentId, event.match.associatedUsers.size());
                    bool ownMatch = std::find(event.match.associatedUsers.begin(), event.match.associatedUsers.end(), selfGuid_) != event.match.associatedUsers.end();
                    auto match = std::find_if(tournament->matches.begin(), tournament->matches.end(), [&](Match const& item) {
                        return item.guid == event.match.guid;
                    });
                    if (match == tournament->matches.end()) tournament->matches.push_back(event.match);
                    else *match = event.match;
                    if (ownMatch) {
                        status_ = event.match.selectedMap
                            ? "Song selected. Waiting for coordinator to start."
                            : "Match Created. Waiting for coordinator to select a song.";
                        if (auto* user = findSelfLocked()) {
                            if (user->playState != PlayState::InGame) user->playState = PlayState::WaitingForCoordinator;
                            userUpdate = *user;
                        }
                    }
                    break;
                }
                case EventKind::MatchDeleted: {
                    auto* tournament = findTournamentLocked(event.tournamentId);
                    bool deletedOwnMatch = std::find(event.match.associatedUsers.begin(), event.match.associatedUsers.end(), selfGuid_) != event.match.associatedUsers.end();
                    if (tournament) {
                        auto match = std::find_if(tournament->matches.begin(), tournament->matches.end(), [&](Match const& item) {
                            return item.guid == event.match.guid;
                        });
                        deletedOwnMatch = deletedOwnMatch ||
                                          (match != tournament->matches.end() &&
                                           std::find(match->associatedUsers.begin(), match->associatedUsers.end(), selfGuid_) != match->associatedUsers.end());
                        std::erase_if(tournament->matches, [&](Match const& item) { return item.guid == event.match.guid; });
                    }
                    if (deletedOwnMatch) {
                        PaperLogger.info("Own match '{}' was deleted; clearing active song/prompt state", event.match.guid);
                        activeSong_.reset();
                        activePrompt_.reset();
                        activeQualifier_.reset();
                        status_ = "Connected to tournament. Waiting for coordinator to create match.";
                        if (auto* user = findSelfLocked()) {
                            user->playState = PlayState::WaitingForCoordinator;
                            user->downloadState = DownloadState::None;
                            userUpdate = *user;
                        }
                    }
                    break;
                }
                case EventKind::QualifierCreated:
                case EventKind::QualifierUpdated: {
                    auto* tournament = findTournamentLocked(event.tournamentId);
                    if (!tournament) break;
                    PaperLogger.info("Qualifier event qualifier='{}' tournament='{}' maps={}", event.qualifier.guid, event.tournamentId, event.qualifier.qualifierMaps.size());
                    auto qualifier = std::find_if(tournament->qualifiers.begin(), tournament->qualifiers.end(), [&](QualifierEvent const& item) {
                        return item.guid == event.qualifier.guid;
                    });
                    if (qualifier == tournament->qualifiers.end()) tournament->qualifiers.push_back(event.qualifier);
                    else *qualifier = event.qualifier;
                    break;
                }
                case EventKind::QualifierDeleted: {
                    auto* tournament = findTournamentLocked(event.tournamentId);
                    if (tournament) std::erase_if(tournament->qualifiers, [&](QualifierEvent const& item) { return item.guid == event.qualifier.guid; });
                    break;
                }
                default:
                    break;
            }
        }
        if (userUpdate) sendUserUpdate(*userUpdate);
        notifyUi();
    }

    void Client::joinTournament(std::string tournamentId) {
        PaperLogger.info("joinTournament requested '{}'", tournamentId);
        if (!connected_) {
            PaperLogger.warn("joinTournament called while disconnected; starting connect instead");
            connect();
            return;
        }

        setStatus("Joining tournament...");
        std::thread([this, tournamentId = std::move(tournamentId)] {
            PaperLogger.info("Join tournament worker started '{}'", tournamentId);
            Request request;
            request.kind = RequestKind::Join;
            request.tournamentId = tournamentId;
            request.modList = collectModList();

            auto future = sendRequest(request);
            if (future.wait_for(std::chrono::seconds(30)) != std::future_status::ready) {
                PaperLogger.error("Join request timed out for '{}'", tournamentId);
                setStatus("Join timed out");
                return;
            }

            auto response = future.get();
            PaperLogger.info("Join response type={} message='{}' tournament='{}' selfGuid='{}'", int(response.type), response.message, response.tournamentId, response.selfGuid);
            if (!isSuccess(response)) {
                setStatus(response.message.empty() ? "Failed to join tournament" : response.message);
                return;
            }

            {
                std::scoped_lock lock(mutex_);
                if (!response.state.tournaments.empty()) state_ = response.state;
                selectedTournamentId_ = response.tournamentId.empty() ? tournamentId : response.tournamentId;
                selfGuid_ = response.selfGuid;
                status_ = "Connected to tournament. Waiting for coordinator to create match.";
            }
            setLocalPlayState(PlayState::WaitingForCoordinator);
            notifyUi();
        }).detach();
    }

    void Client::setLocalPlayState(PlayState playState) {
        PaperLogger.info("setLocalPlayState {}", int(playState));
        std::optional<User> update;
        {
            std::scoped_lock lock(mutex_);
            if (auto* user = findSelfLocked()) {
                user->playState = playState;
                update = *user;
            }
        }
        if (update) sendUserUpdate(*update);
        notifyUi();
    }

    void Client::setLocalDownloadState(DownloadState downloadState) {
        PaperLogger.info("setLocalDownloadState {}", int(downloadState));
        std::optional<User> update;
        {
            std::scoped_lock lock(mutex_);
            if (auto* user = findSelfLocked()) {
                user->downloadState = downloadState;
                update = *user;
            }
        }
        if (update) sendUserUpdate(*update);
        notifyUi();
    }

    void Client::setLocalTeam(std::string teamId) {
        PaperLogger.info("setLocalTeam '{}'", teamId);
        std::optional<User> update;
        {
            std::scoped_lock lock(mutex_);
            if (auto* user = findSelfLocked()) {
                user->teamId = std::move(teamId);
                update = *user;
            }
        }
        if (update) sendUserUpdate(*update);
        notifyUi();
    }

    void Client::clearTransientGameplayState(std::string reason) {
        PaperLogger.info("clearTransientGameplayState reason='{}'", reason);
        bool changed = false;
        std::optional<User> update;
        {
            std::scoped_lock lock(mutex_);
            if (activeSong_ || activePrompt_ || activeQualifier_) changed = true;
            activeSong_.reset();
            activePrompt_.reset();
            activeQualifier_.reset();
            if (auto* user = findSelfLocked()) {
                if (user->playState == PlayState::InGame) changed = true;
                user->playState = PlayState::WaitingForCoordinator;
                user->downloadState = DownloadState::None;
                update = *user;
            }
        }
        if (update) sendUserUpdate(*update);
        if (changed) notifyUi();
    }

    void Client::sendUserUpdate(User const& user) {
        std::string tournamentId = selectedTournamentId();
        if (tournamentId.empty()) {
            PaperLogger.warn("sendUserUpdate ignored because selectedTournamentId is empty");
            return;
        }
        PaperLogger.info("sendUserUpdate tournament='{}' user='{}' team='{}' playState={} downloadState={}", tournamentId, user.guid, user.teamId, int(user.playState), int(user.downloadState));
        Request request;
        request.kind = RequestKind::UpdateUser;
        request.tournamentId = tournamentId;
        request.user = user;
        request.user.modList = collectModList();

        Packet packet;
        packet.kind = PacketKind::Request;
        packet.request = std::move(request);
        sendPacket(packet);
    }

    void Client::dismissPrompt() {
        PaperLogger.info("dismissPrompt");
        {
            std::scoped_lock lock(mutex_);
            activePrompt_.reset();
        }
        notifyUi();
    }

    void Client::sendPromptResponse(Prompt prompt, std::string value) {
        PaperLogger.info("sendPromptResponse requestId='{}' to='{}' value='{}'", prompt.requestId, prompt.fromUserId, value);
        Response response;
        response.kind = ResponseKind::ShowPrompt;
        response.type = ResponseType::Success;
        response.respondingToPacketId = prompt.requestId;
        response.promptValue = std::move(value);

        Packet packet;
        packet.kind = PacketKind::ForwardingPacket;
        packet.request.forwardTo = {prompt.fromUserId};
        packet.response = std::move(response);
        sendPacket(packet);

        dismissPrompt();
    }

    void Client::requestLeaderboard(std::string tournamentId, std::string eventId, std::string mapId) {
        PaperLogger.info("requestLeaderboard tournament='{}' event='{}' map='{}'", tournamentId, eventId, mapId);
        if (!connected_) {
            PaperLogger.warn("requestLeaderboard ignored while disconnected");
            return;
        }
        std::thread([this, tournamentId = std::move(tournamentId), eventId = std::move(eventId), mapId = std::move(mapId)] {
            Request request;
            request.kind = RequestKind::QualifierScores;
            request.tournamentId = tournamentId;
            request.eventId = eventId;
            request.mapId = mapId;

            auto future = sendRequest(request);
            if (future.wait_for(std::chrono::seconds(30)) != std::future_status::ready) {
                PaperLogger.error("Leaderboard request timed out event='{}' map='{}'", eventId, mapId);
                setStatus("Leaderboard request timed out");
                return;
            }

            auto response = future.get();
            PaperLogger.info("Leaderboard response type={} entries={} message='{}'", int(response.type), response.leaderboardEntries.size(), response.message);
            if (isSuccess(response)) {
                {
                    std::scoped_lock lock(mutex_);
                    leaderboards_[qualifierKey(eventId, mapId)] = response.leaderboardEntries;
                }
                notifyUi();
            } else if (!response.message.empty()) {
                setStatus(response.message);
            }
        }).detach();
    }

    void Client::requestRemainingAttempts(std::string tournamentId, std::string eventId, std::string mapId) {
        PaperLogger.info("requestRemainingAttempts tournament='{}' event='{}' map='{}'", tournamentId, eventId, mapId);
        if (!connected_) {
            PaperLogger.warn("requestRemainingAttempts ignored while disconnected");
            return;
        }
        std::thread([this, tournamentId = std::move(tournamentId), eventId = std::move(eventId), mapId = std::move(mapId)] {
            Request request;
            request.kind = RequestKind::RemainingAttempts;
            request.tournamentId = tournamentId;
            request.eventId = eventId;
            request.mapId = mapId;

            auto future = sendRequest(request);
            if (future.wait_for(std::chrono::seconds(30)) != std::future_status::ready) {
                PaperLogger.error("Attempts request timed out event='{}' map='{}'", eventId, mapId);
                setStatus("Attempts request timed out");
                return;
            }

            auto response = future.get();
            PaperLogger.info("Attempts response type={} remaining={} message='{}'", int(response.type), response.remainingAttempts, response.message);
            if (isSuccess(response)) {
                {
                    std::scoped_lock lock(mutex_);
                    remainingAttempts_[qualifierKey(eventId, mapId)] = response.remainingAttempts;
                }
                notifyUi();
            } else if (!response.message.empty()) {
                setStatus(response.message);
            }
        }).detach();
    }

    void Client::submitQualifierScore(Map const& map, LeaderboardEntry score) {
        PaperLogger.info(
            "submitQualifierScore map='{}' event='{}' platformId='{}' username='{}' score={} placeholder={}",
            map.guid,
            score.eventId,
            score.platformId,
            score.username,
            score.modifiedScore,
            score.isPlaceholder
        );
        std::string tournamentId;
        {
            std::scoped_lock lock(mutex_);
            tournamentId = selectedTournamentId_;
        }
        if (tournamentId.empty() || score.eventId.empty() || score.mapId.empty()) {
            PaperLogger.warn("submitQualifierScore ignored tournament='{}' event='{}' map='{}'", tournamentId, score.eventId, score.mapId);
            return;
        }

        Request request;
        request.kind = RequestKind::SubmitQualifierScore;
        request.tournamentId = tournamentId;
        request.qualifierScore = std::move(score);
        request.map = map;

        std::thread([this, request = std::move(request)] {
            auto eventId = request.qualifierScore.eventId;
            auto mapId = request.qualifierScore.mapId;
            auto future = sendRequest(request);
            if (future.wait_for(std::chrono::seconds(30)) != std::future_status::ready) {
                PaperLogger.error("Qualifier score submit timed out event='{}' map='{}'", eventId, mapId);
                setStatus("Qualifier score submit timed out");
                return;
            }

            auto response = future.get();
            PaperLogger.info(
                "Qualifier score response type={} entries={} message='{}' permissionRequired='{}' roles='{}' permissions='{}'",
                int(response.type),
                response.leaderboardEntries.size(),
                response.message,
                response.permissionRequired,
                response.permissionRoles,
                response.permissionPermissions
            );
            if (isSuccess(response)) {
                {
                    std::scoped_lock lock(mutex_);
                    if (!response.leaderboardEntries.empty()) leaderboards_[qualifierKey(eventId, mapId)] = response.leaderboardEntries;
                }
                requestRemainingAttempts(selectedTournamentId(), eventId, mapId);
                notifyUi();
            } else if (!response.message.empty()) {
                setStatus(response.message);
            }
        }).detach();
    }

    Response Client::submitQualifierScoreBlocking(Map const& map, LeaderboardEntry score) {
        PaperLogger.info(
            "submitQualifierScoreBlocking map='{}' event='{}' platformId='{}' username='{}' score={} placeholder={}",
            map.guid,
            score.eventId,
            score.platformId,
            score.username,
            score.modifiedScore,
            score.isPlaceholder
        );

        std::string tournamentId;
        {
            std::scoped_lock lock(mutex_);
            tournamentId = selectedTournamentId_;
        }

        Response failed;
        failed.type = ResponseType::Fail;
        if (tournamentId.empty() || score.eventId.empty() || score.mapId.empty()) {
            failed.message = "Missing qualifier submission identifiers";
            PaperLogger.warn("submitQualifierScoreBlocking ignored tournament='{}' event='{}' map='{}'", tournamentId, score.eventId, score.mapId);
            return failed;
        }

        Request request;
        request.kind = RequestKind::SubmitQualifierScore;
        request.tournamentId = tournamentId;
        request.qualifierScore = std::move(score);
        request.map = map;

        auto eventId = request.qualifierScore.eventId;
        auto mapId = request.qualifierScore.mapId;
        auto future = sendRequest(std::move(request));
        if (future.wait_for(std::chrono::seconds(30)) != std::future_status::ready) {
            PaperLogger.error("Qualifier score submit timed out event='{}' map='{}'", eventId, mapId);
            failed.message = "Qualifier score submit timed out";
            return failed;
        }

        auto response = future.get();
        PaperLogger.info(
            "Qualifier blocking score response type={} entries={} message='{}' permissionRequired='{}' roles='{}' permissions='{}'",
            int(response.type),
            response.leaderboardEntries.size(),
            response.message,
            response.permissionRequired,
            response.permissionRoles,
            response.permissionPermissions
        );
        if (isSuccess(response)) {
            {
                std::scoped_lock lock(mutex_);
                if (!response.leaderboardEntries.empty()) leaderboards_[qualifierKey(eventId, mapId)] = response.leaderboardEntries;
            }
            requestRemainingAttempts(selectedTournamentId(), eventId, mapId);
            notifyUi();
        }
        return response;
    }

    void Client::playQualifierMap(std::string tournamentId, std::string eventId, Map map) {
        PaperLogger.info("playQualifierMap tournament='{}' event='{}' map='{}' levelId='{}'", tournamentId, eventId, map.guid, map.gameplayParameters.beatmap.levelId);
        if (tournamentId.empty() || eventId.empty() || map.guid.empty()) {
            PaperLogger.warn("playQualifierMap ignored due to missing ids");
            return;
        }

        map.gameplayParameters.useSync = false;
        auto attempts = remainingAttempts(eventId, map.guid);
        PaperLogger.info("playQualifierMap attempts state mapAttempts={} serverRemainingAttempts={}", map.gameplayParameters.attempts, attempts);
        if (attempts == 0) {
            PaperLogger.warn("playQualifierMap blocked because attempts are exhausted");
            setStatus("No remaining attempts for this qualifier map");
            return;
        }

        LeaderboardEntry placeholder;
        placeholder.eventId = eventId;
        placeholder.mapId = map.guid;
        placeholder.isPlaceholder = true;
        placeholder.color = "#ffffff";
        auto [authPlatformId, authUsername] = authIdentityForScores();
        if (auto self = selfUser()) {
            PaperLogger.info(
                "Qualifier placeholder identity selfPlatformId='{}' selfName='{}' authPlatformId='{}' authUsername='{}'",
                self->platformId,
                self->name,
                authPlatformId,
                authUsername
            );
            placeholder.platformId = authPlatformId.empty() ? self->platformId : authPlatformId;
            placeholder.username = authUsername.empty() ? self->name : authUsername;
        } else {
            placeholder.platformId = authPlatformId;
            placeholder.username = authUsername;
        }

        auto startQualifier = [this, tournamentId, eventId, map]() {
            {
                std::scoped_lock lock(mutex_);
                activeQualifier_ = std::make_tuple(tournamentId, eventId, map.guid, map);
                activeSong_ = map.gameplayParameters;
            }
            setLocalPlayState(PlayState::InGame);
            setStatus("Starting qualifier map...");
            notifyUi();

            Song::playSong(map.gameplayParameters, [this, eventId, mapId = map.guid, map](TA::Song::PlayResult const& result) {
            auto options = map.gameplayParameters.gameplayModifiers.options;
            auto submitFinishedScore = result.completed && (
                result.type == SongCompletionType::Passed ||
                (result.type == SongCompletionType::Failed && ((options & TA::GameOptions::NoFail) != 0 || (options & TA::GameOptions::DemoNoFail) != 0))
            );
            if (submitFinishedScore) {
                LeaderboardEntry entry;
                entry.eventId = eventId;
                entry.mapId = mapId;
                entry.color = "#ffffff";
                auto [authPlatformId, authUsername] = authIdentityForScores();
                if (auto self = selfUser()) {
                    PaperLogger.info(
                        "Qualifier final score identity selfPlatformId='{}' selfName='{}' authPlatformId='{}' authUsername='{}'",
                        self->platformId,
                        self->name,
                        authPlatformId,
                        authUsername
                    );
                    entry.platformId = authPlatformId.empty() ? self->platformId : authPlatformId;
                    entry.username = authUsername.empty() ? self->name : authUsername;
                } else {
                    entry.platformId = authPlatformId;
                    entry.username = authUsername;
                }
                entry.multipliedScore = result.score;
                entry.modifiedScore = result.score;
                entry.notesMissed = result.misses;
                entry.badCuts = result.badCuts;
                entry.goodCuts = result.goodCuts;
                PaperLogger.info("Submitting qualifier completion score event='{}' map='{}' type={} score={}", eventId, mapId, int(result.type), result.score);
                submitQualifierScore(map, entry);
            } else {
                PaperLogger.info("Qualifier completion score skipped event='{}' map='{}' completed={} type={}", eventId, mapId, result.completed, int(result.type));
            }
            {
                std::scoped_lock lock(mutex_);
                activeSong_.reset();
                activeQualifier_.reset();
            }
            setLocalPlayState(PlayState::InMenu);
            requestLeaderboard(selectedTournamentId(), eventId, mapId);
            requestRemainingAttempts(selectedTournamentId(), eventId, mapId);
            notifyUi();
            }, false);
        };

        auto shouldBurnAttempt = map.gameplayParameters.attempts > 0 || attempts > 0;
        if (!shouldBurnAttempt) {
            PaperLogger.info("Qualifier map appears unlimited and no remaining-attempt state is loaded, no placeholder score will be sent");
            startQualifier();
            return;
        }

        {
            std::scoped_lock lock(mutex_);
            auto key = qualifierKey(eventId, map.guid);
            auto it = remainingAttempts_.find(key);
            if (it != remainingAttempts_.end() && it->second > 0) {
                it->second -= 1;
                PaperLogger.info("Locally decremented remaining attempts before async reservation key='{}' now={}", key, it->second);
            }
        }

        std::thread([this, eventId, map, placeholder = std::move(placeholder)]() mutable {
            PaperLogger.info(
                "Burning qualifier attempt asynchronously event='{}' map='{}' levelId='{}' platformId='{}' mapAttempts={}",
                eventId,
                map.guid,
                map.gameplayParameters.beatmap.levelId,
                placeholder.platformId,
                map.gameplayParameters.attempts
            );
            auto response = submitQualifierScoreBlocking(map, placeholder);
            if (!isSuccess(response)) {
                PaperLogger.error("Qualifier attempt reservation failed event='{}' map='{}' message='{}'", eventId, map.guid, response.message);
                requestRemainingAttempts(selectedTournamentId(), eventId, map.guid);
                return;
            }

            PaperLogger.info("Qualifier attempt reservation completed event='{}' map='{}'", eventId, map.guid);
        }).detach();
        startQualifier();
    }

    void Client::practiceQualifierMap(Map map) {
        PaperLogger.info("practiceQualifierMap map='{}' levelId='{}'", map.guid, map.gameplayParameters.beatmap.levelId);
        auto parameters = map.gameplayParameters;
        parameters.disablePause = false;
        parameters.attempts = 0;
        parameters.useSync = false;
        {
            std::scoped_lock lock(mutex_);
            activeSong_ = parameters;
        }
        setLocalPlayState(PlayState::InGame);
        notifyUi();

        Song::playSong(parameters, [this](TA::Song::PlayResult const& result) {
            PaperLogger.info("Qualifier practice finished completed={} type={} score reporting suppressed", result.completed, int(result.type));
            {
                std::scoped_lock lock(mutex_);
                activeSong_.reset();
            }
            setLocalPlayState(PlayState::InMenu);
            notifyUi();
        }, false);
    }

    void Client::ensureMapDownloaded(Map map) {
        auto levelId = map.gameplayParameters.beatmap.levelId;
        PaperLogger.info("ensureMapDownloaded map='{}' levelId='{}'", map.guid, levelId);
        if (levelId.empty()) {
            PaperLogger.warn("ensureMapDownloaded ignored empty levelId");
            return;
        }

        if (Song::isDownloaded(levelId)) {
            bool shouldSendDownloaded = false;
            {
                std::scoped_lock lock(mutex_);
                auto current = downloadStatesByLevel_.find(levelId);
                shouldSendDownloaded = current == downloadStatesByLevel_.end() || current->second != DownloadState::Downloaded;
                downloadStatesByLevel_[levelId] = DownloadState::Downloaded;
                downloadRequests_[levelId] = false;
            }
            PaperLogger.info("ensureMapDownloaded already installed levelId='{}' shouldSendDownloaded={}", levelId, shouldSendDownloaded);
            if (shouldSendDownloaded) setLocalDownloadState(DownloadState::Downloaded);
            return;
        }

        {
            std::scoped_lock lock(mutex_);
            if (downloadRequests_[levelId]) {
                PaperLogger.info("ensureMapDownloaded already pending levelId='{}'", levelId);
                return;
            }
            downloadRequests_[levelId] = true;
            downloadStatesByLevel_[levelId] = DownloadState::Downloading;
        }

        setLocalDownloadState(DownloadState::Downloading);
        Song::ensureDownloaded(levelId, "", [this, levelId](bool success, std::string message) {
            PaperLogger.info("ensureMapDownloaded callback levelId='{}' success={} message='{}'", levelId, success, message);
            {
                std::scoped_lock lock(mutex_);
                downloadRequests_[levelId] = false;
                downloadStatesByLevel_[levelId] = success ? DownloadState::Downloaded : DownloadState::DownloadError;
            }
            setLocalDownloadState(success ? DownloadState::Downloaded : DownloadState::DownloadError);
            if (!success && !message.empty()) setStatus(message);
        });
    }

    void Client::sendLoadSongResponse(std::string const& requester, std::string const& requestId, std::string const& levelId, bool success, std::string message) {
        PaperLogger.info("sendLoadSongResponse requester='{}' requestId='{}' levelId='{}' success={} message='{}'", requester, requestId, levelId, success, message);
        Response response;
        response.kind = ResponseKind::LoadSong;
        response.type = success ? ResponseType::Success : ResponseType::Fail;
        response.respondingToPacketId = requestId;
        response.levelId = levelId;
        response.message = std::move(message);

        Packet packet;
        packet.kind = PacketKind::ForwardingPacket;
        packet.request.forwardTo = {requester};
        packet.response = std::move(response);
        sendPacket(packet);
    }

    void Client::sendPreloadImageResponse(std::string const& requester, std::string const& requestId, std::string const& fileId, bool success, std::string message) {
        PaperLogger.info("sendPreloadImageResponse requester='{}' requestId='{}' fileId='{}' success={} message='{}'", requester, requestId, fileId, success, message);
        Response response;
        response.kind = ResponseKind::PreloadImageForStreamSync;
        response.type = success ? ResponseType::Success : ResponseType::Fail;
        response.respondingToPacketId = requestId;
        response.fileId = fileId;
        response.message = std::move(message);

        Packet packet;
        packet.kind = PacketKind::ForwardingPacket;
        packet.request.forwardTo = {requester};
        packet.response = std::move(response);
        sendPacket(packet);
    }

    void Client::sendRealtimeScore(RealtimeScore score) {
        PaperLogger.info("sendRealtimeScore requested score={} combo={} maxCombo={} user='{}'", score.score, score.combo, score.maxCombo, score.userGuid);
        std::vector<std::string> audience;
        {
            std::scoped_lock lock(mutex_);
            if (score.userGuid.empty()) score.userGuid = selfGuid_;
            auto* tournament = findTournamentLocked(selectedTournamentId_);
            if (!tournament) {
                PaperLogger.warn("sendRealtimeScore ignored: no selected tournament object");
                return;
            }

            auto match = std::find_if(tournament->matches.begin(), tournament->matches.end(), [&](Match const& item) {
                return std::find(item.associatedUsers.begin(), item.associatedUsers.end(), selfGuid_) != item.associatedUsers.end();
            });
            if (match == tournament->matches.end()) {
                PaperLogger.warn("sendRealtimeScore ignored: no current match for user '{}'", selfGuid_);
                return;
            }

            for (auto const& associatedUser : match->associatedUsers) {
                auto user = std::find_if(tournament->users.begin(), tournament->users.end(), [&](User const& item) {
                    return item.guid == associatedUser;
                });
                if (user != tournament->users.end() && user->clientType != 0) audience.push_back(associatedUser);
            }
        }
        if (audience.empty()) {
            PaperLogger.info("sendRealtimeScore ignored: no coordinator/audience users in match");
            return;
        }
        PaperLogger.info("sendRealtimeScore forwarding to {} users", audience.size());

        Packet packet;
        packet.kind = PacketKind::ForwardingPacket;
        packet.request.forwardTo = std::move(audience);
        packet.push.kind = PushKind::RealtimeScore;
        packet.push.realtimeScore = std::move(score);
        sendPacket(packet);
    }

    void Client::sendSongFinished(GameplayParameters const& parameters, SongCompletionType type, int32_t score, int32_t misses, int32_t badCuts, int32_t goodCuts, float endTime, int32_t maxScore, double accuracy) {
        PaperLogger.info("sendSongFinished levelId='{}' type={} score={} misses={} badCuts={} goodCuts={} endTime={} maxScore={} accuracy={}", parameters.beatmap.levelId, int(type), score, misses, badCuts, goodCuts, endTime, maxScore, accuracy);
        SongFinished songFinished;
        songFinished.beatmap = parameters.beatmap;
        songFinished.type = type;
        songFinished.score = score;
        songFinished.misses = misses;
        songFinished.badCuts = badCuts;
        songFinished.goodCuts = goodCuts;
        songFinished.endTime = endTime;
        songFinished.maxScore = maxScore;
        songFinished.accuracy = accuracy;

        bool activeQualifier = false;
        {
            std::scoped_lock lock(mutex_);
            songFinished.tournamentId = selectedTournamentId_;
            if (auto* user = findSelfLocked()) songFinished.player = *user;
            if (auto* tournament = findTournamentLocked(selectedTournamentId_)) {
                auto match = std::find_if(tournament->matches.begin(), tournament->matches.end(), [&](Match const& item) {
                    return std::find(item.associatedUsers.begin(), item.associatedUsers.end(), selfGuid_) != item.associatedUsers.end();
                });
                if (match != tournament->matches.end()) songFinished.matchId = match->guid;
            }
            activeQualifier = activeQualifier_.has_value();
        }
        PaperLogger.info(
            "sendSongFinished resolved tournament='{}' match='{}' playerGuid='{}' playerPlatformId='{}' activeQualifier={}",
            songFinished.tournamentId,
            songFinished.matchId,
            songFinished.player.guid,
            songFinished.player.platformId,
            activeQualifier
        );

        if (songFinished.tournamentId.empty()) {
            PaperLogger.warn("sendSongFinished ignored because tournamentId is empty");
            return;
        }

        Packet packet;
        packet.kind = PacketKind::Push;
        packet.push.kind = PushKind::SongFinished;
        packet.push.songFinished = std::move(songFinished);
        sendPacket(packet);

        std::optional<std::tuple<std::string, std::string, std::string, Map>> qualifier;
        std::optional<User> self;
        {
            std::scoped_lock lock(mutex_);
            qualifier = activeQualifier_;
            if (auto* user = findSelfLocked()) self = *user;
        }

        if (qualifier && type != SongCompletionType::Quit) {
            auto const& eventId = std::get<1>(*qualifier);
            auto const& mapId = std::get<2>(*qualifier);
            auto const& map = std::get<3>(*qualifier);

            LeaderboardEntry entry;
            entry.eventId = eventId;
            entry.mapId = mapId;
            auto [authPlatformId, authUsername] = authIdentityForScores();
            if (self) {
                PaperLogger.info(
                    "Qualifier song-finished fallback identity selfPlatformId='{}' selfName='{}' authPlatformId='{}' authUsername='{}'",
                    self->platformId,
                    self->name,
                    authPlatformId,
                    authUsername
                );
                entry.platformId = authPlatformId.empty() ? self->platformId : authPlatformId;
                entry.username = authUsername.empty() ? self->name : authUsername;
            } else {
                entry.platformId = authPlatformId;
                entry.username = authUsername;
            }
            entry.multipliedScore = score;
            entry.modifiedScore = score;
            entry.maxPossibleScore = maxScore;
            entry.accuracy = accuracy;
            entry.notesMissed = misses;
            entry.badCuts = badCuts;
            entry.goodCuts = goodCuts;
            entry.color = "#ffffff";
            submitQualifierScore(map, entry);
        }
    }

    void Client::setStatus(std::string value) {
        PaperLogger.info("setStatus '{}'", value);
        {
            std::scoped_lock lock(mutex_);
            status_ = std::move(value);
        }
        notifyUi();
    }

    void Client::failPendingResponses(std::string message) {
        PaperLogger.info("failPendingResponses '{}'", message);
        std::vector<std::promise<Response>> promises;
        {
            std::scoped_lock lock(mutex_);
            for (auto& [_, promise] : pendingResponses_) {
                promises.emplace_back(std::move(promise));
            }
            pendingResponses_.clear();
        }

        Response response;
        response.type = ResponseType::Fail;
        response.message = std::move(message);
        for (auto& promise : promises) {
            try {
                promise.set_value(response);
            } catch (...) {
                PaperLogger.warn("Failed to resolve a pending response promise");
            }
        }
        PaperLogger.info("Resolved {} pending responses", promises.size());
    }

    void Client::notifyUi() {
        PaperLogger.info("notifyUi requested");
        UiCallback callback;
        {
            std::scoped_lock lock(mutex_);
            callback = uiCallback_;
        }
        if (!callback) {
            PaperLogger.info("notifyUi ignored because callback is empty");
            return;
        }
        BSML::MainThreadScheduler::Schedule([callback] { callback(); });
    }
}
