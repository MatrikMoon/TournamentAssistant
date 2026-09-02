<script lang="ts">
  import { onMount } from "svelte";
  import { invoke } from "@tauri-apps/api/core";
  import { save } from "@tauri-apps/plugin-dialog";
  import { MockFleet, type IdentityConfig, type LogEntry, type MockRuntime, type ScoreAction } from "./protocol";

  const fleet = new MockFleet();
  let clients = $state<MockRuntime[]>([]);
  let logs = $state<LogEntry[]>([]);
  let selectedId = $state("");
  let busy = $state(false);
  let uiError = $state("");
  let logFilter = $state("all");
  let expandedLog = $state<number | null>(null);
  let address = $state("server.tournamentassistant.net");
  let port = $state(8675);
  let certificatePath = $state("files/mock.pfx");
  let certificatePassword = $state("");
  let acceptInvalidCertificate = $state(false);
  let identityCount = $state(1);
  let identities = $state<IdentityConfig[]>([{ platformId: "mock-player-1", username: "Mock Player 1" }]);
  let tournamentPassword = $state("");

  let selected = $derived(clients.find((client) => client.id === selectedId) ?? clients[0]);
  let joinedTournament = $derived(selected?.tournaments.find((tournament) => tournament.guid === selected.joinedTournamentId));
  let filteredLogs = $derived(logs.filter((entry) =>
    (logFilter === "all" || entry.level === logFilter) && (!selected || entry.clientId === selected.id)
  ));

  function refresh() {
    clients = [...fleet.clients];
    logs = [...fleet.logs];
    if (!selectedId && clients[0]) selectedId = clients[0].id;
  }

  function resizeIdentities() {
    const count = Math.max(1, Math.min(16, Number(identityCount) || 1));
    identityCount = count;
    while (identities.length < count) {
      const number = identities.length + 1;
      identities.push({ platformId: `mock-player-${number}`, username: `Mock Player ${number}` });
    }
    identities.splice(count);
  }

  async function perform(action: () => Promise<unknown>) {
    uiError = "";
    try { await action(); }
    catch (error) { uiError = String(error); }
    finally { refresh(); }
  }

  async function connect() {
    busy = true;
    selectedId = "";
    resizeIdentities();
    await perform(() => fleet.connect({
      address, port, certificatePath, certificatePassword, acceptInvalidCertificate,
      identities: identities.map((identity) => ({ ...identity })),
    }));
    busy = false;
  }

  async function saveLogs() {
    const path = await save({ defaultPath: `ta-mock-client-${new Date().toISOString().replaceAll(":", "-")}.log` });
    if (path) await perform(() => invoke("save_logs", { path, contents: fleet.exportLogs() }));
  }

  const scoreButtons: { action: ScoreAction; label: string; className: string }[] = [
    { action: "goodCut", label: "Good cut", className: "good" },
    { action: "miss", label: "Miss", className: "warn" },
    { action: "badCut", label: "Bad cut", className: "warn" },
    { action: "bomb", label: "Hit bomb", className: "danger" },
    { action: "wall", label: "Hit wall", className: "danger" },
  ];

  onMount(() => {
    void fleet.initialize();
    const unsubscribe = fleet.subscribe(refresh);
    refresh();
    return () => { unsubscribe(); fleet.destroy(); };
  });
</script>

<svelte:head><title>TA Mock Client</title></svelte:head>

<main class="shell">
  <header class="topbar">
    <div class="brand"><span class="brand-mark">TA</span><div><h1>Mock Client</h1><p>Raw socket player emulator</p></div></div>
    <div class="top-actions">
      <span class="protocol">Protocol 1.3.1</span>
      <button class="ghost" onclick={saveLogs} disabled={!logs.length}>Save logs</button>
      <button class="danger-outline" onclick={() => perform(() => fleet.disconnectAll())} disabled={!clients.length}>Disconnect all</button>
    </div>
  </header>

  {#if uiError}<div class="global-error"><strong>Error</strong><span>{uiError}</span><button onclick={() => uiError = ""}>×</button></div>{/if}

  <div class="workspace">
    <aside class="sidebar">
      <section class="panel connection-panel">
        <div class="section-heading"><div><span class="eyebrow">Connection</span><h2>Raw TLS server</h2></div><span class="pulse" class:active={clients.some(c => c.status === "connected")}></span></div>
        <label>Host<input bind:value={address} disabled={busy} /></label>
        <div class="form-row"><label>Port<input type="number" bind:value={port} disabled={busy} /></label><label>Clients<input type="number" min="1" max="16" bind:value={identityCount} oninput={resizeIdentities} disabled={busy || clients.some(c => c.status === "connected")} /></label></div>
        <label>Mock certificate (.pfx)<input bind:value={certificatePath} disabled={busy} /></label>
        <label>Certificate password<input type="password" bind:value={certificatePassword} placeholder="Optional" disabled={busy} /></label>
        <label class="check"><input type="checkbox" bind:checked={acceptInvalidCertificate} /><span>Accept invalid server certificate</span></label>

        <div class="identity-list">
          <div class="mini-heading">Player identities</div>
          {#each identities as identity, index}
            <div class="identity">
              <span>{index + 1}</span>
              <input aria-label="Player name" bind:value={identity.username} placeholder="Player name" disabled={clients.some(c => c.status === "connected")} />
              <input aria-label="Platform ID" bind:value={identity.platformId} placeholder="Platform ID" disabled={clients.some(c => c.status === "connected")} />
            </div>
          {/each}
        </div>
        <button class="primary wide" onclick={connect} disabled={busy || !address || !certificatePath}>{busy ? "Connecting…" : "Connect mock fleet"}</button>
      </section>

      {#if clients.length}
        <section class="panel client-list">
          <div class="mini-heading">Clients</div>
          {#each clients as client}
            <button class="client-card" class:selected={selected?.id === client.id} onclick={() => selectedId = client.id}>
              <span class="status-dot {client.status}"></span><span><strong>{client.username}</strong><small>{client.platformId}</small></span><em>{client.status}</em>
            </button>
          {/each}
        </section>
      {/if}
    </aside>

    <section class="content">
      {#if !selected}
        <div class="empty panel"><div class="empty-icon">◇</div><h2>Ready to emulate</h2><p>Configure one or more mock identities, then connect through the same raw TLS socket used by PCVR and standalone players.</p></div>
      {:else}
        <div class="client-summary panel">
          <div><span class="eyebrow">Selected client</span><h2>{selected.username}</h2><p>{selected.statusMessage}</p></div>
          <div class="summary-meta"><span class="status-pill {selected.status}">{selected.status}</span><code>{selected.selfGuid || "Awaiting player GUID"}</code><button class="ghost compact" onclick={() => perform(() => fleet.disconnect(selected.id))}>Disconnect</button></div>
        </div>

        {#if selected.status === "error"}<div class="version-error"><strong>Connection or version mismatch</strong><span>{selected.statusMessage}</span></div>{/if}

        {#if !selected.joinedTournamentId}
          <section class="panel">
            <div class="section-heading"><div><span class="eyebrow">Discovery</span><h2>Tournaments</h2></div><span class="count">{selected.tournaments.length}</span></div>
            <label class="password-field">Tournament password<input type="password" bind:value={tournamentPassword} placeholder="Used when joining" /></label>
            <div class="tournament-grid">
              {#each selected.tournaments as tournament}
                <article class="tournament-card" class:blocked={!tournament.settings?.allowMockClients}>
                  {#if tournament.settings?.tournamentImage}<img src={tournament.settings.tournamentImage} alt="" />{:else}<div class="tournament-placeholder">TA</div>{/if}
                  <div class="tournament-info"><h3>{tournament.settings?.tournamentName || "Unnamed tournament"}</h3><code>{tournament.guid}</code><span class:allowed={tournament.settings?.allowMockClients} class="access-badge">{tournament.settings?.allowMockClients ? "Mock clients allowed" : "Mock clients blocked"}</span></div>
                  <button class="primary" disabled={!tournament.settings?.allowMockClients} onclick={() => perform(() => fleet.joinTournament(selected!.id, tournament.guid, tournamentPassword))}>Join</button>
                </article>
              {:else}<div class="inline-empty">No tournaments were returned by the server.</div>{/each}
            </div>
          </section>
        {:else}
          <div class="two-column">
            <section class="panel">
              <div class="section-heading"><div><span class="eyebrow">Tournament</span><h2>{joinedTournament?.settings?.tournamentName || selected.joinedTournamentId}</h2></div><button class="danger-outline compact" onclick={() => perform(() => fleet.leaveTournament(selected!.id))}>Leave tournament</button></div>
              <div class="match-list">
                {#each joinedTournament?.matches ?? [] as match}
                  <article class="match-card" class:joined={selected.currentMatchId === match.guid}>
                    <div><h3>{match.selectedMap?.gameplayParameters?.beatmap?.name || "Unassigned match"}</h3><p>{match.associatedUsers.length} player{match.associatedUsers.length === 1 ? "" : "s"}</p><code>{match.guid}</code></div>
                    {#if selected.currentMatchId === match.guid}<button class="danger-outline compact" onclick={() => perform(() => fleet.leaveMatch(selected!.id))}>Leave</button>{:else}<button class="ghost compact" disabled={!!selected.currentMatchId} onclick={() => perform(() => fleet.joinMatch(selected!.id, match.guid))}>Join match</button>{/if}
                  </article>
                {:else}<div class="inline-empty">No matches are available yet.</div>{/each}
              </div>
            </section>

            <section class="panel gameplay">
              <div class="section-heading"><div><span class="eyebrow">Gameplay</span><h2>{selected.loadedMap?.name || "Waiting for a map"}</h2></div><span class="live" class:active={selected.score.playing}>LIVE</span></div>
              {#if selected.loadedMap}
                <div class="map-banner">{#if selected.loadedMap.coverUrl}<img src={selected.loadedMap.coverUrl} alt="Map cover" />{/if}<div><code>{selected.loadedMap.hash}</code><p>{Math.floor(selected.loadedMap.durationSeconds / 60)}:{String(selected.loadedMap.durationSeconds % 60).padStart(2, "0")} · cached locally</p></div></div>
                <div class="progress"><span style:width={`${Math.min(100, selected.score.songPosition / selected.loadedMap.durationSeconds * 100)}%`}></span></div>
              {:else}<div class="map-waiting">Load and PlaySong packets will download the BeatSaver map and begin automatic simulation.</div>{/if}
              <div class="score-grid"><div><span>Score</span><strong>{selected.score.score.toLocaleString()}</strong></div><div><span>Combo</span><strong>{selected.score.combo}×</strong></div><div><span>Accuracy</span><strong>{selected.score.maxScore ? (selected.score.score / selected.score.maxScore * 100).toFixed(1) : "0.0"}%</strong></div><div><span>Position</span><strong>{selected.score.songPosition.toFixed(1)}s</strong></div></div>
              <div class="score-actions">{#each scoreButtons as item}<button class={item.className} disabled={!selected.score.playing} onclick={() => perform(() => fleet.scoreAction(selected!.id, item.action))}>{item.label}</button>{/each}</div>
              <button class="ghost wide" onclick={() => perform(() => fleet.backToMenu(selected!.id))}>Back to menu</button>
              {#if selected.modifiers.length}<div class="modifiers"><span>Received modifiers</span><div>{#each selected.modifiers as modifier}<em>{modifier}</em>{/each}</div></div>{/if}
            </section>
          </div>
        {/if}

        <section class="panel logs">
          <div class="section-heading"><div><span class="eyebrow">Telemetry</span><h2>Packet & event log</h2></div><div class="log-actions"><select bind:value={logFilter}><option value="all">All packets</option><option value="packet-in">Incoming</option><option value="packet-out">Outgoing</option><option value="error">Errors</option><option value="info">Events</option></select><button class="ghost compact" onclick={() => fleet.clearLogs()}>Clear</button><button class="ghost compact" onclick={saveLogs}>Save</button></div></div>
          <div class="log-table">
            {#each filteredLogs.slice().reverse().slice(0, 250) as entry, index}
              <button class="log-row {entry.level}" onclick={() => expandedLog = expandedLog === index ? null : index}><time>{entry.time.slice(11, 23)}</time><span class="direction">{entry.level}</span><strong>{entry.summary}</strong>{#if entry.detail}<em>⌄</em>{/if}</button>
              {#if expandedLog === index && entry.detail}<pre>{entry.detail}</pre>{/if}
            {:else}<div class="inline-empty">Packets and local events will appear here.</div>{/each}
          </div>
        </section>
      {/if}
    </section>
  </div>
</main>
