<script lang="ts">
  import "../app.scss";
  import Color from "color";
  import {
    authToken,
    log,
    masterConnectState,
    masterConnectStateText,
    taService,
  } from "$lib/stores";
  import Splash from "$lib/components/Splash.svelte";
  import { ConnectState } from "$lib/services/taService";
  import UpdateRequiredDialog from "$lib/dialogs/UpdateRequiredDialog.svelte";
  import { invoke } from "@tauri-apps/api";
  import { onMount } from "svelte";

  const root = window.document.querySelector(":root");

  const primaryColor = new Color("#d60000");
  const backgroundColor = new Color("#424242");

  function getTextColorForBackground() {
    const perspectiveLuminence =
      1 -
      (0.299 * backgroundColor.red() +
        0.587 * backgroundColor.green() +
        0.114 * backgroundColor.blue()) /
        255;

    return perspectiveLuminence < 0.5 ? new Color("black") : new Color("white");
  }

  //Base
  const _root = root as any;
  _root.style.setProperty("--mdc-theme-primary", primaryColor.toString());
  _root.style.setProperty(
    "--mdc-theme-secondary",
    new Color("#878787").toString(),
  ); // A light gray, used to mimick a "Disabled" look for buttons which for some reason aren't showing as disabled properly
  _root.style.setProperty("--background-color", backgroundColor.toString());
  _root.style.setProperty("--mdc-theme-surface", backgroundColor.toString());

  //Text
  _root.style.setProperty(
    "--mdc-theme-text-primary-on-background",
    getTextColorForBackground(),
  );
  _root.style.setProperty(
    "--mdc-theme-text-secondary-on-background",
    getTextColorForBackground().alpha(0.7),
  );
  _root.style.setProperty(
    "--mdc-theme-text-hint-on-background",
    getTextColorForBackground().alpha(0.7),
  );

  //Switch
  _root.style.setProperty(
    "--mdc-switch-selected-track-color",
    primaryColor.alpha(0.4),
  );
  _root.style.setProperty(
    "--mdc-switch-selected-hover-track-color",
    primaryColor.alpha(0.4),
  );
  _root.style.setProperty(
    "--mdc-switch-selected-focus-track-color",
    primaryColor.alpha(0.4),
  );
  _root.style.setProperty(
    "--mdc-switch-selected-pressed-track-color",
    primaryColor.alpha(0.4),
  );
  _root.style.setProperty(
    "--mdc-switch-selected-hover-handle-color",
    primaryColor.alpha(0.7),
  );
  _root.style.setProperty(
    "--mdc-switch-selected-focus-handle-color",
    primaryColor,
  );
  _root.style.setProperty(
    "--mdc-switch-selected-pressed-handle-color",
    primaryColor.alpha(0.7),
  );
  _root.style.setProperty(
    "--mdc-switch-selected-pressed-handle-color",
    primaryColor.alpha(0.7),
  );

  //Console override
  // const oldConsole = (window as any).console;

  // (window as any).console = {
  //   log: function (logParameter: any) {
  //     log.update((x) => [{ message: logParameter, type: "log" }, ...x]);
  //     oldConsole.log(logParameter);
  //   },

  //   debug: function (logParameter: any) {
  //     log.update((x) => [{ message: logParameter, type: "debug" }, ...x]);
  //     oldConsole.info(logParameter);
  //   },

  //   info: function (logParameter: any) {
  //     log.update((x) => [{ message: logParameter, type: "info" }, ...x]);
  //     oldConsole.info(logParameter);
  //   },

  //   warn: function (logParameter: any) {
  //     log.update((x) => [{ message: logParameter, type: "warn" }, ...x]);
  //     oldConsole.warn(logParameter);
  //   },

  //   error: function (logParameter: any) {
  //     log.update((x) => [{ message: logParameter, type: "error" }, ...x]);
  //     oldConsole.error(logParameter);
  //   },

  //   success: function (logParameter: any) {
  //     log.update((x) => [{ message: logParameter, type: "success" }, ...x]);
  //     oldConsole.log(logParameter);
  //   },
  // };

  let updateRequired = false;

  //Set auth token if we already have it
  $taService.setAuthToken($authToken);

  // Kick off the master connection so we get past the splash screen
  $taService.connectToMaster();

  $taService.on("updateRequired", () => {
    updateRequired = true;
  });

  onMount(async () => {
    // On program start, attempt to remove TAUpdater.exe
    await invoke("delete_updater");
  });
</script>

<main>
  {#if updateRequired}
    <UpdateRequiredDialog
      open={updateRequired}
      onDownloadClick={async () => await invoke("update")}
    />
  {:else if $masterConnectState !== ConnectState.Connected}
    <Splash
      connectState={$masterConnectState}
      connectStateText={$masterConnectStateText}
    />
  {:else}
    <slot />
  {/if}
</main>
