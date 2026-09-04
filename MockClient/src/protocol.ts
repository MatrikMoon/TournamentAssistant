import { invoke } from "@tauri-apps/api/core";
import { listen, type UnlistenFn } from "@tauri-apps/api/event";
import {
  Command_ModifyGameplay_Modifier,
  Packet,
  Push_SongFinished_CompletionType,
  RealtimeScore,
  ReplayCompletion,
  ReplayNoteEventType,
  ReplayPlatform,
  Response_ResponseType,
  Timestamp,
  User,
  User_ClientTypes,
  User_DownloadStates,
  User_PlayStates,
  type GameplayParameters,
  type Match,
  type Request,
  type Response,
  type Event,
  type Command,
  type Tournament,
} from "moons-ta-client";

export const mockClientVersion = "1.3.1";
export const mockClientVersionCode = 1310;

export type ConnectionStatus = "idle" | "connecting" | "connected" | "error" | "disconnected";
export type ScoreAction = "goodCut" | "miss" | "badCut" | "bomb" | "wall";

export interface IdentityConfig {
  platformId: string;
  username: string;
  modList: string[];
}

export interface ConnectConfig {
  address: string;
  port: number;
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
  key: string;
  name: string;
  description: string;
  songSubName: string;
  songAuthorName: string;
  levelAuthorName: string;
  bpm: number;
  durationSeconds: number;
  upvotes: number;
  downvotes: number;
  rating: number;
  createdAt: string;
  versionCreatedAt: string;
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

interface BsorVector3 { x: number; y: number; z: number }
interface BsorQuaternion { x: number; y: number; z: number; w: number }
interface BsorPose { position: BsorVector3; rotation: BsorQuaternion }
interface BsorFrame { time: number; fps: number; head: BsorPose; left: BsorPose; right: BsorPose }
interface BsorCutInfo {
  directionOk: boolean; saberSpeed: number; saberDirection: BsorVector3; saberType: number;
  timeDeviation: number; cutDirectionDeviation: number; cutPoint: BsorVector3; cutNormal: BsorVector3;
  cutDistanceToCenter: number; cutAngle: number; beforeCutRating: number; afterCutRating: number;
}
interface BsorNote { noteId: number; eventTime: number; spawnTime: number; eventType: number; cut?: BsorCutInfo }
interface BsorWall { energy: number; time: number }
interface BsorHeight { height: number; time: number }
interface BsorPause { duration: number; time: number }
interface BsorInfo {
  version: string; gameVersion: string; timestamp: string; playerId: string; playerName: string; platform: string;
  trackingSystem: string; hmd: string; controller: string; hash: string; songName: string; mapper: string;
  difficulty: string; score: number; mode: string; environment: string; modifiers: string; jumpDistance: number;
  leftHanded: boolean; height: number; startTime: number; failTime: number; speed: number;
}
interface BsorReplay {
  sourceUrl: string; sourceRank: number; cachedPath: string; info: BsorInfo; frames: BsorFrame[];
  notes: BsorNote[]; walls: BsorWall[]; heights: BsorHeight[]; pauses: BsorPause[];
}

interface ReplayIndices {
  frames: number; notes: number; walls: number; heights: number; pauses: number; sequence: number;
  scoreEvents: number; comboEvents: number; multiplierEvents: number; energyEvents: number; pauseEvents: number;
  lastScoreAt: number;
}

export interface MockRuntime {
  id: string;
  platformId: string;
  username: string;
  modList: string[];
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
  activity: string;
  replay?: BsorReplay;
  replayStreamId?: string;
  replayIndices?: ReplayIndices;
  replayTicking?: boolean;
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

export class MockClients {
  clients: MockRuntime[] = [];
  logs: LogEntry[] = [];
  private listeners = new Set<() => void>();
  private unlisten: UnlistenFn[] = [];
  private replayLoads = new Map<string, Promise<BsorReplay[]>>();

  subscribe(listener: () => void) {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  private changed() {
    for (const listener of this.listeners) listener();
  }

  private setActivity(client: MockRuntime, activity: string) {
    client.activity = activity;
    this.changed();
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
      if (payload.status === "disconnected" || payload.status === "error") {
        this.stopTimers(client);
        client.selfGuid = "";
        client.tournaments = [];
        client.joinedTournamentId = "";
        client.currentMatchId = "";
        client.loadedMap = undefined;
        client.replay = undefined;
        client.modifiers = [];
        client.score = emptyScore();
        client.activity = "Not connected";
      }
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
    this.replayLoads.clear();
    this.clients = config.identities.map((identity, index) => ({
      id: `mock-${index + 1}`,
      platformId: identity.platformId,
      username: identity.username,
      modList: [...identity.modList],
      token: "",
      status: "connecting",
      statusMessage: "Preparing token",
      selfGuid: "",
      tournaments: [],
      joinedTournamentId: "",
      currentMatchId: "",
      score: emptyScore(),
      modifiers: [],
      activity: "Connecting",
    }));
    this.changed();

    await Promise.all(this.clients.map(async (client) => {
      try {
        client.token = await invoke<string>("sign_mock_token", {
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
          connect: { clientVersion: mockClientVersionCode, uiVersion: 0 },
        });
      } catch (error) {
        client.status = "error";
        client.statusMessage = String(error);
        client.activity = "Connection failed";
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
    client.activity = "Disconnected";
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

  async joinTournament(clientId: string, tournamentId: string) {
    const client = this.get(clientId)!;
    this.setActivity(client, "Joining tournament");
    await this.request(client, { oneofKind: "join", join: { tournamentId, password: "", modList: client.modList } });
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
    client.currentMatchId = "";
    client.activity = "Waiting for coordinator to create a match";
    this.changed();
  }

  async setModList(clientId: string, modList: string[]) {
    const client = this.get(clientId);
    if (!client) return;
    client.modList = [...new Set(modList.map(mod => mod.trim()).filter(Boolean))];
    this.setActivity(client, "Updating mod list");
    if (client.joinedTournamentId) await this.updateSelf(client, { modList: client.modList });
    client.activity = "Mod list updated";
    this.log(client.id, "info", "Mod list updated", client.modList.join(", ") || "No mods");
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
      modList: client.modList,
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
        client.activity = "Choose a tournament";
      } else {
        client.status = "error";
        client.statusMessage = connect.message || `Version mismatch (server ${connect.serverVersion}, client ${mockClientVersionCode})`;
      }
    } else if (response.details.oneofKind === "join" && response.type === Response_ResponseType.Success) {
      const join = response.details.join;
      client.selfGuid = join.selfGuid;
      client.joinedTournamentId = join.tournamentId;
      client.tournaments = join.state?.tournaments ?? client.tournaments;
      client.activity = "Waiting for coordinator to create a match";
      void this.updateSelf(client, { playState: User_PlayStates.WaitingForCoordinator });
    } else if (response.details.oneofKind === "leaveTournament" && response.type === Response_ResponseType.Success) {
      client.joinedTournamentId = "";
      client.currentMatchId = "";
      client.loadedMap = undefined;
      client.activity = "Choose a tournament";
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
      this.refreshCurrentMatch(client);
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
    if (match) client.activity = match.selectedMap ? "Match ready" : "Waiting for coordinator to select a map";
    else if (client.joinedTournamentId) client.activity = "Waiting for coordinator to create a match";
  }

  private async handleIncomingRequest(client: MockRuntime, packet: Packet) {
    if (packet.packet.oneofKind !== "request" || packet.packet.request.type.oneofKind !== "loadSong") return;
    const request = packet.packet.request.type.loadSong;
    this.setActivity(client, "Downloading map from BeatSaver");
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
      client.activity = "Map downloaded; waiting to play";
    } catch (error) {
      await this.updateSelf(client, { downloadState: User_DownloadStates.DownloadError });
      this.log(client.id, "error", `Map download failed: ${request.levelId}`, String(error));
      client.activity = "Map download failed";
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
          key: "", description: "", songSubName: "", songAuthorName: "", levelAuthorName: "",
          bpm: 0, durationSeconds: 180, upvotes: 0, downvotes: 0, rating: 0,
          createdAt: "", versionCreatedAt: "", coverUrl: "", downloadUrl: "", cachedPath: "",
        };
        this.log(client.id, "error", "Map metadata unavailable; using a 3-minute simulation", String(error));
      }
    }
    client.loadedMap.gameplay = gameplay;
    client.replay = undefined;
    const replayStreamingEnabled = this.currentTournament(client)?.settings?.enableReplayStreaming === true;
    if (replayStreamingEnabled) {
      this.setActivity(client, `Downloading the first ${this.clients.length} BeatLeader replay${this.clients.length === 1 ? "" : "s"}`);
      try {
        client.replay = await this.replayFor(client, gameplay);
        this.setActivity(client, `Loaded BeatLeader replay #${client.replay.sourceRank} from ${client.replay.info.playerName}`);
        this.log(client.id, "success", `Loaded BeatLeader replay #${client.replay.sourceRank}`, client.replay.cachedPath);
      } catch (error) {
        this.setActivity(client, "Replay unavailable; using score simulation");
        this.log(client.id, "error", "BeatLeader replay unavailable", String(error));
      }
    }
    client.score = { ...emptyScore(), playing: true };
    client.songStartedAt = Date.now();
    await this.updateSelf(client, { playState: User_PlayStates.InGame });
    if (client.simulation) clearInterval(client.simulation);
    if (client.replay) {
      await this.startReplayStream(client);
      client.simulation = setInterval(() => void this.replayTick(client), 100);
    } else {
      client.simulation = setInterval(() => void this.randomScoreTick(client), 500);
    }
    client.activity = client.replay ? "Playing and streaming replay" : "Playing with generated score";
    this.log(client.id, "success", `${client.activity}: ${client.loadedMap.name}`);
  }

  private async replayFor(client: MockRuntime, gameplay: GameplayParameters) {
    const beatmap = gameplay.beatmap!;
    const difficulty = ["Easy", "Normal", "Hard", "Expert", "ExpertPlus"][beatmap.difficulty] ?? String(beatmap.difficulty);
    const characteristic = beatmap.characteristic?.serializedName || "Standard";
    const key = `${beatmap.levelId}|${difficulty}|${characteristic}`;
    let loading = this.replayLoads.get(key);
    if (!loading) {
      loading = invoke<BsorReplay[]>("fetch_beatleader_replays", {
        levelId: beatmap.levelId,
        difficulty,
        characteristic,
        count: this.clients.length,
      });
      this.replayLoads.set(key, loading);
      loading.catch(() => this.replayLoads.delete(key));
    }
    const replays = await loading;
    const index = Math.max(0, this.clients.findIndex(item => item.id === client.id));
    return replays[index] ?? replays[index % replays.length] ?? replays[0];
  }

  private randomScoreTick(client: MockRuntime) {
    const roll = Math.random();
    const action: ScoreAction = roll < 0.94 ? "goodCut" : roll < 0.965 ? "miss" : roll < 0.985 ? "badCut" : roll < 0.995 ? "bomb" : "wall";
    return this.scoreAction(client.id, action, true);
  }

  private replayPlatform(platform: string) {
    const normalized = platform.toLowerCase();
    if (normalized.includes("steam")) return ReplayPlatform.STEAM;
    if (normalized.includes("oculus") || normalized.includes("rift")) return ReplayPlatform.OCULUS_PC;
    if (normalized.includes("quest") || normalized.includes("meta")) return ReplayPlatform.META_QUEST;
    return ReplayPlatform.DEV;
  }

  private replayCursor(client: MockRuntime) {
    const now = BigInt(Date.now());
    return {
      sequence: BigInt(client.replayIndices?.sequence ?? 0),
      songTimeMs: BigInt(Math.max(0, Math.round(client.score.songPosition * 1000))),
      serverTimeUnixMs: 0n,
      clientTimeUnixMs: now,
    };
  }

  private replayCounts(client: MockRuntime) {
    const index = client.replayIndices!;
    return {
      poseFrames: BigInt(index.frames), heightEvents: BigInt(index.heights), noteEvents: BigInt(index.notes),
      scoreEvents: BigInt(index.scoreEvents), comboEvents: BigInt(index.comboEvents),
      multiplierEvents: BigInt(index.multiplierEvents), energyEvents: BigInt(index.energyEvents),
      pauseEvents: BigInt(index.pauseEvents),
    };
  }

  private async startReplayStream(client: MockRuntime) {
    const replay = client.replay!;
    const beatmap = client.loadedMap!.gameplay!.beatmap!;
    const replayModifiers = replay.info.modifiers.split(",").map(value => value.trim()).filter(Boolean);
    client.replayStreamId = crypto.randomUUID();
    client.replayIndices = {
      frames: 0, notes: 0, walls: 0, heights: 0, pauses: 0, sequence: 0,
      scoreEvents: 0, comboEvents: 0, multiplierEvents: 0, energyEvents: 0, pauseEvents: 0, lastScoreAt: 0,
    };
    await this.send(client, {
      oneofKind: "replayStream",
      replayStream: {
        streamId: client.replayStreamId, connectionId: client.id, playerId: client.platformId,
        matchId: client.currentMatchId,
        body: {
          oneofKind: "start",
          start: {
            protocolVersion: 1,
            player: { playerId: client.platformId, platform: this.replayPlatform(replay.info.platform), gameVersion: replay.info.gameVersion, clientVersion: mockClientVersion },
            beatmap: { mapHash: replay.info.hash, levelId: beatmap.levelId, difficulty: beatmap.difficulty, difficultyName: replay.info.difficulty, characteristic: replay.info.mode, modifiers: replayModifiers, maxScore: Math.max(0, client.score.maxScore) },
            clientStartTimeUnixMs: BigInt(Date.now()), serverStartTimeUnixMs: 0n, gameSessionId: crypto.randomUUID(),
            replayMetadata: {
              replayVersion: replay.info.version, levelId: beatmap.levelId, difficulty: beatmap.difficulty,
              characteristic: replay.info.mode, environment: replay.info.environment, modifiers: replayModifiers,
              noteSpawnOffset: 0, leftHanded: replay.info.leftHanded, initialHeight: replay.info.height,
              roomRotation: 0, roomCenter: { x: 0, y: 0, z: 0 }, gameVersion: replay.info.gameVersion,
              pluginVersion: mockClientVersion, platform: replay.info.platform, songSpeed: replay.info.speed || 1,
              jumpDistance: replay.info.jumpDistance,
            },
            replayExtensions: [],
          },
        },
      },
    });
  }

  private replayNote(note: BsorNote) {
    const id = Math.abs(note.noteId);
    const cut = note.cut;
    const eventTypes = [ReplayNoteEventType.GOOD_CUT, ReplayNoteEventType.BAD_CUT, ReplayNoteEventType.MISS, ReplayNoteEventType.BOMB];
    return {
      noteId: {
        timeSeconds: note.spawnTime,
        lineLayer: Math.floor(id / 100) % 10,
        lineIndex: Math.floor(id / 1000) % 10,
        colorType: Math.floor(id / 10) % 10,
        cutDirection: id % 10,
        gameplayType: 0,
        scoringType: Math.floor(id / 10000) - 2,
        cutDirectionAngleOffset: 0,
      },
      eventType: eventTypes[note.eventType] ?? ReplayNoteEventType.UNSPECIFIED,
      cutPoint: cut?.cutPoint, cutNormal: cut?.cutNormal, saberDirection: cut?.saberDirection,
      saberType: cut?.saberType ?? 0, directionOk: cut?.directionOk ?? false,
      saberSpeed: cut?.saberSpeed ?? 0, cutAngle: cut?.cutAngle ?? 0,
      cutDistanceToCenter: cut?.cutDistanceToCenter ?? 0,
      cutDirectionDeviation: cut?.cutDirectionDeviation ?? 0,
      beforeCutRating: cut?.beforeCutRating ?? 0, afterCutRating: cut?.afterCutRating ?? 0,
      timeSeconds: note.eventTime, unityTimescale: 1, timeSyncTimescale: 1,
      timeDeviation: cut?.timeDeviation ?? 0,
    };
  }

  private async replayTick(client: MockRuntime) {
    if (!client.score.playing || !client.replay || !client.replayIndices || client.replayTicking) return;
    client.replayTicking = true;
    try {
      const replay = client.replay;
      const index = client.replayIndices;
      const position = client.songStartedAt ? (Date.now() - client.songStartedAt) / 1000 : 0;
      client.score.songPosition = position;
      if (position - index.lastScoreAt >= .5) {
        index.lastScoreAt = position;
        await this.randomScoreTick(client);
      }
      let remaining = 220;
      const poseFrames = [];
      while (remaining && index.frames < replay.frames.length && replay.frames[index.frames].time <= position) {
        const frame = replay.frames[index.frames++]; remaining--;
        poseFrames.push({ head: frame.head, left: frame.left, right: frame.right, fps: frame.fps, timeSeconds: frame.time });
      }
      const noteEvents = [];
      while (remaining && index.notes < replay.notes.length && replay.notes[index.notes].eventTime <= position) {
        noteEvents.push(this.replayNote(replay.notes[index.notes++])); remaining--;
      }
      const heightEvents = [];
      while (remaining && index.heights < replay.heights.length && replay.heights[index.heights].time <= position) {
        const height = replay.heights[index.heights++]; heightEvents.push({ height: height.height, timeSeconds: height.time }); remaining--;
      }
      const energyEvents = [];
      while (remaining && index.walls < replay.walls.length && replay.walls[index.walls].time <= position) {
        const wall = replay.walls[index.walls++]; energyEvents.push({ energy: wall.energy, timeSeconds: wall.time }); remaining--;
      }
      const pauseEvents = [];
      while (remaining > 1 && index.pauses < replay.pauses.length && replay.pauses[index.pauses].time <= position) {
        const pause = replay.pauses[index.pauses++];
        const clientTime = BigInt((client.songStartedAt ?? Date.now()) + Math.round(pause.time * 1000));
        pauseEvents.push({ paused: true, timeSeconds: pause.time, clientTimeUnixMs: clientTime });
        pauseEvents.push({ paused: false, timeSeconds: pause.time, clientTimeUnixMs: clientTime + BigInt(pause.duration * 1000) });
        remaining -= 2;
      }
      const eventCount = poseFrames.length + noteEvents.length + heightEvents.length + energyEvents.length + pauseEvents.length;
      if (eventCount) {
        const scoreEvents = [{ score: client.score.score, timeSeconds: position, immediateMaxPossibleScore: client.score.maxScore }];
        const comboEvents = [{ combo: client.score.combo, timeSeconds: position }];
        const multiplierEvents = [{ multiplier: client.score.combo >= 14 ? 8 : client.score.combo >= 6 ? 4 : client.score.combo >= 2 ? 2 : 1, nextMultiplierProgress: 0, timeSeconds: position }];
        index.scoreEvents++; index.comboEvents++; index.multiplierEvents++;
        index.energyEvents += energyEvents.length; index.pauseEvents += pauseEvents.length; index.sequence++;
        const times = [
          ...poseFrames.map(item => item.timeSeconds), ...noteEvents.map(item => item.timeSeconds),
          ...heightEvents.map(item => item.timeSeconds), ...energyEvents.map(item => item.timeSeconds),
          ...pauseEvents.map(item => item.timeSeconds),
        ];
        await this.send(client, {
          oneofKind: "replayStream",
          replayStream: {
            streamId: client.replayStreamId!, connectionId: client.id, playerId: client.platformId, matchId: client.currentMatchId,
            body: { oneofKind: "chunk", chunk: {
              cursor: this.replayCursor(client),
              events: { poseFrames, heightEvents, noteEvents, scoreEvents, comboEvents, multiplierEvents, energyEvents, pauseEvents, minTimeSeconds: Math.min(...times), maxTimeSeconds: Math.max(...times) },
              cumulativeEventCounts: this.replayCounts(client),
            } },
          },
        });
      }
      if (position >= (client.loadedMap?.durationSeconds ?? replay.frames.at(-1)?.time ?? 180))
        await this.finishSong(client, Push_SongFinished_CompletionType.Passed);
    } catch (error) {
      client.activity = "Replay stream error";
      this.log(client.id, "error", "Replay streaming failed", String(error));
    } finally {
      client.replayTicking = false;
      this.changed();
    }
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
    if (!client.replay && score.songPosition >= (client.loadedMap?.durationSeconds ?? 180))
      await this.finishSong(client, Push_SongFinished_CompletionType.Passed);
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
    await this.finishReplayStream(client, type);
    // The real clients publish their WaitingForCoordinator state before the
    // result. Coordinators inspect that state when SongFinished is received.
    await this.updateSelf(client, { playState: User_PlayStates.WaitingForCoordinator });
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
    client.activity = type === Push_SongFinished_CompletionType.Passed ? "Song finished; waiting for coordinator" : "Leaving match";
    this.log(client.id, "success", type === Push_SongFinished_CompletionType.Passed ? "Song completed" : "Song quit");
  }

  private async finishReplayStream(client: MockRuntime, type: Push_SongFinished_CompletionType) {
    if (!client.replayStreamId || !client.replayIndices) return;
    const completion = type === Push_SongFinished_CompletionType.Passed ? ReplayCompletion.PASSED : ReplayCompletion.QUIT;
    await this.send(client, {
      oneofKind: "replayStream",
      replayStream: {
        streamId: client.replayStreamId, connectionId: client.id, playerId: client.platformId, matchId: client.currentMatchId,
        body: { oneofKind: "end", end: {
          cursor: this.replayCursor(client), completion,
          score: {
            score: Math.max(0, client.score.score), modifiedScore: Math.max(0, client.score.score),
            maxScore: Math.max(0, client.score.maxScore),
            accuracy: client.score.maxScore ? client.score.score / client.score.maxScore : 0,
            combo: Math.max(0, client.score.combo), maxCombo: Math.max(0, client.score.maxCombo),
            fullCombo: client.score.misses === 0 && client.score.badCuts === 0 && client.score.bombHits === 0 && client.score.wallHits === 0,
            goodCuts: client.score.goodCuts, badCuts: client.score.badCuts, missedNotes: client.score.misses,
            bombHits: client.score.bombHits, wallHits: client.score.wallHits,
          },
          chunkCount: BigInt(client.replayIndices.sequence), cumulativeEventCounts: this.replayCounts(client),
        } },
      },
    });
    client.replayStreamId = undefined;
    client.replayIndices = undefined;
  }

  async finishSongEarly(clientId: string) {
    const client = this.get(clientId);
    if (!client?.score.playing) return;
    this.setActivity(client, "Finishing song early");
    await this.finishSong(client, Push_SongFinished_CompletionType.Passed);
  }

  async backToMenu(clientId: string) {
    const client = this.get(clientId)!;
    if (client.score.playing) await this.finishSong(client, Push_SongFinished_CompletionType.Quit);
    if (client.currentMatchId) await this.leaveMatch(clientId);
    await this.updateSelf(client, { playState: User_PlayStates.InMenu });
    client.activity = "Waiting for coordinator to create a match";
    this.log(client.id, "info", "Returned to menu and left match");
  }

  private stopSimulation(client: MockRuntime, clearMap = true) {
    if (client.simulation) clearInterval(client.simulation);
    client.simulation = undefined;
    client.score.playing = false;
    client.replayStreamId = undefined;
    client.replayIndices = undefined;
    client.replayTicking = false;
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
