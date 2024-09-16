import { readable, writable } from "svelte/store";
import { ConnectState, TAService } from "./services/taService";

export const masterConnectState = writable(ConnectState.NotStarted);
export const masterConnectStateText = writable("Connection not started");

export const connectState = writable(ConnectState.NotStarted);
export const connectStateText = writable("Connection not started");

export const taService = readable<TAService>(undefined, function start(set) {
  const taService = new TAService();

  taService.on("masterConnectionStateChanged", (event) => {
    masterConnectState.set(event.state);
    masterConnectStateText.set(event.text);
  });

  taService.on("connectionStateChanged", (event) => {
    connectState.set(event.state);
    connectStateText.set(event.text);
  });

  set(taService);

  return function stop() {
    console.log("Service STOP called");
    taService.client.disconnect();
    taService.masterClient.disconnect();
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
