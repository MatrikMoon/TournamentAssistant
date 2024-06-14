<script lang="ts">
  import Dialog, { Header, Title, Content, Actions } from "@smui/dialog";
  import IconButton from "@smui/icon-button";
  import LayoutGrid, { Cell } from "@smui/layout-grid";
  import Button, { Label } from "@smui/button";
  import { v4 as uuidv4 } from "uuid";
  import type {
    GameplayParameters,
    Tournament_TournamentSettings_Pool,
  } from "tournament-assistant-client";
  import NameEdit from "$lib/components/NameEdit.svelte";
  import SongList from "$lib/components/SongList.svelte";
  import AddSong from "$lib/components/add-song/AddSong.svelte";
  import type { MapWithSongInfo } from "$lib/globalTypes";
  import { taService } from "$lib/stores";

  export let open = false;
  export let editMode = false;
  export let serverAddress: string;
  export let serverPort: string;
  export let tournamentId: string;
  export let pool: Tournament_TournamentSettings_Pool;

  let mapsWithSongInfo: MapWithSongInfo[] = [];

  // Don't allow creation unless we have all the required fields
  $: canCreate = (pool?.name?.length ?? 0) > 0;

  const createPool = async () => {
    await $taService.addTournamentPool(
      serverAddress,
      serverPort,
      tournamentId,
      pool,
    );

    open = false;
  };

  const onNameUpdated = async () => {
    if (editMode) {
      await $taService.setTournamentPoolName(
        serverAddress,
        serverPort,
        tournamentId,
        pool.guid,
        pool.name,
      );
    }
  };

  const onImageUpdated = async () => {};

  const onSongsAdded = async (result: GameplayParameters[]) => {
    for (let song of result) {
      const map = {
        guid: uuidv4(),
        gameplayParameters: song,
      };

      pool.maps = [...pool.maps, map];

      if (editMode) {
        await $taService.addTournamentPoolMap(
          serverAddress,
          serverPort,
          tournamentId,
          pool.guid,
          map,
        );
      }
    }
  };

  const onRemoveClicked = async (map: MapWithSongInfo) => {
    pool.maps = pool.maps.filter((x) => x.guid !== map.guid);

    if (editMode) {
      await $taService.removeTournamentPoolMap(
        serverAddress,
        serverPort,
        tournamentId,
        pool.guid,
        map.guid,
      );
    }
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
    <Title>Create a Map Pool</Title>
    <IconButton action="cancel" class="material-icons">close</IconButton>
  </Header>
  <Content>
    <LayoutGrid>
      <Cell span={8}>
        <NameEdit
          hint="Pool Name"
          bind:img={pool.image}
          bind:name={pool.name}
          {onNameUpdated}
          {onImageUpdated}
        />
      </Cell>
      <Cell span={8}>
        <SongList
          bind:mapsWithSongInfo
          bind:maps={pool.maps}
          {onRemoveClicked}
        />
        <AddSong {onSongsAdded} {tournamentId} />
      </Cell>
    </LayoutGrid>
  </Content>
  {#if !editMode}
    <Actions>
      <Button>
        <Label>Cancel</Label>
      </Button>
      <Button on:click={createPool} disabled={!canCreate}>
        <Label>Create</Label>
      </Button>
    </Actions>
  {/if}
</Dialog>
