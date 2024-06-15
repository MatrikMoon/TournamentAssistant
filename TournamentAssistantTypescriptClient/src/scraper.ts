import { CustomEventEmitter } from "./custom-event-emitter.js";
import { CoreServer, Tournament } from "./models/models.js";
import { TAClient } from "./tournament-assistant-client.js";

const MASTER_ADDRESS = "dev.tournamentassistant.net";
const MASTER_PORT = "8676";

type OnProgress = {
  totalServers: number;
  succeededServers: number;
  failedServers: number;
  tournaments: Tournament[];
};

type ScraperEvents = {
  onProgress: OnProgress;
};

export function getTournaments(
  token: string,
  onProgress: (
    totalServers: number,
    succeededServers: number,
    failedServers: number
  ) => void,
  onComplete: (tournaments: Tournament[]) => void
) {
  const scraper = new Scraper(token);
  scraper.on("onProgress", (progress) => {
    onProgress(
      progress.totalServers,
      progress.succeededServers,
      progress.failedServers
    );

    if (
      progress.failedServers + progress.succeededServers ===
      progress.totalServers
    ) {
      onComplete(progress.tournaments);
    }
  });
  scraper.getTournaments();
}

class Scraper extends CustomEventEmitter<ScraperEvents> {
  private servers: CoreServer[] = [];
  private tournaments: Tournament[] = [];
  private token: string;

  private succeededServers = 0;
  private failedServers = 0;

  constructor(token: string) {
    super();
    this.token = token;
  }

  public getTournaments() {
    const masterClient = new TAClient();
    masterClient.setAuthToken(this.token);

    masterClient.on("connectedToServer", async (response) => {
      this.servers = response.state!.knownServers;
      this.tournaments = response.state!.tournaments;

      masterClient.disconnect();

      //We successfully got tournaments from the master server
      this.succeededServers++;
      this.emit("onProgress", {
        totalServers: this.servers.length,
        succeededServers: this.succeededServers,
        failedServers: this.failedServers,
        tournaments: this.tournaments,
      });

      //Just running this map kicks off all the Promises, so no need to await them.
      //This is probably a sin. If anyone knows the proper way to do this, hit me up on discord
      this.servers
        .filter(
          (x) =>
            `${x.address}:${x.websocketPort}` !==
            `${MASTER_ADDRESS}:${MASTER_PORT}`
        )
        .map((x) =>
          this.getTournamentsFromServer(x.address, `${x.websocketPort}`)
        );
    });

    masterClient.on("failedToConnectToServer", () => {
      //We failed to get tournaments from the master server
      this.failedServers++;
      this.emit("onProgress", {
        totalServers: this.servers.length,
        succeededServers: this.succeededServers,
        failedServers: this.failedServers,
        tournaments: this.tournaments,
      });
    });

    masterClient.connect(MASTER_ADDRESS, MASTER_PORT);
  }

  private async getTournamentsFromServer(address: string, port: string) {
    const client = new TAClient();
    client.setAuthToken(this.token);

    client.on("connectedToServer", (response) => {
      this.tournaments = [...this.tournaments, ...response.state!.tournaments];

      client.disconnect();

      //We successfully got tournaments from the server
      this.succeededServers++;
      this.emit("onProgress", {
        totalServers: this.servers.length,
        succeededServers: this.succeededServers,
        failedServers: this.failedServers,
        tournaments: this.tournaments,
      });
    });

    client.on("failedToConnectToServer", () => {
      //We failed to get tournaments from the server
      this.failedServers++;
      this.emit("onProgress", {
        totalServers: this.servers.length,
        succeededServers: this.succeededServers,
        failedServers: this.failedServers,
        tournaments: this.tournaments,
      });
    });

    client.connect(address, port);
  }
}
