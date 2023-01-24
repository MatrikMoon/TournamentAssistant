import { CoreServer, Tournament, User_ClientTypes } from "./models/models"
import { TAClient } from "./tournament-assistant-client";

const MASTER_ADDRESS = "server.tournamentassistant.net";
const MASTER_PORT = "2053";

type ScraperEvents = {

}

export type TournamentWithServerInfo = {
    tournament: Tournament;
    address: string;
    port: string;
}

export class Scraper {
    private servers: CoreServer[];
    private tournaments: TournamentWithServerInfo[];

    public constructor() {
        this.servers = [];
        this.tournaments = [];
    }

    public getTournaments() {
        const masterClient = new TAClient(MASTER_ADDRESS, MASTER_PORT, "Typescript Scraper", User_ClientTypes.TemporaryConnection);

        masterClient.on('connectedToServer', async response => {
            console.log(response.state?.knownServers);

            this.servers = response.state!.knownServers;
            this.tournaments = response.state!.tournaments.map(x => {
                return {
                    tournament: x,
                    address: MASTER_ADDRESS,
                    port: MASTER_PORT
                }
            });

            masterClient.disconnect();

            const tasks = this.servers
                .filter(x => `${x.address}:${x.websocketPort}` !== `${MASTER_ADDRESS}:${MASTER_PORT}`)
                .map(x => this.getTournamentsFromServer(x.address, `${x.websocketPort}`));

            await Promise.all(tasks);

            console.log('Promises complete');
        });

        masterClient.connect();
    }

    private async getTournamentsFromServer(address: string, port: string) {
        const client = new TAClient(address, port, "Typescript Scraper", User_ClientTypes.TemporaryConnection);

        client.on('connectedToServer', response => {
            this.tournaments = [...this.tournaments, ...response.state!.tournaments.map(x => {
                return {
                    tournament: x,
                    address: MASTER_ADDRESS,
                    port: MASTER_PORT
                }
            })];

            client.disconnect();
        });

        client.connect();
    }
}