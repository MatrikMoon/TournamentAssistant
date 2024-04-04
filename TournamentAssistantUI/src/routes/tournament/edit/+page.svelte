<script lang="ts">
  import { page } from "$app/stores";
  import LayoutGrid, { Cell } from "@smui/layout-grid";
  import { onDestroy, onMount } from "svelte";
  import type { Tournament } from "tournament-assistant-client";
  import { taService } from "$lib/stores";
  import Textfield from "@smui/textfield";
  import FileDrop from "$lib/components/FileDrop.svelte";
  import Fab, { Icon, Label } from "@smui/fab";
  import { goto } from "$app/navigation";
  import Switch from "@smui/switch";
  import FormField from "@smui/form-field";

  let serverAddress = $page.url.searchParams.get("address")!;
  let serverPort = $page.url.searchParams.get("port")!;
  let tournamentId = $page.url.searchParams.get("tournamentId")!;

  let tournament: Tournament;

  onMount(async () => {
    console.log("onMount onChange");
    await $taService.joinTournament(serverAddress, serverPort, tournamentId);

    onChange();
  });

  const returnToTournamentSelection = () => {
    goto(`/`);
  };

  const updateTournament = async () => {
    if (tournament) {
      await $taService.updateTournament(serverAddress, serverPort, tournament);
    }
  };

  const deleteTournament = async () => {
    if (tournament) {
      await $taService.deleteTournament(serverAddress, serverPort, tournament);
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

  const handleBannedModsInputChange = (event: any) => {
    const newValue = (event.target as HTMLInputElement)?.value;
    if (newValue && tournament?.settings) {
      tournament.settings.bannedMods = newValue.split(", ");

      updateTournament();
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
      <Textfield
        bind:value={tournament.settings.tournamentName}
        on:input={updateTournament}
        variant="outlined"
        label="Tournament Name"
      />
    </Cell>
  {/if}
  <Cell span={4}>
    <FileDrop
      onFileSelected={async (file) => {
        if (tournament?.settings) {
          tournament.settings.tournamentImage = new Uint8Array(
            await file.arrayBuffer(),
          );

          updateTournament();
        }
      }}
      img={tournament?.settings?.tournamentImage}
    />
  </Cell>
  <Cell span={4}>
    <div class="grid-cell">
      <FormField>
        <Switch
          checked={tournament?.settings?.enableTeams}
          on:SMUISwitch:change={() => {
            if (tournament?.settings) {
              tournament.settings.enableTeams =
                !tournament?.settings?.enableTeams;

              updateTournament();
            }
          }}
        />
        <span slot="label">Enable Teams</span>
      </FormField>
    </div>
  </Cell>
  {#if tournament?.settings}
    <Cell>
      <Textfield
        bind:value={tournament.settings.scoreUpdateFrequency}
        on:input={updateTournament}
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
</LayoutGrid>

<div class="delete-tournament-button-container">
  <Fab color="primary" on:click={deleteTournament} extended>
    <Icon class="material-icons">close</Icon>
    <Label>End Tournament</Label>
  </Fab>
</div>

<style lang="scss">
  .grid-cell {
    background-color: rgba($color: #000000, $alpha: 0.1);
    border-radius: 5px;
  }

  .delete-tournament-button-container {
    position: fixed;
    bottom: 2vmin;
    right: 2vmin;
  }
</style>
