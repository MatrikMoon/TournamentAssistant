import { Client } from './client';
import { CustomEventEmitter } from './custom-event-emitter';
import { v4 as uuidv4 } from 'uuid';
import { User, State, Match, User_PlayStates, User_DownloadStates, User_ClientTypes, QualifierEvent, CoreServer } from './models/models';
import { Packet, Command, Acknowledgement_AcknowledgementType, Response_ResponseType, Request } from './models/packets';

// Created by Moon on 6/12/2022

type TAClientEvents = {
    userConnected: User;
    userUpdated: User;
    userDisconnected: User;

    matchCreated: Match;
    matchUpdated: Match;
    matchDeleted: Match;

    qualifierCreated: QualifierEvent;
    qualifierUpdated: QualifierEvent;
    qualifierDeleted: QualifierEvent;

    serverAdded: CoreServer;
    serverDeleted: CoreServer;

    connectedToServer: {};
    failedToConnectToServer: {};
    disconnectedFromServer: {};
    authorizationRequestedFromServer: string;
    authorizedWithServer: {};
    failedToAuthorizeWithServer: {};
};

export class TAClient extends CustomEventEmitter<TAClientEvents> {
    public self: string;
    private state: State;

    private client: Client;
    private name: string;
    private password: string;

    private shouldHeartbeat = false;
    private heartbeatInterval: NodeJS.Timer | undefined;

    constructor(serverAddress: string, port: string, name: string, password?: string, type?: User_ClientTypes) {
        super();
        this.name = name;
        this.password = password ?? '';
        this.self = uuidv4();
        this.state = {
            users: [],
            matches: [],
            events: [],
            knownServers: [],
        };

        this.client = new Client(serverAddress, port);

        this.client.on('packetReceived', this.handlePacket);
        this.client.on('connectedToServer', () => {
            const packet: Packet = {
                from: this.self, //Temporary, will be changed on successful connection
                id: uuidv4(),
                packet: {
                    oneofKind: 'request',
                    request: {
                        type: {
                            oneofKind: 'connect',
                            connect: {
                                user: {
                                    guid: uuidv4(), //will be replaced by server-given id
                                    name: this.name,
                                    clientType: type ?? User_ClientTypes.WebsocketConnection,
                                    userId: '',
                                    playState: User_PlayStates.Waiting,
                                    downloadState: User_DownloadStates.None,
                                    modList: [],
                                    streamDelayMs: BigInt(0),
                                    streamSyncStartMs: BigInt(0),
                                    userImage: new Uint8Array(),
                                },
                                password: this.password,
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
                        from: this.self,
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

    public get isConnected() {
        return this.client.isConnected;
    }

    public get users() {
        return this.state.users;
    }

    public getUser(id: string) {
        return this.state.users.find(x => x.guid === id);
    }

    public get matches() {
        return this.state.matches;
    }

    public getMatch(id: string) {
        return this.state.matches.find(x => x.guid === id);
    }

    public get qualifierEvents() {
        return this.state.events;
    }

    public getQualifierEvent(id: string) {
        return this.state.events.find(x => x.guid === id);
    }

    public get knownServers() {
        return this.state.knownServers;
    }

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
            from: this.self,
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
            from: this.self,
            id: uuidv4(),
            packet: {
                oneofKind: 'request',
                request
            },
        };

        this.forwardToUsers(to, packet);
    }

    private handlePacket = (packet: Packet) => {
        if (packet.packet.oneofKind !== 'acknowledgement') {
            const send: Packet = {
                from: this.self,
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

        if (packet.packet.oneofKind === 'event') {
            const event = packet.packet.event;
            switch (event.changedObject.oneofKind) {
                case 'userAddedEvent': {
                    this.userConnected(event.changedObject.userAddedEvent.user!);
                    break;
                }
                case 'userUpdatedEvent': {
                    this.userUpdated(event.changedObject.userUpdatedEvent.user!);
                    break;
                }
                case 'userLeftEvent': {
                    this.userDisconnected(event.changedObject.userLeftEvent.user!);
                    break;
                }
                case 'matchCreatedEvent': {
                    this.matchCreated(event.changedObject.matchCreatedEvent.match!);
                    break;
                }
                case 'matchUpdatedEvent': {
                    this.matchUpdated(event.changedObject.matchUpdatedEvent.match!);
                    break;
                }
                case 'matchDeletedEvent': {
                    this.matchDeleted(event.changedObject.matchDeletedEvent.match!);
                    break;
                }
                case 'qualifierCreatedEvent': {
                    this.qualifierEventCreated(event.changedObject.qualifierCreatedEvent.event!);
                    break;
                }
                case 'qualifierUpdatedEvent': {
                    this.qualifierEventUpdated(event.changedObject.qualifierUpdatedEvent.event!);
                    break;
                }
                case 'qualifierDeletedEvent': {
                    this.qualifierEventDeleted(event.changedObject.qualifierDeletedEvent.event!);
                    break;
                }
                case 'serverAddedEvent': {
                    this.serverAdded(event.changedObject.serverAddedEvent.server!);
                    break;
                }
                case 'serverDeletedEvent': {
                    this.serverDeleted(event.changedObject.serverDeletedEvent.server!);
                    break;
                }
            }
        }
        else if (packet.packet.oneofKind === 'command') {
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
                    this.state = connect.state!;
                    this.self = connect.selfGuid;

                    console.info(`Successfully authorized with server!`);
                    this.emit('connectedToServer', {});
                } else {
                    console.error(`Failed to authorize with server. Message: ${connect.message}`);
                    this.emit('failedToConnectToServer', {});
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

    private userConnected = (user: User) => {
        this.state.users = [...this.state.users, user];
        this.emit('userConnected', user);
    };

    public updateUser = (user: User) => {
        this.client.send({
            from: this.self,
            id: uuidv4(),
            packet: {
                oneofKind: 'event',
                event: {
                    changedObject: {
                        oneofKind: 'userUpdatedEvent',
                        userUpdatedEvent: {
                            user
                        }
                    }
                }
            }
        })
    }

    private userUpdated = (user: User) => {
        const index = this.state.users.findIndex((x) => x.guid === user.guid);
        this.state.users[index] = user;
        this.emit('userUpdated', user);
    };

    private userDisconnected = (user: User) => {
        this.state.users = this.state.users.filter((x) => x.guid !== user.guid);
        this.emit('userDisconnected', user);
    };

    public createMatch = (match: Match) => {
        this.client.send({
            from: this.self,
            id: uuidv4(),
            packet: {
                oneofKind: 'event',
                event: {
                    changedObject: {
                        oneofKind: 'matchCreatedEvent',
                        matchCreatedEvent: {
                            match
                        }
                    }
                }
            }
        })
    }

    private matchCreated = (match: Match) => {
        this.state.matches = [...this.state.matches, match];
        this.emit('matchCreated', match);
    };

    public updateMatch = (match: Match) => {
        this.client.send({
            from: this.self,
            id: uuidv4(),
            packet: {
                oneofKind: 'event',
                event: {
                    changedObject: {
                        oneofKind: 'matchUpdatedEvent',
                        matchUpdatedEvent: {
                            match
                        }
                    }
                }
            }
        })
    }

    private matchUpdated = (match: Match) => {
        const index = this.state.matches.findIndex((x) => x.guid === match.guid);
        this.state.matches[index] = match;
        this.emit('matchUpdated', match);
    };

    public deleteMatch = (match: Match) => {
        this.client.send({
            from: this.self,
            id: uuidv4(),
            packet: {
                oneofKind: 'event',
                event: {
                    changedObject: {
                        oneofKind: 'matchDeletedEvent',
                        matchDeletedEvent: {
                            match
                        }
                    }
                }
            }
        })
    }

    private matchDeleted = (match: Match) => {
        this.state.matches = this.state.matches.filter((x) => x.guid !== match.guid);
        this.emit('matchDeleted', match);
    };

    public createQualifierEvent = (event: QualifierEvent) => {
        this.client.send({
            from: this.self,
            id: uuidv4(),
            packet: {
                oneofKind: 'event',
                event: {
                    changedObject: {
                        oneofKind: 'qualifierCreatedEvent',
                        qualifierCreatedEvent: {
                            event
                        }
                    }
                }
            }
        })
    }

    private qualifierEventCreated = (event: QualifierEvent) => {
        this.state.events = [...this.state.events, event];
        this.emit('qualifierCreated', event);
    };

    public updateQualifierEvent = (event: QualifierEvent) => {
        this.client.send({
            from: this.self,
            id: uuidv4(),
            packet: {
                oneofKind: 'event',
                event: {
                    changedObject: {
                        oneofKind: 'qualifierUpdatedEvent',
                        qualifierUpdatedEvent: {
                            event
                        }
                    }
                }
            }
        })
    }

    private qualifierEventUpdated = (event: QualifierEvent) => {
        const index = this.state.events.findIndex((x) => x.guid === event.guid);
        this.state.events[index] = event;
        this.emit('qualifierUpdated', event);
    };

    public deleteQualifierEvent = (event: QualifierEvent) => {
        this.client.send({
            from: this.self,
            id: uuidv4(),
            packet: {
                oneofKind: 'event',
                event: {
                    changedObject: {
                        oneofKind: 'qualifierDeletedEvent',
                        qualifierDeletedEvent: {
                            event
                        }
                    }
                }
            }
        })
    }

    private qualifierEventDeleted = (event: QualifierEvent) => {
        this.state.events = this.state.events.filter((x) => x.guid !== event.guid);
        this.emit('qualifierDeleted', event);
    };

    public addServer = (server: CoreServer) => {
        this.client.send({
            from: this.self,
            id: uuidv4(),
            packet: {
                oneofKind: 'event',
                event: {
                    changedObject: {
                        oneofKind: 'serverAddedEvent',
                        serverAddedEvent: {
                            server
                        }
                    }
                }
            }
        })
    }

    private serverAdded = (server: CoreServer) => {
        this.state.knownServers = [...this.state.knownServers, server];
        this.emit('serverAdded', server);
    };

    public deleteServer = (server: CoreServer) => {
        this.client.send({
            from: this.self,
            id: uuidv4(),
            packet: {
                oneofKind: 'event',
                event: {
                    changedObject: {
                        oneofKind: 'serverDeletedEvent',
                        serverDeletedEvent: {
                            server
                        }
                    }
                }
            }
        })
    }

    private serverDeleted = (server: CoreServer) => {
        this.state.knownServers = this.state.knownServers.filter((x) => `${server.address}:${server.port}` !== `${x.address}:${x.port}`);
        this.emit('serverDeleted', server);
    };
}
