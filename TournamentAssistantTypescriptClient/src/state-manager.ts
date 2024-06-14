import { CustomEventEmitter } from "./custom-event-emitter.js";
import {
  User,
  Tournament,
  Match,
  QualifierEvent,
  CoreServer,
  State,
} from "./models/models.js";
import { Packet } from "./models/packets.js";
import { v4 as uuidv4 } from "uuid";
import { Response_ResponseType } from "./models/responses.js";

type StateManagerEvents = {
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
};

export class StateManager extends CustomEventEmitter<StateManagerEvents> {
  private state: State;
  private self: string;

  constructor() {
    super();
    this.self = uuidv4();
    this.state = {
      tournaments: [],
      knownServers: [],
    };
  }

  // --- Packet handler --- //
  public handlePacket(packet: Packet) {
    if (packet.packet.oneofKind === "event") {
      const event = packet.packet.event;
      switch (event.changedObject.oneofKind) {
        case "userAdded": {
          this.userConnected(
            event.changedObject.userAdded.tournamentId,
            event.changedObject.userAdded.user!
          );
          break;
        }
        case "userUpdated": {
          this.userUpdated(
            event.changedObject.userUpdated.tournamentId,
            event.changedObject.userUpdated.user!
          );
          break;
        }
        case "userLeft": {
          this.userDisconnected(
            event.changedObject.userLeft.tournamentId,
            event.changedObject.userLeft.user!
          );
          break;
        }
        case "matchCreated": {
          this.matchCreated(
            event.changedObject.matchCreated.tournamentId,
            event.changedObject.matchCreated.match!
          );
          break;
        }
        case "matchUpdated": {
          this.matchUpdated(
            event.changedObject.matchUpdated.tournamentId,
            event.changedObject.matchUpdated.match!
          );
          break;
        }
        case "matchDeleted": {
          this.matchDeleted(
            event.changedObject.matchDeleted.tournamentId,
            event.changedObject.matchDeleted.match!
          );
          break;
        }
        case "qualifierCreated": {
          this.qualifierEventCreated(
            event.changedObject.qualifierCreated.tournamentId,
            event.changedObject.qualifierCreated.event!
          );
          break;
        }
        case "qualifierUpdated": {
          this.qualifierEventUpdated(
            event.changedObject.qualifierUpdated.tournamentId,
            event.changedObject.qualifierUpdated.event!
          );
          break;
        }
        case "qualifierDeleted": {
          this.qualifierEventDeleted(
            event.changedObject.qualifierDeleted.tournamentId,
            event.changedObject.qualifierDeleted.event!
          );
          break;
        }
        case "tournamentCreated": {
          this.tournamentCreated(
            event.changedObject.tournamentCreated.tournament!
          );
          break;
        }
        case "tournamentUpdated": {
          this.tournamentUpdated(
            event.changedObject.tournamentUpdated.tournament!
          );
          break;
        }
        case "tournamentDeleted": {
          this.tournamentDeleted(
            event.changedObject.tournamentDeleted.tournament!
          );
          break;
        }
        case "serverAdded": {
          this.serverAdded(event.changedObject.serverAdded.server!);
          break;
        }
        case "serverDeleted": {
          this.serverDeleted(event.changedObject.serverDeleted.server!);
          break;
        }
      }
    } else if (packet.packet.oneofKind === "response") {
      const response = packet.packet.response;

      if (response.details.oneofKind === "connect") {
        const connect = response.details.connect;

        if (response.type === Response_ResponseType.Success) {
          this.state = connect.state!;
        }
      } else if (response.details.oneofKind === "join") {
        const join = response.details.join;

        if (response.type === Response_ResponseType.Success) {
          this.state = join.state!;
          this.self = join.selfGuid;
        }
      }
    }
  }

  // --- Helpers --- //
  public getSelfGuid() {
    return this.self;
  }

  public getTournaments() {
    return this.state.tournaments;
  }

  public getTournament(id: string) {
    return this.state.tournaments.find((x) => x.guid === id);
  }

  public getUsers(tournamentId: string) {
    return this.getTournament(tournamentId)?.users;
  }

  public getUser(tournamentId: string, userId: string) {
    return this.getUsers(tournamentId)?.find((x) => x.guid === userId);
  }

  public getMatches(tournamentId: string) {
    return this.getTournament(tournamentId)?.matches;
  }

  public getMatch(tournamentId: string, matchId: string) {
    return this.getMatches(tournamentId)?.find((x) => x.guid === matchId);
  }

  public getQualifiers(tournamentId: string) {
    return this.getTournament(tournamentId)?.qualifiers;
  }

  public getQualifier(tournamentId: string, qualifierId: string) {
    return this.getQualifiers(tournamentId)?.find((x) => x.guid === qualifierId);
  }

  public getKnownServers() {
    return this.state.knownServers;
  }

  // --- Event handlers --- //
  private userConnected = (tournamentId: string, user: User) => {
    const tournament = this.getTournament(tournamentId);
    tournament!.users = [...tournament!.users, user];
    this.emit("userConnected", [user, tournament!]);
  };

  private userUpdated = (tournamentId: string, user: User) => {
    const tournament = this.getTournament(tournamentId);
    const index = tournament!.users.findIndex((x) => x.guid === user.guid);
    tournament!.users[index] = user;
    this.emit("userUpdated", [user, tournament!]);
  };

  private userDisconnected = (tournamentId: string, user: User) => {
    const tournament = this.getTournament(tournamentId);
    tournament!.users = tournament!.users.filter((x) => x.guid !== user.guid);
    this.emit("userDisconnected", [user, tournament!]);
  };

  private matchCreated = (tournamentId: string, match: Match) => {
    const tournament = this.getTournament(tournamentId);
    tournament!.matches = [...tournament!.matches, match];
    this.emit("matchCreated", [match, tournament!]);
  };

  private matchUpdated = (tournamentId: string, match: Match) => {
    const tournament = this.getTournament(tournamentId);
    const index = tournament!.matches.findIndex((x) => x.guid === match.guid);
    tournament!.matches[index] = match;
    this.emit("matchUpdated", [match, tournament!]);
  };

  private matchDeleted = (tournamentId: string, match: Match) => {
    const tournament = this.getTournament(tournamentId);
    tournament!.matches = tournament!.matches.filter(
      (x) => x.guid !== match.guid
    );
    this.emit("matchDeleted", [match, tournament!]);
  };

  private qualifierEventCreated = (
    tournamentId: string,
    event: QualifierEvent
  ) => {
    const tournament = this.getTournament(tournamentId);
    tournament!.qualifiers = [...tournament!.qualifiers, event];
    this.emit("qualifierCreated", [event, tournament!]);
  };

  private qualifierEventUpdated = (
    tournamentId: string,
    event: QualifierEvent
  ) => {
    const tournament = this.getTournament(tournamentId);
    const index = tournament!.qualifiers.findIndex(
      (x) => x.guid === event.guid
    );
    tournament!.qualifiers[index] = event;
    this.emit("qualifierUpdated", [event, tournament!]);
  };

  private qualifierEventDeleted = (
    tournamentId: string,
    event: QualifierEvent
  ) => {
    const tournament = this.getTournament(tournamentId);
    tournament!.qualifiers = tournament!.qualifiers.filter(
      (x) => x.guid !== event.guid
    );
    this.emit("qualifierDeleted", [event, tournament!]);
  };

  private tournamentCreated = (tournament: Tournament) => {
    this.state.tournaments = [...this.state.tournaments, tournament];
    this.emit("tournamentCreated", tournament);
  };

  private tournamentUpdated = (tournament: Tournament) => {
    const index = this.state.tournaments.findIndex(
      (x) => x.guid === tournament.guid
    );
    this.state.tournaments[index] = tournament;
    this.emit("tournamentUpdated", tournament);
  };

  private tournamentDeleted = (tournament: Tournament) => {
    this.state.tournaments = this.state.tournaments.filter(
      (x) => x.guid !== tournament.guid
    );
    this.emit("tournamentDeleted", tournament);
  };

  private serverAdded = (server: CoreServer) => {
    this.state.knownServers = [...this.state.knownServers, server];
    this.emit("serverAdded", server);
  };

  private serverDeleted = (server: CoreServer) => {
    this.state.knownServers = this.state.knownServers.filter(
      (x) => `${server.address}:${server.port}` !== `${x.address}:${x.port}`
    );
    this.emit("serverDeleted", server);
  };
}
