import { Client } from "./client";
import { CustomEventEmitter } from "./custom-event-emitter";
import { v4 as uuidv4 } from "uuid";
import {
  User,
  Match,
  QualifierEvent,
  CoreServer,
  Tournament,
} from "./models/models";
import { Packet, Acknowledgement_AcknowledgementType } from "./models/packets";
import { StateManager } from "./state-manager";
import { Response_Connect, Response_ResponseType } from "./models/responses";
import { Request } from "./models/requests";
import { Command } from "./models/commands";

// Created by Moon on 6/12/2022

export * from "./scraper";
export * from "./models/models";

type TAClientEvents = {
  connectedToServer: Response_Connect;
  connectingToServer: {};
  failedToConnectToServer: {};
  disconnectedFromServer: {};

  authorizationRequestedFromServer: string;
  authorizedWithServer: string;
  failedToAuthorizeWithServer: {};

  joinedTournament: {};
  failedToJoinTournament: {};

  modifiedTournament: {};
  failedToModifyTournament: {};

  modifiedQualifier: {};
  failedToModifyQualifier: {};
};

export class TAClient extends CustomEventEmitter<TAClientEvents> {
  public stateManager: StateManager;

  private client?: Client;
  private token = "";

  private shouldHeartbeat = false;
  private heartbeatInterval: NodeJS.Timer | undefined;

  constructor() {
    super();
    this.stateManager = new StateManager();
  }

  // --- State helpers --- //
  public get isConnected() {
    return this.client?.isConnected ?? false;
  }

  // --- Actions --- //
  public connect(serverAddress: string, port: string) {
    this.shouldHeartbeat = true;

    this.client = new Client(serverAddress, port, this.token);

    this.client.on("packetReceived", this.handlePacket);
    this.client.on("connectedToServer", () => {
      const packet: Packet = {
        token: this.token,
        from: this.stateManager.getSelfGuid(), //Temporary, will be changed on successful connection to tourney
        id: uuidv4(),
        packet: {
          oneofKind: "request",
          request: {
            type: {
              oneofKind: "connect",
              connect: {
                clientVersion: 100,
              },
            },
          },
        },
      };

      this.client?.send(packet);

      if (this.shouldHeartbeat) {
        this.heartbeatInterval = setInterval(() => {
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
    });
    this.client.on("disconnectedFromServer", () => {
      clearInterval(this.heartbeatInterval);

      console.info("Disconnected from server!");
      this.emit("disconnectedFromServer", {});
    });
    this.client.on("failedToConnectToServer", () => {
      console.error("Failed to connect to server!");
      this.emit("failedToConnectToServer", {});
    });

    this.emit("connectingToServer", {});
    this.client.connect();
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

  private forwardToUsers(to: string[], packet: Packet) {
    this.client?.send({
      token: this.token,
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

  private sendCommand(to: string[], command: Command) {
    const packet: Packet = {
      token: this.token,
      from: this.stateManager.getSelfGuid(),
      id: uuidv4(),
      packet: {
        oneofKind: "command",
        command,
      },
    };

    this.forwardToUsers(to, packet);
  }

  private sendRequest(to: string[], request: Request) {
    const packet: Packet = {
      token: this.token,
      from: this.stateManager.getSelfGuid(),
      id: uuidv4(),
      packet: {
        oneofKind: "request",
        request,
      },
    };

    this.forwardToUsers(to, packet);
  }

  public joinTournament(tournamentId: string) {
    this.client?.send({
      token: this.token,
      from: this.stateManager.getSelfGuid(),
      id: uuidv4(),
      packet: {
        oneofKind: "request",
        request: {
          type: {
            oneofKind: "join",
            join: {
              tournamentId,
              password: "",
            },
          },
        },
      },
    });
  }

  // --- Packet Handler --- //
  private handlePacket = (packet: Packet) => {
    this.stateManager.handlePacket(packet);

    if (packet.packet.oneofKind !== "acknowledgement") {
      const send: Packet = {
        token: this.token,
        from: this.stateManager.getSelfGuid(),
        id: uuidv4(),
        packet: {
          oneofKind: "acknowledgement",
          acknowledgement: {
            packetId: packet.id,
            type: Acknowledgement_AcknowledgementType.MessageReceived,
          },
        },
      };

      this.client?.send(send);
    }

    if (packet.packet.oneofKind === "command") {
      const command = packet.packet.command;
      switch (command.type.oneofKind) {
        case "discordAuthorize": {
          this.emit(
            "authorizationRequestedFromServer",
            command.type.discordAuthorize
          );
          break;
        }
      }
    } else if (packet.packet.oneofKind === "response") {
      const response = packet.packet.response;

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
      } else if (response.details.oneofKind === "updateTournament") {
        const modifyTournament = response.details.updateTournament;

        if (response.type === Response_ResponseType.Success) {
          console.info(`Successfully modified tournament!`);
          this.emit("modifiedTournament", {});
        } else {
          console.error(
            `Failed modify tournament. Message: ${modifyTournament.message}`
          );
          this.emit("failedToModifyTournament", {});
        }
      } else if (response.details.oneofKind === "updateQualifierEvent") {
        const modifyQualifier = response.details.updateQualifierEvent;

        if (response.type === Response_ResponseType.Success) {
          console.info(`Successfully modified qualifier!`);
          this.emit("modifiedQualifier", {});
        } else {
          console.error(
            `Failed to modify qualifier. Message: ${modifyQualifier.message}`
          );
          this.emit("failedToModifyQualifier", {});
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
    }
  };

  // --- State Actions --- //
  public updateUser = (tournamentId: string, user: User) => {
    this.client?.send({
      token: this.token,
      from: this.stateManager.getSelfGuid(),
      id: uuidv4(),
      packet: {
        oneofKind: "event",
        event: {
          changedObject: {
            oneofKind: "userUpdated",
            userUpdated: {
              tournamentGuid: tournamentId,
              user,
            },
          },
        },
      },
    });
  };

  public createMatch = (tournamentId: string, match: Match) => {
    this.client?.send({
      token: this.token,
      from: this.stateManager.getSelfGuid(),
      id: uuidv4(),
      packet: {
        oneofKind: "event",
        event: {
          changedObject: {
            oneofKind: "matchCreated",
            matchCreated: {
              tournamentGuid: tournamentId,
              match,
            },
          },
        },
      },
    });
  };

  public updateMatch = (tournamentId: string, match: Match) => {
    this.client?.send({
      token: this.token,
      from: this.stateManager.getSelfGuid(),
      id: uuidv4(),
      packet: {
        oneofKind: "event",
        event: {
          changedObject: {
            oneofKind: "matchUpdated",
            matchUpdated: {
              tournamentGuid: tournamentId,
              match,
            },
          },
        },
      },
    });
  };

  public deleteMatch = (tournamentId: string, match: Match) => {
    this.client?.send({
      token: this.token,
      from: this.stateManager.getSelfGuid(),
      id: uuidv4(),
      packet: {
        oneofKind: "event",
        event: {
          changedObject: {
            oneofKind: "matchDeleted",
            matchDeleted: {
              tournamentGuid: tournamentId,
              match,
            },
          },
        },
      },
    });
  };

  public createQualifierEvent = (
    tournamentId: string,
    event: QualifierEvent
  ) => {
    this.client?.send({
      token: this.token,
      from: this.stateManager.getSelfGuid(),
      id: uuidv4(),
      packet: {
        oneofKind: "event",
        event: {
          changedObject: {
            oneofKind: "qualifierCreated",
            qualifierCreated: {
              tournamentGuid: tournamentId,
              event,
            },
          },
        },
      },
    });
  };

  public updateQualifierEvent = (
    tournamentId: string,
    event: QualifierEvent
  ) => {
    this.client?.send({
      token: this.token,
      from: this.stateManager.getSelfGuid(),
      id: uuidv4(),
      packet: {
        oneofKind: "event",
        event: {
          changedObject: {
            oneofKind: "qualifierUpdated",
            qualifierUpdated: {
              tournamentGuid: tournamentId,
              event,
            },
          },
        },
      },
    });
  };

  public deleteQualifierEvent = (
    tournamentId: string,
    event: QualifierEvent
  ) => {
    this.client?.send({
      token: this.token,
      from: this.stateManager.getSelfGuid(),
      id: uuidv4(),
      packet: {
        oneofKind: "event",
        event: {
          changedObject: {
            oneofKind: "qualifierDeleted",
            qualifierDeleted: {
              tournamentGuid: tournamentId,
              event,
            },
          },
        },
      },
    });
  };

  public createTournament = (tournament: Tournament) => {
    this.client?.send({
      token: this.token,
      from: this.stateManager.getSelfGuid(),
      id: uuidv4(),
      packet: {
        oneofKind: "event",
        event: {
          changedObject: {
            oneofKind: "tournamentCreated",
            tournamentCreated: {
              tournament,
            },
          },
        },
      },
    });
  };

  public updateTournament = (tournament: Tournament) => {
    this.client?.send({
      token: this.token,
      from: this.stateManager.getSelfGuid(),
      id: uuidv4(),
      packet: {
        oneofKind: "event",
        event: {
          changedObject: {
            oneofKind: "tournamentUpdated",
            tournamentUpdated: {
              tournament,
            },
          },
        },
      },
    });
  };

  public deleteTournament = (tournament: Tournament) => {
    this.client?.send({
      token: this.token,
      from: this.stateManager.getSelfGuid(),
      id: uuidv4(),
      packet: {
        oneofKind: "event",
        event: {
          changedObject: {
            oneofKind: "tournamentDeleted",
            tournamentDeleted: {
              tournament,
            },
          },
        },
      },
    });
  };

  public addServer = (server: CoreServer) => {
    this.client?.send({
      token: this.token,
      from: this.stateManager.getSelfGuid(),
      id: uuidv4(),
      packet: {
        oneofKind: "event",
        event: {
          changedObject: {
            oneofKind: "serverAdded",
            serverAdded: {
              server,
            },
          },
        },
      },
    });
  };

  public deleteServer = (server: CoreServer) => {
    this.client?.send({
      token: this.token,
      from: this.stateManager.getSelfGuid(),
      id: uuidv4(),
      packet: {
        oneofKind: "event",
        event: {
          changedObject: {
            oneofKind: "serverDeleted",
            serverDeleted: {
              server,
            },
          },
        },
      },
    });
  };
}
