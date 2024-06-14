<script lang="ts">
  import { page } from "$app/stores";
  import LayoutGrid, { Cell } from "@smui/layout-grid";
  import { onDestroy, onMount } from "svelte";
  import {
    Permissions,
    Response_GetAuthorizedUsers_AuthroizedUser,
    Response_ResponseType,
    type Tournament,
    type Tournament_TournamentSettings_Pool,
    type Tournament_TournamentSettings_Team,
  } from "tournament-assistant-client";
  import { taService } from "$lib/stores";
  import Textfield from "@smui/textfield";
  import Fab, { Icon, Label as FabLabel } from "@smui/fab";
  import { goto } from "$app/navigation";
  import Switch from "@smui/switch";
  import FormField from "@smui/form-field";
  import TournamentNameEdit from "$lib/components/TournamentNameEdit.svelte";
  import TeamList from "$lib/components/TeamList.svelte";
  import { slide } from "svelte/transition";
  import Button, { Label } from "@smui/button";
  import NewTeamDialog from "$lib/dialogs/NewTeamDialog.svelte";
  import PoolList from "$lib/components/PoolList.svelte";
  import EditPoolDialog from "$lib/dialogs/EditPoolDialog.svelte";
  import { v4 as uuidv4 } from "uuid";
  import AuthorizedUserList from "$lib/components/AuthorizedUserList.svelte";
  import AddAuthorizedUserDialog from "$lib/dialogs/AddAuthorizedUserDialog.svelte";

  let serverAddress = $page.url.searchParams.get("address")!;
  let serverPort = $page.url.searchParams.get("port")!;
  let tournamentId = $page.url.searchParams.get("tournamentId")!;

  let nameUpdateTimer: NodeJS.Timeout | undefined;
  let tournament: Tournament;
  let authorizedUsers: Response_GetAuthorizedUsers_AuthroizedUser[] = [];

  let createTeamDialogOpen = false;
  let createPoolDialogOpen = false;
  let addAuthorizedUserDialogOpen = false;

  let selectedPool: Tournament_TournamentSettings_Pool = {
    guid: uuidv4(),
    name: "",
    image: new Uint8Array([1]),
    maps: [],
  };

  onMount(async () => {
    console.log("onMount onChange");
    let joinResponse = await $taService.joinTournament(
      serverAddress,
      serverPort,
      tournamentId,
    );

    if (!joinResponse || joinResponse.type === Response_ResponseType.Success) {
      await onChange();
      await requestAuthorizedUsers();
    }
  });

  const returnToTournamentSelection = () => {
    goto(`/`);
  };

  const deleteTournament = async () => {
    if (tournament) {
      await $taService.deleteTournament(
        serverAddress,
        serverPort,
        tournament.guid,
      );
      returnToTournamentSelection();
    }
  };

  async function onChange() {
    tournament = (await $taService.getTournament(
      serverAddress,
      serverPort,
      tournamentId,
    ))!;
  }

  async function requestAuthorizedUsers() {
    let getAuthorizedUsersResponse = await $taService.getAuthorizedUsers(
      serverAddress,
      serverPort,
      tournamentId,
    );

    if (
      getAuthorizedUsersResponse.type === Response_ResponseType.Success &&
      getAuthorizedUsersResponse.details.oneofKind === "getAuthorizedUsers"
    ) {
      authorizedUsers =
        getAuthorizedUsersResponse.details.getAuthorizedUsers.authorizedUsers;
    }
  }

  const debounceUpdateTournamentName = () => {
    if (tournament) {
      clearTimeout(nameUpdateTimer);
      nameUpdateTimer = setTimeout(async () => {
        await $taService.setTournamentName(
          serverAddress,
          serverPort,
          tournamentId,
          tournament.settings!.tournamentName,
        );
      }, 500);
    }
  };

  const updateTournamentImage = async () => {
    if (tournament) {
      await $taService.setTournamentImage(
        serverAddress,
        serverPort,
        tournamentId,
        tournament.settings!.tournamentImage,
      );
    }
  };

  const handleEnableTeamsChanged = async () => {
    if (tournament?.settings) {
      tournament.settings.enableTeams = !tournament?.settings?.enableTeams;

      await $taService.setTournamentEnableTeams(
        serverAddress,
        serverPort,
        tournamentId,
        tournament.settings.enableTeams,
      );
    }
  };

  const handleEnablePoolsChanged = async () => {
    if (tournament?.settings) {
      tournament.settings.enablePools = !tournament?.settings?.enablePools;

      await $taService.setTournamentEnablePools(
        serverAddress,
        serverPort,
        tournamentId,
        tournament.settings.enablePools,
      );
    }
  };

  const handleShowTournamentButtonChanged = async () => {
    if (tournament?.settings) {
      tournament.settings.showTournamentButton =
        !tournament?.settings?.showTournamentButton;

      await $taService.setTournamentShowTournamentButton(
        serverAddress,
        serverPort,
        tournamentId,
        tournament.settings.showTournamentButton,
      );
    }
  };

  const handleShowQualifierButtonChanged = async () => {
    if (tournament?.settings) {
      tournament.settings.showQualifierButton =
        !tournament?.settings?.showQualifierButton;

      await $taService.setTournamentShowQualifierButton(
        serverAddress,
        serverPort,
        tournamentId,
        tournament.settings.showQualifierButton,
      );
    }
  };

  const handleScoreUpdateFrequencyChanged = async () => {
    if (tournament.settings) {
      $taService.setTournamentScoreUpdateFrequency(
        serverAddress,
        serverPort,
        tournamentId,
        tournament.settings.scoreUpdateFrequency,
      );
    }
  };

  const handleBannedModsInputChange = async (event: any) => {
    const newValue = (event.target as HTMLInputElement)?.value;
    if (tournament?.settings) {
      tournament.settings.bannedMods = newValue?.split(", ") ?? "";

      if (tournament) {
        await $taService.setTournamentBannedMods(
          serverAddress,
          serverPort,
          tournamentId,
          tournament.settings.bannedMods,
        );
      }
    }
  };

  const onCreateTeamClick = () => {
    createTeamDialogOpen = !createTeamDialogOpen;
  };

  const onRemoveTeamClicked = async (
    team: Tournament_TournamentSettings_Team,
  ) => {
    await $taService.removeTournamentTeam(
      serverAddress,
      serverPort,
      tournamentId,
      team.guid,
    );
  };

  const onTeamCreated = async (team: Tournament_TournamentSettings_Team) => {
    await $taService.addTournamentTeam(
      serverAddress,
      serverPort,
      tournamentId,
      team,
    );
  };

  const onCreatePoolClicked = () => {
    selectedPool = {
      guid: uuidv4(),
      name: "",
      image: new Uint8Array([1]),
      maps: [],
    };
    createPoolDialogOpen = !createPoolDialogOpen;
  };

  const onRemovePoolClicked = async (
    pool: Tournament_TournamentSettings_Pool,
  ) => {
    await $taService.removeTournamentPool(
      serverAddress,
      serverPort,
      tournamentId,
      pool.guid,
    );
  };

  const onPoolClicked = async (pool: Tournament_TournamentSettings_Pool) => {
    selectedPool = pool;
    createPoolDialogOpen = true;
  };

  const onAddAuthorizedUserClicked = () => {
    addAuthorizedUserDialogOpen = true;
  };

  const onAuthorizedUserAdded = async (
    discordId: string,
    permissions: Permissions,
  ) => {
    const response = await $taService.addAuthorizedUser(
      serverAddress,
      serverPort,
      tournamentId,
      discordId,
      permissions,
    );

    if (
      response.type === Response_ResponseType.Success &&
      response.details.oneofKind === "addAuthorizedUser"
    ) {
      await requestAuthorizedUsers();
    }
  };

  const onAuthorizedUserRemoved = async (discordId: string) => {
    const response = await $taService.removeAuthorizedUser(
      serverAddress,
      serverPort,
      tournamentId,
      discordId,
    );

    if (
      response.type === Response_ResponseType.Success &&
      response.details.oneofKind === "removeAuthorizedUser"
    ) {
      await requestAuthorizedUsers();
    }
  };

  //When changes happen to the server list, re-render
  $taService.subscribeToTournamentUpdates(onChange);
  onDestroy(() => {
    $taService.subscribeToTournamentUpdates(onChange);
  });
</script>

<div class="page">
  <AddAuthorizedUserDialog
    {serverAddress}
    {serverPort}
    {tournamentId}
    bind:open={addAuthorizedUserDialogOpen}
    onAddClick={onAuthorizedUserAdded}
  />
  <NewTeamDialog
    bind:open={createTeamDialogOpen}
    onCreateClick={onTeamCreated}
  />
  <EditPoolDialog
    bind:open={createPoolDialogOpen}
    {serverAddress}
    {serverPort}
    {tournamentId}
    pool={selectedPool}
    editMode={selectedPool.name.length > 0}
  />
  <LayoutGrid>
    {#if tournament && tournament.settings && tournament.settings.tournamentName}
      <Cell span={4}>
        <TournamentNameEdit
          bind:tournament
          onNameUpdated={debounceUpdateTournamentName}
          onImageUpdated={updateTournamentImage}
        />
      </Cell>
    {/if}
    <Cell span={4}>
      <div class="grid-cell shadow toggles-container">
        <FormField>
          <Switch
            checked={tournament?.settings?.enableTeams}
            on:SMUISwitch:change={handleEnableTeamsChanged}
          />
          <span slot="label">Enable Teams</span>
        </FormField>
        <FormField>
          <Switch
            checked={tournament?.settings?.enablePools}
            on:SMUISwitch:change={handleEnablePoolsChanged}
          />
          <span slot="label">Enable Pools</span>
        </FormField>
        <FormField>
          <Switch
            checked={tournament?.settings?.showTournamentButton}
            on:SMUISwitch:change={handleShowTournamentButtonChanged}
          />
          <span slot="label">Show "Tournament" button</span>
        </FormField>
        <FormField>
          <Switch
            checked={tournament?.settings?.showQualifierButton}
            on:SMUISwitch:change={handleShowQualifierButtonChanged}
          />
          <span slot="label">Show "Qualifier" button</span>
        </FormField>
      </div>
    </Cell>
    {#if tournament?.settings}
      <Cell>
        <Textfield
          bind:value={tournament.settings.scoreUpdateFrequency}
          on:input={handleScoreUpdateFrequencyChanged}
          variant="outlined"
          label="Score Update Frequency (frames)"
        />
      </Cell>
      <Cell>
        <Textfield
          value={tournament.settings.bannedMods.join(", ")}
          on:input={handleBannedModsInputChange}
          variant="outlined"
          label="Banned Mods (comma separated)"
        />
      </Cell>
      <Cell span={8}>
        <div transition:slide>
          <div class="pool-list-title">Authorized users</div>
          <div class="grid-cell">
            <AuthorizedUserList
              {authorizedUsers}
              showRemoveButton={true}
              onRemoveClicked={async (user) =>
                await onAuthorizedUserRemoved(user.discordId)}
            />
            <div class="button">
              <Button variant="raised" on:click={onAddAuthorizedUserClicked}>
                <Label>Add Authorized User</Label>
              </Button>
            </div>
          </div>
        </div>
      </Cell>
    {/if}
    {#if tournament?.settings?.enableTeams}
      <Cell span={8}>
        <div transition:slide>
          <div class="team-list-title">Teams</div>
          <div class="grid-cell">
            <TeamList {tournament} onRemoveClicked={onRemoveTeamClicked} />
            <div class="button">
              <Button variant="raised" on:click={onCreateTeamClick}>
                <Label>Create Team</Label>
              </Button>
            </div>
          </div>
        </div>
      </Cell>
    {/if}
    {#if tournament?.settings?.enablePools}
      <Cell span={8}>
        <div transition:slide>
          <div class="pool-list-title">Map Pools</div>
          <div class="grid-cell">
            <PoolList
              {tournamentId}
              {onPoolClicked}
              onRemoveClicked={onRemovePoolClicked}
              showRemoveButton={true}
            />
            <div class="button">
              <Button variant="raised" on:click={onCreatePoolClicked}>
                <Label>Create Map Pool</Label>
              </Button>
            </div>
          </div>
        </div>
      </Cell>
    {/if}
  </LayoutGrid>

  <div class="delete-tournament-button-container">
    <Fab color="primary" on:click={deleteTournament} extended>
      <Icon class="material-icons">close</Icon>
      <FabLabel>End Tournament</FabLabel>
    </Fab>
  </div>
</div>

<style lang="scss">
  .page {
    margin-bottom: 70px;

    .toggles-container {
      display: flex;
      flex-direction: column;
    }

    .grid-cell {
      min-height: 55px;
      background-color: rgba($color: #000000, $alpha: 0.1);
      border-radius: 5px;

      &.shadow {
        box-shadow: 5px 5px 5px rgba($color: #000000, $alpha: 0.2);
      }

      .button {
        text-align: center;
        padding-bottom: 2vmin;
      }
    }

    .team-list-title,
    .pool-list-title {
      color: var(--mdc-theme-text-primary-on-background);
      background-color: rgba($color: #000000, $alpha: 0.1);
      border-radius: 2vmin 2vmin 0 0;
      text-align: center;
      font-size: 2rem;
      font-weight: 100;
      line-height: 1.1;
      padding: 2vmin;
    }

    .delete-tournament-button-container {
      position: fixed;
      bottom: 2vmin;
      right: 2vmin;
    }
  }
</style>
