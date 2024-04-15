import { Request_LoadSong, Response_ResponseType, TAClient, User_DownloadStates } from "tournament-assistant-client";
import { v4 as uuidv4 } from "uuid";
import { KJUR } from 'jsrsasign';

// Ideally, this should mock what a Player client does in most situations

export class MockPlayer {
    private _client: TAClient;
    private _authToken: string;
    private _lastJoinedTournament: string;

    private getCurrentUnixTimeSeconds(): number {
        return Math.floor(Date.now() / 1000);
    }

    private async generateRSAKeyPair() {
        const keyPair = await window.crypto.subtle.generateKey(
            {
                name: "RSA-OAEP",
                modulusLength: 2048, // Can be 1024, 2048, or 4096
                publicExponent: new Uint8Array([0x01, 0x00, 0x01]),
                hash: { name: "SHA-256" },
            },
            true, // Whether the key is extractable (i.e., can be used in exportKey)
            ["encrypt", "decrypt"] // Use "encrypt" for public key and "decrypt" for private key
        );

        // Export the keys to PEM format
        const publicKey = await this.exportCryptoKey(keyPair.publicKey);
        const privateKey = await this.exportCryptoKey(keyPair.privateKey);

        return { publicKey, privateKey };
    }

    private async exportCryptoKey(key: CryptoKey) {
        const format = key.type === "public" ? "spki" : "pkcs8";
        const exported = await window.crypto.subtle.exportKey(
            format,
            key
        );
        const exportedAsString = this.ab2str(exported);
        const exportedAsBase64 = window.btoa(exportedAsString);
        const pemExported = `-----BEGIN ${key.type === "public" ? "PUBLIC" : "PRIVATE"} KEY-----\n${this.formatAsPem(exportedAsBase64)}\n-----END ${key.type === "public" ? "PUBLIC" : "PRIVATE"} KEY-----\n`;

        return pemExported;
    }

    private ab2str(buf: ArrayBuffer) {
        return String.fromCharCode.apply(null, Array.from(new Uint8Array(buf)));
    }

    private formatAsPem(str: string) {
        let finalString = '';
        while (str.length > 0) {
            finalString += str.substring(0, 64) + '\n';
            str = str.substring(64);
        }

        return finalString;
    }

    private generateName(desiredLength: number = -1): string {
        const consonants = ["b", "c", "d", "f", "g", "h", "j", "k", "l", "m", "l", "n", "p", "q", "r", "s", "sh", "zh", "t", "v", "w", "x"];
        const vowels = ["a", "e", "i", "o", "u", "ae", "y"];

        if (desiredLength < 0) {
            desiredLength = Math.floor(Math.random() * (20 - 6 + 1)) + 6;
        }

        let name = "";

        for (let i = 0; i < desiredLength; i++) {
            name += i % 2 === 0
                ? consonants[Math.floor(Math.random() * consonants.length)]
                : vowels[Math.floor(Math.random() * vowels.length)];

            if (i === 0) {
                name = name.toUpperCase();
            }
        }

        return name;
    }


    private async authToken() {
        const key = await this.generateRSAKeyPair();

        const platformId = Math.floor(Math.random() * (9999999999 - 1000000000 + 1)) + 1000000000;
        const discordId = Math.floor(Math.random() * (9999999999 - 1000000000 + 1)) + 1000000000;
        const name = this.generateName();

        const claims = {
            sub: uuidv4(),
            iss: "ta_plugin_mock",
            aud: "ta_users",
            iat: this.getCurrentUnixTimeSeconds(),
            exp: this.getCurrentUnixTimeSeconds() + 12 * 60 * 60, // Current time + 12 hours in seconds
            'ta:platform_id': platformId,
            'ta:platform_username': name,
            'ta:discord_id': discordId,
            'ta:discord_name': name,
            'ta:discord_avatar': 'https://i.pinimg.com/originals/47/a6/63/47a6630b73b1c5b2c9714ea64df278c1.jpg'
        };

        try {

            const header = JSON.stringify({ alg: "RS256", typ: "JWT" });
            return KJUR.jws.JWS.sign("RS256", header, JSON.stringify(claims), key.privateKey);
        } catch (error) {
            console.error('Error generating token:', error);
        }

        return '';
    }

    constructor() {
        this._client = new TAClient();
        this._authToken = '';
        this._lastJoinedTournament = '';
        this.mockLoadSong = this.mockLoadSong.bind(this);
    }

    public async connect(serverAddress: string, serverPort: string) {
        this._authToken = await this.authToken();
        this._client.setAuthToken(this._authToken);

        this._client.on('loadSongRequested', this.mockLoadSong);

        return this._client.connect(serverAddress, serverPort);
    }

    public join(tournamentId: string) {
        this._lastJoinedTournament = tournamentId;
        return this._client.joinTournament(tournamentId);
    }

    private async mockLoadSong(params: [string, string, Request_LoadSong]) {
        const self = this._client.stateManager.getUser(this._lastJoinedTournament, this._client.stateManager.getSelfGuid());
        if (self) {
            self.downloadState = User_DownloadStates.Downloaded;
            await this._client.updateUser(this._lastJoinedTournament, self)
            await this._client.sendResponse({
                type: Response_ResponseType.Success,
                respondingToPacketId: params[0],
                details: {
                    oneofKind: "loadSong",
                    loadSong: {
                        levelId: params[2].levelId
                    }
                }
            }, [params[1]])
        }
    }
}
