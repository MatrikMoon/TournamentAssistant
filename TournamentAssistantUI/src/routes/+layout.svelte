<script lang="ts">
  import "../app.scss";
  import Color from "color";
  import {
    authToken,
    masterConnectState,
    masterConnectStateText,
    taService,
  } from "$lib/stores";
  import Splash from "$lib/components/Splash.svelte";
  import { ConnectState } from "$lib/services/taService";

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
    "--mdc-theme-primary-shaded",
    primaryColor.alpha(0.5)
  );
  _root.style.setProperty("--background-color", backgroundColor.toString());
  _root.style.setProperty("--mdc-theme-surface", backgroundColor.toString());
  _root.style.setProperty(
    "--background-color-shaded-1",
    backgroundColor.darken(0.1)
  );
  _root.style.setProperty(
    "--background-color-shaded-4",
    backgroundColor.darken(0.4)
  );

  //Text
  _root.style.setProperty(
    "--mdc-theme-text-primary-on-background",
    getTextColorForBackground()
  );
  _root.style.setProperty(
    "--mdc-theme-text-secondary-on-background",
    getTextColorForBackground().alpha(0.7)
  );
  _root.style.setProperty(
    "--mdc-theme-text-hint-on-background",
    getTextColorForBackground().alpha(0.7)
  );

  //Switch
  _root.style.setProperty(
    "--mdc-switch-selected-track-color",
    primaryColor.alpha(0.4)
  );
  _root.style.setProperty(
    "--mdc-switch-selected-hover-track-color",
    primaryColor.alpha(0.4)
  );
  _root.style.setProperty(
    "--mdc-switch-selected-focus-track-color",
    primaryColor.alpha(0.4)
  );
  _root.style.setProperty(
    "--mdc-switch-selected-pressed-track-color",
    primaryColor.alpha(0.4)
  );
  _root.style.setProperty(
    "--mdc-switch-selected-hover-handle-color",
    primaryColor.alpha(0.7)
  );
  _root.style.setProperty(
    "--mdc-switch-selected-focus-handle-color",
    primaryColor
  );
  _root.style.setProperty(
    "--mdc-switch-selected-pressed-handle-color",
    primaryColor.alpha(0.7)
  );
  _root.style.setProperty(
    "--mdc-switch-selected-pressed-handle-color",
    primaryColor.alpha(0.7)
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

  //Set auth token if we already have it
  $taService.setAuthToken($authToken);

  // Do *something* so the connection kicks off
  $taService.connectToMaster();

  console.log(window.location);
</script>

<main class="container">
  {#if $masterConnectState !== ConnectState.Connected}
    <Splash
      connectState={$masterConnectState}
      connectStateText={$masterConnectStateText}
    />
  {:else}
    <!-- <TopAppBar variant="static" color={"primary"}>
      <Row>
        <Section align="end" toolbar>
          <IconButton class="material-icons" aria-label="Account">
            account_circle
          </IconButton>
        </Section>
      </Row>
    </TopAppBar> -->
    <slot />
  {/if}
</main>
