<script lang="ts">
  import {
    GameplayModifiers_GameOptions,
    Map,
  } from "tournament-assistant-client";
  import List, { Item, Graphic, Meta, Text, SecondaryText } from "@smui/list";
  import { BeatSaverService } from "$lib/services/beatSaver/beatSaverService";
  import type { MapWithSongInfo } from "../../lib/globalTypes";
  import { getBadgeTextFromDifficulty, getSelectedEnumMembers } from "../utils";
  import CircularProgress from "@smui/circular-progress";
  import type { SongInfo, SongInfos } from "$lib/services/beatSaver/songInfo";

  export let edit = false;
  export let showTarget = false;
  export let maps: Map[];
  export let mapsWithSongInfo: MapWithSongInfo[] = [];
  export let onItemClicked: (map: MapWithSongInfo) => Promise<void> = async (
    map: MapWithSongInfo
  ) => {};
  export let onEditClicked: (map: MapWithSongInfo) => Promise<void> = async (
    map: MapWithSongInfo
  ) => {};
  export let onRemoveClicked: (map: MapWithSongInfo) => Promise<void>;

  let downloadingCoverArtForMaps: Map[] = [];
  let progressTarget = 0;
  let progressCurrent = 0;

  $: {
    // Info for progress spinner
    progressTarget = maps.length;
    updateProgress();
  }

  // This is broken off from the above to avoid reactivity on mapsWithSongInfo
  const updateProgress = () => {
    progressCurrent = mapsWithSongInfo.filter((x) =>
      maps.map((x) => x.guid).includes(x.guid)
    ).length;
  };

  // This chaotic function handles the automatic downloading of cover art. Potentially worth revisiting...
  // It's called a number of times due to using both `qualifier` and `downloadingCoverArtForMaps` on the
  // right-hand side of assignments inside. It still manages to avoid spamming the BeatSaver api though,
  // so... Meh?
  $: {
    const updateCoverArt = async () => {
      // We don't want to spam the API with requests if we don't have to, so we'll reuse maps we already have
      let missingItems = maps.filter(
        (x) => mapsWithSongInfo.find((y) => y.guid === x.guid) === undefined
      );

      // This function may trigger rapidly, and includes an async action below, so if there's any currently
      // downloading cover art, we should ignore it and let the existing download finish
      missingItems = missingItems.filter(
        (x) => !downloadingCoverArtForMaps.find((y) => x.guid === y.guid)
      );

      // Now, we *are* going to download whatever's left, so we should go ahead and add it to the downloading list
      downloadingCoverArtForMaps = [
        ...downloadingCoverArtForMaps,
        ...missingItems,
      ];

      let addedItems: MapWithSongInfo[] = [];

      // To avoid absolutely crushing the beatsaver api, we'll batch requests
      // The /maps/ids endpoint has a max size of 50
      const chunkSize = 50;

      for (let i = 0; i < missingItems.length; i += chunkSize) {
        const chunk = missingItems
          .slice(i, i + chunkSize)
          .map((x) =>
            x.gameplayParameters!.beatmap!.levelId.substring(
              "custom_level_".length
            )
          );

        let songInfo: SongInfo | undefined;
        let songInfos: SongInfos | undefined;

        // Endpoint returns a single object if only one song is requested
        if (chunk.length === 1) {
          songInfo = await BeatSaverService.getSongInfoByHash(chunk[0]);
        } else {
          songInfos = await BeatSaverService.getSongInfosByHash(chunk);
        }

        for (let hash of chunk) {
          const map = missingItems.find(
            (x) =>
              x.gameplayParameters!.beatmap!.levelId.substring(
                "custom_level_".length
              ) === hash
          )!;

          songInfo = songInfos?.[hash.toLowerCase()] ?? songInfo;

          if (songInfo) {
            addedItems.push({
              ...map,
              songInfo,
            });

            // Increment progress
            progressCurrent++;
          }
        }
      }

      // Merge added items into mapsWithSongInfo while removing items that have also been removed from the qualifier model
      mapsWithSongInfo = [...mapsWithSongInfo, ...addedItems].filter((x) =>
        maps.map((y) => y.guid).includes(x.guid)
      );

      // Remove the items that have downloaded from the in-progress list
      downloadingCoverArtForMaps = downloadingCoverArtForMaps.filter(
        (x) => !addedItems.find((y) => x.guid === y.guid)
      );
    };

    updateCoverArt();
  }
</script>

{#if progressTarget > 0 && progressTarget !== progressCurrent}
  <div class="progress">
    <CircularProgress
      style="height: 48px; width: 48px;"
      progress={progressCurrent / progressTarget}
    />
  </div>
{/if}

<List threeLine avatarList singleSelection>
  {#each mapsWithSongInfo as map}
    <Item
      class="preview-item"
      on:SMUI:action={() => onItemClicked !== undefined && onItemClicked(map)}
    >
      <Graphic
        style="background-image: url({BeatSaverService.currentVersion(
          map.songInfo
        )?.coverURL}); background-size: contain"
      />
      <Text>
        <div class="title-text">
          <!-- more null checks -->
          {#if map.gameplayParameters && map.gameplayParameters.beatmap?.difficulty !== undefined}
            <div
              class={`difficulty-badge difficulty-badge-${map.gameplayParameters?.beatmap?.difficulty}`}
            >
              {getBadgeTextFromDifficulty(
                map.gameplayParameters?.beatmap?.difficulty
              )}
            </div>
          {/if}

          {map.songInfo.name}
        </div>
        <SecondaryText>
          {map.songInfo.metadata.levelAuthorName}
        </SecondaryText>
        <!-- more null checks -->
        {#if map.gameplayParameters && map.gameplayParameters.gameplayModifiers}
          <SecondaryText>
            {[
              map.gameplayParameters.attempts > 0
                ? `${map.gameplayParameters.attempts} attempts`
                : "",
              showTarget ? `Target: ${map.gameplayParameters.target}` : "",
              map.gameplayParameters.disablePause ? "Disable Pause" : "",
              map.gameplayParameters.disableFail ? "Disable Fail" : "",
              getSelectedEnumMembers(
                GameplayModifiers_GameOptions,
                map.gameplayParameters.gameplayModifiers.options
              )
                .filter(
                  (x) =>
                    x !==
                    GameplayModifiers_GameOptions[
                      GameplayModifiers_GameOptions.None
                    ]
                )
                .map((x) => `${x}`)
                .join(" - "),
            ]
              .filter((x) => !!x)
              .join(" - ")}
          </SecondaryText>
        {/if}
      </Text>
      <div class="meta-buttons">
        {#if edit}
          <Meta
            class="material-icons"
            on:click$stopPropagation={() => onEditClicked(map)}
          >
            edit_square
          </Meta>
        {/if}
        <Meta
          class="material-icons"
          on:click$stopPropagation={() => onRemoveClicked(map)}
        >
          close
        </Meta>
      </div>
    </Item>
  {/each}
</List>

<style lang="scss">
  .progress {
    text-align: center;
  }

  .title-text {
    margin-top: 10px;
    display: flex;
    align-items: center;

    .difficulty-badge {
      margin-right: 5px;
      padding: 2px 4px;
      border-radius: 5px;

      &-0 {
        background-color: rgba($color: green, $alpha: 0.4);
      }

      &-1 {
        background-color: rgba($color: blue, $alpha: 0.4);
      }

      &-2 {
        background-color: rgba($color: orange, $alpha: 0.4);
      }

      &-3 {
        background-color: rgba($color: red, $alpha: 0.4);
      }

      &-4 {
        background-color: rgba($color: rgb(247, 0, 255), $alpha: 0.4);
      }
    }
  }

  .meta-buttons {
    display: flex;
    margin-left: auto;
  }
</style>
