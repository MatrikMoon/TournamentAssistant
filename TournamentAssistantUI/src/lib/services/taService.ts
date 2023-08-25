import { masterAddress, masterPort } from "$lib/constants";
import {
  CoreServer,
  CustomEventEmitter,
  Match,
  QualifierEvent,
  Response_ResponseType,
  TAClient,
  Tournament,
  User,
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
  masterConnectionStateChanged: { state: ConnectState; text: string };
  connectionStateChanged: { state: ConnectState; text: string };
};

export class TAService extends CustomEventEmitter<TAServiceEvents> {
  private _masterClient: TAClient;
  private _client: TAClient;
  private authToken?: string;

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
      this.setAuthToken(token); //If the master server client has a token, it's probably (TODO: !!) valid for any server
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
    this.authToken = token;

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

  public subscribeToTournamentUpdates(fn: (tournament: Tournament) => void) {
    this._masterClient.stateManager.on("tournamentCreated", fn);
    this._masterClient.stateManager.on("tournamentUpdated", fn);
    this._masterClient.stateManager.on("tournamentDeleted", fn);
  }

  public unsubscribeFromTournamentUpdates(
    fn: (tournament: Tournament) => void
  ) {
    this._masterClient.stateManager.removeListener("tournamentCreated", fn);
    this._masterClient.stateManager.removeListener("tournamentUpdated", fn);
    this._masterClient.stateManager.removeListener("tournamentDeleted", fn);
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

  public async createTournament(
    serverAddress: string,
    serverPort: string,
    tournament: Tournament
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.createTournament(tournament);
  }

  public async joinTournament(
    serverAddress: string,
    serverPort: string,
    tournamentId: string
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);

    //Check if we are already in the correct tournament
    const self = this._client.stateManager.getUser(
      tournamentId,
      this._client.stateManager.getSelfGuid()
    );

    //We're connected, but haven't joined the tournament. Let's do that
    if (!self) {
      const joinResult = await this._client.joinTournament(tournamentId);
      if (joinResult.type === Response_ResponseType.Fail) {
        throw new Error("Failed to join tournament");
      }
      return joinResult;
    }

    return Promise.resolve(true);
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

  public async createMatch(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    match: Match
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.createMatch(tournamentId, match);
  }

  public async joinMatch(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    matchId: string
  ) {
    if (!this._client.isConnected) {
      const connectResult = await this._client.connect(
        serverAddress,
        serverPort
      );

      if (connectResult.type === Response_ResponseType.Fail) {
        throw new Error("Failed to connect to server");
      }
    }

    //If we're not in the tournament, join!
    await this.joinTournament(serverAddress, serverPort, tournamentId);

    //If we're not yet in the match, we'll add ourself
    const selfGuid = this._client.stateManager.getSelfGuid();
    const match = this._client.stateManager.getMatch(tournamentId, matchId)!;
    if (!match.associatedUsers.includes(selfGuid)) {
      match.associatedUsers = [...match.associatedUsers, selfGuid];
      const updateResult = await this._client.updateMatch(tournamentId, match);
      if (updateResult.type === Response_ResponseType.Fail) {
        throw new Error("Failed to join match");
      }
      return updateResult;
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

  public async createQualifier(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    qualifier: QualifierEvent
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.createQualifierEvent(tournamentId, qualifier);
  }

  public async updateQualifier(
    serverAddress: string,
    serverPort: string,
    tournamentId: string,
    qualifier: QualifierEvent
  ) {
    await this.ensureConnectedToServer(serverAddress, serverPort);
    return await this._client.updateQualifierEvent(tournamentId, qualifier);
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
}
