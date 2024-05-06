<script lang="ts">
  import { page } from "$app/stores";
  import LayoutGrid, { Cell } from "@smui/layout-grid";
  import UserList from "$lib/components/UserList.svelte";
  import AddSong from "$lib/components/add-song/AddSong.svelte";
  import Fab, { Icon, Label } from "@smui/fab";
  import { taService } from "$lib/stores";
  import { onDestroy, onMount } from "svelte";
  import type { MapWithSongInfo } from "$lib/globalTypes";
  import SongList from "$lib/components/SongList.svelte";
  import {
    Response_ResponseType,
    type GameplayParameters,
    type Map,
    User_ClientTypes,
    User,
    User_PlayStates,
    Push_SongFinished,
  } from "tournament-assistant-client";
  import { v4 as uuidv4 } from "uuid";
  import NowPlayingCard from "$lib/components/NowPlayingCard.svelte";
  import { fly } from "svelte/transition";
  import ResultsDialog from "$lib/dialogs/ResultsDialog.svelte";
  import { goto } from "$app/navigation";
  import StreamSync from "$lib/components/StreamSync.svelte";

  let serverAddress = $page.url.searchParams.get("address")!;
  let serverPort = $page.url.searchParams.get("port")!;
  let tournamentId = $page.url.searchParams.get("tournamentId")!;
  let matchId = $page.url.searchParams.get("matchId")!;

  let selectedSongId = "";
  let playWithSync: () => Promise<void>;

  let resultsDialogOpen = false;
  let results: Push_SongFinished[] = [];

  let users: User[] = [];
  $: players = users.filter((x) => x.clientType === User_ClientTypes.Player);
  $: anyPlayersInGame = !!players.find(
    (x) => x.playState === User_PlayStates.InGame,
  );

  let nowPlaying: string | undefined = undefined;
  let nowPlayingSongInfo: MapWithSongInfo | undefined = undefined;
  let maps: Map[] = [];
  let mapsWithSongInfo: MapWithSongInfo[] = [];
  let allPlayersLoadedMap = false;
  $: nowPlayingSongInfo = mapsWithSongInfo.find((x) => x.guid === nowPlaying);
  $: canPlay = nowPlayingSongInfo !== undefined && allPlayersLoadedMap;

  onMount(async () => {
    try {
      await $taService.joinMatch(
        serverAddress,
        serverPort,
        tournamentId,
        matchId,
      );

      const match = await $taService.getMatch(
        serverAddress,
        serverPort,
        tournamentId,
        matchId,
      );

      // Set the maps list, only on mount
      maps = match?.selectedMap ? [match!.selectedMap!] : [];
      nowPlaying = match?.selectedMap?.guid;

      await onChange();
    } catch {
      // If there's an error joining the match, return to the match select screen
      goto(
        `/tournament/match-select?tournamentId=${tournamentId}&address=${serverAddress}&port=${serverPort}`,
        {
          replaceState: true,
        },
      );
    }
  });

  async function onChange() {
    const tournamentUsers = (await $taService.getTournament(
      serverAddress,
      serverPort,
      tournamentId,
    ))!.users;

    const match = await $taService.getMatch(
      serverAddress,
      serverPort,
      tournamentId,
      matchId,
    );

    users = tournamentUsers.filter((x) =>
      match?.associatedUsers.includes(x.guid),
    );
  }

  const onSongFinished = (result: Push_SongFinished) => {
    // Add the results to the list
    results = [...results, result];

    // If we receive a SongFinished push, and there's
    // no remaining players InGame, we've received all
    // the scores and should display the results screen
    const allPlayersDone = (resultsDialogOpen = players.every(
      (x) => x.playState === User_PlayStates.WaitingForCoordinator,
    ));

    if (allPlayersDone) {
      resultsDialogOpen = true;
    }
  };

  $taService.subscribeToUserUpdates(onChange);
  $taService.subscribeToMatchUpdates(onChange);
  $taService.client.on("songFinished", onSongFinished);
  onDestroy(() => {
    $taService.unsubscribeFromUserUpdates(onChange);
    $taService.unsubscribeFromMatchUpdates(onChange);
    $taService.client.removeListener("songFinished", onSongFinished);
  });

  const onSongsAdded = async (result: GameplayParameters[]) => {
    for (let song of result) {
      const newMap: Map = {
        guid: uuidv4(),
        gameplayParameters: song,
      };

      maps = [...maps, newMap];

      // If there is no song currently selected, set it, and tell players to load it
      if (!nowPlaying) {
        nowPlaying = newMap.guid;
        await sendLoadSong(newMap);
      }
    }
  };

  const onPlayClicked = async () => {
    results = [];

    $taService.sendPlaySongCommand(
      serverAddress,
      serverPort,
      nowPlayingSongInfo!.gameplayParameters!,
      players.map((x) => x.guid),
    );
  };

  const onPlayWithSyncClicked = async () => {
    results = [];

    const parametersWithSync = {
      ...nowPlayingSongInfo!.gameplayParameters!,
      useSync: true,
    };

    $taService.sendPlaySongCommand(
      serverAddress,
      serverPort,
      parametersWithSync,
      players.map((x) => x.guid),
    );

    await playWithSync();
  };

  const onReturnToMenuClicked = async () => {
    $taService.sendReturnToMenuCommand(
      serverAddress,
      serverPort,
      players.map((x) => x.guid),
    );
  };

  const onSongListItemClicked = async (map: MapWithSongInfo) => {
    nowPlaying = map.guid;
    sendLoadSong(map);
  };

  const onRemoveClicked = async (map: MapWithSongInfo) => {
    maps = maps.filter((x) => x.guid !== map.guid);
    nowPlaying = undefined;
  };

  const sendLoadSong = async (map: Map) => {
    allPlayersLoadedMap = false;

    const match = await $taService.getMatch(
      serverAddress,
      serverPort,
      tournamentId,
      matchId,
    );

    if (!match) {
      return;
    }

    // Update selectedLevel of match;
    await $taService.setMatchMap(
      serverAddress,
      serverPort,
      tournamentId,
      matchId,
      map,
    );

    const allPlayersResponses = await $taService.sendLoadSongRequest(
      serverAddress,
      serverPort,
      map.gameplayParameters!.beatmap!.levelId,
      players.map((x) => x.guid),
    );

    if (
      allPlayersResponses.every(
        (x) => x.response.type === Response_ResponseType.Success,
      )
    ) {
      allPlayersLoadedMap = true;
    }
  };
</script>

<div>
  <!-- <div class="match-title">{tournament?.settings?.tournamentName}</div> -->
  <div class="match-title">Select a song, difficulty, and characteristic</div>

  <LayoutGrid>
    <Cell span={4}>
      <div class="player-list-title">Players</div>
      <div class="grid-cell">
        <UserList {serverAddress} {serverPort} {tournamentId} {matchId} />
      </div>
    </Cell>
    <Cell span={4}>
      <div class="now-playing-title">Now Playing</div>
      <div class="grid-cell">
        <NowPlayingCard bind:mapWithSongInfo={nowPlayingSongInfo} />
        <div class="play-buttons-container">
          <!-- This is ugly, but for some reason any svelte like the below:
            color={canPlay ? "primary" : "secondary"} doesn't function when
            inside LayoutGrid unless we do this. Go figure -->
          {#if anyPlayersInGame}
            <div class="play-button">
              <Fab color="primary" on:click={onReturnToMenuClicked} extended>
                <Icon class="material-icons">keyboard_return</Icon>
                <Label>Return to Menu</Label>
              </Fab>
            </div>
          {:else if canPlay}
            <div class="play-button">
              <Fab color="primary" on:click={onPlayClicked} extended>
                <Icon class="material-icons">play_arrow</Icon>
                <Label>Play</Label>
              </Fab>
            </div>
            <div class="play-button">
              <Fab color="primary" on:click={onPlayWithSyncClicked} extended>
                <Icon class="material-icons">play_arrow</Icon>
                <Label>Play with Sync</Label>
              </Fab>
            </div>
          {:else}
            <div class="play-button">
              <Fab extended disabled>
                <Icon class="material-icons">play_arrow</Icon>
                <Label>Play</Label>
              </Fab>
            </div>
            <div class="play-button">
              <Fab extended disabled>
                <Icon class="material-icons">play_arrow</Icon>
                <Label>Play with Sync</Label>
              </Fab>
            </div>
          {/if}
        </div>
      </div>
    </Cell>
    <Cell span={8}>
      <div class="up-next-title">Up Next</div>
      <div class="grid-cell">
        <div class="song-list-container">
          <SongList
            bind:mapsWithSongInfo
            {maps}
            onItemClicked={onSongListItemClicked}
            {onRemoveClicked}
          />
          <div class="song-list-addsong">
            <AddSong bind:selectedSongId {onSongsAdded} />
          </div>
        </div>
      </div>
    </Cell>
  </LayoutGrid>

  <div in:fly={{ duration: 800 }}>
    {#if nowPlayingSongInfo}
      <ResultsDialog
        bind:open={resultsDialogOpen}
        {results}
        mapWithSongInfo={nowPlayingSongInfo}
      />
    {/if}
  </div>

  <StreamSync {players} bind:playWithSync />

  <!-- <div class="fab-container">
    <Fab color="primary" on:click={() => {}} extended>
      <Icon class="material-icons">close</Icon>
      <Label>End Match</Label>
    </Fab>
  </div> -->
</div>

<style lang="scss">
  .grid-cell {
    background-color: rgba($color: #000000, $alpha: 0.1);
  }

  .play-buttons-container {
    display: flex;
    justify-content: center;

    * {
      margin: 10px;
    }
  }

  .match-title {
    color: var(--mdc-theme-text-primary-on-background);
    background-color: rgba($color: #000000, $alpha: 0.1);
    border-radius: 2vmin;
    text-align: center;
    font-size: 2rem;
    font-weight: 100;
    line-height: 1.1;
    padding: 2vmin;
  }

  .player-list-title,
  .now-playing-title,
  .up-next-title {
    color: var(--mdc-theme-text-primary-on-background);
    background-color: rgba($color: #000000, $alpha: 0.1);
    border-radius: 2vmin 2vmin 0 0;
    text-align: center;
    font-size: 2rem;
    font-weight: 100;
    line-height: 1.1;
    padding: 2vmin;
  }

  // .fab-container {
  //   position: fixed;
  //   bottom: 2vmin;
  //   right: 2vmin;
  // }
</style>
