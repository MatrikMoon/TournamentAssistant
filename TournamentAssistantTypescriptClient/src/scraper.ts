import { CustomEventEmitter } from "./custom-event-emitter";
import { CoreServer, Tournament, User_ClientTypes } from "./models/models"
import { TAClient } from "./tournament-assistant-client";

const MASTER_ADDRESS = "server.tournamentassistant.net";
const MASTER_PORT = "2053";

export type TournamentWithServerInfo = {
    tournament: Tournament;
    address: string;
    port: string;
}

type OnProgress = {
    totalServers: number;
    succeededServers: number;
    failedServers: number;
    tournaments: TournamentWithServerInfo[];
}

type ScraperEvents = {
    onProgress: OnProgress;
}

export function getTournaments(
    onProgress: (totalServers: number, succeededServers: number, failedServers: number) => void,
    onComplete: (tournaments: TournamentWithServerInfo[]) => void) {

    const scraper = new Scraper();
    scraper.on('onProgress', (progress) => {
        onProgress(progress.totalServers, progress.succeededServers, progress.failedServers);

        if (progress.failedServers + progress.succeededServers === progress.totalServers) {
            onComplete(progress.tournaments);
        }
    });
    scraper.getTournaments();
};

class Scraper extends CustomEventEmitter<ScraperEvents> {
    private servers: CoreServer[] = [];
    private tournaments: TournamentWithServerInfo[] = [];

    private succeededServers = 0;
    private failedServers = 0;

    public getTournaments() {
        const masterClient = new TAClient(MASTER_ADDRESS, MASTER_PORT, "Typescript Scraper", User_ClientTypes.TemporaryConnection);

        masterClient.on('connectedToServer', async response => {
            this.servers = response.state!.knownServers;
            this.tournaments = response.state!.tournaments.map(x => {
                return {
                    tournament: x,
                    address: MASTER_ADDRESS,
                    port: MASTER_PORT
                }
            });

            masterClient.disconnect();

            //We successfully got tournaments from the master server
            this.succeededServers++;
            this.emit('onProgress', { totalServers: this.servers.length, succeededServers: this.succeededServers, failedServers: this.failedServers, tournaments: this.tournaments });

            //Just running this map kicks off all the Promises, so no need to await them.
            //This is probably a sin. If anyone knows the proper way to do this, hit me up on discord
            this.servers
                .filter(x => `${x.address}:${x.websocketPort}` !== `${MASTER_ADDRESS}:${MASTER_PORT}`)
                .map(x => this.getTournamentsFromServer(x.address, `${x.websocketPort}`));
        });

        masterClient.on('failedToConnectToServer', () => {
            //We failed to get tournaments from the master server
            this.failedServers++;
            this.emit('onProgress', { totalServers: this.servers.length, succeededServers: this.succeededServers, failedServers: this.failedServers, tournaments: this.tournaments });
        });

        masterClient.connect();
    }

    private async getTournamentsFromServer(address: string, port: string) {
        const client = new TAClient(address, port, "Typescript Scraper", User_ClientTypes.TemporaryConnection);

        client.on('connectedToServer', response => {
            this.tournaments = [...this.tournaments, ...response.state!.tournaments.map(x => {
                return {
                    tournament: x,
                    address,
                    port
                }
            })];

            client.disconnect();

            //We successfully got tournaments from the server
            this.succeededServers++;
            this.emit('onProgress', { totalServers: this.servers.length, succeededServers: this.succeededServers, failedServers: this.failedServers, tournaments: this.tournaments });
        });

        client.on('failedToConnectToServer', () => {
            //We failed to get tournaments from the server
            this.failedServers++;
            this.emit('onProgress', { totalServers: this.servers.length, succeededServers: this.succeededServers, failedServers: this.failedServers, tournaments: this.tournaments });
        });

        client.connect();
    }
}