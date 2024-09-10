<script lang="ts">
  import Dialog, { Header, Title, Content, Actions } from "@smui/dialog";
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
  import EditSongDialog from "./EditSongDialog.svelte";
  import type { SongInfo } from "$lib/services/beatSaver/songInfo";
  import { xyz } from "color";

  export let open = false;
  export let editMode = false;
  export let serverAddress: string;
  export let serverPort: string;
  export let tournamentId: string;
  export let pool: Tournament_TournamentSettings_Pool;

  let editSongDialogOpen = false;
  let editSongDialogGameplayParameters: GameplayParameters | undefined =
    undefined;
  let editSongDialogSongInfolist: SongInfo | undefined = undefined;
  let editSongDialogMapId: string | undefined = undefined;

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
    if (editMode) {
      await $taService.addTournamentPoolMaps(
        serverAddress,
        serverPort,
        tournamentId,
        pool.guid,
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
      pool.maps = [
        ...pool.maps,
        ...result.map((x) => {
          return {
            guid: uuidv4(),
            gameplayParameters: x,
          };
        }),
      ];
    }
  };

  const onSongUpdated = async (result: GameplayParameters) => {
    if (editMode) {
      await $taService.updateTournamentPoolMap(
        serverAddress,
        serverPort,
        tournamentId,
        pool.guid,
        {
          guid: editSongDialogMapId!,
          gameplayParameters: result,
        },
      );
    } else {
      pool.maps = [
        ...pool.maps.filter((x) => x.guid !== editSongDialogMapId!),
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

<Dialog bind:open scrimClickAction="" escapeKeyAction="">
  <EditSongDialog
    bind:open={editSongDialogOpen}
    gameplayParameters={editSongDialogGameplayParameters}
    songInfoList={editSongDialogSongInfolist}
    {onSongUpdated}
  />
  <Header>
    <Title>Create a Map Pool</Title>
  </Header>
  <Content>
    <LayoutGrid>
      <Cell span={12}>
        <NameEdit
          hint="Pool Name"
          bind:img={pool.image}
          bind:name={pool.name}
          {onNameUpdated}
          {onImageUpdated}
        />
      </Cell>
      <Cell span={12}>
        <SongList
          edit={true}
          bind:maps={pool.maps}
          {onEditClicked}
          {onRemoveClicked}
        />
        <AddSong {onSongsAdded} {tournamentId} />
      </Cell>
    </LayoutGrid>
  </Content>
  <Actions>
    {#if editMode}
      <Button>
        <Label>Done</Label>
      </Button>
    {:else}
      <Button>
        <Label>Cancel</Label>
      </Button>
      <Button on:click={createPool} disabled={!canCreate}>
        <Label>Create</Label>
      </Button>
    {/if}
  </Actions>
</Dialog>
