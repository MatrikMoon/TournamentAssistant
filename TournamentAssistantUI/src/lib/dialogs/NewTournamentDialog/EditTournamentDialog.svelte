<script lang="ts">
  import Dialog, { Header, Title, Content, Actions } from "@smui/dialog";
  import IconButton from "@smui/icon-button";
  import LayoutGrid, { Cell } from "@smui/layout-grid";
  import Select, { Option } from "@smui/select";
  import Button, { Label } from "@smui/button";
  import { v4 as uuidv4 } from "uuid";
  import type { CoreServer, Tournament } from "tournament-assistant-client";
  import { onDestroy, onMount } from "svelte";
  import { taService } from "$lib/stores";
  import TournamentNameEdit from "$lib/components/TournamentNameEdit.svelte";

  export let onCreateClick = () => {};

  export let open = false;
  export let host: CoreServer;
  export let tournament: Tournament = {
    guid: uuidv4(),
    users: [],
    matches: [],
    qualifiers: [],
    settings: {
      tournamentName: "",
      tournamentImage: new Uint8Array([1]),
      enableTeams: false,
      enablePools: false,
      showTournamentButton: true,
      showQualifierButton: true,
      roles: [],
      teams: [],
      scoreUpdateFrequency: 30,
      bannedMods: [],
      pools: [],
      myPermissions: [],
    },
  };

  //Update the Tournament's assigned server whenever that value changes
  $: tournament.server = host;

  let knownServers: CoreServer[] = [];

  onMount(async () => {
    console.log("onMount getKnownServers");
    await onChange();
  });

  async function onChange() {
    knownServers = await $taService.getKnownServers();
  }

  //When changes happen to the server list, re-render
  $taService.subscribeToServerUpdates(onChange);
  onDestroy(() => {
    $taService.unsubscribeFromServerUpdates(onChange);
  });

  //Don't allow creation unless we have all the required fields
  $: canCreate =
    host && (tournament?.settings?.tournamentName?.length ?? 0) > 0;

  const createTournament = async () => {
    onCreateClick();

    open = false;
  };
</script>

<Dialog bind:open scrimClickAction="" escapeKeyAction="">
  <Header>
    <Title>Create a Tournament</Title>
  </Header>
  <Content>
    <LayoutGrid>
      <Cell span={12}>
        <div class="min-size-cell">
          <Select
            bind:value={host}
            key={(item) => `${item?.address}:${item?.websocketPort}`}
            label="Server"
            variant="outlined"
          >
            {#each knownServers as host}
              <Option value={host}>
                {`${host.address}:${host.websocketPort}`}
              </Option>
            {/each}
          </Select>
        </div>
      </Cell>
      <Cell span={12}>
        <div class="min-size-cell">
          <TournamentNameEdit bind:tournament />
        </div>
      </Cell>
    </LayoutGrid>
  </Content>
  <Actions>
    <Button>
      <Label>Cancel</Label>
    </Button>
    <Button on:click={createTournament} disabled={!canCreate}>
      <Label>Create</Label>
    </Button>
  </Actions>
</Dialog>

<style lang="scss">
  .min-size-cell {
    min-width: 400px;
  }
</style>
