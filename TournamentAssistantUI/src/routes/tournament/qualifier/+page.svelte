<script lang="ts">
  import { onDestroy } from "svelte";
  import { page } from "$app/stores";
  import AddSong from "$lib/components/add-song/AddSong.svelte";
  import FormField from "@smui/form-field";
  import Textfield from "@smui/textfield";
  import { taService } from "$lib/stores";
  import {
    QualifierEvent_EventSettings,
    type QualifierEvent,
    GameplayParameters,
    Response_ResponseType,
    LeaderboardEntry,
    QualifierEvent_LeaderboardSort,
    Tournament,
  } from "tournament-assistant-client";
  import Switch from "@smui/switch";
  import { onMount } from "svelte";
  import { Icon, Label } from "@smui/button";
  import Fab from "@smui/fab";
  import { v4 as uuidv4 } from "uuid";
  import { slide } from "svelte/transition";
  import { goto } from "$app/navigation";
  import { Workbook } from "exceljs";
  import { saveAs } from "file-saver";
  import Select, { Option } from "@smui/select";
  import type { MapWithSongInfo } from "$lib/globalTypes";
  import SongList from "$lib/components/SongList.svelte";
  import NameEdit from "$lib/components/NameEdit.svelte";
  import type { SongInfo } from "$lib/services/beatSaver/songInfo";
  import EditSongDialog from "$lib/dialogs/EditSongDialog.svelte";
  import Dialog, { Actions, Content, Header, Title } from "@smui/dialog";
  import Button from "@smui/button";

  let serverAddress = $page.url.searchParams.get("address")!;
  let serverPort = $page.url.searchParams.get("port")!;
  let tournamentId = $page.url.searchParams.get("tournamentId")!;
  let qualifierId = $page.url.searchParams.get("qualifierId")!;

  let editDisabled = false;

  let editSongDialogOpen = false;
  let editSongDialogGameplayParameters: GameplayParameters | undefined =
    undefined;
  let editSongDialogSongInfolist: SongInfo | undefined = undefined;
  let editSongDialogMapId: string | undefined = undefined;

  $: shouldShowTargetTextbox =
    qualifier?.sort === QualifierEvent_LeaderboardSort.BadCutsTarget ||
    qualifier?.sort === QualifierEvent_LeaderboardSort.GoodCutsTarget ||
    qualifier?.sort === QualifierEvent_LeaderboardSort.MaxComboTarget ||
    qualifier?.sort === QualifierEvent_LeaderboardSort.NotesMissedTarget ||
    qualifier?.sort === QualifierEvent_LeaderboardSort.ModifiedScoreTarget;

  let deleteQualifierWarningOpen = false;

  let nameUpdateTimer: NodeJS.Timeout | undefined;
  let infoChannelUpdateTimer: NodeJS.Timeout | undefined;

  let tournament: Tournament | undefined;
  let qualifier: QualifierEvent = {
    guid: "",
    name: "",
    infoChannel: {
      id: "0",
      name: "dummy",
    },
    qualifierMaps: [],
    flags: 0,
    sort: 0,
    image: new Uint8Array([1]),
  };

  let mapsWithSongInfo: MapWithSongInfo[] = [];

  onMount(async () => {
    console.log("onMount joinTournament/getQualifier");

    await $taService.joinTournament(serverAddress, serverPort, tournamentId);

    await onTournamentChanged();
    await onQualifierChanged();
  });

  async function onQualifierChanged() {
    if (qualifierId) {
      const newQualifier = await $taService.getQualifier(
        serverAddress,
        serverPort,
        tournamentId,
        qualifierId,
      );

      // If the change was deleting the qualifier, throw us back out of this page
      if (newQualifier) {
        qualifier = newQualifier;
        qualifier.infoChannel ??= {
          id: "0",
          name: "dummy",
        };
      } else {
        returnToQualifierSelection();
      }
    }
  }

  async function onTournamentChanged() {
    tournament = await $taService.getTournament(
      serverAddress,
      serverPort,
      tournamentId,
    );
  }

  //When changes happen, re-render
  $taService.subscribeToTournamentUpdates(onTournamentChanged);
  $taService.subscribeToQualifierUpdates(onQualifierChanged);
  onDestroy(() => {
    $taService.unsubscribeFromTournamentUpdates(onTournamentChanged);
    $taService.unsubscribeFromQualifierUpdates(onQualifierChanged);
  });

  //Don't allow creation unless we have all the required fields
  // let canCreate = false;
  // $: if (qualifier.name.length > 0) {
  //   canCreate = true;
  // }

  const returnToQualifierSelection = () => {
    goto(
      `/tournament/qualifier-select?tournamentId=${tournamentId}&address=${serverAddress}&port=${serverPort}`,
    );
  };

  const sanitizeStringForExcel = (name: string) => {
    return name
      .replaceAll("[", "")
      .replaceAll("]", "")
      .replaceAll("?", "")
      .replaceAll(":", "")
      .replaceAll("*", "")
      .replaceAll("/", "")
      .replaceAll("\\", "");
  };

  const createQualifier = async () => {
    await $taService.createQualifier(
      serverAddress,
      serverPort,
      tournamentId,
      qualifier,
    );

    // Bounce back out to selection so that when it's clicked again, we have the right query params
    // TODO: can probably do history rewriting instead of this
    returnToQualifierSelection();
  };

  const deleteQualifier = async () => {
    // We only want realtime updates on qualifiers that already exist, so if there's
    // no qualifierId in the path, we'll hold off on this
    if (qualifierId) {
      await $taService.deleteQualifier(
        serverAddress,
        serverPort,
        tournamentId,
        qualifierId,
      );
    }
  };

  const onSongsAdded = async (result: GameplayParameters[]) => {
    if (qualifierId) {
      await $taService.addQualifierMaps(
        serverAddress,
        serverPort,
        tournamentId,
        qualifierId,
        [
          ...result.map((x) => {
            return {
              guid: uuidv4(),
              gameplayParameters: x,
            };
          }),
        ],
      );
    } else {
      qualifier.qualifierMaps = [
        ...qualifier.qualifierMaps,
        ...result.map((x) => {
          return { guid: uuidv4(), gameplayParameters: x };
        }),
      ];
    }
  };

  const onSongUpdated = async (result: GameplayParameters) => {
    if (qualifierId) {
      await $taService.updateQualifierMap(
        serverAddress,
        serverPort,
        tournamentId,
        qualifierId,
        {
          guid: editSongDialogMapId!,
          gameplayParameters: result,
        },
      );
    } else {
      qualifier.qualifierMaps = [
        ...qualifier.qualifierMaps.filter(
          (x) => x.guid !== editSongDialogMapId!,
        ),
        {
          guid: editSongDialogMapId!,
          gameplayParameters: result,
        },
      ];
    }

    editSongDialogMapId = undefined;
    editSongDialogGameplayParameters = undefined;
    editSongDialogSongInfolist = undefined;
  };

  const onEditClicked = async (map: MapWithSongInfo) => {
    editSongDialogMapId = map.guid;
    editSongDialogGameplayParameters = map.gameplayParameters;
    editSongDialogSongInfolist = map.songInfo;
    editSongDialogOpen = true;
  };

  const onRemoveClicked = async (map: MapWithSongInfo) => {
    if (qualifierId) {
      $taService.removeQualifierMap(
        serverAddress,
        serverPort,
        tournamentId,
        qualifierId,
        map.guid,
      );
    } else {
      qualifier.qualifierMaps = qualifier.qualifierMaps.filter(
        (x) => x.guid !== map.guid,
      );
    }
  };

  const onSortOptionClicked = async (sort: QualifierEvent_LeaderboardSort) => {
    $taService.setQualifierLeaderboardSort(
      serverAddress,
      serverPort,
      tournamentId,
      qualifierId,
      sort,
    );
  };

  const debounceUpdateQualifierName = () => {
    if (qualifierId) {
      clearTimeout(nameUpdateTimer);
      nameUpdateTimer = setTimeout(async () => {
        await $taService.setQualifierName(
          serverAddress,
          serverPort,
          tournamentId,
          qualifierId,
          qualifier.name,
        );
      }, 500);
    }
  };

  const updateQualifierImage = async () => {
    if (qualifierId) {
      await $taService.setQualifierImage(
        serverAddress,
        serverPort,
        tournamentId,
        qualifierId,
        qualifier.image,
      );
    }
  };

  const debounceUpdateInfoChannel = () => {
    if (qualifierId) {
      clearTimeout(infoChannelUpdateTimer);
      infoChannelUpdateTimer = setTimeout(async () => {
        await $taService.setQualifierInfoChannel(
          serverAddress,
          serverPort,
          tournamentId,
          qualifierId,
          qualifier.infoChannel!,
        );
      }, 500);
    }
  };

  const onFlagsChanged = async () => {
    if (qualifierId) {
      await $taService.setQualifierFlags(
        serverAddress,
        serverPort,
        tournamentId,
        qualifierId,
        qualifier.flags,
      );
    }
  };

  const onGetScoresClicked = async () => {
    const workbook = new Workbook();

    for (let map of qualifier.qualifierMaps) {
      //let sanitizationRegex = new RegExp("[\[/\?'\]\*:]");
      // TODO: Revisit this with regex. Regex was being dumb
      const sanitizedWorksheetName = sanitizeStringForExcel(
        map.gameplayParameters!.beatmap!.name,
      );
      const worksheet = workbook.addWorksheet(sanitizedWorksheetName);

      const scoresResponse = await $taService.getLeaderboard(
        serverAddress,
        serverPort,
        tournamentId,
        qualifier.guid,
        map.guid,
      );

      if (
        scoresResponse.type === Response_ResponseType.Success &&
        scoresResponse.details.oneofKind === "leaderboardEntries"
      ) {
        const getScoreValueByQualifierSettings = (
          score: LeaderboardEntry,
          sort: QualifierEvent_LeaderboardSort,
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

        worksheet.addRow([
          "Score Value (by qualifier settings)",
          "Modified Score",
          "Accuracy",
          "Max Combo",
          "Notes Missed",
          "Bad Cuts",
          "Good Cuts",
          "Username",
          "Full Combo",
        ]);

        for (let score of scoresResponse.details.leaderboardEntries.scores) {
          worksheet.addRow([
            getScoreValueByQualifierSettings(score, qualifier.sort),
            score.modifiedScore,
            // score.accuracy, TODO: Comment back in when acc calculations are fixed
            ((score.modifiedScore / score.maxPossibleScore) * 100).toFixed(2),
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

    saveAs(
      new Blob([buffer]),
      `${sanitizeStringForExcel(qualifier.name)} Scores.xlsx`,
    );
  };
</script>

<EditSongDialog
  bind:open={editSongDialogOpen}
  showMatchOnlyOptions={false}
  showTargetTextbox={shouldShowTargetTextbox}
  gameplayParameters={editSongDialogGameplayParameters}
  songInfoList={editSongDialogSongInfolist}
  {onSongUpdated}
/>

<Dialog
  bind:open={deleteQualifierWarningOpen}
  scrimClickAction=""
  escapeKeyAction=""
>
  <Header>
    <Title>Delete this qualifier</Title>
  </Header>
  <Content>
    Are you sure you want to end the qualifier? You will not be able to
    <!-- svelte-ignore a11y-click-events-have-key-events -->
    <div class="download-hint" on:click={onGetScoresClicked}>
      download the score spreadsheet
    </div>
    later
  </Content>
  <Actions>
    <Button>
      <Label>Cancel</Label>
    </Button>
    <Button on:click={deleteQualifier}>
      <Label>Delete</Label>
    </Button>
  </Actions>
</Dialog>

<div class="page">
  <div class="qualifier-title">
    Select a song, difficulty, and characteristic
  </div>

  <div class="grid">
    <div class="column">
      <div class="cell">
        <NameEdit
          hint="Qualifier Name"
          bind:img={qualifier.image}
          bind:name={qualifier.name}
          onNameUpdated={debounceUpdateQualifierName}
          onImageUpdated={updateQualifierImage}
        />
      </div>
    </div>
    <div class="column">
      <div class="cell">
        <div class="qualifier-toggles">
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

                onFlagsChanged();
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

                onFlagsChanged();
              }}
            />
            <span slot="label">Disable Scoresaber submission</span>
          </FormField>
          <div class="bot-toggles">
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

                  onFlagsChanged();
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

                  onFlagsChanged();
                }}
              />
              <span slot="label">Enable discord bot score feed</span>
            </FormField>
            <div class="bot-hint">
              Need to
              <a
                href="https://discord.com/oauth2/authorize?client_id=708801604719214643&permissions=0&integration_type=0&scope=bot"
                target="_blank"
              >
                add the TA discord bot
              </a>
              ?
            </div>
          </div>
          <Select
            variant="outlined"
            bind:value={qualifier.sort}
            label="Leaderboard Sort Type"
            key={(option) => `${option}`}
            class="sort-type"
          >
            {#each Object.keys(QualifierEvent_LeaderboardSort) as sortType}
              {#if Number(sortType) >= 0}
                <Option
                  value={Number(sortType)}
                  on:click={() => onSortOptionClicked(Number(sortType))}
                >
                  {QualifierEvent_LeaderboardSort[Number(sortType)]}
                </Option>
              {/if}
            {/each}
          </Select>
        </div>
      </div>
      <!-- null check of qualifier.infoChannel, also conditional depending on related switches -->
      {#if qualifier.infoChannel && ((qualifier.flags & QualifierEvent_EventSettings.EnableDiscordLeaderboard) === QualifierEvent_EventSettings.EnableDiscordLeaderboard || (qualifier.flags & QualifierEvent_EventSettings.EnableDiscordScoreFeed) === QualifierEvent_EventSettings.EnableDiscordScoreFeed)}
        <div class="cell" transition:slide>
          <Textfield
            bind:value={qualifier.infoChannel.id}
            on:input={debounceUpdateInfoChannel}
            variant="outlined"
            label="Leaderboard Channel ID"
            disabled={editDisabled}
          />
        </div>
      {/if}
    </div>
  </div>
  <div class="song-list-container">
    <div class="song-list-title">Song List</div>
    <SongList
      edit={true}
      showTarget={shouldShowTargetTextbox}
      bind:mapsWithSongInfo
      maps={qualifier.qualifierMaps}
      {onEditClicked}
      {onRemoveClicked}
    />
    {#if tournament}
      <AddSong
        {onSongsAdded}
        {tournamentId}
        showMatchOnlyOptions={false}
        showTargetTextbox={shouldShowTargetTextbox}
      />
    {/if}
  </div>

  <div class="fab-container" transition:slide>
    {#if !qualifierId}
      <Fab color="primary" on:click={createQualifier} extended>
        <Icon class="material-icons">add</Icon>
        <Label>Create Qualifier</Label>
      </Fab>
    {:else}
      <Fab color="primary" on:click={onGetScoresClicked} extended>
        <Icon class="material-icons">download</Icon>
        <Label>Download Score Spreadsheet</Label>
      </Fab>
      <Fab
        color="primary"
        on:click={() => (deleteQualifierWarningOpen = true)}
        extended
      >
        <Icon class="material-icons">close</Icon>
        <Label>End Qualifier</Label>
      </Fab>
    {/if}
  </div>
</div>

<style lang="scss">
  .download-hint {
    display: inline;
    color: var(--mdc-theme-primary);
    cursor: pointer;
    text-decoration: underline;
  }

  .page {
    display: flex;
    flex-direction: column;
    align-items: center;
    margin-bottom: 70px;

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

        .qualifier-toggles {
          background-color: rgba($color: #000000, $alpha: 0.1);
          border-radius: 5px;

          // Make these darker so it highlights they require the bot
          .bot-toggles {
            background-color: rgba($color: #000000, $alpha: 0.1);
            border-radius: 5px;
            margin: 0 10px 10px 10px;

            .bot-hint {
              color: var(--mdc-theme-text-primary-on-background);
              border-radius: 2vmin;
              text-align: center;
              font-weight: 100;
              line-height: 1.1;
              padding: 0 0 10px 0;

              a {
                color: var(--mdc-theme-primary);
              }
            }
          }

          :global(.sort-type) {
            padding-top: 10px;
          }
        }
      }
    }

    .song-list-container {
      max-width: 700px;
      width: -webkit-fill-available;
      background-color: rgba($color: #000000, $alpha: 0.1);
      border-radius: 5px;
      margin-top: 10px;

      .song-list-title {
        color: var(--mdc-theme-text-primary-on-background);
        border-radius: 2vmin 2vmin 0 0;
        text-align: center;
        font-size: 2rem;
        font-weight: 100;
        line-height: 1.1;
        padding: 2vmin;
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

    .fab-container {
      position: fixed;
      bottom: 2vmin;
      right: 2vmin;
    }
  }
</style>
