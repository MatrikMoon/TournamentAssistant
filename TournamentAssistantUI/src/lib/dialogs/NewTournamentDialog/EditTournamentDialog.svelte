<script lang="ts">
  import Dialog, { Header, Title, Content, Actions } from "@smui/dialog";
  import IconButton from "@smui/icon-button";
  import LayoutGrid, { Cell } from "@smui/layout-grid";
  import FileDrop from "$lib/components/FileDrop.svelte";
  import Select, { Option } from "@smui/select";
  import Textfield from "@smui/textfield";
  import Button, { Label } from "@smui/button";
  import { v4 as uuidv4 } from "uuid";
  import type { CoreServer, Tournament } from "tournament-assistant-client";
  import { onDestroy, onMount } from "svelte";
  import { taService } from "$lib/stores";

  export let onCreateClick = () => {};

  export let open = false;
  export let host: CoreServer;
  export let tournament: Tournament = {
    guid: uuidv4(),
    users: [],
    matches: [],
    qualifiers: [],
    settings: {
      tournamentName: "Default Tournament Name",
      tournamentImage: new Uint8Array([1]),
      enableTeams: false,
      teams: [],
      scoreUpdateFrequency: 30,
      bannedMods: [],
      pools: [],
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

  let tournamentName = "";
  let image: File;

  //If we don't yet have any host information
  // $: if (knownHosts.length == 0) {
  //   knownHosts = $masterClient.stateManager.getKnownServers();
  // }

  //Don't allow creation unless we have all the required fields
  let canCreate = false;
  $: if (host && tournamentName.length > 0) {
    canCreate = true;
  }

  const createTournament = async () => {
    const loadedImage = await image?.arrayBuffer();

    tournament = {
      ...tournament,
      settings: {
        ...tournament.settings!,
        tournamentName,
        tournamentImage: loadedImage
          ? new Uint8Array(loadedImage)
          : new Uint8Array([1]),
        enableTeams: false,
      },
    };

    onCreateClick();

    open = false;
  };
</script>

<Dialog
  bind:open
  fullscreen
  scrimClickAction=""
  escapeKeyAction=""
  aria-labelledby="fullscreen-title"
  aria-describedby="fullscreen-content"
>
  <Header>
    <Title>Create a Tournament</Title>
    <IconButton action="cancel" class="material-icons">close</IconButton>
  </Header>
  <Content>
    <LayoutGrid>
      <Cell span={12}>
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
      </Cell>
      <Cell span={4}>
        <Textfield
          bind:value={tournamentName}
          variant="outlined"
          label="Tournament Name"
        />
      </Cell>
      <Cell span={4}>
        <FileDrop onFileSelected={(file) => (image = file)} />
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
