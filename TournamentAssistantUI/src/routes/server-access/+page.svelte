<script lang="ts">
  import { onMount } from "svelte";
  import { goto } from "$app/navigation";
  import { page } from "$app/stores";
  import TADrawer from "$lib/components/TADrawer.svelte";
  import { taService } from "$lib/stores";
  import {
    GlobalConfiguration,
    masterAddress,
    masterPort,
  } from "tournament-assistant-client";

  const serverAddress = $page.url.searchParams.get("address") ?? masterAddress;
  const serverPort = $page.url.searchParams.get("port") ?? masterPort.toString();
  let configuration: GlobalConfiguration | undefined;
  let fullAccessIds = "";
  let endpointManagerIds = "";
  let error = "";
  let message = "";
  let saving = false;

  const items = [{
    name: "Server Access",
    isActive: true,
    onClick: () => false,
  }];

  function parseIds(value: string) {
    return value.split(/[\s,]+/).map((x) => x.trim()).filter(Boolean);
  }

  async function load() {
    error = "";
    const response = await $taService.getGlobalConfiguration(serverAddress, serverPort);
    if (response.details.oneofKind !== "getGlobalConfiguration") {
      error = "You do not have access to server configuration.";
      return;
    }
    configuration = response.details.getGlobalConfiguration.configuration;
    fullAccessIds = configuration?.fullAccessDiscordIds.join("\n") ?? "";
    endpointManagerIds = configuration?.endpointManagerDiscordIds.join("\n") ?? "";
  }

  async function save() {
    if (!configuration) return;
    saving = true;
    error = "";
    message = "";
    configuration.fullAccessDiscordIds = parseIds(fullAccessIds);
    configuration.endpointManagerDiscordIds = parseIds(endpointManagerIds);
    try {
      const response = await $taService.updateGlobalConfiguration(serverAddress, serverPort, configuration);
      if (response.details.oneofKind !== "updateGlobalConfiguration") {
        error = "The server rejected the configuration update.";
        return;
      }
      configuration = response.details.updateGlobalConfiguration.configuration;
      message = response.details.updateGlobalConfiguration.message;
    } catch (reason) {
      error = reason instanceof Error ? reason.message : "Could not update configuration.";
    } finally {
      saving = false;
    }
  }

  onMount(async () => {
    try {
      await load();
    } catch (reason) {
      error = reason instanceof Error ? reason.message : "Could not load configuration.";
    }
  });
</script>

<TADrawer onHomeClicked={() => goto("/")} {items}>
  <section class="configuration">
    <h1>Server access control</h1>
    {#if error}<p class="error">{error}</p>{/if}
    {#if message}<p class="success">{message}</p>{/if}

    {#if configuration}
      <div class="administrators">
        <label>
          Full-access Discord IDs
          <textarea rows="5" bind:value={fullAccessIds} />
        </label>
        <label>
          Endpoint-manager Discord IDs
          <textarea rows="5" bind:value={endpointManagerIds} />
        </label>
      </div>

      <div class="endpoint-list">
        <div class="endpoint header">
          <span>Handler</span><span>WebSocket</span><span>REST</span><span>Player</span>
        </div>
        {#each configuration.endpoints as endpoint}
          <div class:core={endpoint.isCore} class="endpoint">
            <div>
              <strong>{endpoint.displayName}</strong>
              {#if endpoint.route}<small>{endpoint.route}</small>{/if}
              {#if endpoint.isCore}<small>Core handler</small>{/if}
            </div>
            <input aria-label="WebSocket enabled" type="checkbox" disabled={endpoint.isCore || !endpoint.supportsWebsocket} bind:checked={endpoint.websocketEnabled} />
            <input aria-label="REST enabled" type="checkbox" disabled={endpoint.isCore || !endpoint.supportsRest} bind:checked={endpoint.restEnabled} />
            <input aria-label="Player enabled" type="checkbox" disabled={endpoint.isCore || !endpoint.supportsPlayer} bind:checked={endpoint.playerEnabled} />
          </div>
        {/each}
      </div>

      <button disabled={saving} on:click={save}>{saving ? "Saving…" : "Save changes"}</button>
    {/if}
  </section>
</TADrawer>

<style lang="scss">
  .configuration { color: var(--mdc-theme-text-primary-on-background); max-width: 1100px; margin: auto; }
  .administrators { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; margin-bottom: 24px; }
  label { display: grid; gap: 8px; }
  textarea { padding: 10px; resize: vertical; }
  .endpoint-list { background: rgba(0, 0, 0, 0.12); border-radius: 8px; overflow: hidden; margin-bottom: 18px; }
  .endpoint { display: grid; grid-template-columns: minmax(260px, 1fr) 100px 100px 100px; gap: 8px; align-items: center; padding: 10px 14px; border-bottom: 1px solid rgba(255, 255, 255, 0.12); }
  .endpoint > input { justify-self: center; }
  .endpoint small { display: block; opacity: 0.7; overflow-wrap: anywhere; }
  .header { font-weight: bold; }
  .core { opacity: 0.75; }
  button { padding: 10px 18px; }
  .error { color: #ff8a80; }
  .success { color: #9ccc65; }
  @media (max-width: 700px) {
    .endpoint { grid-template-columns: minmax(150px, 1fr) repeat(3, 60px); font-size: 0.85rem; }
  }
</style>
