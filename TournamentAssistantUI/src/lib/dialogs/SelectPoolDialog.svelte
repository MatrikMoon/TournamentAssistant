<script lang="ts">
  import Dialog, { Header, Title, Content, Actions } from "@smui/dialog";
  import IconButton from "@smui/icon-button";
  import Button, { Label } from "@smui/button";
  import type {
    Tournament,
    Tournament_TournamentSettings_Pool,
  } from "tournament-assistant-client";
  import PoolList from "$lib/components/PoolList.svelte";

  export let open = false;
  export let tournament: Tournament;
  export let onPoolClicked: (
    pool: Tournament_TournamentSettings_Pool,
  ) => Promise<void>;
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
    <Title>Select Pool</Title>
    <IconButton action="cancel" class="material-icons">close</IconButton>
  </Header>
  <Content>
    <PoolList {tournament} {onPoolClicked} />
  </Content>
  <Actions>
    <Button>
      <Label>Cancel</Label>
    </Button>
  </Actions>
</Dialog>
