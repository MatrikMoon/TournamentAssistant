<script lang="ts">
  import Color from "color";
  import { ColorScanner, type ColorBar } from "$lib/colorScanner";
  import type { User } from "tournament-assistant-client";
  import { taService } from "$lib/stores";
  import { page } from "$app/stores";

  type UserWithColorBar = {
    user: User;
    colorBar: ColorBar;
  };

  let serverAddress = $page.url.searchParams.get("address")!;
  let serverPort = $page.url.searchParams.get("port")!;

  export let players: User[] = [];
  export const playWithSync = async () => {
    measureStartTime = undefined;
    timeoutStartTime = new Date().getTime();
    foundPlayers = [];
    playersWithDelayInfo = [];
    await sendLoadColorbarRequests();
    await startCapture();
  };

  const generatePermutations = <T,>(array: T[]): T[][] => {
    if (array.length === 0) return [[]];
    const result: T[][] = [];
    for (let i = 0; i < array.length; i++) {
      const rest = generatePermutations(
        array.slice(0, i).concat(array.slice(i + 1))
      );
      for (const perm of rest) {
        result.push([array[i], ...perm]);
      }
    }
    return result;
  };

  const colorGreen = Color({ r: 34, g: 177, b: 76 });
  const colorBlue = Color({ r: 0, g: 162, b: 232 });
  const colorRed = Color({ r: 237, g: 28, b: 36 });
  const colorYellow = Color({ r: 255, g: 242, b: 0 });
  const colorPermutations = generatePermutations([
    colorGreen,
    colorBlue,
    colorRed,
    colorYellow,
  ]);

  $: playersWithColors = players.map((x, index) => {
    return {
      ...x,
      colorSequence: colorPermutations[index],
    };
  });

  let captureStream: MediaStream | undefined;
  let videoElement: HTMLVideoElement | undefined;
  let videoCopyCanvas: HTMLCanvasElement | undefined;
  let colorGenerationCanvas: HTMLCanvasElement | undefined;

  let timeoutMs = 1000 * 60; // Timeout after roughly one minute
  let isCapturingScreen = false;
  let measureStartTime: number | undefined;
  let timeoutStartTime: number | undefined;

  let foundPlayers: UserWithColorBar[] = [];
  let playersWithDelayInfo: User[] = [];

  const startCapture = async () => {
    try {
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
      isCapturingScreen = false;
      console.error(`Error: ${err}`);
    }
  };

  const drawVideoFrameToCanvas = () => {
    if (videoElement!.readyState === videoElement!.HAVE_ENOUGH_DATA) {
      let context = videoCopyCanvas!.getContext("2d", {
        willReadFrequently: true,
      });

      // Draw video to canvas so we can extact image data from it
      videoCopyCanvas!.width = videoElement!.videoWidth;
      videoCopyCanvas!.height = videoElement!.videoHeight;
      context?.drawImage(
        videoElement!,
        0,
        0,
        videoCopyCanvas!.width,
        videoCopyCanvas!.height
      );

      const imageData = context?.getImageData(
        0,
        0,
        videoCopyCanvas!.width,
        videoCopyCanvas!.height
      );

      // If we haven't found all the players yet
      // For every player that hasn't been located yet, try to locate them
      if (
        !playersWithColors.every((x) =>
          foundPlayers.find((y) => x.guid === y.user.guid)
        )
      ) {
        for (const player of playersWithColors.filter(
          (x) => !foundPlayers.find((y) => x.guid === y.user.guid)
        )) {
          console.log(`Testing sequence location for ${player.name}`);
          const sequenceLocation = ColorScanner.getLocationOfSequence(
            player.colorSequence[0],
            player.colorSequence[1],
            player.colorSequence[2],
            player.colorSequence[3],
            imageData!
          );

          if (sequenceLocation) {
            console.log(`FOUND ${player.name} at:`, sequenceLocation);
            foundPlayers = [
              ...foundPlayers,
              { user: player, colorBar: sequenceLocation },
            ];
            if (
              playersWithColors.every((x) =>
                foundPlayers.find((y) => x.guid === y.user.guid)
              )
            ) {
              console.log(
                "Found all players! Switching to look for black screen"
              );
              sendLoadBlackImageRequests();
            }
          } else {
            console.log(
              `Couldn't find ${player.name}, will try again if we don't time out`
            );
          }
        }
      } else {
        // Keep checking for players which haven't had a delay found yet
        for (const player of foundPlayers.filter(
          (x) => !playersWithDelayInfo.find((y) => x.user.guid === y.guid)
        )) {
          if (
            ColorScanner.isPixelColor(
              new Color({ r: 0, g: 0, b: 0 }),
              player.colorBar.centerPoint.x,
              player.colorBar.centerPoint.y,
              imageData!
            )
          ) {
            delayFound(player.user);
          }
        }
      }

      if (
        playersWithColors.every((x) =>
          playersWithDelayInfo.find((y) => x.guid === y.guid)
        ) ||
        new Date().getTime() - timeoutStartTime! >= timeoutMs
      ) {
        if (new Date().getTime() - timeoutStartTime! >= timeoutMs) {
          console.log("Timeout hit, stopping scan");
        } else {
          console.log("FOUND ALL!");
        }

        isCapturingScreen = false;
        captureStream!.getVideoTracks()[0].stop();
      }
    }

    if (isCapturingScreen) {
      requestAnimationFrame(drawVideoFrameToCanvas);
    }
  };

  const sendLoadColorbarRequests = async () => {
    for (const player of playersWithColors) {
      console.log(`Generating colors for ${player.name}`);
      const bitmap = await generateColorBitmap(player.colorSequence);
      await $taService.sendLoadImageRequest(
        serverAddress,
        serverPort,
        bitmap!,
        [player.guid]
      );
    }

    await $taService.sendShowImageCommand(
      serverAddress,
      serverPort,
      playersWithColors.map((x) => x.guid)
    );
  };

  const sendLoadBlackImageRequests = async () => {
    const bitmap = await generateBlackBitmap();

    console.log("Sending load black image");
    await $taService.sendLoadImageRequest(
      serverAddress,
      serverPort,
      bitmap!,
      foundPlayers.map((x) => x.user.guid)
    );

    measureStartTime = new Date().getTime();

    await $taService.sendShowImageCommand(
      serverAddress,
      serverPort,
      foundPlayers.map((x) => x.user.guid)
    );
  };

  const delayFound = async (user: User) => {
    user.streamDelayMs = BigInt(new Date().getTime() - measureStartTime!);
    console.log(`${user.name} delay found!: `, user.streamDelayMs);

    playersWithDelayInfo = [...playersWithDelayInfo, user];

    // If this is the final player, we can start the match!
    if (
      playersWithColors.every((x) =>
        playersWithDelayInfo.find((y) => x.guid === y.guid)
      )
    ) {
      console.log(`${user.name} WAS FINAL DELAY!`);

      const maxDelay = playersWithDelayInfo.sort(
        (a, b) => Number(b.streamDelayMs) - Number(a.streamDelayMs)
      )[0].streamDelayMs;

      for (const player of playersWithDelayInfo) {
        setTimeout(
          async () => {
            $taService.sendStreamSyncFinishedCommand(
              serverAddress,
              serverPort,
              [player.guid]
            );
          },
          Number(maxDelay) - Number(player.streamDelayMs)
        );
      }
    }
  };

  const generateColorBitmap = async (
    colors: Color[]
  ): Promise<Uint8Array | undefined> => {
    const ctx = colorGenerationCanvas!.getContext("2d");
    if (!ctx) {
      console.error("Unable to get canvas context!");
      return;
    }

    colorGenerationCanvas!.width = 1920;
    colorGenerationCanvas!.height = 1080;

    let squareSize = 100;
    let gap = 300;
    let x = 0;
    let y = 0;
    const groupWidth = colors.length * squareSize;
    const totalWidth = groupWidth + gap;

    while (y + squareSize <= colorGenerationCanvas!.height) {
      x = 0;
      while (x + groupWidth <= colorGenerationCanvas!.width) {
        for (let i = 0; i < colors.length; i++) {
          ctx.fillStyle = colors[i].toString();
          ctx.fillRect(x + i * squareSize, y, squareSize, squareSize);
        }
        x += totalWidth; // Move to the start of the next group including the gap
      }
      y += squareSize + gap; // Move down for the next row of groups
    }

    // Use createImageBitmap to create a bitmap from the canvas
    const blob = await new Promise<Blob | null>((resolve) =>
      colorGenerationCanvas!.toBlob(resolve)
    );
    const arrayBuffer = await blob!.arrayBuffer();
    return new Uint8Array(arrayBuffer);
  };

  const generateBlackBitmap = async (): Promise<Uint8Array | undefined> => {
    const ctx = colorGenerationCanvas!.getContext("2d");
    if (!ctx) {
      console.error("Unable to get canvas context!");
      return;
    }

    colorGenerationCanvas!.width = 1920;
    colorGenerationCanvas!.height = 1080;

    ctx.fillStyle = new Color({ r: 0, g: 0, b: 0 }).toString();
    ctx.fillRect(0, 0, 1920, 1080);

    // Use createImageBitmap to create a bitmap from the canvas
    const blob = await new Promise<Blob | null>((resolve) =>
      colorGenerationCanvas!.toBlob(resolve)
    );
    const arrayBuffer = await blob!.arrayBuffer();
    return new Uint8Array(arrayBuffer);
  };

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

<!-- svelte-ignore a11y-media-has-caption -->
<video
  bind:this={videoElement}
  use:srcObject={captureStream}
  autoplay
  playsinline
  hidden
/>
<canvas bind:this={videoCopyCanvas} hidden />
<canvas bind:this={colorGenerationCanvas} hidden />

<style lang="scss">
</style>
