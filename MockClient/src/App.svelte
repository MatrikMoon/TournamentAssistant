<script lang="ts">
  import { onMount } from "svelte";
  import { invoke } from "@tauri-apps/api/core";
  import { save } from "@tauri-apps/plugin-dialog";
  import { MockClients, mockClientVersion, type IdentityConfig, type LogEntry, type MockRuntime, type ScoreAction } from "./protocol";

  const manager = new MockClients();
  let clients = $state<MockRuntime[]>([]);
  let logs = $state<LogEntry[]>([]);
  let selectedId = $state("");
  let busy = $state(false);
  let uiError = $state("");
  let logFilter = $state("all");
  let expandedLog = $state<number | null>(null);
  let address = $state("server.tournamentassistant.net");
  let port = $state(8675);
  let acceptInvalidCertificate = $state(false);
  let identities = $state<IdentityConfig[]>([{ platformId: "", username: "Mock Player 1", modList: ["TournamentAssistant"] }]);
  let setupModDrafts = $state<string[]>([""]);
  let runtimeModDrafts = $state<Record<string, string>>({});

  let controllingAll = $derived(selectedId === "all");
  let selected = $derived(clients.find((client) => client.id === selectedId) ?? clients[0]);
  let targets = $derived(controllingAll ? clients.filter(client => client.status === "connected") : selected ? [selected] : []);
  let joinedTournament = $derived(selected?.tournaments.find((tournament) => tournament.guid === selected.joinedTournamentId));
  let filteredLogs = $derived(logs.filter((entry) =>
    (logFilter === "all" || entry.level === logFilter) && (controllingAll || !selected || entry.clientId === selected.id)
  ));

  function refresh() {
    clients = [...manager.clients];
    logs = [...manager.logs];
    if (!selectedId && clients[0]) selectedId = clients[0].id;
  }

  function addIdentity() {
    if (identities.length >= 16) return;
    const number = identities.length + 1;
    identities.push({ platformId: "", username: `Mock Player ${number}`, modList: ["TournamentAssistant"] });
    setupModDrafts.push("");
  }

  function removeIdentity(index: number) {
    identities.splice(index, 1);
    setupModDrafts.splice(index, 1);
  }

  function addSetupMod(index: number) {
    const mod = setupModDrafts[index]?.trim();
    if (!mod || identities[index].modList.includes(mod)) return;
    identities[index].modList.push(mod);
    setupModDrafts[index] = "";
  }

  function removeSetupMod(index: number, mod: string) {
    identities[index].modList = identities[index].modList.filter(item => item !== mod);
  }

  async function addRuntimeMod(client: MockRuntime) {
    const mod = runtimeModDrafts[client.id]?.trim();
    if (!mod) return;
    runtimeModDrafts[client.id] = "";
    await perform(() => manager.setModList(client.id, [...client.modList, mod]));
  }

  function removeRuntimeMod(client: MockRuntime, mod: string) {
    return perform(() => manager.setModList(client.id, client.modList.filter(item => item !== mod)));
  }

  async function perform(action: () => Promise<unknown>) {
    uiError = "";
    try { await action(); }
    catch (error) { uiError = String(error); }
    finally { refresh(); }
  }

  function performForTargets(action: (client: MockRuntime) => Promise<unknown>) {
    return perform(() => Promise.all(targets.map(action)));
  }

  const difficultyName = (difficulty?: number) => ["Easy", "Normal", "Hard", "Expert", "Expert+"][difficulty ?? -1] ?? `Difficulty ${difficulty}`;
  const duration = (seconds: number) => `${Math.floor(seconds / 60)}:${String(Math.round(seconds % 60)).padStart(2, "0")}`;
  const packetJson = (value: unknown) => JSON.stringify(value, (_, item) => typeof item === "bigint" ? item.toString() : item, 2);

  async function connect() {
    busy = true;
    selectedId = "";
    await perform(() => manager.connect({
      address, port, acceptInvalidCertificate,
      identities: identities.map((identity) => ({ ...identity, modList: [...identity.modList] })),
    }));
    busy = false;
  }

  async function saveLogs() {
    const path = await save({ defaultPath: `ta-mock-client-${new Date().toISOString().replaceAll(":", "-")}.log` });
    if (path) await perform(() => invoke("save_logs", { path, contents: manager.exportLogs() }));
  }

  const scoreButtons: { action: ScoreAction; label: string; className: string }[] = [
    { action: "goodCut", label: "Good cut", className: "good" },
    { action: "miss", label: "Miss", className: "warn" },
    { action: "badCut", label: "Bad cut", className: "warn" },
    { action: "bomb", label: "Hit bomb", className: "danger" },
    { action: "wall", label: "Hit wall", className: "danger" },
  ];

  onMount(() => {
    void manager.initialize();
    const unsubscribe = manager.subscribe(refresh);
    refresh();
    return () => { unsubscribe(); manager.destroy(); };
  });
</script>

<svelte:head><title>TA Mock Client</title></svelte:head>

<main class="shell">
  <header class="topbar">
    <div class="brand"><span class="brand-mark">TA</span><div><h1>Mock Client</h1><p>Player test utility</p></div></div>
    <div class="top-actions">
      <span class="protocol">Protocol {mockClientVersion}</span>
      <button class="ghost" onclick={saveLogs} disabled={!logs.length}>Save logs</button>
      <button class="danger-outline" onclick={() => perform(() => manager.disconnectAll())} disabled={!clients.length}>Disconnect all</button>
    </div>
  </header>

  {#if uiError}<div class="global-error"><strong>Error</strong><span>{uiError}</span><button onclick={() => uiError = ""}>×</button></div>{/if}

  <div class="workspace">
    <aside class="sidebar">
      <section class="panel connection-panel">
        <div class="section-heading"><h2>Connection</h2><span class="pulse" class:active={clients.some(c => c.status === "connected")}></span></div>
        <label>Host<input bind:value={address} disabled={busy} /></label>
        <label>Port<input type="number" bind:value={port} disabled={busy} /></label>
        <label class="check"><input type="checkbox" bind:checked={acceptInvalidCertificate} /><span>Accept invalid server certificate</span></label>

        <div class="identity-list">
          <div class="identity-heading"><h3 class="list-title">Mock clients</h3><button class="add-client" aria-label="Add mock client" title="Add mock client" onclick={addIdentity} disabled={identities.length >= 16 || clients.some(c => c.status === "connected")}><span>+</span> Add client</button></div>
          {#each identities as identity, index}
            <div class="identity">
              <span>{index + 1}</span>
              <div class="identity-fields">
                <input aria-label="Player name" bind:value={identity.username} placeholder="Player name" disabled={clients.some(c => c.status === "connected")} />
                <input aria-label="Client ID" bind:value={identity.platformId} placeholder="76561198000000000" disabled={clients.some(c => c.status === "connected")} />
                <div class="mod-editor setup-mods">
                  <div class="mod-chips">{#each identity.modList as mod}<button title={`Remove ${mod}`} onclick={() => removeSetupMod(index, mod)} disabled={clients.some(c => c.status === "connected")}>{mod}<span>×</span></button>{/each}</div>
                  <div class="mod-entry"><input aria-label="Add mod" bind:value={setupModDrafts[index]} placeholder="Add mod name" onkeydown={(event) => event.key === "Enter" && addSetupMod(index)} disabled={clients.some(c => c.status === "connected")} /><button onclick={() => addSetupMod(index)} disabled={!setupModDrafts[index]?.trim() || clients.some(c => c.status === "connected")}>+</button></div>
                </div>
              </div>
              <button class="remove-client" aria-label={`Remove ${identity.username}`} title="Remove client" onclick={() => removeIdentity(index)} disabled={clients.some(c => c.status === "connected")}><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 7h16M9 7V4h6v3m-8 0 1 13h8l1-13M10 11v5m4-5v5" /></svg></button>
            </div>
          {/each}
        </div>
        <button class="primary wide" onclick={connect} disabled={busy || !address || identities.length === 0 || identities.some(identity => !identity.platformId.trim() || !identity.username.trim())}>{busy ? "Connecting…" : `Connect ${identities.length} client${identities.length === 1 ? "" : "s"}`}</button>
      </section>

      {#if clients.length}
        <section class="panel client-list">
          <h3 class="list-title">Clients</h3>
          {#if clients.length > 1}
            <button class="client-card all-clients" class:selected={controllingAll} onclick={() => selectedId = "all"}>
              <span class="status-dot connected"></span><span><strong>All mock clients</strong><small>{clients.length} connected identities</small></span><em>group</em>
            </button>
          {/if}
          {#each clients as client}
            <button class="client-card" class:selected={!controllingAll && selected?.id === client.id} onclick={() => selectedId = client.id}>
              <span class="status-dot {client.status}"></span><span><strong>{client.username}</strong><small>{client.platformId}</small></span><em>{client.status}</em>
            </button>
          {/each}
        </section>
      {/if}
    </aside>

    <section class="content">
      {#if !selected}
        <div class="empty panel"><h2>No client connected</h2><p>Set the player identities and connect to begin testing.</p></div>
      {:else}
        <div class="client-summary panel">
          <div><h2>{controllingAll ? "All mock clients" : selected.username}</h2><p>{controllingAll ? `Controlling ${targets.length} clients together` : selected.statusMessage}</p></div>
          <div class="summary-meta"><span class="status-pill {selected.status}">{controllingAll ? `${targets.filter(client => client.status === "connected").length}/${targets.length} connected` : selected.status}</span>{#if !controllingAll}<code>{selected.selfGuid || "Awaiting player GUID"}</code>{/if}<button class="ghost compact" onclick={() => controllingAll ? perform(() => manager.disconnectAll()) : perform(() => manager.disconnect(selected.id))}>{controllingAll ? "Disconnect all" : "Disconnect"}</button></div>
        </div>

        {#each targets.filter(client => client.status === "error") as failedClient}
          <div class="version-error"><strong>{failedClient.username}</strong><span>{failedClient.statusMessage}</span></div>
        {/each}

        {#if targets.some(client => client.status === "connected")}
        <section class="panel runtime-config">
          <div class="section-heading"><h2>Client status and mods</h2><span class="count">{targets.length}</span></div>
          <div class="runtime-client-list">
            {#each targets as client}
              <div class="runtime-client">
                <div class="runtime-state"><strong>{client.username}</strong><span class="status-dot {client.status}"></span><small>{client.activity}</small></div>
                <div class="mod-editor"><div class="mod-chips">{#each client.modList as mod}<button title={`Remove ${mod}`} onclick={() => removeRuntimeMod(client, mod)}>{mod}<span>×</span></button>{:else}<em>No mods reported</em>{/each}</div><div class="mod-entry"><input aria-label={`Add mod for ${client.username}`} bind:value={runtimeModDrafts[client.id]} placeholder="Add mod name" onkeydown={(event) => event.key === "Enter" && addRuntimeMod(client)} /><button onclick={() => addRuntimeMod(client)} disabled={!runtimeModDrafts[client.id]?.trim()}>+</button></div></div>
              </div>
            {/each}
          </div>
        </section>

        {#if !selected.joinedTournamentId}
          <section class="panel">
            <div class="section-heading"><h2>Tournaments</h2><span class="count">{selected.tournaments.length}</span></div>
            <div class="tournament-grid">
              {#each selected.tournaments as tournament}
                <article class="tournament-card" class:blocked={!tournament.settings?.allowMockClients}>
                  {#if tournament.settings?.tournamentImage}<img src={tournament.settings.tournamentImage} alt="" />{:else}<div class="tournament-placeholder">TA</div>{/if}
                  <div class="tournament-info"><h3>{tournament.settings?.tournamentName || "Unnamed tournament"}</h3><code>{tournament.guid}</code><span class:allowed={tournament.settings?.allowMockClients} class="access-badge">{tournament.settings?.allowMockClients ? "Mock clients allowed" : "Mock clients blocked"}</span></div>
                  <button class="primary" disabled={!tournament.settings?.allowMockClients} onclick={() => performForTargets(client => manager.joinTournament(client.id, tournament.guid))}>{controllingAll ? "Join all" : "Join"}</button>
                </article>
              {:else}<div class="inline-empty">No tournaments were returned by the server.</div>{/each}
            </div>
          </section>
        {:else}
          <div class="two-column">
            <section class="panel">
              <div class="section-heading"><h2>{joinedTournament?.settings?.tournamentName || selected.joinedTournamentId}</h2><button class="danger-outline compact" onclick={() => performForTargets(client => manager.leaveTournament(client.id))}>Leave {controllingAll ? "all" : "tournament"}</button></div>
              <div class="match-list">
                {#each joinedTournament?.matches ?? [] as match}
                  <article class="match-card" class:joined={selected.currentMatchId === match.guid}>
                    <div><h3>{match.selectedMap?.gameplayParameters?.beatmap?.name || "Unassigned match"}</h3><p>{match.associatedUsers.length} player{match.associatedUsers.length === 1 ? "" : "s"}</p><code>{match.guid}</code></div>
                    {#if selected.currentMatchId === match.guid}<button class="danger-outline compact" onclick={() => performForTargets(client => manager.leaveMatch(client.id))}>Leave {controllingAll ? "all" : ""}</button>{:else}<button class="ghost compact" disabled={!!selected.currentMatchId} onclick={() => performForTargets(client => manager.joinMatch(client.id, match.guid))}>Join {controllingAll ? "all" : "match"}</button>{/if}
                  </article>
                {:else}<div class="inline-empty">No matches are available yet.</div>{/each}
              </div>
            </section>

            {#if !targets.some(client => client.currentMatchId)}
              <section class="panel coordinator-wait"><span class="wait-spinner"></span><div><h2>Waiting for coordinator to create a match</h2><p>The client is in the tournament and ready. Gameplay controls will appear after it joins a match.</p></div></section>
            {:else}<section class="gameplay-stack">
              {#if controllingAll}
                <div class="panel group-controls"><strong>All-client controls</strong><div class="score-actions">{#each scoreButtons as item}<button class={item.className} disabled={!targets.some(client => client.score.playing)} onclick={() => performForTargets(client => manager.scoreAction(client.id, item.action))}>{item.label}</button>{/each}</div><div class="finish-actions"><button class="primary wide" disabled={!targets.some(client => client.score.playing)} onclick={() => performForTargets(client => manager.finishSongEarly(client.id))}>Finish songs early</button><button class="ghost wide" onclick={() => performForTargets(client => manager.backToMenu(client.id))}>Back all to menu</button></div></div>
              {/if}
              <div class="client-game-grid">
                {#each targets as player}
                  {#if !player.currentMatchId}<article class="panel coordinator-wait compact-wait"><span class="wait-spinner"></span><div><h2>{player.username}</h2><p>Waiting for coordinator to create a match</p></div></article>{:else}<article class="panel gameplay player-game-card">
                    <div class="section-heading"><div><h2>{player.username}</h2><p>{player.loadedMap?.name || "Waiting for a map"}</p></div><span class="live" class:active={player.score.playing}>{player.score.playing ? "LIVE" : "IDLE"}</span></div>
                    {#if player.loadedMap}
                      <div class="map-banner">{#if player.loadedMap.coverUrl}<img src={player.loadedMap.coverUrl} alt={`${player.loadedMap.name} cover`} />{:else}<div class="cover-placeholder">♪</div>{/if}<div><h3>{player.loadedMap.name}{player.loadedMap.songSubName ? ` ${player.loadedMap.songSubName}` : ""}</h3><p>{player.loadedMap.songAuthorName || "Unknown artist"} · mapped by {player.loadedMap.levelAuthorName || "unknown"}</p><code>{player.loadedMap.key ? `${player.loadedMap.key} · ` : ""}{player.loadedMap.hash}</code></div></div>
                      <div class="map-facts"><span><small>Characteristic</small><strong>{player.loadedMap.gameplay?.beatmap?.characteristic?.serializedName || "—"}</strong></span><span><small>Difficulty</small><strong>{difficultyName(player.loadedMap.gameplay?.beatmap?.difficulty)}</strong></span><span><small>BPM</small><strong>{player.loadedMap.bpm || "—"}</strong></span><span><small>Duration</small><strong>{duration(player.loadedMap.durationSeconds)}</strong></span><span><small>Rating</small><strong>{player.loadedMap.rating ? `${(player.loadedMap.rating * 100).toFixed(1)}%` : "—"}</strong></span><span><small>Votes</small><strong>↑ {player.loadedMap.upvotes} · ↓ {player.loadedMap.downvotes}</strong></span></div>
                      <div class="progress"><span style:width={`${Math.min(100, player.score.songPosition / player.loadedMap.durationSeconds * 100)}%`}></span></div>
                    {:else}<div class="map-waiting">Load Song and Play Song packets will fetch the BeatSaver map and begin automatic play.</div>{/if}
                    <div class="score-grid"><div><span>Score</span><strong>{player.score.score.toLocaleString()}</strong></div><div><span>Combo</span><strong>{player.score.combo}×</strong></div><div><span>Accuracy</span><strong>{player.score.maxScore ? (player.score.score / player.score.maxScore * 100).toFixed(1) : "0.0"}%</strong></div><div><span>Position</span><strong>{player.score.songPosition.toFixed(1)}s</strong></div></div>
                    <div class="event-counts"><span>Good <strong>{player.score.goodCuts}</strong></span><span>Miss <strong>{player.score.misses}</strong></span><span>Bad <strong>{player.score.badCuts}</strong></span><span>Bomb <strong>{player.score.bombHits}</strong></span><span>Wall <strong>{player.score.wallHits}</strong></span></div>
                    <div class="score-actions mini-actions">{#each scoreButtons as item}<button class={item.className} disabled={!player.score.playing} onclick={() => perform(() => manager.scoreAction(player.id, item.action))}>{item.label}</button>{/each}</div>
                    <div class="finish-actions"><button class="primary wide" disabled={!player.score.playing} onclick={() => perform(() => manager.finishSongEarly(player.id))}>Finish song early</button><button class="ghost wide" onclick={() => perform(() => manager.backToMenu(player.id))}>Back to menu</button></div>
                    {#if player.modifiers.length}<div class="modifiers"><span>Gameplay events</span><div>{#each player.modifiers as modifier}<em>{modifier}</em>{/each}</div></div>{/if}
                    {#if player.replay}<div class="replay-source"><strong>Replay streaming</strong><span>BeatLeader #{player.replay.sourceRank} · {player.replay.info.playerName}</span><small>{player.replay.frames.length.toLocaleString()} frames · {player.replay.notes.length.toLocaleString()} notes</small></div>{/if}
                    {#if player.loadedMap}
                      <details class="map-details"><summary>Map and gameplay details</summary><div class="detail-copy">{#if player.loadedMap.description}<p>{player.loadedMap.description}</p>{/if}<a href={player.loadedMap.downloadUrl}>BeatSaver download</a><small>BeatSaver created: {player.loadedMap.createdAt || "unknown"} · Version: {player.loadedMap.versionCreatedAt || "unknown"}</small></div><pre>{packetJson(player.loadedMap.gameplay)}</pre></details>
                    {/if}
                  </article>{/if}
                {/each}
              </div>
            </section>{/if}
          </div>
        {/if}
        {:else}
          <div class="empty panel"><h2>Not connected</h2><p>{selected.statusMessage || "The server connection is closed. Connect again when the server is available."}</p></div>
        {/if}

        <section class="panel logs">
          <div class="section-heading"><h2>Packet log</h2><div class="log-actions"><select bind:value={logFilter}><option value="all">All packets</option><option value="packet-in">Incoming</option><option value="packet-out">Outgoing</option><option value="error">Errors</option><option value="info">Events</option></select><button class="ghost compact" onclick={() => manager.clearLogs()}>Clear</button><button class="ghost compact" onclick={saveLogs}>Save</button></div></div>
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
