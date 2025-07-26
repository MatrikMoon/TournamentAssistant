<script lang="ts">
  import Dialog, { Header, Title, Content, Actions } from "@smui/dialog";
  import LayoutGrid, { Cell } from "@smui/layout-grid";
  import Button, { Label } from "@smui/button";
  import NameEdit from "$lib/components/NameEdit.svelte";

  export let onCreateClick = (teamName: string, teamImage: Uint8Array) => {};

  export let open = false;
  export let teamName = "";
  export let teamImage = new Uint8Array([1]);

  // Don't allow creation unless we have all the required fields
  $: canCreate = (teamName?.length ?? 0) > 0;

  const createTeam = async () => {
    onCreateClick(teamName, teamImage);
    open = false;
  };
</script>

<Dialog bind:open scrimClickAction="" escapeKeyAction="">
  <Header>
    <Title>Create a Team</Title>
  </Header>
  <Content>
    <LayoutGrid>
      <Cell span={12}>
        <NameEdit hint="Team Name" bind:img={teamImage} bind:name={teamName} />
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
