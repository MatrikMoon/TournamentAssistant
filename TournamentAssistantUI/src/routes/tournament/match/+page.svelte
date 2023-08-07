<script lang="ts">
  import { page } from "$app/stores";
  import LayoutGrid, { Cell } from "@smui/layout-grid";
  import UserList from "$lib/components/UserList.svelte";
  import DebugLog from "$lib/components/DebugLog.svelte";
  import SongOptions from "$lib/components/SongOptions.svelte";
  import QrScanner from "qr-scanner";
  import Button from "@smui/button";
  import { taService } from "$lib/stores";
  import { onMount } from "svelte";

  let serverAddress = $page.url.searchParams.get("address")!;
  let serverPort = $page.url.searchParams.get("port")!;
  let tournamentId = $page.url.searchParams.get("tournamentId")!;
  let matchId = $page.url.searchParams.get("matchId")!;

  let videoElement: HTMLVideoElement | undefined;
  let captureStream: MediaStream | undefined;

  onMount(async () => {
    await $taService.joinMatch(
      serverAddress,
      serverPort,
      tournamentId,
      matchId
    );
  });

  async function scanQRCode() {
    try {
      const result = await QrScanner.scanImage(videoElement!, {
        returnDetailedScanResult: true,
      });
      console.log({ result });
    } catch (e) {
      console.log({ e });
    }

    requestAnimationFrame(scanQRCode);
  }

  async function startCapture() {
    try {
      const displayMediaOptions = {
        video: {
          displaySurface: "window",
        },
        audio: false,
      };

      captureStream = await navigator.mediaDevices.getDisplayMedia(
        displayMediaOptions
      );

      requestAnimationFrame(scanQRCode);
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
      <SongOptions {serverAddress} {serverPort} {tournamentId} {matchId} />
    </Cell>
    <Cell>
      <Button on:click={startCapture}>Play (With Sync)</Button>
    </Cell>
    <Cell span={12}>
      <div class="grid-cell">
        <DebugLog />
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
</div>

<style lang="scss">
  .grid-cell {
    background-color: rgba($color: #000000, $alpha: 0.1);
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

  .player-list-title {
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
