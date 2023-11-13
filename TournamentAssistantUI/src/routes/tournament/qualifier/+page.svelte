<script lang="ts">
  import { onDestroy } from "svelte";
  import { page } from "$app/stores";
  import AddSong from "$lib/components/AddSong.svelte";
  import FormField from "@smui/form-field";
  import Textfield from "@smui/textfield";
  import FileDrop from "$lib/components/FileDrop.svelte";
  import { taService } from "$lib/stores";
  import {
    QualifierEvent_EventSettings,
    type QualifierEvent,
    GameplayParameters,
    Response_ResponseType,
    QualifierEvent_QualifierMap,
    GameplayModifiers_GameOptions,
    LeaderboardEntry,
    QualifierEvent_LeaderboardSort,
  } from "tournament-assistant-client";
  import Switch from "@smui/switch";
  import { onMount } from "svelte";
  import Button, { Icon, Label } from "@smui/button";
  import Fab from "@smui/fab";
  import { v4 as uuidv4 } from "uuid";
  import { slide } from "svelte/transition";
  import { goto } from "$app/navigation";
  import { Workbook } from "exceljs";
  import { saveAs } from "file-saver";
  import List, {
    Item,
    Graphic,
    Meta,
    Text,
    PrimaryText,
    SecondaryText,
  } from "@smui/list";
  import { BeatSaverService } from "$lib/services/beatSaver/beatSaverService";
  import type { SongInfo } from "$lib/services/beatSaver/songInfo";
  import Select, { Option } from "@smui/select";

  interface QualifierMapWithSongInfo extends QualifierEvent_QualifierMap {
    songInfo: SongInfo;
  }

  let serverAddress = $page.url.searchParams.get("address")!;
  let serverPort = $page.url.searchParams.get("port")!;
  let tournamentId = $page.url.searchParams.get("tournamentId")!;
  let qualifierId = $page.url.searchParams.get("qualifierId")!;

  let selectedSongId = "";
  let resultGameplayParameters: GameplayParameters | undefined = undefined;

  let editDisabled = false;

  let qualifier: QualifierEvent = {
    guid: "",
    name: "",
    guild: {
      id: "0",
      name: "dummy",
    },
    infoChannel: {
      id: "0",
      name: "dummy",
    },
    qualifierMaps: [],
    flags: 0,
    sort: 0,
    image: new Uint8Array([1]),
  };

  let downloadingCoverArtForMaps: QualifierEvent_QualifierMap[] = [];
  let qualifierMapsWithSongInfo: QualifierMapWithSongInfo[] = [];

  // This chaotic function handles the automatic downloading of cover art. Potentially worth revisiting...
  // It's called a number of times due to using both `qualifier` and `downloadingCoverArtForMaps` on the
  // right-hand side of assignments inside. It still manages to avoid spamming the BeatSaver api though,
  // so... Meh?
  $: {
    const updateCoverArt = async () => {
      // We don't want to spam the API with requests if we don't have to, so we'll reuse maps we already have
      let missingItems = qualifier.qualifierMaps.filter(
        (x) =>
          qualifierMapsWithSongInfo.find(
            (y) =>
              y.gameplayParameters?.beatmap?.levelId ===
              x.gameplayParameters?.beatmap?.levelId
          ) === undefined
      );

      console.log({ missingItems });

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

      let addedItems: QualifierMapWithSongInfo[] = [];

      for (let item of missingItems) {
        const songInfo = await BeatSaverService.getSongInfoByHash(
          item.gameplayParameters!.beatmap!.levelId.substring(
            "custom_level_".length
          )
        );

        if (songInfo) {
          addedItems.push({
            ...item,
            songInfo,
          });
        }
      }

      console.log({ addedItems });

      // Merge added items into qualifierMapsWithSongInfo while removing items that have also been removed from the qualifier model
      qualifierMapsWithSongInfo = [
        ...qualifierMapsWithSongInfo,
        ...addedItems,
      ].filter((x) =>
        qualifier.qualifierMaps.map((y) => y.guid).includes(x.guid)
      );

      // Remove the items that have downloaded from the in-progress list
      downloadingCoverArtForMaps = downloadingCoverArtForMaps.filter(
        (x) => !addedItems.find((y) => x.guid === y.guid)
      );
    };

    console.log("updateCoverArt");
    updateCoverArt();
  }

  onMount(async () => {
    console.log("onMount joinTournament/getQualifier");

    await $taService.joinTournament(serverAddress, serverPort, tournamentId);

    await onQualifierChanged();
  });

  async function onQualifierChanged() {
    if (qualifierId) {
      const newQualifier = await $taService.getQualifier(
        serverAddress,
        serverPort,
        tournamentId,
        qualifierId
      );

      // If the change was deleting the qualifier, throw us back out of this page
      if (newQualifier) {
        qualifier = newQualifier;
      } else {
        returnToQualifierSelection();
      }
    }
  }

  //When changes happen, re-render
  $taService.subscribeToQualifierUpdates(onQualifierChanged);
  onDestroy(() => {
    $taService.unsubscribeFromQualifierUpdates(onQualifierChanged);
  });

  //Don't allow creation unless we have all the required fields
  // let canCreate = false;
  // $: if (qualifier.name.length > 0) {
  //   canCreate = true;
  // }

  const returnToQualifierSelection = () => {
    goto(
      `/tournament/qualifier-select?tournamentId=${tournamentId}&address=${serverAddress}&port=${serverPort}`
    );
  };

  const createQualifier = async () => {
    await $taService.createQualifier(
      serverAddress,
      serverPort,
      tournamentId,
      qualifier
    );

    // Bounce back out to selection so that when it's clicked again, we have the right query params
    // TODO: can probably do history rewriting instead of this
    returnToQualifierSelection();
  };

  const updateQualifier = async () => {
    // We only want realtime updates on qualifiers that already exist, so if there's
    // no qualifierId in the path, we'll hold off on this
    if (qualifierId) {
      await $taService.updateQualifier(
        serverAddress,
        serverPort,
        tournamentId,
        qualifier
      );
    }
  };

  const deleteQualifier = async () => {
    // We only want realtime updates on qualifiers that already exist, so if there's
    // no qualifierId in the path, we'll hold off on this
    if (qualifierId) {
      await $taService.deleteQualifier(
        serverAddress,
        serverPort,
        tournamentId,
        qualifier
      );
    }
  };

  const onAddClicked = async (
    showScoreboard: boolean,
    disablePause: boolean,
    disableFail: boolean,
    disableScoresaberSubmission: boolean,
    disableCustomNotesOnStream: boolean,
    attempts: number
  ) => {
    console.log({ oldMaps: qualifier.qualifierMaps });

    qualifier.qualifierMaps = [
      ...qualifier.qualifierMaps,
      {
        guid: uuidv4(),
        gameplayParameters: resultGameplayParameters,
        disablePause,
        attempts,
      },
    ];

    console.log({ newMaps: qualifier.qualifierMaps });

    await updateQualifier();

    selectedSongId = "";
  };

  const onRemoveClicked = async (map: QualifierMapWithSongInfo) => {
    qualifier.qualifierMaps = qualifier.qualifierMaps.filter(
      (x) => x.guid !== map.guid
    );

    await updateQualifier();
  };

  const onGetScoresClicked = async () => {
    const workbook = new Workbook();

    for (let map of qualifier.qualifierMaps) {
      //let sanitizationRegex = new RegExp("[\[/\?'\]\*:]");
      // TODO: Revisit this with regex. Regex was being dumb
      const sanitizedWorksheetName = map.gameplayParameters?.beatmap?.name
        .replaceAll("[", "")
        .replaceAll("]", "")
        .replaceAll("?", "")
        .replaceAll(":", "")
        .replaceAll("*", "")
        .replaceAll("/", "")
        .replaceAll("\\", "");
      const worksheet = workbook.addWorksheet(sanitizedWorksheetName);

      const scoresResponse = await $taService.getLeaderboard(
        serverAddress,
        serverPort,
        qualifier.guid,
        map.guid
      );

      if (
        scoresResponse.type === Response_ResponseType.Success &&
        scoresResponse.details.oneofKind === "leaderboardEntries"
      ) {
        const getScoreValueByQualifierSettings = (
          score: LeaderboardEntry,
          sort: QualifierEvent_LeaderboardSort
        ) => {
          switch (sort) {
            case QualifierEvent_LeaderboardSort.NotesMissed:
            case QualifierEvent_LeaderboardSort.NotesMissedAscending:
              return score.notesMissed;
            case QualifierEvent_LeaderboardSort.BadCuts:
            case QualifierEvent_LeaderboardSort.BadCutsAscending:
              return score.badCuts;
            case QualifierEvent_LeaderboardSort.GoodCuts:
            case QualifierEvent_LeaderboardSort.GoodCutsAscending:
              return score.goodCuts;
            case QualifierEvent_LeaderboardSort.MaxCombo:
            case QualifierEvent_LeaderboardSort.MaxComboAscending:
              return score.maxCombo;
            default:
              return score.modifiedScore;
          }
        };

        for (let score of scoresResponse.details.leaderboardEntries.scores) {
          worksheet.addRow([
            getScoreValueByQualifierSettings(score, qualifier.sort),
            score.modifiedScore,
            score.accuracy,
            score.maxCombo,
            score.notesMissed,
            score.badCuts,
            score.goodCuts,
            score.username,
            score.fullCombo ? "FC" : "",
          ]);
        }
      }
    }

    const buffer = await workbook.xlsx.writeBuffer();

    saveAs(new Blob([buffer]), "Leaderboards.xlsx");
  };

  function getSelectedEnumMembers<T extends Record<keyof T, number>>(
    enumType: T,
    value: number
  ): Extract<keyof T, string>[] {
    function hasFlag(value: number, flag: number): boolean {
      return (value & flag) === flag;
    }

    const selectedMembers: Extract<keyof T, string>[] = [];
    for (const member in enumType) {
      if (hasFlag(value, enumType[member])) {
        selectedMembers.push(member);
      }
    }
    return selectedMembers;
  }

  function debounce<T extends unknown[], U>(
    callback: (...args: T) => PromiseLike<U> | U,
    wait: number
  ) {
    let state:
      | undefined
      | {
          timeout: ReturnType<typeof setTimeout>;
          promise: Promise<U>;
          resolve: (value: U | PromiseLike<U>) => void;
          reject: (value: any) => void;
          latestArgs: T;
        } = undefined;

    return (...args: T): Promise<U> => {
      if (!state) {
        state = {} as any;
        state!.promise = new Promise((resolve, reject) => {
          state!.resolve = resolve;
          state!.reject = reject;
        });
      }
      clearTimeout(state!.timeout);
      state!.latestArgs = args;
      state!.timeout = setTimeout(() => {
        const s = state!;
        state = undefined;
        try {
          s.resolve(callback(...s.latestArgs));
        } catch (e) {
          s.reject(e);
        }
      }, wait);

      return state!.promise;
    };
  }

  $: console.log({ qualifierMapsWithSongInfo });
</script>

<div class="page">
  <div class="qualifier-title">
    Select a song, difficulty, and characteristic
  </div>

  <div class="grid">
    <div class="column">
      <div class="cell">
        <Textfield
          bind:value={qualifier.name}
          on:input={updateQualifier}
          variant="outlined"
          label="Qualifier Name"
          disabled={editDisabled}
        />
      </div>
      <div class="cell">
        <FileDrop
          onFileSelected={async (file) => {
            const loadedImage = await file?.arrayBuffer();

            qualifier.image = loadedImage
              ? new Uint8Array(loadedImage)
              : new Uint8Array([1]);
          }}
          disabled={editDisabled}
        />
      </div>
      <div class="cell">
        <Button on:click={deleteQualifier}>End Qualifier</Button>
      </div>
      <div class="cell">
        <Button on:click={onGetScoresClicked}>Get Qualifier Scores</Button>
      </div>
    </div>
    <div class="column">
      <!-- null check of qualifier.infoChannel, not conditional textfield -->
      {#if qualifier.infoChannel}
        <div class="cell">
          <Textfield
            bind:value={qualifier.infoChannel.id}
            on:input={updateQualifier}
            variant="outlined"
            label="Leaderboard Channel ID"
            disabled={editDisabled}
          />
        </div>
      {/if}
      <div class="cell qualifier-toggles">
        <FormField>
          <Switch
            checked={(qualifier.flags &
              QualifierEvent_EventSettings.HideScoresFromPlayers) ===
              QualifierEvent_EventSettings.HideScoresFromPlayers}
            on:SMUISwitch:change={(e) => {
              if (e.detail.selected) {
                qualifier.flags |=
                  QualifierEvent_EventSettings.HideScoresFromPlayers;
              } else {
                qualifier.flags &=
                  ~QualifierEvent_EventSettings.HideScoresFromPlayers;
              }

              updateQualifier();
            }}
          />
          <span slot="label">Hide scores from players</span>
        </FormField>
        <FormField>
          <Switch
            checked={(qualifier.flags &
              QualifierEvent_EventSettings.DisableScoresaberSubmission) ===
              QualifierEvent_EventSettings.DisableScoresaberSubmission}
            on:SMUISwitch:change={(e) => {
              if (e.detail.selected) {
                qualifier.flags |=
                  QualifierEvent_EventSettings.DisableScoresaberSubmission;
              } else {
                qualifier.flags &=
                  ~QualifierEvent_EventSettings.DisableScoresaberSubmission;
              }

              updateQualifier();
            }}
          />
          <span slot="label">Disable Scoresaber submission</span>
        </FormField>
        <FormField>
          <Switch
            checked={(qualifier.flags &
              QualifierEvent_EventSettings.EnableDiscordLeaderboard) ===
              QualifierEvent_EventSettings.EnableDiscordLeaderboard}
            on:SMUISwitch:change={(e) => {
              if (e.detail.selected) {
                qualifier.flags |=
                  QualifierEvent_EventSettings.EnableDiscordLeaderboard;
              } else {
                qualifier.flags &=
                  ~QualifierEvent_EventSettings.EnableDiscordLeaderboard;
              }

              updateQualifier();
            }}
          />
          <span slot="label">Enable discord bot leaderboard</span>
        </FormField>
        <FormField>
          <Switch
            checked={(qualifier.flags &
              QualifierEvent_EventSettings.EnableDiscordScoreFeed) ===
              QualifierEvent_EventSettings.EnableDiscordScoreFeed}
            on:SMUISwitch:change={(e) => {
              if (e.detail.selected) {
                qualifier.flags |=
                  QualifierEvent_EventSettings.EnableDiscordScoreFeed;
              } else {
                qualifier.flags &=
                  ~QualifierEvent_EventSettings.EnableDiscordScoreFeed;
              }

              updateQualifier();
            }}
          />
          <span slot="label">Enable discord bot score feed</span>
        </FormField>
        <Select
          variant="outlined"
          bind:value={qualifier.sort}
          label="Leaderboard Sort Type"
          key={(option) => `${option}`}
          class="sort-type"
        >
          {#each Object.keys(QualifierEvent_LeaderboardSort) as sortType}
            {#if Number(sortType) >= 0}
              <Option value={Number(sortType)}>
                {QualifierEvent_LeaderboardSort[Number(sortType)]}
              </Option>
            {/if}
          {/each}
        </Select>
      </div>
    </div>
  </div>
  <div class="song-list-container">
    <List threeLine avatarList singleSelection>
      {#each qualifierMapsWithSongInfo as map}
        <Item class="preview-item">
          <Graphic
            style="background-image: url({BeatSaverService.currentVersion(
              map.songInfo
            )?.coverURL}); background-size: contain"
          />
          <Text>
            <PrimaryText>{map.songInfo.name}</PrimaryText>
            <SecondaryText>
              {map.songInfo.metadata.levelAuthorName}
            </SecondaryText>
            <!-- more null checks -->
            {#if map.gameplayParameters && map.gameplayParameters.gameplayModifiers}
              <SecondaryText>
                {map.attempts > 0
                  ? `${map.attempts} attempts - `
                  : ""}{map.disablePause
                  ? "Disable Pause - "
                  : ""}{getSelectedEnumMembers(
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
                  .join(" - ")}
              </SecondaryText>
            {/if}
          </Text>
          <Meta class="material-icons" on:click={() => onRemoveClicked(map)}>
            close
          </Meta>
        </Item>
      {/each}
    </List>
    <div class="song-list-addsong">
      <AddSong
        {serverAddress}
        {serverPort}
        {tournamentId}
        bind:selectedSongId
        bind:resultGameplayParameters
        {onAddClicked}
      />
    </div>
  </div>

  {#if !qualifierId}
    <div class="create-qualifier-button-container" transition:slide>
      <Fab color="primary" on:click={createQualifier} extended>
        <Icon class="material-icons">add</Icon>
        <Label>Create Qualifier</Label>
      </Fab>
    </div>
  {/if}
</div>

<style lang="scss">
  .page {
    display: flex;
    flex-direction: column;
    align-items: center;
    margin-bottom: 70px;

    .grid {
      margin-top: 10px;
      display: flex;
      max-width: 700px;

      .column {
        width: 350px;

        .cell {
          padding: 5px;
        }

        .qualifier-toggles {
          margin: 5px;
          border: 1px solid var(--mdc-theme-text-secondary-on-background);
          border-radius: 5px;

          :global(.sort-type) {
            padding-top: 10px;
          }
        }
      }
    }

    .song-list-container {
      margin-top: 10px;
      max-width: 700px;
      width: -webkit-fill-available;
      background-color: rgba($color: #000000, $alpha: 0.1);
      border-radius: 5px;

      .song-list-addsong {
        margin: 0 10px 10px 10px;
      }
    }

    .qualifier-title {
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

    .create-qualifier-button-container {
      position: fixed;
      bottom: 2vmin;
      right: 2vmin;
    }
  }
</style>
