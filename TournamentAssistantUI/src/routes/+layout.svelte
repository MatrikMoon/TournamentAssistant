<script>
  import "../app.scss";
  import Color from "color";
  import Button, { Label } from "@smui/button";
  import { authToken, client } from "$lib/stores";
  import defaultLogo from "$lib/assets/icon.png";
  import { User_ClientTypes } from "tournament-assistant-client";
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
  root.style.setProperty("--mdc-theme-primary", primaryColor.toString());
  root.style.setProperty("--mdc-theme-primary-shaded", primaryColor.alpha(0.5));
  root.style.setProperty("--background-color", backgroundColor.toString());
  root.style.setProperty("--mdc-theme-surface", backgroundColor.toString());
  root.style.setProperty(
    "--background-color-shaded-1",
    backgroundColor.darken(0.1)
  );
  root.style.setProperty(
    "--background-color-shaded-4",
    backgroundColor.darken(0.4)
  );

  //Text
  root.style.setProperty(
    "--mdc-theme-text-primary-on-background",
    getTextColorForBackground()
  );
  root.style.setProperty(
    "--mdc-theme-text-secondary-on-background",
    getTextColorForBackground().alpha(0.7)
  );
  root.style.setProperty(
    "--mdc-theme-text-hint-on-background",
    getTextColorForBackground().alpha(0.7)
  );

  //Switch
  root.style.setProperty(
    "--mdc-switch-selected-track-color",
    primaryColor.alpha(0.4)
  );
  root.style.setProperty(
    "--mdc-switch-selected-hover-track-color",
    primaryColor.alpha(0.4)
  );
  root.style.setProperty(
    "--mdc-switch-selected-focus-track-color",
    primaryColor.alpha(0.4)
  );
  root.style.setProperty(
    "--mdc-switch-selected-pressed-track-color",
    primaryColor.alpha(0.4)
  );
  root.style.setProperty(
    "--mdc-switch-selected-hover-handle-color",
    primaryColor.alpha(0.7)
  );
  root.style.setProperty(
    "--mdc-switch-selected-focus-handle-color",
    primaryColor
  );
  root.style.setProperty(
    "--mdc-switch-selected-pressed-handle-color",
    primaryColor.alpha(0.7)
  );
  root.style.setProperty(
    "--mdc-switch-selected-pressed-handle-color",
    primaryColor.alpha(0.7)
  );

  //Authorization
  const onLoginClick = () => {
    $client.once("authorizedWithServer", () => {
      //The token is saved in the handler set up in the store, so all we need to do is close it
      $client.disconnect();
    });

    $client.connect(
      "server.tournamentassistant.net",
      "2053",
      "TAUI",
      User_ClientTypes.WebsocketConnection
    );
  };

  //Set auth token if we already have it
  $client.setAuthToken($authToken);
</script>

<main class="container">
  {#if !$authToken}
    <div class="splash">
      <img src={defaultLogo} alt="Splash logo" class="logo" />
      <div>Not connected</div>
      <Button variant="raised" on:click={onLoginClick}>
        <Label>Login</Label>
      </Button>
    </div>
  {:else}
    <slot />
  {/if}
</main>

<style lang="scss">
  .splash {
    text-align: center;
    padding: 1em;
    margin: 0 auto;

    div {
      color: var(--mdc-theme-text-primary-on-background);
      font-size: 2rem;
      font-weight: 100;
      line-height: 1.1;
      margin: 2rem auto;
    }

    .logo {
      height: 16rem;
      width: 16rem;
    }
  }
</style>
