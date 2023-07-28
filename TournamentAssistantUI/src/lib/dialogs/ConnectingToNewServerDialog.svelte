<script lang="ts">
  import Dialog, { Header, Title, Content, Actions } from "@smui/dialog";
  import IconButton from "@smui/icon-button";
  import Button, { Label } from "@smui/button";

  export let open = false;

  export let onContinueClick = () => {};
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
    <Title>Warning</Title>
  </Header>
  <Content>
    You are about to connect to a server hosted by a 3rd party. You may be asked
    to log in again.
  </Content>
  <Actions>
    <Button>
      <Label>Cancel</Label>
    </Button>
    <Button on:click={onContinueClick}>
      <Label>Continue</Label>
    </Button>
  </Actions>
</Dialog>
