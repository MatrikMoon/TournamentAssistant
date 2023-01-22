import { readable, writable } from 'svelte/store';
import { TAClient } from 'tournament-assistant-client'

export enum ConnectState {
    NotStarted = 0,
    Connected,
    FailedToConnect,
    Disconnected,
};

export const selectedUserGuid = writable('');

export const connectState = writable(ConnectState.NotStarted);
export const connectStateText = writable("Connecting to server");

export const client = readable<TAClient>(undefined, function start(set) {
    const taClient = new TAClient('server.tournamentassistant.net', '2053', 'Admin Panel', 1);

    set(taClient);

    taClient.on("connectedToServer", () => {
        connectState.set(ConnectState.Connected);
        connectStateText.set("Connected");
        console.log("ClientOn: Connected");
    });

    taClient.on("failedToConnectToServer", () => {
        connectState.set(ConnectState.FailedToConnect);
        connectStateText.set("Failed to connect to the server");
        console.log("ClientOn: FailedToConnect");
    });

    taClient.on("disconnectedFromServer", () => {
        connectState.set(ConnectState.Disconnected);
        connectStateText.set("Disconnected");
        console.log("ClientOn: Disconnected");
    });

    return function stop() {
        taClient.disconnect();
    };
});

export interface Log {
    message: string;
    type: 'log' | 'info' | 'warn' | 'error' | 'success';
}

export const log = writable<Log[]>([]);