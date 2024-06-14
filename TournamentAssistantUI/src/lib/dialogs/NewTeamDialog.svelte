<script lang="ts">
  import Dialog, { Header, Title, Content, Actions } from "@smui/dialog";
  import IconButton from "@smui/icon-button";
  import LayoutGrid, { Cell } from "@smui/layout-grid";
  import Button, { Label } from "@smui/button";
  import { v4 as uuidv4 } from "uuid";
  import type { Tournament_TournamentSettings_Team } from "tournament-assistant-client";
  import NameEdit from "$lib/components/NameEdit.svelte";

  export let onCreateClick = (team: Tournament_TournamentSettings_Team) => {};

  export let open = false;
  export let team: Tournament_TournamentSettings_Team = {
    guid: uuidv4(),
    name: "",
    image: new Uint8Array([1]),
  };

  // Don't allow creation unless we have all the required fields
  $: canCreate = (team?.name?.length ?? 0) > 0;

  const createTeam = async () => {
    team.guid = uuidv4();
    onCreateClick(team);
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
    <Title>Create a Team</Title>
    <IconButton action="cancel" class="material-icons">close</IconButton>
  </Header>
  <Content>
    <LayoutGrid>
      <Cell span={8}>
        <NameEdit
          hint="Team Name"
          bind:img={team.image}
          bind:name={team.name}
        />
      </Cell>
    </LayoutGrid>
  </Content>
  <Actions>
    <Button>
      <Label>Cancel</Label>
    </Button>
    <Button on:click={createTeam} disabled={!canCreate}>
      <Label>Create</Label>
    </Button>
  </Actions>
</Dialog>
