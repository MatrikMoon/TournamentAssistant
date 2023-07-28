import { readable, writable } from "svelte/store";
import { TAClient } from "tournament-assistant-client";

export enum ConnectState {
  NotStarted = 0,
  Connected,
  Connecting,
  FailedToConnect,
  Disconnected,
}

export const masterServerAddress = writable("server.tournamentassistant.net");
export const masterServerPort = writable("2053");

export const masterConnectState = writable(ConnectState.NotStarted);
export const masterConnectStateText = writable("Connection not started");

export const masterClient = readable<TAClient>(undefined, function start(set) {
  const taClient = new TAClient();

  set(taClient);

  taClient.on("connectedToServer", () => {
    masterConnectState.set(ConnectState.Connected);
    masterConnectStateText.set("Connected");
    console.log("MasterClientOn: Connected");
  });

  taClient.on("connectingToServer", () => {
    masterConnectState.set(ConnectState.Connecting);
    masterConnectStateText.set("Connecting to server");
    console.log("MasterClientOn: Connecting");
  });

  taClient.on("failedToConnectToServer", () => {
    masterConnectState.set(ConnectState.FailedToConnect);
    masterConnectStateText.set("Failed to connect to the server");
    console.log("MasterClientOn: FailedToConnect");
  });

  taClient.on("disconnectedFromServer", () => {
    masterConnectState.set(ConnectState.Disconnected);
    masterConnectStateText.set("Disconnected");
    console.log("MasterClientOn: Disconnected");
  });

  taClient.on("authorizationRequestedFromServer", (url) => {
    authToken.set("");
    window.open(url, "_blank", "width=500, height=800");
  });

  taClient.on("authorizedWithServer", (token) => {
    authToken.set(token);
    taClient.setAuthToken(token);
    console.log(`Master Authorized: ${token}`);
  });

  return function stop() {
    console.log("Master STOP called");
    taClient.disconnect();
  };
});

export const connectState = writable(ConnectState.NotStarted);
export const connectStateText = writable("Connection not started");

export const client = readable<TAClient>(undefined, function start(set) {
  const taClient = new TAClient();

  set(taClient);

  taClient.on("connectedToServer", () => {
    connectState.set(ConnectState.Connected);
    connectStateText.set("Connected");
    console.log("ClientOn: Connected");
  });

  taClient.on("connectingToServer", () => {
    connectState.set(ConnectState.Connecting);
    connectStateText.set("Connecting to server");
    console.log("ClientOn: Connecting");
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

  taClient.on("authorizationRequestedFromServer", (url) => {
    authToken.set("");
    window.open(url, "_blank", "width=500, height=800");
  });

  taClient.on("authorizedWithServer", (token) => {
    authToken.set(token);
    taClient.setAuthToken(token);
    console.log(`Authorized: ${token}`);
  });

  return function stop() {
    console.log("STOP called");
    taClient.disconnect();
  };
});

export interface Log {
  message: string;
  type: "log" | "debug" | "info" | "warn" | "error" | "success";
}

export const log = writable<Log[]>([]);

export const authToken = writable<string>(
  window.localStorage.getItem("authToken") ?? ""
);

authToken.subscribe((value) => {
  window.localStorage.setItem("authToken", value);
});
