<script lang="ts">
  import { page } from "$app/stores";
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
    Tournament,
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

  let tournament: Tournament | undefined;
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
    tournament = await $taService.getTournament(
      serverAddress,
      serverPort,
      tournamentId,
    );

    const tournamentUsers = tournament!.users;

    const match = await $taService.getMatch(
      serverAddress,
      serverPort,
      tournamentId,
      matchId,
    );

    if (match) {
      users = tournamentUsers.filter((x) =>
        match?.associatedUsers.includes(x.guid),
      );
    } else {
      // If the match no longer exists, return to match select screen
      goto(
        `/tournament/match-select?tournamentId=${tournamentId}&address=${serverAddress}&port=${serverPort}`,
        {
          replaceState: true,
        },
      );
    }
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

  const onEndMatchClicked = async () => {
    await $taService.deleteMatch(
      serverAddress,
      serverPort,
      tournamentId,
      matchId,
    );
  };

  $taService.subscribeToTournamentUpdates(onChange);
  $taService.subscribeToUserUpdates(onChange);
  $taService.subscribeToMatchUpdates(onChange);
  $taService.client.on("songFinished", onSongFinished);
  onDestroy(() => {
    $taService.unsubscribeFromTournamentUpdates(onChange);
    $taService.unsubscribeFromUserUpdates(onChange);
    $taService.unsubscribeFromMatchUpdates(onChange);
    $taService.client.removeListener("songFinished", onSongFinished);
  });
</script>

<div class="page">
  <!-- <div class="match-title">{tournament?.settings?.tournamentName}</div> -->
  <div class="match-title">Select a song, difficulty, and characteristic</div>

  <div class="grid">
    <div class="column">
      <div class="cell">
        <div class="player-list-title">Players</div>
        <div class="shaded-box">
          <UserList {serverAddress} {serverPort} {tournamentId} {matchId} />
        </div>
      </div>
    </div>
    <div class="column">
      <div class="cell">
        <div class="now-playing-title">Now Playing</div>
        <div class="shaded-box">
          <NowPlayingCard bind:mapWithSongInfo={nowPlayingSongInfo} />
          <div class="play-buttons-container">
            {#if anyPlayersInGame}
              <div class="play-button">
                <Fab color="primary" on:click={onReturnToMenuClicked} extended>
                  <Icon class="material-icons">keyboard_return</Icon>
                  <Label>Return to Menu</Label>
                </Fab>
              </div>
            {:else}
              <div class="play-button">
                <Fab
                  color={canPlay ? "primary" : "secondary"}
                  on:click={canPlay ? onPlayClicked : undefined}
                  extended
                  disabled={!canPlay}
                >
                  <Icon class="material-icons">play_arrow</Icon>
                  <Label>Play</Label>
                </Fab>
              </div>
              <div class="play-button">
                <Fab
                  color={canPlay ? "primary" : "secondary"}
                  on:click={canPlay ? onPlayWithSyncClicked : undefined}
                  extended
                  disabled={!canPlay}
                >
                  <Icon class="material-icons">play_arrow</Icon>
                  <Label>Play with Sync</Label>
                </Fab>
              </div>
            {/if}
          </div>
        </div>
      </div>
    </div>
  </div>

  <div class="up-next">
    <div class="up-next-title">Up Next</div>
    <div class="shaded-box">
      <div class="song-list-container">
        <SongList
          bind:mapsWithSongInfo
          {maps}
          onItemClicked={onSongListItemClicked}
          {onRemoveClicked}
        />
        {#if tournament}
          <div class="song-list-addsong">
            <AddSong bind:selectedSongId {onSongsAdded} {tournamentId} />
          </div>
        {/if}
      </div>
    </div>
  </div>

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

  <div class="fab-container">
    <Fab color="primary" on:click={onEndMatchClicked} extended>
      <Icon class="material-icons">close</Icon>
      <Label>End Match</Label>
    </Fab>
  </div>
</div>

<style lang="scss">
  .page {
    display: flex;
    flex-direction: column;
    align-items: center;
    margin-bottom: 70px;

    .match-title {
      color: var(--mdc-theme-text-primary-on-background);
      background-color: rgba($color: #000000, $alpha: 0.1);
      border-radius: 2vmin;
      text-align: center;
      font-size: 2rem;
      font-weight: 100;
      line-height: 1.1;
      padding: 2vmin;
      width: -webkit-fill-available;
    }

    .grid {
      display: flex;
      width: -webkit-fill-available;
      max-width: 700px;
      min-width: none;
      margin-top: 5px;

      .column {
        width: -webkit-fill-available;
        max-width: 350px;

        .cell {
          padding: 5px;
        }
      }
    }

    .shaded-box {
      background-color: rgba($color: #000000, $alpha: 0.1);
    }

    .play-buttons-container {
      display: flex;
      justify-content: center;

      * {
        margin: 10px;
      }
    }

    .up-next {
      width: -webkit-fill-available;
      max-width: 700px;
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

    .fab-container {
      position: fixed;
      bottom: 2vmin;
      right: 2vmin;
    }
  }
</style>
