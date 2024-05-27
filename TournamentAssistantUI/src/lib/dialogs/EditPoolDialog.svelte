<script lang="ts">
  import Dialog, { Header, Title, Content, Actions } from "@smui/dialog";
  import IconButton from "@smui/icon-button";
  import LayoutGrid, { Cell } from "@smui/layout-grid";
  import Button, { Label } from "@smui/button";
  import { v4 as uuidv4 } from "uuid";
  import type {
    GameplayParameters,
    Tournament,
    Tournament_TournamentSettings_Pool,
  } from "tournament-assistant-client";
  import NameEdit from "$lib/components/NameEdit.svelte";
  import SongList from "$lib/components/SongList.svelte";
  import AddSong from "$lib/components/add-song/AddSong.svelte";
  import type { MapWithSongInfo } from "$lib/globalTypes";

  export let onCreateClick = (pool: Tournament_TournamentSettings_Pool) => {};

  export let open = false;
  export let tournament: Tournament;
  export let pool: Tournament_TournamentSettings_Pool;

  let mapsWithSongInfo: MapWithSongInfo[] = [];

  // Don't allow creation unless we have all the required fields
  $: canCreate = (pool?.name?.length ?? 0) > 0;

  const createPool = async () => {
    onCreateClick(pool);
    open = false;
  };

  const onSongsAdded = async (result: GameplayParameters[]) => {
    for (let song of result) {
      pool.maps = [
        ...pool.maps,
        {
          guid: uuidv4(),
          gameplayParameters: song,
        },
      ];
    }
  };

  const onRemoveClicked = async (map: MapWithSongInfo) => {
    pool.maps = pool.maps.filter((x) => x.guid !== map.guid);
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
        />
      </Cell>
      <Cell span={8}>
        <SongList
          bind:mapsWithSongInfo
          bind:maps={pool.maps}
          {onRemoveClicked}
        />
        <AddSong {onSongsAdded} {tournament} />
      </Cell>
    </LayoutGrid>
  </Content>
  <Actions>
    <Button>
      <Label>Cancel</Label>
    </Button>
    <Button on:click={createPool} disabled={!canCreate}>
      <Label>Create</Label>
    </Button>
  </Actions>
</Dialog>
