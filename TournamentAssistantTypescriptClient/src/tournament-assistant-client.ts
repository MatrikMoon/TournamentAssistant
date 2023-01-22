import { Client } from './client';
import { CustomEventEmitter } from './custom-event-emitter';
import { v4 as uuidv4 } from 'uuid';
import { User, State, Match, User_ClientTypes, QualifierEvent, CoreServer, Tournament } from './models/models';
import { Packet, Command, Acknowledgement_AcknowledgementType, Response_ResponseType, Request } from './models/packets';

// Created by Moon on 6/12/2022

type TAClientEvents = {
    userConnected: [User, Tournament];
    userUpdated: [User, Tournament];
    userDisconnected: [User, Tournament];

    matchCreated: [Match, Tournament];
    matchUpdated: [Match, Tournament];
    matchDeleted: [Match, Tournament];

    qualifierCreated: [QualifierEvent, Tournament];
    qualifierUpdated: [QualifierEvent, Tournament];
    qualifierDeleted: [QualifierEvent, Tournament];

    tournamentCreated: Tournament;
    tournamentUpdated: Tournament;
    tournamentDeleted: Tournament;

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

    private shouldHeartbeat = false;
    private heartbeatInterval: NodeJS.Timer | undefined;

    constructor(serverAddress: string, port: string, name: string, type?: User_ClientTypes) {
        super();
        this.name = name;
        this.self = uuidv4();
        this.state = {
            tournaments: [],
            knownServers: [],
        };

        this.client = new Client(serverAddress, port);

        this.client.on('packetReceived', this.handlePacket);
        this.client.on('connectedToServer', () => {
            const packet: Packet = {
                from: this.self, //Temporary, will be changed on successful connection to tourney
                id: uuidv4(),
                packet: {
                    oneofKind: 'request',
                    request: {
                        type: {
                            oneofKind: 'info',
                            info: {
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

    public getTournament(id: string) {
        return this.state.tournaments.find(x => x.guid === id);
    }

    public getTournaments() {
        return this.state.tournaments;
    }

    public getUsers(tournamentId: string) {
        return this.getTournament(tournamentId)?.users;
    }

    public getUser(tournamentId: string, userId: string) {
        return this.getTournament(tournamentId)?.users.find(x => x.guid === userId);
    }

    public getMatches(tournamentId: string) {
        return this.getTournament(tournamentId)?.matches;
    }

    public getMatch(tournamentId: string, matchId: string) {
        return this.getTournament(tournamentId)?.matches.find(x => x.guid === matchId);
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
                case 'userAdded': {
                    this.userConnected(event.changedObject.userAdded.tournamentGuid, event.changedObject.userAdded.user!);
                    break;
                }
                case 'userUpdated': {
                    this.userUpdated(event.changedObject.userUpdated.tournamentGuid, event.changedObject.userUpdated.user!);
                    break;
                }
                case 'userLeft': {
                    this.userDisconnected(event.changedObject.userLeft.tournamentGuid, event.changedObject.userLeft.user!);
                    break;
                }
                case 'matchCreated': {
                    this.matchCreated(event.changedObject.matchCreated.tournamentGuid, event.changedObject.matchCreated.match!);
                    break;
                }
                case 'matchUpdated': {
                    this.matchUpdated(event.changedObject.matchUpdated.tournamentGuid, event.changedObject.matchUpdated.match!);
                    break;
                }
                case 'matchDeleted': {
                    this.matchDeleted(event.changedObject.matchDeleted.tournamentGuid, event.changedObject.matchDeleted.match!);
                    break;
                }
                case 'qualifierCreated': {
                    this.qualifierEventCreated(event.changedObject.qualifierCreated.tournamentGuid, event.changedObject.qualifierCreated.event!);
                    break;
                }
                case 'qualifierUpdated': {
                    this.qualifierEventUpdated(event.changedObject.qualifierUpdated.tournamentGuid, event.changedObject.qualifierUpdated.event!);
                    break;
                }
                case 'qualifierDeleted': {
                    this.qualifierEventDeleted(event.changedObject.qualifierDeleted.tournamentGuid, event.changedObject.qualifierDeleted.event!);
                    break;
                }
                case 'tournamentCreated': {
                    this.tournamentCreated(event.changedObject.tournamentCreated.tournament!);
                    break;
                }
                case 'tournamentUpdated': {
                    this.tournamentUpdated(event.changedObject.tournamentUpdated.tournament!);
                    break;
                }
                case 'tournamentDeleted': {
                    this.tournamentDeleted(event.changedObject.tournamentDeleted.tournament!);
                    break;
                }
                case 'serverAdded': {
                    this.serverAdded(event.changedObject.serverAdded.server!);
                    break;
                }
                case 'serverDeleted': {
                    this.serverDeleted(event.changedObject.serverDeleted.server!);
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

    private userConnected = (tournamentId: string, user: User) => {
        const tournament = this.getTournament(tournamentId);
        tournament!.users = [...tournament!.users, user];
        this.emit('userConnected', [user, tournament!]);
    };

    public updateUser = (tournamentId: string, user: User) => {
        this.client.send({
            from: this.self,
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

    private userUpdated = (tournamentId: string, user: User) => {
        const tournament = this.getTournament(tournamentId);
        const index = tournament!.users.findIndex((x) => x.guid === user.guid);
        tournament!.users[index] = user;
        this.emit('userUpdated', [user, tournament!]);
    };

    private userDisconnected = (tournamentId: string, user: User) => {
        const tournament = this.getTournament(tournamentId);
        tournament!.users = tournament!.users.filter((x) => x.guid !== user.guid);
        this.emit('userDisconnected', [user, tournament!]);
    };

    public createMatch = (tournamentId: string, match: Match) => {
        this.client.send({
            from: this.self,
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

    private matchCreated = (tournamentId: string, match: Match) => {
        const tournament = this.getTournament(tournamentId);
        tournament!.matches = [...tournament!.matches, match];
        this.emit('matchCreated', [match, tournament!]);
    };

    public updateMatch = (tournamentId: string, match: Match) => {
        this.client.send({
            from: this.self,
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

    private matchUpdated = (tournamentId: string, match: Match) => {
        const tournament = this.getTournament(tournamentId);
        const index = tournament!.matches.findIndex((x) => x.guid === match.guid);
        tournament!.matches[index] = match;
        this.emit('matchUpdated', [match, tournament!]);
    };

    public deleteMatch = (tournamentId: string, match: Match) => {
        this.client.send({
            from: this.self,
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

    private matchDeleted = (tournamentId: string, match: Match) => {
        const tournament = this.getTournament(tournamentId);
        tournament!.matches = tournament!.matches.filter((x) => x.guid !== match.guid);
        this.emit('matchDeleted', [match, tournament!]);
    };

    public createQualifierEvent = (tournamentId: string, event: QualifierEvent) => {
        this.client.send({
            from: this.self,
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

    private qualifierEventCreated = (tournamentId: string, event: QualifierEvent) => {
        const tournament = this.getTournament(tournamentId);
        tournament!.qualifiers = [...tournament!.qualifiers, event];
        this.emit('qualifierCreated', [event, tournament!]);
    };

    public updateQualifierEvent = (tournamentId: string, event: QualifierEvent) => {
        this.client.send({
            from: this.self,
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

    private qualifierEventUpdated = (tournamentId: string, event: QualifierEvent) => {
        const tournament = this.getTournament(tournamentId);
        const index = tournament!.qualifiers.findIndex((x) => x.guid === event.guid);
        tournament!.qualifiers[index] = event;
        this.emit('qualifierUpdated', [event, tournament!]);
    };

    public deleteQualifierEvent = (tournamentId: string, event: QualifierEvent) => {
        this.client.send({
            from: this.self,
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

    private qualifierEventDeleted = (tournamentId: string, event: QualifierEvent) => {
        const tournament = this.getTournament(tournamentId);
        tournament!.qualifiers = tournament!.qualifiers.filter((x) => x.guid !== event.guid);
        this.emit('qualifierDeleted', [event, tournament!]);
    };

    public createTournament = (tournament: Tournament) => {
        this.client.send({
            from: this.self,
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

    private tournamentCreated = (tournament: Tournament) => {
        this.state.tournaments = [...this.state.tournaments, tournament];
        this.emit('tournamentCreated', tournament);
    };

    public updateTournament = (tournament: Tournament) => {
        this.client.send({
            from: this.self,
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

    private tournamentUpdated = (tournament: Tournament) => {
        const index = this.state.tournaments.findIndex((x) => x.guid === tournament.guid);
        this.state.tournaments[index] = tournament;
        this.emit('tournamentUpdated', tournament);
    };

    public deleteTournament = (tournament: Tournament) => {
        this.client.send({
            from: this.self,
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

    private tournamentDeleted = (tournament: Tournament) => {
        this.state.tournaments = this.state.tournaments.filter((x) => x.guid !== tournament.guid);
        this.emit('tournamentDeleted', tournament);
    };

    public addServer = (server: CoreServer) => {
        this.client.send({
            from: this.self,
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
                        oneofKind: 'serverDeleted',
                        serverDeleted: {
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
