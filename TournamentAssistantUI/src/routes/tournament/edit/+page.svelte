<script lang="ts">
  import { page } from "$app/stores";
  import LayoutGrid, { Cell } from "@smui/layout-grid";
  import { onDestroy, onMount } from "svelte";
  import type { Tournament } from "tournament-assistant-client";
  import { taService } from "$lib/stores";
  import Textfield from "@smui/textfield";
  import Fab, { Icon, Label } from "@smui/fab";
  import { goto } from "$app/navigation";
  import Switch from "@smui/switch";
  import FormField from "@smui/form-field";
  import TournamentNameEdit from "$lib/components/TournamentNameEdit.svelte";
  import TeamList from "$lib/components/TeamList.svelte";
  import { slide } from "svelte/transition";

  let serverAddress = $page.url.searchParams.get("address")!;
  let serverPort = $page.url.searchParams.get("port")!;
  let tournamentId = $page.url.searchParams.get("tournamentId")!;

  let nameUpdateTimer: NodeJS.Timeout | undefined;
  let tournament: Tournament;

  onMount(async () => {
    console.log("onMount onChange");
    await $taService.joinTournament(serverAddress, serverPort, tournamentId);

    onChange();
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

      if (tournament) {
        await $taService.setTournamentEnableTeams(
          serverAddress,
          serverPort,
          tournamentId,
          tournament.settings.enableTeams,
        );
      }
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
    if (newValue && tournament?.settings) {
      tournament.settings.bannedMods = newValue.split(", ");

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

  //When changes happen to the server list, re-render
  $taService.subscribeToTournamentUpdates(onChange);
  onDestroy(() => {
    $taService.subscribeToTournamentUpdates(onChange);
  });
</script>

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
    <div class="grid-cell shadow">
      <FormField>
        <Switch
          checked={tournament?.settings?.enableTeams}
          on:SMUISwitch:change={handleEnableTeamsChanged}
        />
        <span slot="label">Enable Teams</span>
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
  {/if}
  {#if tournament?.settings?.enableTeams}
    <Cell span={8}>
      <div transition:slide>
        <div class="team-list-title">Teams</div>
        <div class="grid-cell">
          <TeamList {tournament} />
        </div>
      </div>
    </Cell>
  {/if}
</LayoutGrid>

<div class="delete-tournament-button-container">
  <Fab color="primary" on:click={deleteTournament} extended>
    <Icon class="material-icons">close</Icon>
    <Label>End Tournament</Label>
  </Fab>
</div>

<style lang="scss">
  .grid-cell {
    min-height: 55px;
    background-color: rgba($color: #000000, $alpha: 0.1);
    border-radius: 5px;

    &.shadow {
      box-shadow: 5px 5px 5px rgba($color: #000000, $alpha: 0.2);
    }
  }

  .team-list-title {
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
</style>
