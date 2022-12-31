import { Client } from './client';
import { CustomEventEmitter } from './custom-event-emitter';
import { v4 as uuidv4 } from 'uuid';

// Created by Moon on 6/12/2022

type TAClientEvents = {
    userConnected: User;
    userUpdated: User;
    userDisconnected: User;

    roomCreated: WatchParty_Room;
    roomUpdated: WatchParty_Room;
    roomDeleted: WatchParty_Room;

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

    constructor(host: string, port: string, name: string, password?: string, type?: ConnectionInfo_ClientTypes) {
        super();
        this.name = name;
        this.password = password ?? '';
        this.self = uuidv4();
        this.state = {
            users: [],
            rooms: []
        };

        this.client = new Client(host, port);

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
                                    //TODO
                                },
                                password: this.password,
                                clientVersion: 0,
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
                case 'roomCreatedEvent': {
                    this.roomCreated(event.changedObject.roomCreatedEvent.room!);
                    break;
                }
                case 'roomUpdatedEvent': {
                    this.roomUpdated(event.changedObject.roomUpdatedEvent.room!);
                    break;
                }
                case 'roomDeletedEvent': {
                    this.roomDeleted(event.changedObject.roomDeletedEvent.room!);
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
            else if (response.details.oneofKind === 'passiveCommandResponse') {
                const passiveCommandResponse = response.details.passiveCommandResponse;

                if (response.type === Response_ResponseType.Success) {
                    (console as any).success(passiveCommandResponse.response);
                }
                else {
                    console.error(passiveCommandResponse.response);
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

    public createRoom = (room: WatchParty_Room) => {
        this.client.send({
            from: this.self,
            id: uuidv4(),
            packet: {
                oneofKind: 'event',
                event: {
                    changedObject: {
                        oneofKind: 'roomCreatedEvent',
                        roomCreatedEvent: {
                            room
                        }
                    }
                }
            }
        })
    }

    private roomCreated = (room: WatchParty_Room) => {
        this.state.rooms = [...this.state.rooms, room];
        this.emit('roomCreated', room);
    };

    public updateRoom = (room: WatchParty_Room) => {
        this.client.send({
            from: this.self,
            id: uuidv4(),
            packet: {
                oneofKind: 'event',
                event: {
                    changedObject: {
                        oneofKind: 'roomUpdatedEvent',
                        roomUpdatedEvent: {
                            room
                        }
                    }
                }
            }
        })
    }

    private roomUpdated = (room: WatchParty_Room) => {
        const index = this.state.rooms.findIndex((x) => x.guid === room.guid);
        this.state.rooms[index] = room;
        this.emit('roomUpdated', room);
    };

    public deleteRoom = (room: WatchParty_Room) => {
        this.client.send({
            from: this.self,
            id: uuidv4(),
            packet: {
                oneofKind: 'event',
                event: {
                    changedObject: {
                        oneofKind: 'roomDeletedEvent',
                        roomDeletedEvent: {
                            room
                        }
                    }
                }
            }
        })
    }

    private roomDeleted = (room: WatchParty_Room) => {
        this.state.rooms = this.state.rooms.filter((x) => x.guid !== room.guid);
        this.emit('roomDeleted', room);
    };
}
