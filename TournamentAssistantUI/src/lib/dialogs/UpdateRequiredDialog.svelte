<script lang="ts">
  import Dialog, { Header, Title, Content, Actions } from "@smui/dialog";
  import Button, { Label } from "@smui/button";

  export let open = false;

  export let onDownloadClick = () => {};
</script>

<Dialog bind:open scrimClickAction="" escapeKeyAction="">
  <Header>
    <Title>Update Required</Title>
  </Header>
  <Content>There is a new update available</Content>
  <Actions>
    <Button on:click={onDownloadClick}>
      <Label>Download</Label>
    </Button>
  </Actions>
</Dialog>
