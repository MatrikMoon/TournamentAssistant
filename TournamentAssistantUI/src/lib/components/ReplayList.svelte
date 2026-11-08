<script lang="ts">
  import { onDestroy, onMount } from "svelte";
  import DataTable, { Head, Body, Row, Cell, Pagination } from "@smui/data-table";
  import IconButton from "@smui/icon-button";
  import {
    Response_ResponseType,
    type Push_ReplaySubmitted,
    type ReplayRecording,
  } from "tournament-assistant-client";
  import { taService } from "$lib/stores";

  export let serverAddress: string;
  export let serverPort: string;
  export let tournamentId: string;
  export let canRemove = false;

  const pageSize = 10;
  const difficultyNames = ["Easy", "Normal", "Hard", "Expert", "Expert+"];

  let replays: ReplayRecording[] = [];
  let totalCount = 0;
  let page = 0;
  let storageAvailable = true;
  let copiedGuid = "";

  $: lastPage = Math.max(0, Math.ceil(totalCount / pageSize) - 1);
  $: firstShown = totalCount === 0 ? 0 : page * pageSize + 1;
  $: lastShown = Math.min(totalCount, (page + 1) * pageSize);

  async function loadPage(pageToLoad: number) {
    const response = await $taService.getReplays(
      serverAddress,
      serverPort,
      tournamentId,
      pageToLoad,
      pageSize
    );

    if (
      response.type === Response_ResponseType.Success &&
      response.details.oneofKind === "getReplays"
    ) {
      const result = response.details.getReplays;
      replays = result.replays;
      totalCount = result.totalCount;
      storageAvailable = result.storageAvailable;
      page = pageToLoad;

      // The last replay of the last page was removed
      if (replays.length === 0 && page > 0) {
        await loadPage(page - 1);
      }
    }
  }

  async function download(replay: ReplayRecording) {
    const data = await $taService.downloadReplay(
      serverAddress,
      serverPort,
      replay.downloadUrl
    );
    const url = URL.createObjectURL(new Blob([data], { type: "application/octet-stream" }));
    const link = document.createElement("a");
    link.href = url;
    link.download = replay.fileName;
    link.click();
    URL.revokeObjectURL(url);
  }

  async function copyDownloadLink(replay: ReplayRecording) {
    await navigator.clipboard.writeText(replay.downloadUrl);
    copiedGuid = replay.guid;
    setTimeout(() => {
      if (copiedGuid === replay.guid) copiedGuid = "";
    }, 1800);
  }

  async function remove(replay: ReplayRecording) {
    await $taService.deleteReplay(serverAddress, serverPort, tournamentId, replay.guid);
    await loadPage(page);
  }

  const formatDate = (unixMs: bigint) => new Date(Number(unixMs)).toLocaleString();
  const formatSize = (bytes: bigint) => `${(Number(bytes) / 1024 / 1024).toFixed(1)} MB`;

  // New replays show up at the top, so only the first page needs refreshing
  const onReplaySubmitted = async (event: Push_ReplaySubmitted) => {
    if (event.tournamentId === tournamentId && event.stored && page === 0) {
      await loadPage(0);
    }
  };

  onMount(async () => {
    $taService.subscribeToReplaySubmissions(onReplaySubmitted);
    await loadPage(0);
  });

  onDestroy(() => {
    $taService.unsubscribeFromReplaySubmissions(onReplaySubmitted);
  });
</script>

{#if !storageAvailable}
  <div class="message">Replay storage is not available on this server right now</div>
{:else if totalCount === 0}
  <div class="message">No replays have been recorded yet</div>
{:else}
  <DataTable table$aria-label="Replays" style="width: 100%;">
    <Head>
      <Row>
        <Cell>Player</Cell>
        <Cell>Map</Cell>
        <Cell>Recorded</Cell>
        <Cell>Expires</Cell>
        <Cell numeric>Size</Cell>
        <Cell />
      </Row>
    </Head>
    <Body>
      {#each replays as replay (replay.guid)}
        <Row>
          <Cell>{replay.playerName || replay.platformId}</Cell>
          <Cell title={replay.mapHash}>
            {replay.mapHash.slice(0, 8)} {replay.characteristic} {difficultyNames[replay.difficulty] ?? ""}
          </Cell>
          <Cell>{formatDate(replay.recordedAt)}</Cell>
          <Cell>{formatDate(replay.expiresAt)}</Cell>
          <Cell numeric>{formatSize(replay.size)}</Cell>
          <Cell>
            <IconButton class="material-icons" title="Download" on:click={() => download(replay)}>download</IconButton>
            <IconButton
              class="material-icons"
              title={copiedGuid === replay.guid ? "Copied" : "Copy download link"}
              on:click={() => copyDownloadLink(replay)}
            >
              {copiedGuid === replay.guid ? "check" : "link"}
            </IconButton>
            {#if canRemove}
              <IconButton class="material-icons" title="Remove" on:click={() => remove(replay)}>delete</IconButton>
            {/if}
          </Cell>
        </Row>
      {/each}
    </Body>

    <Pagination slot="paginate">
      <svelte:fragment slot="total">
        {firstShown}-{lastShown} of {totalCount}
      </svelte:fragment>

      <IconButton class="material-icons" action="prev-page" on:click={() => loadPage(page - 1)} disabled={page === 0}>chevron_left</IconButton>
      <IconButton class="material-icons" action="next-page" on:click={() => loadPage(page + 1)} disabled={page >= lastPage}>chevron_right</IconButton>
    </Pagination>
  </DataTable>
{/if}

<style lang="scss">
  .message {
    color: var(--mdc-theme-text-primary-on-background);
    text-align: center;
    padding: 2vmin;
  }
</style>
