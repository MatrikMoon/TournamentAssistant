import {
  CoreServer,
  CustomEventEmitter,
  GameplayParameters,
  Match,
  QualifierEvent,
  QualifierEvent_EventSettings,
  QualifierEvent_LeaderboardSort,
  Response_ResponseType,
  TAClient,
  Tournament,
  User,
  masterAddress,
  masterPort,
  Map,
  Tournament_TournamentSettings_Team,
  Tournament_TournamentSettings_Pool,
  Channel,
  Response_Connect_ConnectFailReason,
} from "tournament-assistant-client";

// Intended to act as an in-between between the UI and TAUI,
// so that TAUI pages don't have to handle as much connection logic

export enum ConnectState {
  NotStarted = 0,
  Connected,
  Connecting,
  FailedToConnect,
  Disconnected,
}

type TAServiceEvents = {
  updateRequired: {};
  masterConnectionStateChanged: { state: ConnectState; text: string };
  connectionStateChanged: { state: ConnectState; text: string };
};

export class TAService extends CustomEventEmitter<TAServiceEvents> {
  private _masterClient: TAClient;
  private _client: TAClient;

  constructor() {
    super();

    this._masterClient = new TAClient();
    this._client = new TAClient();

    // Master listeners
    this.masterClient.on("connectedToServer", () => {
      this.emit("masterConnectionStateChanged", {
        state: ConnectState.Connected,
        text: "Connected",
      });
      console.log("MasterClientOn: Connected");
    });

    this.masterClient.on("connectingToServer", () => {
      this.emit("masterConnectionStateChanged", {
        state: ConnectState.Connecting,
        text: "Connecting to server",
      });
      console.log("MasterClientOn: Connecting");
    });

    this.masterClient.on("failedToConnectToServer", () => {
      this.emit("masterConnectionStateChanged", {
        state: ConnectState.FailedToConnect,
        text: "Failed to connect to the server",
      });
      console.log("MasterClientOn: FailedToConnect");
    });

    this.masterClient.on("disconnectedFromServer", () => {
      this.emit("masterConnectionStateChanged", {
        state: ConnectState.Disconnected,
        text: "Disconnected",
      });
      console.log("MasterClientOn: Disconnected");
    });

    this.masterClient.on("authorizationRequestedFromServer", (url) => {
      this.masterClient.setAuthToken("");
      window.open(url, "_blank", "width=500, height=800");
    });

    this.masterClient.on("authorizedWithServer", (token) => {
      this.setAuthToken(token); // If the master server client has a token, it's probably (TODO: !!) valid for any server
      console.log(`Master Authorized: ${token}`);
    });

    // Client listeners
    this._client.on("connectedToServer", () => {
      this.emit("connectionStateChanged", {
        state: ConnectState.Connected,
        text: "Connected",
      });
      console.log("ClientOn: Connected");
    });

    this._client.on("connectingToServer", () => {
      this.emit("connectionStateChanged", {
        state: ConnectState.Connecting,
        text: "Connecting to server",
      });
      console.log("ClientOn: Connecting");
    });

    this._client.on("failedToConnectToServer", () => {
      this.emit("connectionStateChanged", {
        state: ConnectState.FailedToConnect,
        text: "Failed to connect to the server",
      });
      console.log("ClientOn: FailedToConnect");
    });

    this._client.on("disconnectedFromServer", () => {
      this.emit("connectionStateChanged", {
        state: ConnectState.Disconnected,
        text: "Disconnected",
      });
      console.log("ClientOn: Disconnected");
    });

    this._client.on("authorizationRequestedFromServer", (url) => {
      this.client.setAuthToken("");
      window.open(url, "_blank", "width=500, height=800");
    });

    this._client.on("authorizedWithServer", (token) => {
      this.client.setAuthToken(token);
      console.log(`Authorized: ${token}`);
    });
  }

  public get client() {
    return this._client;
  }

  public get masterClient() {
    return this._masterClient;
  }

  private async ensureConnectedToMasterServer() {
    if (!this._masterClient.isConnected) {
      const connectResult = await this._masterClient.connect(
        masterAddress,
        masterPort
      );

      if (connectResult.type === Response_ResponseType.Fail) {
        if (connectResult.details.oneofKind === "connect" &&
          connectResult.details.connect.reason === Response_Connect_ConnectFailReason.IncorrectVersion) {
          this.emit("updateRequired", {});
        }

        throw new Error("Failed to connect to server");
      }
    }
  }

  private async ensureConnectedToServer(
    serverAddress: string,
    serverPort: string
  ) {
    // If we're already CONNECTING, just wait on that result
    if (this._client.isConnecting) {
      return new Promise<void>((resolve) => {
        const onConnectionComplete = () => {
          this.client.removeListener("connectedToServer", onConnectionComplete);
          this.client.removeListener(
            "failedToConnectToServer",
            onConnectionComplete
          );

          resolve();
        };

        this.client.on("connectedToServer", onConnectionComplete);
        this.client.on("failedToConnectToServer", onConnectionComplete);
      });
    } else if (!this._client.isConnected) {
      const connectResult = await this._client.connect(
        serverAddress,
        serverPort
      );

      if (connectResult.type === Response_ResponseType.Fail) {
        throw new Error("Failed to connect to server");
      }
    }
  }

  public setAuthToken(token: string) {
    this._masterClient.setAuthToken(token);
    this._client.setAuthToken(token);
  }

  public async connectToMaster() {
    await this.ensureConnectedToMasterServer();
  }

  public subscribeToServerUpdates(fn: (server: CoreServer) => void) {
    this._masterClient.stateManager.on("serverAdded", fn);
    this._masterClient.stateManager.on("serverDeleted", fn);
  }

  public unsubscribeFromServerUpdates(fn: (server: CoreServer) => void) {
    this._masterClient.stateManager.removeListener("serverAdded", fn);
    this._masterClient.stateManager.removeListener("serverDeleted", fn);
  }

  // This one listens specifically to the master client for tournament updates.
  // Basically just for the master tournament list
  public subscribeToMasterTournamentUpdates(fn: (tournament: Tournament) => void) {
    this._masterClient.stateManager.on("tournamentCreated", fn);
    this._masterClient.stateManager.on("tournamentUpdated", fn);
    this._masterClient.stateManager.on("tournamentDeleted", fn);
  }

  public unsubscribeFromMasterTournamentUpdates(
    fn: (tournament: Tournament) => void
  ) {
    this._masterClient.stateManager.removeListener("tournamentCreated", fn);
    this._masterClient.stateManager.removeListener("tournamentUpdated", fn);
    this._masterClient.stateManager.removeListener("tournamentDeleted", fn);
  }

  public subscribeToTournamentUpdates(fn: (tournament: Tournament) => void) {
    this._client.stateManager.on("tournamentCreated", fn);
    this._client.stateManager.on("tournamentUpdated", fn);
    this._client.stateManager.on("tournamentDeleted", fn);
  }

  public unsubscribeFromTournamentUpdates(
    fn: (tournament: Tournament) => void
  ) {
    this._client.stateManager.removeListener("tournamentCreated", fn);
    this._client.stateManager.removeListener("tournamentUpdated", fn);
    this._client.stateManager.removeListener("tournamentDeleted", fn);
  }

  public subscribeToMatchUpdates(fn: (event: [Match, Tournament]) => void) {
    this._client.stateManager.on("matchCreated", fn);
    this._client.stateManager.on("matchUpdated", fn);
    this._client.stateManager.on("matchDeleted", fn);
  }

  public unsubscribeFromMatchUpdates(fn: (event: [Match, Tournament]) => void) {
    this._client.stateManager.removeListener("matchCreated", fn);
    this._client.stateManager.removeListener("matchUpdated", fn);
    this._client.stateManager.removeListener("matchDeleted", fn);
  }

  public subscribeToUserUpdates(fn: (event: [User, Tournament]) => void) {
    this._client.stateManager.on("userConnected", fn);
    this._client.stateManager.on("userUpdated", fn);
    this._client.stateManager.on("userDisconnected", fn);
  }

  public unsubscribeFromUserUpdates(fn: (event: [User, Tournament]) => void) {
    this._client.stateManager.removeListener("userConnected", fn);
    this._client.stateManager.removeListener("userUpdated", fn);
    this._client.stateManager.removeListener("userDisconnected", fn);
  }

  public subscribeToQualifierUpdates(
    fn: (event: [QualifierEvent, Tournament]) => void
  ) {
    this._client.stateManager.on("qualifierCreated", fn);
    this._client.stateManager.on("qualifierUpdated", fn);
    this._client.stateManager.on("qualifierDeleted", fn);
  }

  public unsubscribeFromQualifierUpdates(
    fn: (event: [QualifierEvent, Tournament]) => void
  ) {
    this._client.stateManager.removeListener("qualifierCreated", fn);
    this._client.stateManager.removeListener("qualifierUpdated", fn);
    this._client.stateManager.removeListener("qualifierDeleted", fn);
  }

  // -- State Getters, misc commands/requests -- //
  public async getKnownServers() {
    await this.ensureConnectedToMasterServer();
    return this._masterClient.stateManager.getKnownServers();
  }

  public async getTournament(
    serverAddress: string,
    serverPort: string,
    tournamentId: string
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return this._client.stateManager.getTournament(tournamentId);
  }

  public async getTournaments() {
    await this.ensureConnectedToMasterServer();
    return this._masterClient.stateManager.getTournaments();
  }

  public async joinTournament(
    serverAddress: string,
    serverPort: string,
    tournamentId: string
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);

    // Check if we are already in the correct tournament
    const self = this._client.stateManager.getUser(
      tournamentId,
      this._client.stateManager.getSelfGuid()
    );

    // We're connected, but haven't joined the tournament. Let's do that
    if (!self) {
      const joinResult = await this._client.joinTournament(tournamentId);
      if (joinResult.type === Response_ResponseType.Fail) {
        throw new Error("Failed to join tournament");
      }
      return joinResult;
    }
  }

  public async getMatch(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    matchId: string
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return this._client.stateManager.getMatch(tournamentId, matchId);
  }

  public async getMatches(
    serverAddress: string,
    serverPort: string,
    tournamentId: string
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return this._client.stateManager.getMatches(tournamentId);
  }

  public async joinMatch(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    matchId: string
  ) {
    // If we're not in the tournament, join!
    await this.joinTournament(serverAddress, serverPort, tournamentId);

    // If we're not yet in the match, we'll add ourself
    const selfGuid = this._client.stateManager.getSelfGuid();
    const match = this._client.stateManager.getMatch(tournamentId, matchId)!;
    if (!match.associatedUsers.includes(selfGuid)) {
      const updateResponse = await this._client.addUserToMatch(tournamentId, matchId, selfGuid);
      if (updateResponse.type === Response_ResponseType.Fail) {
        throw new Error("Failed to join match");
      }
      return updateResponse;
    }

    return Promise.resolve(true);
  }

  public async getQualifier(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    qualifierId: string
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return this._client.stateManager.getQualifier(tournamentId, qualifierId);
  }

  public async getQualifiers(
    serverAddress: string,
    serverPort: string,
    tournamentId: string
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return this._client.stateManager.getQualifiers(tournamentId);
  }

  public async getLeaderboard(
    serverAddress: string,
    serverPort: string,
    qualifierId: string,
    mapId: string
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.getLeaderboard(qualifierId, mapId);
  }

  public async getUser(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    userId: string
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return this._client.stateManager.getUser(tournamentId, userId);
  }

  public async sendLoadSongRequest(
    serverAddress: string,
    serverPort: string,
    levelId: string,
    playerIds: string[]
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return this._client.loadSong(levelId, playerIds);
  }

  public async sendLoadImageRequest(
    serverAddress: string,
    serverPort: string,
    bitmap: Uint8Array,
    playerIds: string[]
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return this._client.loadImage(bitmap, playerIds);
  }

  public async sendShowImageCommand(
    serverAddress: string,
    serverPort: string,
    playerIds: string[]
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return this._client.showLoadedImage(playerIds);
  }

  public async sendStreamSyncFinishedCommand(
    serverAddress: string,
    serverPort: string,
    playerIds: string[]
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return this._client.delayTestFinished(playerIds);
  }

  public async sendPlaySongCommand(
    serverAddress: string,
    serverPort: string,
    gameplayParameters: GameplayParameters,
    playerIds: string[]
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return this._client.playSong(gameplayParameters, playerIds);
  }

  public async sendReturnToMenuCommand(
    serverAddress: string,
    serverPort: string,
    playerIds: string[]
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return this._client.returnToMenu(playerIds);
  }

  // -- Basic events -- //
  public async createMatch(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    match: Match
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.createMatch(tournamentId, match);
  }

  public async addUserToMatch(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    matchId: string,
    userId: string
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.addUserToMatch(tournamentId, matchId, userId);
  }

  public async removeUserFromMatch(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    matchId: string,
    userId: string
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.removeUserFromMatch(tournamentId, matchId, userId);
  }

  public async setMatchLeader(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    matchId: string,
    userId: string
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.setMatchLeader(tournamentId, matchId, userId);
  }

  public async setMatchMap(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    matchId: string,
    map: Map
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.setMatchMap(tournamentId, matchId, map);
  }

  public async deleteMatch(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    matchId: string
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.deleteMatch(tournamentId, matchId);
  }

  public async createQualifier(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    qualifier: QualifierEvent
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.createQualifierEvent(tournamentId, qualifier);
  }

  public async setQualifierName(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    qualifierId: string,
    qualifierName: string
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.setQualifierName(tournamentId, qualifierId, qualifierName);
  }

  public async setQualifierImage(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    qualifierId: string,
    qualifierImage: Uint8Array
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.setQualifierImage(tournamentId, qualifierId, qualifierImage);
  }

  public async setQualifierInfoChannel(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    qualifierId: string,
    infoChannel: Channel
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.setQualifierInfoChannel(tournamentId, qualifierId, infoChannel);
  }

  public async setQualifierFlags(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    qualifierId: string,
    qualifierFlags: QualifierEvent_EventSettings
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.setQualifierFlags(tournamentId, qualifierId, qualifierFlags);
  }

  public async setQualifierLeaderboardSort(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    qualifierId: string,
    qualifierLeaderboardSort: QualifierEvent_LeaderboardSort
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.setQualifierLeaderboardSort(tournamentId, qualifierId, qualifierLeaderboardSort);
  }

  public async addQualifierMap(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    qualifierId: string,
    map: Map
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.addQualifierMap(tournamentId, qualifierId, map);
  }

  public async updateQualifierMap(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    qualifierId: string,
    map: Map
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.updateQualifierMap(tournamentId, qualifierId, map);
  }

  public async removeQualifierMap(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    qualifierId: string,
    mapId: string
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.removeQualifierMap(tournamentId, qualifierId, mapId);
  }

  public async deleteQualifier(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    qualifierId: string
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.deleteQualifierEvent(tournamentId, qualifierId);
  }

  public async createTournament(
    serverAddress: string,
    serverPort: string,
    tournament: Tournament
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.createTournament(tournament);
  }

  public async setTournamentName(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    tournamentName: string
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.setTournamentName(tournamentId, tournamentName);
  }

  public async setTournamentImage(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    tournamentImage: Uint8Array
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.setTournamentImage(tournamentId, tournamentImage);
  }

  public async setTournamentEnableTeams(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    enableTeams: boolean
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.setTournamentEnableTeams(tournamentId, enableTeams);
  }

  public async setTournamentEnablePools(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    enablePools: boolean
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.setTournamentEnablePools(tournamentId, enablePools);
  }

  public async setTournamentShowTournamentButton(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    showTournamentButton: boolean
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.setTournamentShowTournamentButton(tournamentId, showTournamentButton);
  }

  public async setTournamentShowQualifierButton(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    showQualifierButton: boolean
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.setTournamentShowQualifierButton(tournamentId, showQualifierButton);
  }

  public async setTournamentScoreUpdateFrequency(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    scoreUpdateFrequency: number
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.setTournamentScoreUpdateFrequency(tournamentId, scoreUpdateFrequency);
  }

  public async setTournamentBannedMods(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    bannedMods: string[]
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.setTournamentBannedMods(tournamentId, bannedMods);
  }

  public async addTournamentTeam(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    team: Tournament_TournamentSettings_Team
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.addTournamentTeam(tournamentId, team);
  }

  public async setTournamentTeamName(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    teamId: string,
    teamName: string
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.setTournamentTeamName(tournamentId, teamId, teamName);
  }

  public async setTournamentTeamImage(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    teamId: string,
    teamImage: Uint8Array
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.setTournamentTeamImage(tournamentId, teamId, teamImage);
  }

  public async removeTournamentTeam(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    teamId: string
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.removeTournamentTeam(tournamentId, teamId);
  }

  public async addTournamentPool(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    pool: Tournament_TournamentSettings_Pool
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.addTournamentPool(tournamentId, pool);
  }

  public async setTournamentPoolName(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    poolId: string,
    poolName: string
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.setTournamentPoolName(tournamentId, poolId, poolName);
  }

  public async addTournamentPoolMap(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    poolId: string,
    map: Map
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.addTournamentPoolMap(tournamentId, poolId, map);
  }

  public async updateTournamentPoolMap(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    poolId: string,
    map: Map
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.updateTournamentPoolMap(tournamentId, poolId, map);
  }

  public async removeTournamentPoolMap(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    poolId: string,
    mapId: string
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.removeTournamentPoolMap(tournamentId, poolId, mapId);
  }

  public async removeTournamentPool(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    poolId: string
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.removeTournamentPool(tournamentId, poolId);
  }

  public async deleteTournament(
    serverAddress: string,
    serverPort: string,
    tournamentId: string
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.deleteTournament(tournamentId);
  }
}
