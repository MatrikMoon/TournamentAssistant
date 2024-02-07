<script lang="ts">
  import { page } from "$app/stores";
  import LayoutGrid, { Cell } from "@smui/layout-grid";
  import UserList from "$lib/components/UserList.svelte";
  import DebugLog from "$lib/components/DebugLog.svelte";
  import AddSong from "$lib/components/AddSong.svelte";
  import Button from "@smui/button";
  import { taService } from "$lib/stores";
  import { onMount } from "svelte";
  import { ColorScanner } from "$lib/colorScanner";
  import Color from "color";
  import { invoke } from "@tauri-apps/api";

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
  ) => {};

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

      const changedImage: number[] = [];

      for (
        let pixel = 0;
        pixel < imageData!.width * imageData!.height;
        pixel++
      ) {
        const locationInArray = pixel * 4;
        const pixelCoordinates = ColorScanner.xyFromArrayLocation(
          pixel,
          imageData!.width,
        );

        const data = imageData!.data;
        const color = Color({
          r: data[locationInArray],
          g: data[locationInArray + 1],
          b: data[locationInArray + 2],
          alpha: data[locationInArray + 3],
        });

        if (
          Math.abs(
            pixelCoordinates.x - (sequenceLocation?.centerPoint.x ?? 0),
          ) <= 20 &&
          Math.abs(
            pixelCoordinates.y - (sequenceLocation?.centerPoint.y ?? 0),
          ) <= 20
        ) {
          changedImage.push(0);
          changedImage.push(0);
          changedImage.push(0);
          changedImage.push(255);
        } else {
          changedImage.push(pixelCoordinates.x % 10 === 0 ? 0 : color.red());
          changedImage.push(color.green());
          changedImage.push(color.blue());
          changedImage.push(color.alpha() * 255);
        }
      }

      canvasElementOut!.width = imageData!.width;
      canvasElementOut!.height = imageData!.height;
      canvasElementOut!
        .getContext("2d")
        ?.putImageData(
          new ImageData(
            new Uint8ClampedArray(changedImage),
            canvasElementOut!.width,
            canvasElementOut!.height,
          ),
          0,
          0,
        );

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
    const monitors = JSON.parse(await invoke<any>("get_monitors"));
    console.log("MONITORS:\n", monitors);
    console.log("READING:\n", monitors[0].name);

    const pixels = await invoke("get_pixels", {
      monitorName: monitors[0].name,
    });
    console.log("PIXELS:\n", pixels);

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

  $: console.log(window.location);
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
    <Cell span={8}>
      <AddSong
        {serverAddress}
        {serverPort}
        {tournamentId}
        {matchId}
        onAddClicked={onLoadClicked}
      />
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
  <canvas bind:this={canvasElement} hidden></canvas>
  <canvas bind:this={canvasElementOut}></canvas>
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
