import { Client } from './client';
import { CustomEventEmitter } from './custom-event-emitter';
import { v4 as uuidv4 } from 'uuid';
import { User, Match, User_ClientTypes, QualifierEvent, CoreServer, Tournament } from './models/models';
import { Packet, Command, Acknowledgement_AcknowledgementType, Response_ResponseType, Request, Response_Connect } from './models/packets';
import { StateManager } from './state-manager';

// Created by Moon on 6/12/2022

export * from './scraper'

type TAClientEvents = {
    connectedToServer: Response_Connect;
    failedToConnectToServer: {};
    disconnectedFromServer: {};
    authorizationRequestedFromServer: string;
    authorizedWithServer: {};
    failedToAuthorizeWithServer: {};

    joinedTournament: {};
    failedToJoinTournament: {};
};

export class TAClient extends CustomEventEmitter<TAClientEvents> {
    public stateManager: StateManager;

    private client: Client;
    private name: string;

    private shouldHeartbeat = false;
    private heartbeatInterval: NodeJS.Timer | undefined;

    constructor(serverAddress: string, port: string, name: string, type?: User_ClientTypes) {
        super();
        this.name = name;
        this.stateManager = new StateManager();

        this.client = new Client(serverAddress, port);

        this.client.on('packetReceived', this.handlePacket);
        this.client.on('connectedToServer', () => {
            const packet: Packet = {
                from: this.stateManager.getSelfGuid(), //Temporary, will be changed on successful connection to tourney
                id: uuidv4(),
                packet: {
                    oneofKind: 'request',
                    request: {
                        type: {
                            oneofKind: 'connect',
                            connect: {
                                clientVersion: 100,
                            }
                        }
                    }
                },
            };

            this.client.send(packet);

            if (this.shouldHeartbeat) {
                this.heartbeatInterval = setInterval(() => {
                    this.client.send({
                        from: this.stateManager.getSelfGuid(),
                        id: uuidv4(),
                        packet: {
                            oneofKind: 'command',
                            command: {
                                type: {
                                    oneofKind: 'heartbeat',
                                    heartbeat: true
                                }
                            }
                        }
                    })
                }, 10000);
            }
        });
        this.client.on('disconnectedFromServer', () => {
            clearInterval(this.heartbeatInterval);

            console.info('Disconnected from server!');
            this.emit('disconnectedFromServer', {});
        });
        this.client.on('failedToConnectToServer', () => {
            console.error('Failed to connect to server!');
            this.emit('failedToConnectToServer', {});
        });
    }

    // --- State helpers --- //
    public get isConnected() {
        return this.client.isConnected;
    }

    // --- Actions --- //
    public connect() {
        this.shouldHeartbeat = true;

        console.info(`Connecting to server!`);
        this.client.connect();
    }

    public disconnect() {
        this.shouldHeartbeat = false;

        console.info(`Disconnecting from server!`);
        this.client.disconnect();
    }

    private forwardToUsers(to: string[], packet: Packet) {
        this.client.send({
            from: packet.from,
            id: packet.id,
            packet: {
                oneofKind: 'forwardingPacket',
                forwardingPacket: {
                    forwardTo: to,
                    packet
                }
            }
        })
    }

    public sendCommand(to: string[], command: Command) {
        const packet: Packet = {
            from: this.stateManager.getSelfGuid(),
            id: uuidv4(),
            packet: {
                oneofKind: 'command',
                command
            },
        };

        this.forwardToUsers(to, packet);
    }

    public sendRequest(to: string[], request: Request) {
        const packet: Packet = {
            from: this.stateManager.getSelfGuid(),
            id: uuidv4(),
            packet: {
                oneofKind: 'request',
                request
            },
        };

        this.forwardToUsers(to, packet);
    }

    // --- Packet Handler --- //
    private handlePacket = (packet: Packet) => {
        this.stateManager.handlePacket(packet);

        if (packet.packet.oneofKind !== 'acknowledgement') {
            const send: Packet = {
                from: this.stateManager.getSelfGuid(),
                id: uuidv4(),
                packet: {
                    oneofKind: 'acknowledgement',
                    acknowledgement: {
                        packetId: packet.id,
                        type: Acknowledgement_AcknowledgementType.MessageReceived
                    }
                }
            };

            this.client.send(send);
        }

        if (packet.packet.oneofKind === 'command') {
            const command = packet.packet.command;
            switch (command.type.oneofKind) {
                case 'discordAuthorize': {
                    this.emit('authorizationRequestedFromServer', command.type.discordAuthorize);
                    break;
                }
            }
        }
        else if (packet.packet.oneofKind === 'response') {
            const response = packet.packet.response;

            if (response.details.oneofKind === 'connect') {
                const connect = response.details.connect;

                if (response.type === Response_ResponseType.Success) {
                    console.info(`Successfully connected to server!`);
                    this.emit('connectedToServer', connect);
                } else {
                    console.error(`Failed to connect to server. Message: ${connect.message}`);
                    this.emit('failedToConnectToServer', {});
                }
            }
            else if (response.details.oneofKind === 'join') {
                const join = response.details.join;

                if (response.type === Response_ResponseType.Success) {
                    console.info(`Successfully joined tournament!`);
                    this.emit('joinedTournament', {});
                } else {
                    console.error(`Failed to join server. Message: ${join.message}`);
                    this.emit('failedToJoinTournament', {});
                }
            }
        }
        else if (packet.packet.oneofKind === 'push') {
            const push = packet.packet.push;

            if (push.data.oneofKind === 'discordAuthorized') {
                if (push.data.discordAuthorized.success) {
                    this.emit('authorizedWithServer', {});
                }
                else {
                    this.emit('failedToAuthorizeWithServer', {});
                }
            }
        }
    };

    // --- State Actions --- //
    public updateUser = (tournamentId: string, user: User) => {
        this.client.send({
            from: this.stateManager.getSelfGuid(),
            id: uuidv4(),
            packet: {
                oneofKind: 'event',
                event: {
                    changedObject: {
                        oneofKind: 'userUpdated',
                        userUpdated: {
                            tournamentGuid: tournamentId,
                            user
                        }
                    }
                }
            }
        })
    }

    public createMatch = (tournamentId: string, match: Match) => {
        this.client.send({
            from: this.stateManager.getSelfGuid(),
            id: uuidv4(),
            packet: {
                oneofKind: 'event',
                event: {
                    changedObject: {
                        oneofKind: 'matchCreated',
                        matchCreated: {
                            tournamentGuid: tournamentId,
                            match
                        }
                    }
                }
            }
        })
    }

    public updateMatch = (tournamentId: string, match: Match) => {
        this.client.send({
            from: this.stateManager.getSelfGuid(),
            id: uuidv4(),
            packet: {
                oneofKind: 'event',
                event: {
                    changedObject: {
                        oneofKind: 'matchUpdated',
                        matchUpdated: {
                            tournamentGuid: tournamentId,
                            match
                        }
                    }
                }
            }
        })
    }

    public deleteMatch = (tournamentId: string, match: Match) => {
        this.client.send({
            from: this.stateManager.getSelfGuid(),
            id: uuidv4(),
            packet: {
                oneofKind: 'event',
                event: {
                    changedObject: {
                        oneofKind: 'matchDeleted',
                        matchDeleted: {
                            tournamentGuid: tournamentId,
                            match
                        }
                    }
                }
            }
        })
    }

    public createQualifierEvent = (tournamentId: string, event: QualifierEvent) => {
        this.client.send({
            from: this.stateManager.getSelfGuid(),
            id: uuidv4(),
            packet: {
                oneofKind: 'event',
                event: {
                    changedObject: {
                        oneofKind: 'qualifierCreated',
                        qualifierCreated: {
                            tournamentGuid: tournamentId,
                            event
                        }
                    }
                }
            }
        })
    }

    public updateQualifierEvent = (tournamentId: string, event: QualifierEvent) => {
        this.client.send({
            from: this.stateManager.getSelfGuid(),
            id: uuidv4(),
            packet: {
                oneofKind: 'event',
                event: {
                    changedObject: {
                        oneofKind: 'qualifierUpdated',
                        qualifierUpdated: {
                            tournamentGuid: tournamentId,
                            event
                        }
                    }
                }
            }
        })
    }

    public deleteQualifierEvent = (tournamentId: string, event: QualifierEvent) => {
        this.client.send({
            from: this.stateManager.getSelfGuid(),
            id: uuidv4(),
            packet: {
                oneofKind: 'event',
                event: {
                    changedObject: {
                        oneofKind: 'qualifierDeleted',
                        qualifierDeleted: {
                            tournamentGuid: tournamentId,
                            event
                        }
                    }
                }
            }
        })
    }

    public createTournament = (tournament: Tournament) => {
        this.client.send({
            from: this.stateManager.getSelfGuid(),
            id: uuidv4(),
            packet: {
                oneofKind: 'event',
                event: {
                    changedObject: {
                        oneofKind: 'tournamentCreated',
                        tournamentCreated: {
                            tournament
                        }
                    }
                }
            }
        })
    }

    public updateTournament = (tournament: Tournament) => {
        this.client.send({
            from: this.stateManager.getSelfGuid(),
            id: uuidv4(),
            packet: {
                oneofKind: 'event',
                event: {
                    changedObject: {
                        oneofKind: 'tournamentUpdated',
                        tournamentUpdated: {
                            tournament
                        }
                    }
                }
            }
        })
    }

    public deleteTournament = (tournament: Tournament) => {
        this.client.send({
            from: this.stateManager.getSelfGuid(),
            id: uuidv4(),
            packet: {
                oneofKind: 'event',
                event: {
                    changedObject: {
                        oneofKind: 'tournamentDeleted',
                        tournamentDeleted: {
                            tournament
                        }
                    }
                }
            }
        })
    }

    public addServer = (server: CoreServer) => {
        this.client.send({
            from: this.stateManager.getSelfGuid(),
            id: uuidv4(),
            packet: {
                oneofKind: 'event',
                event: {
                    changedObject: {
                        oneofKind: 'serverAdded',
                        serverAdded: {
                            server
                        }
                    }
                }
            }
        })
    }

    public deleteServer = (server: CoreServer) => {
        this.client.send({
            from: this.stateManager.getSelfGuid(),
            id: uuidv4(),
            packet: {
                oneofKind: 'event',
                event: {
                    changedObject: {
                        oneofKind: 'serverDeleted',
                        serverDeleted: {
                            server
                        }
                    }
                }
            }
        })
    }
}
