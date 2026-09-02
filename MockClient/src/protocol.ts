import { invoke } from "@tauri-apps/api/core";
import { listen, type UnlistenFn } from "@tauri-apps/api/event";
import {
  Command_ModifyGameplay_Modifier,
  Packet,
  Push_SongFinished_CompletionType,
  RealtimeScore,
  Response_ResponseType,
  Timestamp,
  User,
  User_ClientTypes,
  User_DownloadStates,
  User_PlayStates,
  versionCode,
  type GameplayParameters,
  type Match,
  type Request,
  type Response,
  type Event,
  type Command,
  type Tournament,
} from "moons-ta-client";

export type ConnectionStatus = "idle" | "connecting" | "connected" | "error" | "disconnected";
export type ScoreAction = "goodCut" | "miss" | "badCut" | "bomb" | "wall";

export interface IdentityConfig {
  platformId: string;
  username: string;
}

export interface ConnectConfig {
  address: string;
  port: number;
  certificatePath: string;
  certificatePassword: string;
  acceptInvalidCertificate: boolean;
  identities: IdentityConfig[];
}

export interface LogEntry {
  time: string;
  clientId: string;
  level: "info" | "success" | "error" | "packet-in" | "packet-out";
  summary: string;
  detail?: string;
}

export interface LoadedMap {
  hash: string;
  name: string;
  durationSeconds: number;
  coverUrl: string;
  downloadUrl: string;
  cachedPath: string;
  gameplay?: GameplayParameters;
}

export interface ScoreState {
  score: number;
  maxScore: number;
  combo: number;
  maxCombo: number;
  goodCuts: number;
  misses: number;
  badCuts: number;
  bombHits: number;
  wallHits: number;
  songPosition: number;
  health: number;
  playing: boolean;
}

export interface MockRuntime {
  id: string;
  platformId: string;
  username: string;
  token: string;
  status: ConnectionStatus;
  statusMessage: string;
  selfGuid: string;
  tournaments: Tournament[];
  joinedTournamentId: string;
  currentMatchId: string;
  loadedMap?: LoadedMap;
  score: ScoreState;
  modifiers: string[];
  heartbeat?: ReturnType<typeof setInterval>;
  simulation?: ReturnType<typeof setInterval>;
  songStartedAt?: number;
}

interface RawPacketEvent { clientId: string; payload: string }
interface SocketStatusEvent { clientId: string; status: ConnectionStatus; message: string }

const emptyScore = (): ScoreState => ({
  score: 0, maxScore: 0, combo: 0, maxCombo: 0, goodCuts: 0,
  misses: 0, badCuts: 0, bombHits: 0, wallHits: 0,
  songPosition: 0, health: 1, playing: false,
});

const bytesToBase64 = (bytes: Uint8Array) => {
  let binary = "";
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary);
};

const base64ToBytes = (value: string) => {
  const binary = atob(value);
  return Uint8Array.from(binary, (character) => character.charCodeAt(0));
};

const packetDetail = (packet: Packet) => JSON.stringify(packet, (_, value) =>
  typeof value === "bigint" ? value.toString() : value, 2);

const packetSummary = (packet: Packet) => {
  switch (packet.packet.oneofKind) {
    case "response": return `response:${packet.packet.response.details.oneofKind ?? "empty"} ${packet.packet.response.type === Response_ResponseType.Success ? "success" : "failed"}`;
    case "request": return `request:${packet.packet.request.type.oneofKind ?? "empty"}`;
    case "command": return `command:${packet.packet.command.type.oneofKind ?? "empty"}`;
    case "event": return `event:${packet.packet.event.changedObject.oneofKind ?? "empty"}`;
    case "push": return `push:${packet.packet.push.data.oneofKind ?? "empty"}`;
    default: return packet.packet.oneofKind ?? "empty";
  }
};

export class MockFleet {
  clients: MockRuntime[] = [];
  logs: LogEntry[] = [];
  private listeners = new Set<() => void>();
  private unlisten: UnlistenFn[] = [];

  subscribe(listener: () => void) {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  private changed() {
    for (const listener of this.listeners) listener();
  }

  private log(clientId: string, level: LogEntry["level"], summary: string, detail?: string) {
    this.logs = [...this.logs.slice(-4998), { time: new Date().toISOString(), clientId, level, summary, detail }];
    this.changed();
  }

  async initialize() {
    this.unlisten.push(await listen<RawPacketEvent>("mock-raw-packet", ({ payload }) => {
      void this.receive(payload.clientId, base64ToBytes(payload.payload));
    }));
    this.unlisten.push(await listen<SocketStatusEvent>("mock-socket-status", ({ payload }) => {
      const client = this.get(payload.clientId);
      if (!client) return;
      client.status = payload.status;
      client.statusMessage = payload.message;
      this.log(client.id, payload.status === "error" ? "error" : "info", payload.message);
      if (payload.status === "disconnected" || payload.status === "error") this.stopTimers(client);
      this.changed();
    }));
  }

  destroy() {
    for (const unlisten of this.unlisten) unlisten();
    for (const client of this.clients) this.stopTimers(client);
  }

  private get(id: string) { return this.clients.find((client) => client.id === id); }

  async connect(config: ConnectConfig) {
    await this.disconnectAll();
    this.logs = [];
    this.clients = config.identities.map((identity, index) => ({
      id: `mock-${index + 1}`,
      platformId: identity.platformId,
      username: identity.username,
      token: "",
      status: "connecting",
      statusMessage: "Preparing token",
      selfGuid: "",
      tournaments: [],
      joinedTournamentId: "",
      currentMatchId: "",
      score: emptyScore(),
      modifiers: [],
    }));
    this.changed();

    await Promise.all(this.clients.map(async (client) => {
      try {
        client.token = await invoke<string>("sign_mock_token", {
          certificatePath: config.certificatePath,
          certificatePassword: config.certificatePassword,
          platformId: client.platformId,
          username: client.username,
        });
        await invoke("connect_socket", {
          clientId: client.id,
          address: config.address,
          port: config.port,
          acceptInvalidCertificate: config.acceptInvalidCertificate,
        });
        client.heartbeat = setInterval(() => void this.send(client, {
          oneofKind: "heartbeat", heartbeat: true,
        }), 10_000);
        await this.request(client, {
          oneofKind: "connect",
          connect: { clientVersion: versionCode, uiVersion: 0 },
        });
      } catch (error) {
        client.status = "error";
        client.statusMessage = String(error);
        this.log(client.id, "error", "Connection failed", String(error));
      }
    }));
    this.changed();
  }

  async disconnectAll() {
    await Promise.all(this.clients.map((client) => this.disconnect(client.id)));
  }

  async disconnect(clientId: string) {
    const client = this.get(clientId);
    if (!client) return;
    this.stopTimers(client);
    await invoke("disconnect_socket", { clientId }).catch(() => undefined);
    client.status = "disconnected";
    client.statusMessage = "Disconnected by user";
    this.changed();
  }

  private stopTimers(client: MockRuntime) {
    if (client.heartbeat) clearInterval(client.heartbeat);
    if (client.simulation) clearInterval(client.simulation);
    client.heartbeat = undefined;
    client.simulation = undefined;
    client.score.playing = false;
  }

  private async send(client: MockRuntime, body: Packet["packet"], from = client.selfGuid) {
    const packet = Packet.create({ token: client.token, id: crypto.randomUUID(), from, packet: body });
    this.log(client.id, "packet-out", packetSummary(packet), packetDetail(packet));
    await invoke("send_packet", { clientId: client.id, payload: bytesToBase64(Packet.toBinary(packet)) });
    return packet.id;
  }

  private request(client: MockRuntime, type: Request["type"]) {
    return this.send(client, { oneofKind: "request", request: { type } });
  }

  async joinTournament(clientId: string, tournamentId: string, password = "") {
    const client = this.get(clientId)!;
    await this.request(client, { oneofKind: "join", join: { tournamentId, password, modList: ["TA Mock Client"] } });
  }

  async leaveTournament(clientId: string) {
    const client = this.get(clientId)!;
    if (!client.joinedTournamentId) return;
    await this.request(client, {
      oneofKind: "leaveTournament",
      leaveTournament: { tournamentId: client.joinedTournamentId },
    });
  }

  async joinMatch(clientId: string, matchId: string) {
    const client = this.get(clientId)!;
    await this.request(client, {
      oneofKind: "addUserToMatch",
      addUserToMatch: { tournamentId: client.joinedTournamentId, matchId, userId: client.selfGuid },
    });
  }

  async leaveMatch(clientId: string) {
    const client = this.get(clientId)!;
    if (!client.currentMatchId) return;
    await this.request(client, {
      oneofKind: "removeUserFromMatch",
      removeUserFromMatch: { tournamentId: client.joinedTournamentId, matchId: client.currentMatchId, userId: client.selfGuid },
    });
  }

  private currentTournament(client: MockRuntime) {
    return client.tournaments.find((tournament) => tournament.guid === client.joinedTournamentId);
  }

  private self(client: MockRuntime) {
    return this.currentTournament(client)?.users.find((user) => user.guid === client.selfGuid);
  }

  private currentMatch(client: MockRuntime): Match | undefined {
    return this.currentTournament(client)?.matches.find((match) => match.guid === client.currentMatchId);
  }

  private async updateSelf(client: MockRuntime, changes: Partial<User>) {
    const current = this.self(client) ?? User.create({
      guid: client.selfGuid, name: client.username, platformId: client.platformId,
      clientType: User_ClientTypes.Player, isMock: true,
    });
    Object.assign(current, changes);
    await this.request(client, {
      oneofKind: "updateUser",
      updateUser: { tournamentId: client.joinedTournamentId, user: current },
    });
  }

  private async receive(clientId: string, bytes: Uint8Array) {
    const client = this.get(clientId);
    if (!client) return;
    try {
      const packet = Packet.fromBinary(bytes);
      this.log(client.id, "packet-in", packetSummary(packet), packetDetail(packet));
      if (packet.packet.oneofKind === "response") this.handleResponse(client, packet.packet.response);
      else if (packet.packet.oneofKind === "event") this.handleEvent(client, packet.packet.event.changedObject);
      else if (packet.packet.oneofKind === "command") await this.handleCommand(client, packet.packet.command);
      else if (packet.packet.oneofKind === "request") await this.handleIncomingRequest(client, packet);
    } catch (error) {
      this.log(client.id, "error", "Could not decode incoming packet", String(error));
    }
    this.changed();
  }

  private handleResponse(client: MockRuntime, response: Response) {
    if (response.details.oneofKind === "connect") {
      const connect = response.details.connect;
      if (response.type === Response_ResponseType.Success) {
        client.tournaments = connect.state?.tournaments ?? [];
        client.status = "connected";
        client.statusMessage = `Connected · server protocol ${connect.serverVersion}`;
      } else {
        client.status = "error";
        client.statusMessage = connect.message || `Version mismatch (server ${connect.serverVersion}, client ${versionCode})`;
      }
    } else if (response.details.oneofKind === "join" && response.type === Response_ResponseType.Success) {
      const join = response.details.join;
      client.selfGuid = join.selfGuid;
      client.joinedTournamentId = join.tournamentId;
      client.tournaments = join.state?.tournaments ?? client.tournaments;
      void this.updateSelf(client, { playState: User_PlayStates.WaitingForCoordinator });
    } else if (response.details.oneofKind === "leaveTournament" && response.type === Response_ResponseType.Success) {
      client.joinedTournamentId = "";
      client.currentMatchId = "";
      client.loadedMap = undefined;
      this.stopSimulation(client, false);
    }
    if (response.type !== Response_ResponseType.Success) {
      this.log(client.id, "error", `Request failed: ${response.details.oneofKind ?? "unknown"}`, packetDetail(Packet.create({ packet: { oneofKind: "response", response } })));
    }
  }

  private handleEvent(client: MockRuntime, event: Event["changedObject"]) {
    const tournament = this.currentTournament(client);
    if (event.oneofKind === "tournamentUpdated") {
      const updated = event.tournamentUpdated.tournament;
      if (updated) client.tournaments = [...client.tournaments.filter((item) => item.guid !== updated.guid), updated];
    } else if (event.oneofKind === "matchCreated" && tournament && event.matchCreated.match) {
      tournament.matches = [...tournament.matches, event.matchCreated.match];
    } else if (event.oneofKind === "matchUpdated" && tournament && event.matchUpdated.match) {
      tournament.matches = [...tournament.matches.filter((match) => match.guid !== event.matchUpdated.match!.guid), event.matchUpdated.match];
      this.refreshCurrentMatch(client);
    } else if (event.oneofKind === "matchDeleted" && tournament && event.matchDeleted.match) {
      tournament.matches = tournament.matches.filter((match) => match.guid !== event.matchDeleted.match!.guid);
      this.refreshCurrentMatch(client);
    } else if ((event.oneofKind === "userAdded" || event.oneofKind === "userUpdated") && tournament) {
      const user = event.oneofKind === "userAdded" ? event.userAdded.user : event.userUpdated.user;
      if (user) tournament.users = [...tournament.users.filter((item) => item.guid !== user.guid), user];
    } else if (event.oneofKind === "userLeft" && tournament && event.userLeft.user) {
      tournament.users = tournament.users.filter((user) => user.guid !== event.userLeft.user!.guid);
    }
  }

  private refreshCurrentMatch(client: MockRuntime) {
    const match = this.currentTournament(client)?.matches.find((item) => item.associatedUsers.includes(client.selfGuid));
    client.currentMatchId = match?.guid ?? "";
  }

  private async handleIncomingRequest(client: MockRuntime, packet: Packet) {
    if (packet.packet.oneofKind !== "request" || packet.packet.request.type.oneofKind !== "loadSong") return;
    const request = packet.packet.request.type.loadSong;
    await this.updateSelf(client, { downloadState: User_DownloadStates.Downloading });
    try {
      client.loadedMap = await invoke<LoadedMap>("fetch_beatsaver_map", { levelId: request.levelId });
      await this.updateSelf(client, { downloadState: User_DownloadStates.Downloaded });
      await this.forward(client, [packet.from], {
        oneofKind: "response",
        response: {
          type: Response_ResponseType.Success,
          respondingToPacketId: packet.id,
          details: { oneofKind: "loadSong", loadSong: { levelId: request.levelId, message: "Map cached" } },
        },
      });
      this.log(client.id, "success", `Downloaded ${client.loadedMap.name}`, client.loadedMap.cachedPath);
    } catch (error) {
      await this.updateSelf(client, { downloadState: User_DownloadStates.DownloadError });
      this.log(client.id, "error", `Map download failed: ${request.levelId}`, String(error));
    }
  }

  private async handleCommand(client: MockRuntime, command: Command) {
    if (command.type.oneofKind === "returnToMenu") {
      await this.backToMenu(client.id);
    } else if (command.type.oneofKind === "playSong") {
      await this.play(client, command.type.playSong.gameplayParameters);
    } else if (command.type.oneofKind === "modifyGameplay") {
      const names = ["Colors flipped", "Hands flipped", "Blue notes disabled", "Red notes disabled"];
      const modifier = command.type.modifyGameplay.modifier;
      client.modifiers = [...client.modifiers, names[modifier] ?? Command_ModifyGameplay_Modifier[modifier]];
      this.log(client.id, "info", names[modifier] ?? `Gameplay modifier ${modifier}`);
    } else if (command.type.oneofKind === "showColorForStreamSync") {
      this.log(client.id, "info", `Stream-sync color: ${command.type.showColorForStreamSync.color}`);
    }
  }

  private async forward(client: MockRuntime, recipients: string[], body: Packet["packet"]) {
    const inner = Packet.create({ token: client.token, id: crypto.randomUUID(), from: client.selfGuid, packet: body });
    return this.send(client, {
      oneofKind: "forwardingPacket",
      forwardingPacket: { forwardTo: recipients, packet: inner },
    });
  }

  private async play(client: MockRuntime, gameplay?: GameplayParameters) {
    if (!gameplay?.beatmap) {
      this.log(client.id, "error", "Play command did not include a beatmap");
      return;
    }
    if (!client.loadedMap || !gameplay.beatmap.levelId.includes(client.loadedMap.hash)) {
      try {
        client.loadedMap = await invoke<LoadedMap>("fetch_beatsaver_map", { levelId: gameplay.beatmap.levelId });
      } catch (error) {
        client.loadedMap = {
          hash: gameplay.beatmap.levelId, name: gameplay.beatmap.name || gameplay.beatmap.levelId,
          durationSeconds: 180, coverUrl: "", downloadUrl: "", cachedPath: "",
        };
        this.log(client.id, "error", "Map metadata unavailable; using a 3-minute simulation", String(error));
      }
    }
    client.loadedMap.gameplay = gameplay;
    client.score = { ...emptyScore(), playing: true };
    client.songStartedAt = Date.now();
    await this.updateSelf(client, { playState: User_PlayStates.InGame });
    if (client.simulation) clearInterval(client.simulation);
    client.simulation = setInterval(() => {
      const roll = Math.random();
      const action: ScoreAction = roll < 0.94 ? "goodCut" : roll < 0.965 ? "miss" : roll < 0.985 ? "badCut" : roll < 0.995 ? "bomb" : "wall";
      void this.scoreAction(client.id, action, true);
    }, 500);
    this.log(client.id, "success", `Playing ${client.loadedMap.name} automatically`);
  }

  async scoreAction(clientId: string, action: ScoreAction, automatic = false) {
    const client = this.get(clientId)!;
    if (!client.score.playing) return;
    const score = client.score;
    if (action === "goodCut") {
      score.goodCuts += 1;
      score.combo += 1;
      score.maxCombo = Math.max(score.maxCombo, score.combo);
      const multiplier = score.combo >= 14 ? 8 : score.combo >= 6 ? 4 : score.combo >= 2 ? 2 : 1;
      score.score += Math.round((100 + Math.random() * 15) * multiplier);
    } else {
      score.combo = 0;
      if (action === "miss") score.misses += 1;
      if (action === "badCut") score.badCuts += 1;
      if (action === "bomb") score.bombHits += 1;
      if (action === "wall") { score.wallHits += 1; score.health = Math.max(0, score.health - 0.05); }
      if (!automatic) this.log(client.id, "info", `Manual score event: ${action}`);
    }
    const notes = score.goodCuts + score.misses + score.badCuts;
    score.maxScore = Math.max(115, notes * 920);
    score.songPosition = client.songStartedAt ? (Date.now() - client.songStartedAt) / 1000 : 0;
    await this.sendRealtimeScore(client);
    if (score.songPosition >= (client.loadedMap?.durationSeconds ?? 180)) await this.finishSong(client, Push_SongFinished_CompletionType.Passed);
    this.changed();
  }

  private async sendRealtimeScore(client: MockRuntime) {
    const match = this.currentMatch(client);
    const recipients = match?.associatedUsers.filter((id) => id !== client.selfGuid) ?? [];
    const timestamp = Timestamp.now();
    const score = RealtimeScore.create({
      userGuid: client.selfGuid,
      score: client.score.score,
      scoreWithModifiers: client.score.score,
      maxScore: client.score.maxScore,
      maxScoreWithModifiers: client.score.maxScore,
      combo: client.score.combo,
      playerHealth: client.score.health,
      accuracy: client.score.maxScore ? client.score.score / client.score.maxScore : 0,
      songPosition: client.score.songPosition,
      notesMissed: client.score.misses,
      badCuts: client.score.badCuts,
      bombHits: client.score.bombHits,
      wallHits: client.score.wallHits,
      maxCombo: client.score.maxCombo,
      timestamp,
      songStartTime: Timestamp.fromDate(new Date(client.songStartedAt ?? Date.now())),
    });
    await this.forward(client, recipients, { oneofKind: "push", push: { data: { oneofKind: "realtimeScore", realtimeScore: score } } });
  }

  private async finishSong(client: MockRuntime, type: Push_SongFinished_CompletionType) {
    if (!client.score.playing) return;
    if (client.simulation) clearInterval(client.simulation);
    client.simulation = undefined;
    client.score.playing = false;
    const beatmap = client.loadedMap?.gameplay?.beatmap;
    await this.send(client, {
      oneofKind: "push",
      push: {
        data: {
          oneofKind: "songFinished",
          songFinished: {
            player: this.self(client), beatmap, type,
            score: client.score.score, misses: client.score.misses, badCuts: client.score.badCuts,
            goodCuts: client.score.goodCuts, endTime: client.score.songPosition,
            tournamentId: client.joinedTournamentId, matchId: client.currentMatchId,
            maxScore: client.score.maxScore,
            accuracy: client.score.maxScore ? client.score.score / client.score.maxScore : 0,
          },
        },
      },
    });
    await this.updateSelf(client, { playState: User_PlayStates.WaitingForCoordinator });
    this.log(client.id, "success", type === Push_SongFinished_CompletionType.Passed ? "Song completed" : "Returned to menu");
  }

  async backToMenu(clientId: string) {
    const client = this.get(clientId)!;
    if (client.score.playing) await this.finishSong(client, Push_SongFinished_CompletionType.Quit);
    else await this.updateSelf(client, { playState: User_PlayStates.InMenu });
  }

  private stopSimulation(client: MockRuntime, clearMap = true) {
    if (client.simulation) clearInterval(client.simulation);
    client.simulation = undefined;
    client.score.playing = false;
    if (clearMap) client.loadedMap = undefined;
  }

  exportLogs() {
    return this.logs.map((entry) => `[${entry.time}] [${entry.clientId}] [${entry.level}] ${entry.summary}${entry.detail ? `\n${entry.detail}` : ""}`).join("\n");
  }

  clearLogs() {
    this.logs = [];
    this.changed();
  }
}
