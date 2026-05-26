<script lang="ts">
    import Color from "color";
    import { ColorScanner, type ColorBar } from "$lib/colorScanner";
    import type { User } from "tournament-assistant-client";
    import { taService } from "$lib/stores";
    import { page } from "$app/stores";

    type UserWithColors = User & { colorSequence: Color[] };

    type FoundPlayer = {
        user: UserWithColors;
        colorBar: ColorBar;
    };

    const TIMEOUT_MS = 1000 * 90; // Abort after ~1.5 minutes
    const DISCOVERY_INTERVAL_MS = 100; // Poll interval during color discovery
    const SCAN_RADIUS = 2; // Sample +-2px around each center point (4px window)

    const colorGreen = Color({ r: 34, g: 177, b: 76 });
    const colorBlue = Color({ r: 0, g: 162, b: 232 });
    const colorRed = Color({ r: 237, g: 28, b: 36 });
    const colorYellow = Color({ r: 255, g: 242, b: 0 });

    const generatePermutations = <T,>(array: T[]): T[][] => {
        if (array.length === 0) return [[]];
        const result: T[][] = [];
        for (let i = 0; i < array.length; i++) {
            const rest = generatePermutations(array.slice(0, i).concat(array.slice(i + 1)));
            for (const perm of rest) {
                result.push([array[i], ...perm]);
            }
        }
        return result;
    };

    const colorPermutations = generatePermutations([colorGreen, colorBlue, colorRed, colorYellow]);

    let serverAddress  = $page.url.searchParams.get("address")!;
    let serverPort     = $page.url.searchParams.get("port")!;
    let tournamentId   = $page.url.searchParams.get("tournamentId")!;

    export let players: User[] = [];

    export const playWithSync = async () => {
        reset();
        await sendLoadColorbarRequests();
        await startCapture();
    };

    $: playersWithColors = players.map((user, index): UserWithColors => ({
        ...user,
        colorSequence: colorPermutations[index],
    }));

    let captureStream: MediaStream | undefined;
    let videoElement: HTMLVideoElement | undefined;
    let videoCopyCanvas: HTMLCanvasElement | undefined;
    let colorGenerationCanvas: HTMLCanvasElement | undefined;

    // Held across the two scan phases
    let isCapturingScreen = false;
    let measureStartTime: number | undefined;
    let timeoutStartTime: number | undefined;

    let foundPlayers: FoundPlayer[] = [];
    let playersWithDelayInfo: UserWithColors[] = [];

    // Discovery phase: whether a scan is currently running (prevents overlap)
    let discoveryBusy = false;
    let discoveryTimerId: ReturnType<typeof setTimeout> | undefined;

    // Black-screen phase: RAF handle
    let blackScreenRafId: number | undefined;

    const reset = () => {
        measureStartTime = undefined;
        timeoutStartTime = new Date().getTime();
        foundPlayers = [];
        playersWithDelayInfo = [];
        discoveryBusy = false;

        if (discoveryTimerId !== undefined) {
            clearTimeout(discoveryTimerId);
            discoveryTimerId = undefined;
        }
        if (blackScreenRafId !== undefined) {
            cancelAnimationFrame(blackScreenRafId);
            blackScreenRafId = undefined;
        }
    };

    const startCapture = async () => {
        try {
            captureStream = await navigator.mediaDevices.getDisplayMedia({
                video: { displaySurface: "monitor" },
                audio: false,
            });

            isCapturingScreen = true;

            // Size the canvas once when we know the video dimensions
            videoElement!.addEventListener("loadedmetadata", () => {
                videoCopyCanvas!.width  = videoElement!.videoWidth;
                videoCopyCanvas!.height = videoElement!.videoHeight;
            }, { once: true });

            // Give the video a moment to stabilise, then kick off discovery
            discoveryTimerId = setTimeout(runDiscoveryTick, DISCOVERY_INTERVAL_MS);
        } catch (err) {
            isCapturingScreen = false;
            console.error(`Error starting screen capture: ${err}`);
        }
    };

    const stopCapture = () => {
        isCapturingScreen = false;
        captureStream?.getVideoTracks()[0].stop();

        if (discoveryTimerId !== undefined) {
            clearTimeout(discoveryTimerId);
            discoveryTimerId = undefined;
        }
        if (blackScreenRafId !== undefined) {
            cancelAnimationFrame(blackScreenRafId);
            blackScreenRafId = undefined;
        }
    };

    const captureCurrentFrame = (): CanvasRenderingContext2D | null => {
        if (videoElement!.readyState < videoElement!.HAVE_ENOUGH_DATA) return null;

        const ctx = videoCopyCanvas!.getContext("2d", { willReadFrequently: true });
        if (!ctx) return null;

        // Re-sync canvas size only if the stream resolution changed mid-session
        if (
            videoCopyCanvas!.width  !== videoElement!.videoWidth ||
            videoCopyCanvas!.height !== videoElement!.videoHeight
        ) {
            videoCopyCanvas!.width  = videoElement!.videoWidth;
            videoCopyCanvas!.height = videoElement!.videoHeight;
        }

        ctx.drawImage(videoElement!, 0, 0, videoCopyCanvas!.width, videoCopyCanvas!.height);
        return ctx;
    };

    // Phase 1 - Color discovery
    const runDiscoveryTick = () => {
        discoveryTimerId = undefined;

        if (!isCapturingScreen) return;

        // Timeout guard
        if (new Date().getTime() - timeoutStartTime! >= TIMEOUT_MS) {
            console.log("Stream sync timed out during color discovery");
            // Actually stop captuing the screen and put TA back in a normal state ;)
            // I was considering adding a returnToMenu call for each player here. Could be useful
            stopCapture();
            return;
        }

        if (discoveryBusy) {
            // Still busy from last tick - reschedule and try again
            discoveryTimerId = setTimeout(runDiscoveryTick, DISCOVERY_INTERVAL_MS);
            return;
        }

        discoveryBusy = true;

        const ctx = captureCurrentFrame();
        if (!ctx) {
            discoveryBusy = false;
            discoveryTimerId = setTimeout(runDiscoveryTick, DISCOVERY_INTERVAL_MS);
            return;
        }

        // Downscale to half resolution for discovery to reduce getImageData cost,
        // while preserving aspect ratio and retaining enough detail for large colour squares.
        const srcW = videoCopyCanvas!.width;
        const srcH = videoCopyCanvas!.height;
        const halfW = Math.floor(srcW / 2);
        const halfH = Math.floor(srcH / 2);

        // Draw a half-res copy into a temporary canvas
        const halfCanvas = document.createElement("canvas");
        halfCanvas.width = halfW;
        halfCanvas.height = halfH;
        const halfCtx = halfCanvas.getContext("2d", { willReadFrequently: true })!;
        halfCtx.drawImage(videoCopyCanvas!, 0, 0, halfW, halfH);

        const imageData = halfCtx.getImageData(0, 0, halfW, halfH);

        // Scale factor to map half-res coordinates back to full-res
        const scaleX = srcW / halfW;
        const scaleY = srcH / halfH;

        const stillMissing = playersWithColors.filter(
            (p) => !foundPlayers.find((f) => f.user.guid === p.guid)
        );

        for (const player of stillMissing) {
            console.log(`Scanning for ${player.name}…`);

            const location = ColorScanner.getLocationOfSequence(
                player.colorSequence[0],
                player.colorSequence[1],
                player.colorSequence[2],
                player.colorSequence[3],
                imageData
            );

            if (location) {
                // Map center point back to full-res coordinates
                const fullResBar: ColorBar = {
                    ...location,
                    centerPoint: {
                        x: Math.round(location.centerPoint.x * scaleX),
                        y: Math.round(location.centerPoint.y * scaleY),
                    },
                };

                console.log(`Found ${player.name} at (full-res):`, fullResBar.centerPoint);

                foundPlayers = [...foundPlayers, { user: player, colorBar: fullResBar }];
            } else {
                console.log(`${player.name} not found in this frame`);
            }
        }

        const allFound = playersWithColors.every((p) => foundPlayers.find((f) => f.user.guid === p.guid));

        discoveryBusy = false;

        if (allFound) {
            console.log("All players located — switching to black-screen phase");
            // Fire-and-forget; RAF loop starts after the await chain resolves
            sendLoadBlackImageRequests().then(() => {
                blackScreenRafId = requestAnimationFrame(runBlackScreenFrame);
            });
        } else {
            // Schedule the next discovery tick — we naturally grab the latest frame then
            discoveryTimerId = setTimeout(runDiscoveryTick, DISCOVERY_INTERVAL_MS);
        }
    };

    // Phase 2 - Black screen detection
    const runBlackScreenFrame = () => {
        if (!isCapturingScreen) return;

        const ctx = captureCurrentFrame();
        if (!ctx) {
            blackScreenRafId = requestAnimationFrame(runBlackScreenFrame);
            return;
        }

        // Only check players whose delay hasn't been recorded yet
        const pending = foundPlayers.filter(
            (f) => !playersWithDelayInfo.find((d) => d.guid === f.user.guid)
        );

        for (const found of pending) {
            const { x, y } = found.colorBar.centerPoint;

            // Sample a small window (SCAN_RADIUS px each direction) and average
            const sampleData = ctx.getImageData(
                Math.max(0, x - SCAN_RADIUS),
                Math.max(0, y - SCAN_RADIUS),
                SCAN_RADIUS * 2,
                SCAN_RADIUS * 2
            );

            if (isRegionBlack(sampleData)) {
                delayFound(found.user);
            }
        }

        // Timeout guard
        if (new Date().getTime() - timeoutStartTime! >= TIMEOUT_MS) {
            console.log("Stream sync timed out during black-screen detection");
            stopCapture();
            return;
        }

        const allDelaysFound = playersWithColors.every(
            (p) => playersWithDelayInfo.find((d) => d.guid === p.guid)
        );

        if (!allDelaysFound) {
            blackScreenRafId = requestAnimationFrame(runBlackScreenFrame);
        }
        // If all found, delayFound() will have already called stopCapture()
    };

    // Returns true if the averaged colour of a sampled region is close to black
    const isRegionBlack = (imageData: ImageData): boolean => {
        const { data, width, height } = imageData;
        let totalR = 0, totalG = 0, totalB = 0;
        const pixelCount = width * height;

        for (let i = 0; i < data.length; i += 4) {
            totalR += data[i];
            totalG += data[i + 1];
            totalB += data[i + 2];
        }

        const avgR = totalR / pixelCount;
        const avgG = totalG / pixelCount;
        const avgB = totalB / pixelCount;

        // Allow a small threshold to handle compression artefacts
        return avgR < 16 && avgG < 16 && avgB < 16;
    };

    const sendLoadColorbarRequests = async () => {
        for (const player of playersWithColors) {
            console.log(`Generating colour bar for ${player.name}`);
            const bitmap = await generateColorBitmap(player.colorSequence);
            await $taService.sendLoadImageRequest(
                serverAddress, serverPort, tournamentId,
                bitmap!, [player.guid]
            );
        }

        await $taService.sendShowImageCommand(
            serverAddress, serverPort, tournamentId,
            playersWithColors.map((p) => p.guid)
        );
    };

    const sendLoadBlackImageRequests = async () => {
        const bitmap = await generateBlackBitmap();
        console.log("Sending black image to all found players");

        await $taService.sendLoadImageRequest(
            serverAddress, serverPort, tournamentId,
            bitmap!, foundPlayers.map((f) => f.user.guid)
        );

        measureStartTime = new Date().getTime();

        await $taService.sendShowImageCommand(
            serverAddress, serverPort, tournamentId,
            foundPlayers.map((f) => f.user.guid)
        );
    };

    const delayFound = (user: UserWithColors) => {
        user.streamDelayMs = BigInt(new Date().getTime() - measureStartTime!);
        console.log(`${user.name} delay: ${user.streamDelayMs}ms`);

        playersWithDelayInfo = [...playersWithDelayInfo, user];

        const allDelaysFound = playersWithColors.every(
            (p) => playersWithDelayInfo.find((d) => d.guid === p.guid)
        );

        if (!allDelaysFound) return;

        console.log("All delays found — firing sync commands");

        const maxDelay = playersWithDelayInfo.reduce((max, p) => (p.streamDelayMs! > max ? p.streamDelayMs! : max), 0n);

        for (const player of playersWithDelayInfo) {
            const offsetMs = Number(maxDelay) - Number(player.streamDelayMs);
            setTimeout(() => {
                $taService.sendStreamSyncFinishedCommand(
                    serverAddress, serverPort, tournamentId,
                    [player.guid]
                );
            }, offsetMs);
        }

        stopCapture();
    };

    const generateColorBitmap = async (colors: Color[]): Promise<Uint8Array | undefined> => {
        const ctx = colorGenerationCanvas!.getContext("2d");
        if (!ctx) {
            console.error("Unable to get canvas context for colour bitmap");
            return;
        }

        colorGenerationCanvas!.width = 1920;
        colorGenerationCanvas!.height = 1080;

        const squareSize = 100;
        const gap = 300;
        const groupWidth = colors.length * squareSize;
        const totalWidth = groupWidth + gap;

        for (let y = 0; y + squareSize <= colorGenerationCanvas!.height; y += squareSize + gap) {
            for (let x = 0; x + groupWidth <= colorGenerationCanvas!.width; x += totalWidth) {
                for (let i = 0; i < colors.length; i++) {
                    ctx.fillStyle = colors[i].toString();
                    ctx.fillRect(x + i * squareSize, y, squareSize, squareSize);
                }
            }
        }

        const blob = await new Promise<Blob | null>((resolve) =>
            colorGenerationCanvas!.toBlob(resolve)
        );
        return new Uint8Array(await blob!.arrayBuffer());
    };

    const generateBlackBitmap = async (): Promise<Uint8Array | undefined> => {
        const ctx = colorGenerationCanvas!.getContext("2d");
        if (!ctx) {
            console.error("Unable to get canvas context for black bitmap");
            return;
        }

        colorGenerationCanvas!.width  = 1920;
        colorGenerationCanvas!.height = 1080;

        ctx.fillStyle = "#000000";
        ctx.fillRect(0, 0, 1920, 1080);

        const blob = await new Promise<Blob | null>((resolve) =>
            colorGenerationCanvas!.toBlob(resolve)
        );
        return new Uint8Array(await blob!.arrayBuffer());
    };

    function srcObject(node: HTMLVideoElement, stream?: MediaStream) {
        node.srcObject = stream ?? null;
        return {
            update(newStream: MediaStream) {
                if (node.srcObject !== newStream) node.srcObject = newStream;
            },
        };
    }
</script>

<!-- svelte-ignore a11y-media-has-caption -->
<video bind:this={videoElement} use:srcObject={captureStream} autoplay playsinline hidden />
<canvas bind:this={videoCopyCanvas} hidden />
<canvas bind:this={colorGenerationCanvas} hidden />

<style lang="scss">
</style>
