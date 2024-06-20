import { Client } from "./client.js";
import { CustomEventEmitter } from "./custom-event-emitter.js";
import { v4 as uuidv4 } from "uuid";
import {
  User,
  Match,
  QualifierEvent,
  CoreServer,
  Tournament,
  GameplayParameters,
  Map,
  QualifierEvent_EventSettings,
  QualifierEvent_LeaderboardSort,
  Tournament_TournamentSettings_Team,
  Tournament_TournamentSettings_Pool,
  Permissions,
} from "./models/models.js";
import { Packet } from "./models/packets.js";
import { StateManager } from "./state-manager.js";
import {
  Response,
  Response_Connect,
  Response_ResponseType,
} from "./models/responses.js";
import { Request, Request_LoadSong } from "./models/requests.js";
import { Command } from "./models/commands.js";
import { versionCode } from "./constants.js";
import { Channel, Push_SongFinished } from "./models/index.js";
import WebSocket from "ws";

// Created by Moon on 6/12/2022

export * from "./scraper.js";
export * from "./models/models.js";

export type ResponseFromUser = { userId: string; response: Response };

type TAClientEvents = {
  connectedToServer: Response_Connect;
  connectingToServer: {};
  failedToConnectToServer: {};
  disconnectedFromServer: {};

  authorizationRequestedFromServer: string;
  authorizedWithServer: string;
  failedToAuthorizeWithServer: {};

  loadSongRequested: [string, string, Request_LoadSong];

  songFinished: Push_SongFinished;

  responseReceived: ResponseFromUser;

  joinedTournament: {};
  failedToJoinTournament: {};

  createdTournament: {};
  updatedTournament: {};
  deletedTournament: {};
  failedToCreateTournament: {};
  failedToUpdateTournament: {};
  failedToDeleteTournament: {};

  createdMatch: {};
  updatedMatch: {};
  deletedMatch: {};
  failedToCreateMatch: {};
  failedToUpdateMatch: {};
  failedToDeleteMatch: {};

  createdQualifier: {};
  updatedQualifier: {};
  deletedQualifier: {};
  failedToCreateQualifier: {};
  failedToUpdateQualifier: {};
  failedToDeleteQualifier: {};
};

export class TAClient extends CustomEventEmitter<TAClientEvents> {
  public stateManager: StateManager;

  private client?: Client;
  private token = "";

  private shouldHeartbeat = false;
  private heartbeatInterval: number | undefined;

  constructor() {
    super();
    this.stateManager = new StateManager();
  }

  // --- State helpers --- //
  public get isConnected() {
    return this.client?.isConnected ?? false;
  }

  public get isConnecting() {
    return this.client?.readyState === WebSocket.CONNECTING;
  }

  // --- Actions --- //
  public async connect(serverAddress: string, port: string) {
    this.shouldHeartbeat = true;

    this.client = new Client(serverAddress, port, this.token);

    this.client.on("packetReceived", this.handlePacket);

    this.client.on("disconnectedFromServer", () => {
      clearInterval(this.heartbeatInterval!);

      console.info("Disconnected from server!");
      this.emit("disconnectedFromServer", {});
    });

    this.client.on("failedToConnectToServer", () => {
      console.error("Failed to connect to server!");
      this.emit("failedToConnectToServer", {});
    });

    this.emit("connectingToServer", {});

    // Create a promise that resolves when connected to the server
    const connectPromise = new Promise<Response>((resolve, reject) => {
      const onConnectedToServer = async () => {
        const response = await this.sendRequest({
          type: {
            oneofKind: "connect",
            connect: {
              clientVersion: versionCode,
            },
          },
        });

        if (this.shouldHeartbeat) {
          this.heartbeatInterval = window.setInterval(() => {
            this.client?.send({
              token: this.token,
              from: this.stateManager.getSelfGuid(),
              id: uuidv4(),
              packet: {
                oneofKind: "command",
                command: {
                  type: {
                    oneofKind: "heartbeat",
                    heartbeat: true,
                  },
                },
              },
            });
          }, 10000);
        }

        this.client?.removeListener("connectedToServer", onConnectedToServer);

        clearTimeout(timeout);

        if (response.length <= 0) {
          reject("Server timed out");
        } else {
          resolve(response[0].response);
        }
      };

      // Return what we have after 5 seconds
      const createTimeout = (time: number) => {
        return setTimeout(() => {
          this.client?.removeListener("connectedToServer", onConnectedToServer);
          reject("Server timed out");
        }, time);
      };

      const timeout = createTimeout(30000);

      this.client?.on("connectedToServer", onConnectedToServer);
    });

    this.client.connect();

    return connectPromise;
  }

  public disconnect() {
    this.shouldHeartbeat = false;

    console.info(`Disconnecting from server!`);
    this.client?.disconnect();
  }

  public setAuthToken(token: string) {
    this.token = token;
    this.client?.setToken(token);
  }

  private forwardToUsers(packet: Packet, to: string[]) {
    this.client?.send({
      token: "", // Overridden in this.send()
      from: packet.from,
      id: packet.id,
      packet: {
        oneofKind: "forwardingPacket",
        forwardingPacket: {
          forwardTo: to,
          packet,
        },
      },
    });
  }

  private sendCommand(command: Command, to: string[]) {
    const packet: Packet = {
      token: this.token,
      from: this.stateManager.getSelfGuid(),
      id: uuidv4(),
      packet: {
        oneofKind: "command",
        command,
      },
    };

    this.forwardToUsers(packet, to);
  }

  private async sendRequest(
    request: Request,
    to?: string[]
  ): Promise<ResponseFromUser[]> {
    const packet: Packet = {
      token: "", // Overridden in this.send()
      from: this.stateManager.getSelfGuid(),
      id: uuidv4(),
      packet: {
        oneofKind: "request",
        request,
      },
    };

    const responseDictionary: ResponseFromUser[] = [];

    // Create a promise that resolves when all responses are received
    const responsesPromise = new Promise<ResponseFromUser[]>((resolve) => {
      const addListeners = () => {
        this.on("responseReceived", onResponseReceived);
        this.on("authorizationRequestedFromServer", onAuthorizationRequested);
        this.on("authorizedWithServer", onAuthroizedWithServer);
      };

      const removeListeners = () => {
        this.removeListener("responseReceived", onResponseReceived);
        this.removeListener(
          "authorizationRequestedFromServer",
          onAuthorizationRequested
        );
        this.removeListener("authorizedWithServer", onAuthroizedWithServer);
      };

      // Check that we got responses from all expected users
      const checkResponses = () => {
        const responseUsers = responseDictionary.map((x) => x.userId);
        const expectedUsers = to ?? ["00000000-0000-0000-0000-000000000000"]; // If we didn't forward this to any users, we should expect a response from the server

        if (responseUsers.length !== expectedUsers.length) {
          return;
        }

        const sortedArr1 = responseUsers.slice().sort();
        const sortedArr2 = expectedUsers.slice().sort();

        for (let i = 0; i < sortedArr1.length; i++) {
          if (sortedArr1[i] !== sortedArr2[i]) {
            return;
          }
        }

        // All responses are received, clean up and resolve
        removeListeners();
        clearTimeout(timeout);
        resolve(responseDictionary);
      };

      // Add to the dictionary when the response is to this packet, and from an expected user
      const onResponseReceived = (response: ResponseFromUser) => {
        const expectedUsers = to ?? ["00000000-0000-0000-0000-000000000000"]; // If we didn't forward this to any users, we should expect a response from the server

        if (
          response.response.respondingToPacketId === packet.id &&
          expectedUsers.includes(response.userId)
        ) {
          responseDictionary.push({
            userId: response.userId,
            response: response.response,
          });

          checkResponses();
        }
      };

      // Return what we have after 5 seconds
      const createTimeout = (time: number) => {
        return setTimeout(() => {
          removeListeners();
          resolve(responseDictionary);
        }, time);
      };

      const timeout = createTimeout(30000);

      // If authorization is requested, we're assuming an external application will handle
      // resetting the auth token, so we'll extend the timeout by 30 seconds and try again
      // if a successful auth is noticed
      const onAuthorizationRequested = () => {
        clearTimeout(timeout);
        createTimeout(30000);
      };

      // Retry on successful authorization
      const onAuthroizedWithServer = () => {
        sendRequest();
      };

      addListeners();
    });

    // Assume forwardToUsers emits the 'responseReceived' event asynchronously
    const sendRequest = () => {
      if (to) {
        this.forwardToUsers(packet, to);
      } else {
        this.client?.send(packet, to);
      }
    };

    sendRequest();

    return responsesPromise;
  }

  public async sendResponse(response: Response, to?: string[]) {
    const packet: Packet = {
      token: this.token,
      from: this.stateManager.getSelfGuid(),
      id: uuidv4(),
      packet: {
        oneofKind: "response",
        response,
      },
    };

    if (to) {
      this.forwardToUsers(packet, to);
    } else {
      this.client?.send(packet, to);
    }
  }

  // --- Commands --- //
  public playSong = (gameplayParameters: GameplayParameters, userIds: string[]) => {
    this.sendCommand({
      type: {
        oneofKind: "playSong",
        playSong: {
          gameplayParameters,
        },
      },
    }, userIds);
  };

  public returnToMenu = (userIds: string[]) => {
    this.sendCommand({
      type: {
        oneofKind: "returnToMenu",
        returnToMenu: true,
      },
    }, userIds);
  };

  public showLoadedImage = (userIds: string[]) => {
    this.sendCommand({
      type: {
        oneofKind: "streamSyncShowImage",
        streamSyncShowImage: true,
      },
    }, userIds);
  };

  public delayTestFinished = (userIds: string[]) => {
    this.sendCommand({
      type: {
        oneofKind: "delayTestFinish",
        delayTestFinish: true,
      },
    }, userIds);
  };

  // --- Requests --- //
  public joinTournament = async (tournamentId: string) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "join",
        join: {
          tournamentId,
          password: "",
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public getLeaderboard = async (tournamentId: string, qualifierId: string, mapId: string) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "qualifierScores",
        qualifierScores: {
          tournamentId: tournamentId,
          eventId: qualifierId,
          mapId,
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public loadSong = async (levelId: string, userIds: string[]) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "loadSong",
        loadSong: {
          levelId,
          customHostUrl: ""
        },
      },
    }, userIds);

    if (response.length <= 0) {
      throw new Error("Server timed out, or no users responded");
    }

    return response;
  };

  public loadImage = async (bitmap: Uint8Array, userIds: string[]) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "preloadImageForStreamSync",
        preloadImageForStreamSync: {
          fileId: uuidv4(),
          data: bitmap,
          compressed: false
        },
      },
    }, userIds);

    if (response.length <= 0) {
      throw new Error("Server timed out, or no users responded");
    }

    return response;
  };

  // --- Packet Handler --- //
  private handlePacket = (packet: Packet) => {
    this.stateManager.handlePacket(packet);

    // if (packet.packet.oneofKind !== "acknowledgement") {
    //   const send: Packet = {
    //     token: this.token,
    //     from: this.stateManager.getSelfGuid(),
    //     id: uuidv4(),
    //     packet: {
    //       oneofKind: "acknowledgement",
    //       acknowledgement: {
    //         packetId: packet.id,
    //         type: Acknowledgement_AcknowledgementType.MessageReceived,
    //       },
    //     },
    //   };

    //   this.client?.send(send);
    // }

    if (packet.packet.oneofKind === "command") {
      const command = packet.packet.command;

      if (command.type.oneofKind === 'discordAuthorize') {
        this.emit(
          "authorizationRequestedFromServer",
          command.type.discordAuthorize
        );
      }
    } else if (packet.packet.oneofKind === 'request') {
      const request = packet.packet.request;

      if (request.type.oneofKind === 'loadSong') {
        this.emit("loadSongRequested", [packet.id, packet.from, request.type.loadSong]);
      }
    } else if (packet.packet.oneofKind === "response") {
      const response = packet.packet.response;

      this.emit("responseReceived", {
        userId: packet.from,
        response: response,
      });

      if (response.details.oneofKind === "connect") {
        const connect = response.details.connect;

        if (response.type === Response_ResponseType.Success) {
          console.info(`Successfully connected to server!`);
          this.emit("connectedToServer", connect);
        } else {
          console.error(
            `Failed to connect to server. Message: ${connect.message}`
          );
          this.emit("failedToConnectToServer", {});
        }
      } else if (response.details.oneofKind === "join") {
        const join = response.details.join;

        if (response.type === Response_ResponseType.Success) {
          console.info(`Successfully joined tournament!`);
          this.emit("joinedTournament", {});
        } else {
          console.error(`Failed to join server. Message: ${join.message}`);
          this.emit("failedToJoinTournament", {});
        }
      } else if (response.details.oneofKind === "createTournament") {
        const createTournament = response.details.createTournament;

        if (response.type === Response_ResponseType.Success) {
          console.info(`Successfully created tournament!`);
          this.emit("createdTournament", {});
        } else {
          console.error(
            `Failed to create tournament. Message: ${createTournament.message}`
          );
          this.emit("failedToCreateTournament", {});
        }
      } else if (response.details.oneofKind === "updateTournament") {
        const updateTournament = response.details.updateTournament;

        if (response.type === Response_ResponseType.Success) {
          console.info(`Successfully modified tournament!`);
          this.emit("updatedTournament", {});
        } else {
          console.error(
            `Failed update tournament. Message: ${updateTournament.message}`
          );
          this.emit("failedToUpdateTournament", {});
        }
      } else if (response.details.oneofKind === "deleteTournament") {
        const deleteTournament = response.details.deleteTournament;

        if (response.type === Response_ResponseType.Success) {
          console.info(`Successfully deleted tournament!`);
          this.emit("deletedTournament", {});
        } else {
          console.error(
            `Failed to delete tournament. Message: ${deleteTournament.message}`
          );
          this.emit("failedToDeleteTournament", {});
        }
      } else if (response.details.oneofKind === "createMatch") {
        const createMatch = response.details.createMatch;

        if (response.type === Response_ResponseType.Success) {
          console.info(`Successfully created match!`);
          this.emit("createdMatch", {});
        } else {
          console.error(
            `Failed to create Match. Message: ${createMatch.message}`
          );
          this.emit("failedToCreateMatch", {});
        }
      } else if (response.details.oneofKind === "updateMatch") {
        const updateMatch = response.details.updateMatch;

        if (response.type === Response_ResponseType.Success) {
          console.info(`Successfully modified match!`);
          this.emit("updatedMatch", {});
        } else {
          console.error(`Failed update Match. Message: ${updateMatch.message}`);
          this.emit("failedToUpdateMatch", {});
        }
      } else if (response.details.oneofKind === "deleteMatch") {
        const deleteMatch = response.details.deleteMatch;

        if (response.type === Response_ResponseType.Success) {
          console.info(`Successfully deleted match!`);
          this.emit("deletedMatch", {});
        } else {
          console.error(
            `Failed to delete Match. Message: ${deleteMatch.message}`
          );
          this.emit("failedToDeleteMatch", {});
        }
      } else if (response.details.oneofKind === "createQualifierEvent") {
        const createQualifierEvent = response.details.createQualifierEvent;

        if (response.type === Response_ResponseType.Success) {
          console.info(`Successfully created qualifier!`);
          this.emit("createdQualifier", {});
        } else {
          console.error(
            `Failed to create qualifier. Message: ${createQualifierEvent.message}`
          );
          this.emit("failedToCreateQualifier", {});
        }
      } else if (response.details.oneofKind === "updateQualifierEvent") {
        const modifyQualifier = response.details.updateQualifierEvent;

        if (response.type === Response_ResponseType.Success) {
          console.info(`Successfully modified qualifier!`);
          this.emit("updatedQualifier", {});
        } else {
          console.error(
            `Failed to update qualifier. Message: ${modifyQualifier.message}`
          );
          this.emit("failedToUpdateQualifier", {});
        }
      } else if (response.details.oneofKind === "deleteQualifierEvent") {
        const deleteQualifierEvent = response.details.deleteQualifierEvent;

        if (response.type === Response_ResponseType.Success) {
          console.info(`Successfully deleted qualifier!`);
          this.emit("deletedQualifier", {});
        } else {
          console.error(
            `Failed to delete qualifier. Message: ${deleteQualifierEvent.message}`
          );
          this.emit("failedToDeleteQualifier", {});
        }
      }
    } else if (packet.packet.oneofKind === "push") {
      const push = packet.packet.push;

      if (push.data.oneofKind === "discordAuthorized") {
        if (push.data.discordAuthorized.success) {
          this.emit("authorizedWithServer", push.data.discordAuthorized.token);
        } else {
          this.emit("failedToAuthorizeWithServer", {});
        }
      }
      else if (push.data.oneofKind === "songFinished") {
        this.emit("songFinished", push.data.songFinished);
      }
    }
  };

  // --- State Actions --- //
  public updateUser = async (tournamentId: string, user: User) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "updateUser",
        updateUser: {
          tournamentId,
          user,
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public createMatch = async (tournamentId: string, match: Match) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "createMatch",
        createMatch: {
          tournamentId,
          match,
        },
      },
    });

    // Checking oneOfKind here helps typescript identify what type of response it is
    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };


  public addUserToMatch = async (tournamentId: string, matchId: string, userId: string) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "addUserToMatch",
        addUserToMatch: {
          tournamentId,
          matchId,
          userId
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public removeUserFromMatch = async (tournamentId: string, matchId: string, userId: string) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "removeUserFromMatch",
        removeUserFromMatch: {
          tournamentId,
          matchId,
          userId
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public setMatchLeader = async (tournamentId: string, matchId: string, userId: string) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "setMatchLeader",
        setMatchLeader: {
          tournamentId,
          matchId,
          userId
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public setMatchMap = async (tournamentId: string, matchId: string, map: Map) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "setMatchMap",
        setMatchMap: {
          tournamentId,
          matchId,
          map
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public deleteMatch = async (tournamentId: string, matchId: string) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "deleteMatch",
        deleteMatch: {
          tournamentId,
          matchId,
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public createQualifierEvent = async (
    tournamentId: string,
    event: QualifierEvent
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "createQualifierEvent",
        createQualifierEvent: {
          tournamentId,
          event,
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public setQualifierName = async (
    tournamentId: string,
    qualifierId: string,
    qualifierName: string
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "setQualifierName",
        setQualifierName: {
          tournamentId,
          qualifierId,
          qualifierName
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public setQualifierImage = async (
    tournamentId: string,
    qualifierId: string,
    qualifierImage: Uint8Array
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "setQualifierImage",
        setQualifierImage: {
          tournamentId,
          qualifierId,
          qualifierImage
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public setQualifierInfoChannel = async (
    tournamentId: string,
    qualifierId: string,
    infoChannel: Channel
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "setQualifierInfoChannel",
        setQualifierInfoChannel: {
          tournamentId,
          qualifierId,
          infoChannel
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public setQualifierFlags = async (
    tournamentId: string,
    qualifierId: string,
    qualifierFlags: QualifierEvent_EventSettings
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "setQualifierFlags",
        setQualifierFlags: {
          tournamentId,
          qualifierId,
          qualifierFlags
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public setQualifierLeaderboardSort = async (
    tournamentId: string,
    qualifierId: string,
    qualifierLeaderboardSort: QualifierEvent_LeaderboardSort
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "setQualifierLeaderboardSort",
        setQualifierLeaderboardSort: {
          tournamentId,
          qualifierId,
          qualifierLeaderboardSort
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public addQualifierMap = async (
    tournamentId: string,
    qualifierId: string,
    map: Map
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "addQualifierMap",
        addQualifierMap: {
          tournamentId,
          qualifierId,
          map
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public updateQualifierMap = async (
    tournamentId: string,
    qualifierId: string,
    map: Map
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "updateQualifierMap",
        updateQualifierMap: {
          tournamentId,
          qualifierId,
          map
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public removeQualifierMap = async (
    tournamentId: string,
    qualifierId: string,
    mapId: string
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "removeQualifierMap",
        removeQualifierMap: {
          tournamentId,
          qualifierId,
          mapId
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public deleteQualifierEvent = async (
    tournamentId: string,
    qualifierId: string
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "deleteQualifierEvent",
        deleteQualifierEvent: {
          tournamentId,
          qualifierId,
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public addAuthorizedUser = async (tournamentId: string, discordId: string, permissionFlags: Permissions) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "addAuthorizedUser",
        addAuthorizedUser: {
          tournamentId,
          discordId,
          permissionFlags
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public removeAuthorizedUser = async (tournamentId: string, discordId: string) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "removeAuthorizedUser",
        removeAuthorizedUser: {
          tournamentId,
          discordId
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public getAuthorizedUsers = async (tournamentId: string) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "getAuthorizedUsers",
        getAuthorizedUsers: {
          tournamentId,
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public getDiscordInfo = async (tournamentId: string, discordId: string) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "getDiscordInfo",
        getDiscordInfo: {
          tournamentId,
          discordId
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public createTournament = async (tournament: Tournament) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "createTournament",
        createTournament: {
          tournament,
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public setTournamentName = async (
    tournamentId: string,
    tournamentName: string
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "setTournamentName",
        setTournamentName: {
          tournamentId,
          tournamentName
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public setTournamentImage = async (
    tournamentId: string,
    tournamentImage: Uint8Array
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "setTournamentImage",
        setTournamentImage: {
          tournamentId,
          tournamentImage
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public setTournamentEnableTeams = async (
    tournamentId: string,
    enableTeams: boolean
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "setTournamentEnableTeams",
        setTournamentEnableTeams: {
          tournamentId,
          enableTeams
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public setTournamentEnablePools = async (
    tournamentId: string,
    enablePools: boolean
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "setTournamentEnablePools",
        setTournamentEnablePools: {
          tournamentId,
          enablePools
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public setTournamentShowTournamentButton = async (
    tournamentId: string,
    showTournamentButton: boolean
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "setTournamentShowTournamentButton",
        setTournamentShowTournamentButton: {
          tournamentId,
          showTournamentButton
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public setTournamentShowQualifierButton = async (
    tournamentId: string,
    showQualifierButton: boolean
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "setTournamentShowQualifierButton",
        setTournamentShowQualifierButton: {
          tournamentId,
          showQualifierButton
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public setTournamentAllowUnauthorizedView = async (
    tournamentId: string,
    allowUnauthorizedView: boolean
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "setTournamentAllowUnauthorizedView",
        setTournamentAllowUnauthorizedView: {
          tournamentId,
          allowUnauthorizedView
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public setTournamentScoreUpdateFrequency = async (
    tournamentId: string,
    scoreUpdateFrequency: number
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "setTournamentScoreUpdateFrequency",
        setTournamentScoreUpdateFrequency: {
          tournamentId,
          scoreUpdateFrequency
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public setTournamentBannedMods = async (
    tournamentId: string,
    bannedMods: string[]
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: 'setTournamentBannedMods',
        setTournamentBannedMods: {
          tournamentId,
          bannedMods
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public addTournamentTeam = async (
    tournamentId: string,
    team: Tournament_TournamentSettings_Team
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "addTournamentTeam",
        addTournamentTeam: {
          tournamentId,
          team
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public setTournamentTeamName = async (
    tournamentId: string,
    teamId: string,
    teamName: string
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "setTournamentTeamName",
        setTournamentTeamName: {
          tournamentId,
          teamId,
          teamName
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public setTournamentTeamImage = async (
    tournamentId: string,
    teamId: string,
    teamImage: Uint8Array
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "setTournamentTeamImage",
        setTournamentTeamImage: {
          tournamentId,
          teamId,
          teamImage
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public removeTournamentTeam = async (
    tournamentId: string,
    teamId: string
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "removeTournamentTeam",
        removeTournamentTeam: {
          tournamentId,
          teamId
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public addTournamentPool = async (
    tournamentId: string,
    pool: Tournament_TournamentSettings_Pool
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "addTournamentPool",
        addTournamentPool: {
          tournamentId,
          pool
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public setTournamentPoolName = async (
    tournamentId: string,
    poolId: string,
    poolName: string
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "setTournamentPoolName",
        setTournamentPoolName: {
          tournamentId,
          poolId,
          poolName
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public addTournamentPoolMap = async (
    tournamentId: string,
    poolId: string,
    map: Map
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "addTournamentPoolMap",
        addTournamentPoolMap: {
          tournamentId,
          poolId,
          map
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public updateTournamentPoolMap = async (
    tournamentId: string,
    poolId: string,
    map: Map
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "updateTournamentPoolMap",
        updateTournamentPoolMap: {
          tournamentId,
          poolId,
          map
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public removeTournamentPoolMap = async (
    tournamentId: string,
    poolId: string,
    mapId: string
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "removeTournamentPoolMap",
        removeTournamentPoolMap: {
          tournamentId,
          poolId,
          mapId
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public removeTournamentPool = async (
    tournamentId: string,
    poolId: string
  ) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "removeTournamentPool",
        removeTournamentPool: {
          tournamentId,
          poolId
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public deleteTournament = async (tournamentId: string) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "deleteTournament",
        deleteTournament: {
          tournamentId,
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };

  public addServer = async (server: CoreServer, authToken?: string) => {
    const response = await this.sendRequest({
      type: {
        oneofKind: "addServer",
        addServer: {
          server,
          authToken: authToken ?? "",
        },
      },
    });

    if (response.length <= 0) {
      throw new Error("Server timed out");
    }

    return response[0].response;
  };
}
