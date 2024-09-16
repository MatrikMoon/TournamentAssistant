<script lang="ts">
  import { getAllLevels, isOstName } from "$lib/services/ostService";
  import {
    GameplayModifiers_GameOptions,
    PlayerSpecificSettings_ArcVisibilityType,
    PlayerSpecificSettings_NoteJumpDurationTypeSettings,
    PlayerSpecificSettings_PlayerOptions,
    Tournament_TournamentSettings_Pool,
    type GameplayParameters,
  } from "tournament-assistant-client";
  import { Icon } from "@smui/button";
  import { slide } from "svelte/transition";
  import Autocomplete from "@smui-extra/autocomplete";
  import CircularProgress from "@smui/circular-progress";
  import { BeatSaverService } from "$lib/services/beatSaver/beatSaverService";
  import type { SongInfo, SongInfos } from "$lib/services/beatSaver/songInfo";
  import Paper from "@smui/paper";
  import Fab from "@smui/fab";
  import { Input } from "@smui/textfield";
  import Tooltip, { Wrapper } from "@smui/tooltip";
  import { PlaylistService } from "$lib/services/bplist/playlistService";
  import type { Playlist } from "$lib/services/bplist/playlist";
  import SelectPoolDialog from "$lib/dialogs/SelectPoolDialog.svelte";
  import EditSong from "./EditSong.svelte";

  export let tournamentId: string;
  export let selectedSongId = "";
  export let downloadError = false;
  export let gameplayParameters: GameplayParameters[] | undefined = undefined;
  export let onSongsAdded = (result: GameplayParameters[]) => {};
  export let showMatchOnlyOptions = true;
  export let showQualifierOnlyOptions = true;

  let fileInput: HTMLInputElement | undefined;
  let playlist: Playlist | undefined;
  let addingPlaylistOrPool = false;
  let downloadedPlaylistOrPool = false;
  let selectMapPoolDialogOpen = false;

  let songInfoList: SongInfo[] = [];
  let downloading = false;
  $: expanded =
    songInfoList.length > 0 &&
    (selectedSongId.length > 0 ||
      (addingPlaylistOrPool && downloadedPlaylistOrPool));

  const downloadSongAndAddToList = async (
    songId: string,
    songInfo: SongInfo | undefined = undefined,
  ) => {
    if (isOstName(songId)) {
    } else {
      downloading = true;

      try {
        if (!songInfo) {
          if (songId.length === 40) {
            songInfo = await BeatSaverService.getSongInfoByHash(songId);
          } else {
            songInfo = await BeatSaverService.getSongInfo(songId);
          }
        }

        songInfoList = [...songInfoList, songInfo];
        const currentVersion = BeatSaverService.currentVersion(songInfo);

        // Get the default characteristic. Prefer "Standard", or choose
        // first if it doesn't exist
        const characteristics = BeatSaverService.characteristics(songInfo);
        const selectedCharacteristic =
          characteristics.find((x) => x === "Standard") ?? characteristics[0];

        // Get the default difficulty. Prefer ExpertPlus
        const selectedDifficulty =
          BeatSaverService.getClosestDifficultyPreferLower(
            songInfo,
            selectedCharacteristic,
            "ExpertPlus",
          );

        // Add acquired info to results list
        gameplayParameters = [
          ...(gameplayParameters ?? []),
          {
            beatmap: {
              name: songInfo.name,
              levelId: `custom_level_${currentVersion!.hash.toUpperCase()}`,
              characteristic: {
                serializedName: selectedCharacteristic ?? "",
                difficulties: [],
              },
              difficulty: BeatSaverService.getDifficultyAsNumber(
                selectedDifficulty!,
              ),
            },
            playerSettings: {
              playerHeight: 0,
              sfxVolume: 0,
              saberTrailIntensity: 0,
              noteJumpStartBeatOffset: 0,
              noteJumpFixedDuration: 0,
              options: PlayerSpecificSettings_PlayerOptions.NoPlayerOptions,
              noteJumpDurationTypeSettings:
                PlayerSpecificSettings_NoteJumpDurationTypeSettings.Dynamic,
              arcVisibilityType: PlayerSpecificSettings_ArcVisibilityType.None,
            },
            gameplayModifiers: {
              options: GameplayModifiers_GameOptions.None,
            },
            attempts: 0,
            showScoreboard: false,
            disablePause: false,
            disableFail: false,
            disableScoresaberSubmission: false,
            disableCustomNotesOnStream: false,
            useSync: false,
          },
        ];

        downloadError = false;
      } catch (e) {
        console.error(e);
        downloadError = true;
      }

      downloading = false;
    }
  };

  const onDownloadClicked = async () => {
    selectedSongId = BeatSaverService.sanitizeSongId(selectedSongId);
    downloadSongAndAddToList(selectedSongId);
  };

  const onLoadFromPlaylistClicked = async () => {
    fileInput?.addEventListener("change", handleFileChange);
    fileInput?.click();
  };

  const onLoadFromPoolClicked = async () => {
    selectMapPoolDialogOpen = !selectMapPoolDialogOpen;
  };

  const onPoolSelected = async (pool: Tournament_TournamentSettings_Pool) => {
    selectMapPoolDialogOpen = false;
    downloadedPlaylistOrPool = false;
    addingPlaylistOrPool = true;

    try {
      for (let song of pool.maps) {
        gameplayParameters = [
          ...(gameplayParameters ?? []),
          song.gameplayParameters!,
        ];

        onSongsAdded(gameplayParameters);
        onInputChanged();
        selectedSongId = "";
      }
      downloadedPlaylistOrPool = true;
    } catch (e) {
      console.error(e);
      addingPlaylistOrPool = false;
    }
  };

  const handleFileChange = async (event: Event) => {
    downloadedPlaylistOrPool = false;
    addingPlaylistOrPool = true;

    try {
      const files = (event.target as HTMLInputElement).files;
      if (files && files.length > 0) {
        playlist = await PlaylistService.loadPlaylist(files[0]);

        // To avoid absolutely crushing the beatsaver api, we'll batch requests
        // The /maps/ids endpoint has a max size of 50
        const chunkSize = 50;

        for (let i = 0; i < playlist.songs.length; i += chunkSize) {
          const chunk = playlist.songs
            .slice(i, i + chunkSize)
            .map((x) => x.hash);

          let songInfo: SongInfo | undefined;
          let songInfos: SongInfos | undefined;

          // Endpoint returns a single object if only one song is requested
          if (chunk.length === 1) {
            songInfo = await BeatSaverService.getSongInfoByHash(chunk[0]);
          } else {
            songInfos = await BeatSaverService.getSongInfosByHash(chunk);
          }

          for (let hash of chunk) {
            songInfo = songInfos?.[hash.toLowerCase()] ?? songInfo;
            await downloadSongAndAddToList(hash.toLowerCase()!, songInfo);
          }
        }
      }

      downloadedPlaylistOrPool = true;
    } catch (e) {
      console.error(e);
      addingPlaylistOrPool = false;
    }
  };

  const onInputChanged = () => {
    // When the input changes, reset the loaded song info and chosen settings
    songInfoList = [];
    gameplayParameters = undefined;
    playlist = undefined;
    downloadError = false;
    addingPlaylistOrPool = false;
    downloadedPlaylistOrPool = false;
  };
</script>

<SelectPoolDialog
  bind:open={selectMapPoolDialogOpen}
  {tournamentId}
  onPoolClicked={onPoolSelected}
/>

<div class="add-song">
  <Wrapper>
    <Paper
      class={downloadError ? "text-box-paper invalid" : "text-box-paper"}
      elevation={6}
    >
      <div class="text-box-input-group">
        <div class="search-icon">
          <Icon class="material-icons">search</Icon>
        </div>
        <div class="search-autocomplete">
          <Autocomplete
            bind:text={selectedSongId}
            options={getAllLevels()}
            getOptionLabel={(option) => option?.levelName ?? ""}
            label="Song ID"
            combobox
            textfield$variant="outlined"
          >
            <Input
              bind:value={selectedSongId}
              on:input={onInputChanged}
              placeholder="Song ID"
              class="text-box-input"
            />
          </Autocomplete>
        </div>
        <div class="action-buttons">
          {#if selectedSongId.length === 0 && !downloadedPlaylistOrPool}
            <Wrapper>
              <div
                class="add-from-playlist-fab"
                in:slide={{ axis: "x", delay: 250 }}
                out:slide={{ axis: "x" }}
              >
                <input
                  type="file"
                  bind:this={fileInput}
                  accept=".bplist"
                  hidden
                />
                <Fab
                  color="primary"
                  mini
                  on:click={() => {
                    if (!addingPlaylistOrPool) {
                      onLoadFromPlaylistClicked();
                    }
                  }}
                >
                  {#if addingPlaylistOrPool && !downloadedPlaylistOrPool}
                    <CircularProgress
                      style="height: 32px; width: 32px;"
                      indeterminate
                    />
                  {:else}
                    <Icon class="material-icons">playlist_add</Icon>
                  {/if}
                </Fab>
                <Tooltip>Load from Playlist</Tooltip>
              </div>
            </Wrapper>

            <Wrapper>
              {#if !addingPlaylistOrPool || downloadedPlaylistOrPool}
                <div
                  class="add-from-pool-fab"
                  in:slide={{ axis: "x", delay: 250 }}
                  out:slide={{ axis: "x" }}
                >
                  <input
                    type="file"
                    bind:this={fileInput}
                    accept=".bplist"
                    hidden
                  />
                  <Fab color="primary" mini on:click={onLoadFromPoolClicked}>
                    <Icon class="material-icons">pool</Icon>
                  </Fab>
                  <Tooltip>Load from Map Pool</Tooltip>
                </div>
              {/if}
            </Wrapper>
          {/if}
          {#if selectedSongId.length > 0 && !addingPlaylistOrPool}
            <div
              class="download-fab"
              in:slide={{ axis: "x", delay: 250 }}
              out:slide={{ axis: "x" }}
            >
              <Fab
                color="primary"
                mini
                on:click={() => {
                  if (!downloading) {
                    onDownloadClicked();
                  }
                }}
              >
                {#if downloading}
                  <CircularProgress
                    style="height: 32px; width: 32px;"
                    indeterminate
                  />
                {:else}
                  <Icon class="material-icons">arrow_downward</Icon>
                {/if}
              </Fab>
            </div>
          {/if}
        </div>
      </div>

      <!-- expanded implies songInfo, but for svelte to compile the each loop we need to assert it's not undefined-->
      {#if expanded}
        <EditSong
          bind:gameplayParameters
          {showMatchOnlyOptions}
          {showQualifierOnlyOptions}
          {songInfoList}
          {addingPlaylistOrPool}
          onSongsAdded={(result) => {
            onInputChanged();
            selectedSongId = "";
            onSongsAdded(result);
          }}
        />
      {/if}
    </Paper>
    <!-- Already tried doing this by removing the element with svelte, and it threw
         a DOM error. So now it's done with a CSS class. It works, don't touch it -->
    <Tooltip class={downloadError ? "" : "tooltip-hidden"}>
      Is that song ID correct?
    </Tooltip>
  </Wrapper>
</div>

<style lang="scss">
  .add-song {
    display: flex;
    justify-content: center;
    align-items: center;

    :global(.text-box-paper.invalid) {
      background-color: rgba($color: red, $alpha: 0.1);
    }

    :global(.text-box-paper) {
      padding: 0;
      background-color: rgba($color: #000000, $alpha: 0.1);
      min-width: min-content;
      width: -webkit-fill-available;

      :global(.text-box-input-group) {
        display: flex;
        align-items: center;
        flex-grow: 1;
        margin: 0 12px;
        padding: 0 12px;
        height: 48px;

        // Remove the shadow we've set for all Autocompletes in app.scss
        :global(.smui-autocomplete) {
          box-shadow: none;
        }

        :global(> *) {
          display: inline-block;
          margin: 0 12px;
        }

        :global(.text-box-input::placeholder) {
          color: var(--mdc-theme-text-secondary-on-background);
          opacity: 0.6;
        }
      }
    }

    .search-icon {
      margin-top: 5px; // Again don't ask
      color: var(--mdc-theme-text-secondary-on-background);
    }

    .search-autocomplete {
      width: -webkit-fill-available;
    }

    .action-buttons {
      // The following two are needed to make the transition look nice
      display: flex;
      align-items: center;
      margin: 0;

      .download-fab,
      .add-from-playlist-fab,
      .add-from-pool-fab {
        margin-left: 5px;
        :global(circle) {
          stroke: white;
        }
      }
    }

    :global(.preview-list) {
      padding: 0;

      :global(.preview-item) {
        background-color: rgba($color: #000000, $alpha: 0.1);
      }
    }
  }

  :global(.tooltip-hidden) {
    display: none;
  }
</style>
