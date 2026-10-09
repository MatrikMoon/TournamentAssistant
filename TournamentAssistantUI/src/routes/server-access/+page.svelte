<script lang="ts">
  import { onMount } from "svelte";
  import { goto } from "$app/navigation";
  import { page } from "$app/stores";
  import TADrawer from "$lib/components/TADrawer.svelte";
  import { taService } from "$lib/stores";
  import {
    GlobalConfiguration,
    Response_ResponseType,
    type Tournament,
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
  let tournaments: Tournament[] = [];
  let linkDrafts: Record<string, {
    linked: boolean;
    beatKhanaGuid: string;
    saving: boolean;
    message: string;
    error: string;
  }> = {};

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

    tournaments = await $taService.getServerTournaments(serverAddress, serverPort);
    linkDrafts = Object.fromEntries(tournaments.map((tournament) => [
      tournament.guid,
      {
        linked: tournament.settings?.isBkTournament ?? false,
        beatKhanaGuid: tournament.settings?.beatKhanaTournamentGuid ?? "",
        saving: false,
        message: "",
        error: "",
      },
    ]));
  }

  function updateLinkDraft(tournamentId: string, update: Partial<typeof linkDrafts[string]>) {
    linkDrafts = {
      ...linkDrafts,
      [tournamentId]: { ...linkDrafts[tournamentId], ...update },
    };
  }

  async function saveLink(tournamentId: string) {
    const draft = linkDrafts[tournamentId];
    if (!draft) return;

    updateLinkDraft(tournamentId, { saving: true, message: "", error: "" });
    try {
      const response = await $taService.setBKTournamentLink(
        serverAddress,
        serverPort,
        tournamentId,
        draft.linked,
        draft.linked ? draft.beatKhanaGuid.trim() : ""
      );
      if (response.details.oneofKind !== "setBkTournamentLink" || response.type !== Response_ResponseType.Success) {
        const responseMessage = response.details.oneofKind === "setBkTournamentLink"
          ? response.details.setBkTournamentLink.message
          : "The server rejected the BeatKhana link update.";
        updateLinkDraft(tournamentId, { error: responseMessage });
        return;
      }

      const result = response.details.setBkTournamentLink;
      const updated = result.tournament;
      if (updated) {
        tournaments = tournaments.map((tournament) =>
          tournament.guid === tournamentId ? updated : tournament
        );
        updateLinkDraft(tournamentId, {
          linked: updated.settings?.isBkTournament ?? false,
          beatKhanaGuid: updated.settings?.beatKhanaTournamentGuid ?? "",
          message: result.message,
        });
      }
    } catch (reason) {
      updateLinkDraft(tournamentId, {
        error: reason instanceof Error ? reason.message : "Could not update the BeatKhana link.",
      });
    } finally {
      updateLinkDraft(tournamentId, { saving: false });
    }
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

      <section class="bk-links" aria-labelledby="bk-links-heading">
        <div class="section-heading">
          <div>
            <p class="eyebrow">Full-access administrators</p>
            <h2 id="bk-links-heading">BeatKhana tournament links</h2>
          </div>
          <p>Only linked tournaments accept the BeatKhana authoritative token.</p>
        </div>

        <div class="tournament-grid">
          {#each tournaments as tournament (tournament.guid)}
            <article class:linked={linkDrafts[tournament.guid]?.linked} class="tournament-card">
              <div class="card-heading">
                <div class="tournament-mark">
                  {(tournament.settings?.tournamentName || "T").slice(0, 1).toUpperCase()}
                </div>
                <div>
                  <h3>{tournament.settings?.tournamentName || "Unnamed tournament"}</h3>
                  <code>{tournament.guid}</code>
                </div>
                <label class="link-toggle">
                  <input
                    type="checkbox"
                    checked={linkDrafts[tournament.guid]?.linked}
                    on:change={(event) => updateLinkDraft(tournament.guid, {
                      linked: event.currentTarget.checked,
                      message: "",
                      error: "",
                    })}
                  />
                  <span>{linkDrafts[tournament.guid]?.linked ? "Linked" : "Not linked"}</span>
                </label>
              </div>

              <label class="guid-field">
                BeatKhana tournament GUID
                <input
                  type="text"
                  value={linkDrafts[tournament.guid]?.beatKhanaGuid}
                  disabled={!linkDrafts[tournament.guid]?.linked}
                  placeholder="00000000-0000-0000-0000-000000000000"
                  on:input={(event) => updateLinkDraft(tournament.guid, {
                    beatKhanaGuid: event.currentTarget.value,
                    message: "",
                    error: "",
                  })}
                />
              </label>

              <div class="card-actions">
                <div class="status" aria-live="polite">
                  {#if linkDrafts[tournament.guid]?.error}
                    <span class="error">{linkDrafts[tournament.guid].error}</span>
                  {:else if linkDrafts[tournament.guid]?.message}
                    <span class="success">{linkDrafts[tournament.guid].message}</span>
                  {/if}
                </div>
                <button
                  disabled={linkDrafts[tournament.guid]?.saving || (linkDrafts[tournament.guid]?.linked && !linkDrafts[tournament.guid]?.beatKhanaGuid.trim())}
                  on:click={() => saveLink(tournament.guid)}
                >
                  {linkDrafts[tournament.guid]?.saving ? "Saving…" : "Save link"}
                </button>
              </div>
            </article>
          {/each}
        </div>
      </section>
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
  .bk-links { margin-top: 42px; }
  .section-heading { display: flex; justify-content: space-between; gap: 24px; align-items: end; margin-bottom: 18px; }
  .section-heading h2 { margin: 2px 0 0; font-size: 1.65rem; }
  .section-heading > p { max-width: 460px; margin: 0; opacity: 0.72; text-align: right; }
  .eyebrow { margin: 0; color: var(--mdc-theme-primary); font-size: 0.75rem; font-weight: 700; letter-spacing: 0.1em; text-transform: uppercase; }
  .tournament-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(330px, 1fr)); gap: 16px; }
  .tournament-card { position: relative; overflow: hidden; padding: 18px; border: 1px solid rgba(255, 255, 255, 0.12); border-radius: 14px; background: linear-gradient(145deg, rgba(255, 255, 255, 0.075), rgba(0, 0, 0, 0.16)); box-shadow: 0 8px 24px rgba(0, 0, 0, 0.16); }
  .tournament-card.linked { border-color: rgba(126, 87, 194, 0.75); box-shadow: 0 8px 30px rgba(103, 58, 183, 0.2); }
  .tournament-card::before { content: ""; position: absolute; inset: 0 auto 0 0; width: 4px; background: transparent; }
  .tournament-card.linked::before { background: var(--mdc-theme-primary); }
  .card-heading { display: grid; grid-template-columns: 46px minmax(0, 1fr) auto; gap: 12px; align-items: center; margin-bottom: 18px; }
  .tournament-mark { display: grid; place-items: center; width: 46px; height: 46px; border-radius: 12px; background: rgba(103, 58, 183, 0.2); color: #d1c4e9; font-size: 1.3rem; font-weight: 800; }
  .card-heading h3 { margin: 0 0 4px; font-size: 1.05rem; }
  .card-heading code { display: block; overflow: hidden; opacity: 0.6; font-size: 0.7rem; text-overflow: ellipsis; }
  .link-toggle { display: flex; gap: 7px; align-items: center; font-size: 0.78rem; font-weight: 600; }
  .guid-field input { box-sizing: border-box; width: 100%; padding: 11px 12px; border: 1px solid rgba(255, 255, 255, 0.16); border-radius: 7px; background: rgba(0, 0, 0, 0.18); color: inherit; font-family: monospace; }
  .guid-field input:focus { border-color: var(--mdc-theme-primary); outline: none; }
  .guid-field input:disabled { opacity: 0.45; }
  .card-actions { display: flex; justify-content: space-between; gap: 12px; align-items: center; margin-top: 14px; }
  .status { min-height: 1.2em; font-size: 0.78rem; }
  .card-actions button { white-space: nowrap; }
  @media (max-width: 700px) {
    .endpoint { grid-template-columns: minmax(150px, 1fr) repeat(3, 60px); font-size: 0.85rem; }
    .section-heading { display: block; }
    .section-heading > p { margin-top: 8px; text-align: left; }
    .tournament-grid { grid-template-columns: 1fr; }
    .card-heading { grid-template-columns: 42px minmax(0, 1fr); }
    .link-toggle { grid-column: 1 / -1; }
  }
</style>
