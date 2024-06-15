<script lang="ts">
  import Dialog, { Header, Title, Content, Actions } from "@smui/dialog";
  import Button, { Label } from "@smui/button";
  import type { Tournament_TournamentSettings_Pool } from "tournament-assistant-client";
  import PoolList from "$lib/components/PoolList.svelte";

  export let open = false;
  export let tournamentId: string;
  export let onPoolClicked: (
    pool: Tournament_TournamentSettings_Pool,
  ) => Promise<void>;
</script>

<Dialog bind:open scrimClickAction="" escapeKeyAction="">
  <Header>
    <Title>Select Pool</Title>
  </Header>
  <Content>
    <PoolList {tournamentId} {onPoolClicked} />
  </Content>
  <Actions>
    <Button>
      <Label>Cancel</Label>
    </Button>
  </Actions>
</Dialog>
