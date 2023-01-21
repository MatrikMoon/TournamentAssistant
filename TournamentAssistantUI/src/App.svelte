<script lang="ts">
  import { client, ConnectState, connectState } from "./stores";
  import ServerSelection from "./pages/ServerSelection.svelte";
  import Splash from "./pages/Splash.svelte";

  $client.connect();

  //Enable dark mode
  window.document.body.classList.toggle("dark-mode");
</script>

<link rel="stylesheet" href="node_modules/svelte-material-ui/bare.css" />

<main>
  {#if $connectState !== ConnectState.Connected}
    <Splash />
  {:else}
    <ServerSelection />
  {/if}
</main>

<style lang="scss">
  @import "../node_modules/svelte-material-ui/bare.css";

  :global {
    body {
      --mdc-theme-primary: #d60000;
      --mdc-theme-surface: rgba($color: #000000, $alpha: 0.1);
      --mdc-theme-on-surface: #d60000;
      --background-color: #fff;
      background-color: var(--background-color);
      transition: background-color 0.3s;

      &.dark-mode {
        --mdc-theme-primary: #d60000;
        --mdc-theme-on-surface: #fff;
        --mdc-theme-text-primary-on-background: #fff;
        --mdc-theme-text-secondary-on-background: #cacaca;
        --mdc-theme-text-hint-on-background: #cacaca;
        --background-color: #424242;
        transition: background-color 0.3s;
      }
    }
  }

  :root {
    font-family: "Roboto", sans-serif;
  }
</style>
