<script lang="ts">
  import { page } from "$app/stores";
  import LayoutGrid, { Cell } from "@smui/layout-grid";
  import UserList from "$lib/components/UserList.svelte";
  import DebugLog from "$lib/components/DebugLog.svelte";
  import AddSong from "$lib/components/AddSong.svelte";
  import Fab, { Icon, Label } from "@smui/fab";
  import { taService } from "$lib/stores";
  import { onMount } from "svelte";
  import { ColorScanner } from "$lib/colorScanner";
  import Color from "color";
  import type { QualifierMapWithSongInfo } from "$lib/globalTypes";
  import SongList from "$lib/components/SongList.svelte";
  import type {
    GameplayParameters,
    QualifierEvent_QualifierMap,
  } from "tournament-assistant-client";
  import { v4 as uuidv4 } from "uuid";
  import NowPlayingCard from "$lib/components/NowPlayingCard.svelte";

  let serverAddress = $page.url.searchParams.get("address")!;
  let serverPort = $page.url.searchParams.get("port")!;
  let tournamentId = $page.url.searchParams.get("tournamentId")!;
  let matchId = $page.url.searchParams.get("matchId")!;

  let videoElement: HTMLVideoElement | undefined;
  let canvasElement: HTMLCanvasElement | undefined;
  let canvasElementOut: HTMLCanvasElement | undefined;
  let captureStream: MediaStream | undefined;

  let frames = 0;
  let isCapturingScreen = false;

  let nowPlaying: string | undefined = undefined;
  let nowPlayingSongInfo: QualifierMapWithSongInfo | undefined = undefined;
  let maps: QualifierEvent_QualifierMap[] = [];
  let mapsWithSongInfo: QualifierMapWithSongInfo[] = [];
  $: nowPlayingSongInfo = mapsWithSongInfo.find((x) => x.guid === nowPlaying);
  $: canPlay = nowPlayingSongInfo !== undefined;

  let selectedSongId = "";
  let resultGameplayParameters: GameplayParameters | undefined = undefined;

  onMount(async () => {
    await $taService.joinMatch(
      serverAddress,
      serverPort,
      tournamentId,
      matchId,
    );
  });

  const onLoadClicked = async (
    showScoreboard: boolean,
    disablePause: boolean,
    disableFail: boolean,
    disableScoresaberSubmission: boolean,
    disableCustomNotesOnStream: boolean,
    attempts: number,
  ) => {
    nowPlaying = uuidv4();

    maps = [
      ...maps,
      {
        guid: nowPlaying,
        gameplayParameters: resultGameplayParameters,
        disablePause,
        attempts,
      },
    ];

    selectedSongId = "";
  };

  const onPlayClicked = () => {};

  const onSongListItemClicked = async (map: QualifierMapWithSongInfo) => {
    nowPlaying = map.guid;
  };

  const onRemoveClicked = async (map: QualifierMapWithSongInfo) => {
    maps = maps.filter((x) => x.guid !== map.guid);
  };

  function drawVideoFrameToCanvas() {
    if (videoElement!.readyState === videoElement!.HAVE_ENOUGH_DATA) {
      let context = canvasElement!.getContext("2d", {
        willReadFrequently: true,
      });

      canvasElement!.width = videoElement!.videoWidth;
      canvasElement!.height = videoElement!.videoHeight;
      context?.drawImage(
        videoElement!,
        0,
        0,
        canvasElement!.width,
        canvasElement!.height,
      );

      const imageData = context?.getImageData(
        0,
        0,
        canvasElement!.width,
        canvasElement!.height,
      );

      console.log("Testing sequence location");
      const sequenceLocation = ColorScanner.getLocationOfSequence(
        Color({ r: 34, g: 177, b: 76 }),
        Color({ r: 0, g: 162, b: 232 }),
        Color({ r: 237, g: 28, b: 36 }),
        Color({ r: 255, g: 242, b: 0 }),
        imageData!,
      );

      console.log("sequenceLocation:", sequenceLocation);

      if (sequenceLocation || frames >= 0) {
        isCapturingScreen = false;
        captureStream!.getVideoTracks()[0].stop();
      }

      frames++;
    }

    if (isCapturingScreen) {
      requestAnimationFrame(drawVideoFrameToCanvas);
    }
  }

  async function startCapture() {
    try {
      frames = 0;

      const displayMediaOptions = {
        video: {
          displaySurface: "monitor",
        },
        audio: false,
      };

      captureStream =
        await navigator.mediaDevices.getDisplayMedia(displayMediaOptions);

      isCapturingScreen = true;

      requestAnimationFrame(drawVideoFrameToCanvas);
    } catch (err) {
      console.error(`Error: ${err}`);
    }
  }

  function srcObject(node: HTMLVideoElement, stream?: MediaStream) {
    node.srcObject = stream!;
    return {
      update(newStream: MediaStream) {
        if (node.srcObject != newStream) {
          node.srcObject = newStream;
        }
      },
    };
  }
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
          <div class="play-button">
            <Fab
              color={canPlay ? "primary" : "secondary"}
              on:click={onPlayClicked}
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
              on:click={onPlayClicked}
              extended
              disabled={!canPlay}
            >
              <Icon class="material-icons">play_arrow</Icon>
              <Label>Play with Sync</Label>
            </Fab>
          </div>
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
            <AddSong
              {serverAddress}
              {serverPort}
              {tournamentId}
              bind:selectedSongId
              bind:resultGameplayParameters
              onAddClicked={onLoadClicked}
            />
          </div>
        </div>
      </div>
    </Cell>
  </LayoutGrid>
  <!-- svelte-ignore a11y-media-has-caption -->
  <video
    bind:this={videoElement}
    use:srcObject={captureStream}
    autoplay
    playsinline
    hidden
  />
  <canvas bind:this={canvasElement} hidden></canvas>
  <canvas bind:this={canvasElementOut}></canvas>
</div>

<style lang="scss">
  .grid-cell {
    background-color: rgba($color: #000000, $alpha: 0.1);
  }

  .play-buttons-container {
    display: flex;
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
</style>
