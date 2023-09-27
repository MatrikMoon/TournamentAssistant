<script lang="ts">
  import "./TADrawer.scss";
  import IconButton from "@smui/icon-button";
  import { Row } from "@smui/data-table";
  import { Item, Text } from "@smui/list";
  import Drawer, { AppContent } from "@smui/drawer";
  import TopAppBar from "@smui/top-app-bar";
  import {
    getAvatarFromToken,
    getUsernameFromToken,
  } from "$lib/services/jwtService";
  import { authToken } from "$lib/stores";
  import defaultLogo from "../assets/icon.png";
  import Button from "@smui/button";
  import Snackbar, { Label, Actions } from "@smui/snackbar";

  type DrawerItem = {
    name: string;
    isActive: boolean;
    onClick: () => void | boolean;
  };

  export let items: DrawerItem[] = [];

  let open = false;
  let snackbarSuccess: Snackbar;
  let snackbarError: Snackbar;

  const onCopyClick = () => {
    navigator.clipboard
      .writeText($authToken)
      .then(snackbarSuccess.open, snackbarError.open);
  };
</script>

<Snackbar bind:this={snackbarSuccess} class="demo-success">
  <Label>
    Heyya George. Copied your token to your clipboard. Should be good for a
    month o7
  </Label>
  <Actions>
    <IconButton class="material-icons" title="Dismiss">close</IconButton>
  </Actions>
</Snackbar>

<Snackbar bind:this={snackbarError} class="demo-error">
  <Label>Oops. Failed to copy your token to your clipboard. Dang.</Label>
  <Actions>
    <IconButton class="material-icons" title="Dismiss">close</IconButton>
  </Actions>
</Snackbar>

<Drawer variant="dismissible" bind:open>
  <div class="profile-card">
    <span
      class="profile-image"
      style="background-image: url({getAvatarFromToken($authToken) ??
        defaultLogo});"
    />
    <div class="profile-text">
      <div class="profile-username">{getUsernameFromToken($authToken)}</div>
      <div class="profile-subtext">Welcome!</div>
    </div>
  </div>

  <!-- <Button on:click={onCopyClick}>George</Button> -->

  <div class="divider" />

  <!-- This used to be wrapped in <Content> and <List> tags, but it seems that when the each was a child of those,
    it wouldn't rerender when the list changed. Which is weird, yeah, but I guess now we're doing it this way.
    Also putting a <div> here seems to trick some part of the css to thinking it's in a list, so the margin looks right.
    Dunno how that works, but I'll take it. -->
  <div>
    {#each items as item}
      <Item
        href="javascript:void(0)"
        on:click={() => (open = !item.onClick())}
        activated={item.isActive}
      >
        <Text>{item.name}</Text>
      </Item>
    {/each}
  </div>
</Drawer>

<AppContent>
  <TopAppBar variant="static" color={"primary"}>
    <Row>
      <div class="menu-button-container">
        <IconButton
          class="material-icons"
          aria-label="Menu"
          on:click={() => (open = !open)}>menu</IconButton
        >
      </div>
    </Row>
  </TopAppBar>
  <div class="content">
    <slot />
  </div>
</AppContent>

<style lang="scss">
  .profile-card {
    margin: 10px;
    display: flex;
    align-items: center;

    .profile-text {
      padding: 0 10px;
      align-items: center;

      .profile-username {
        color: var(--mdc-theme-text-primary-on-background);
        font-size: var(--mdc-typography-headline6-font-size, 1.25rem);
        font-weight: var(--mdc-typography-headline6-font-weight, 500);
      }

      .profile-subtext {
        color: var(--mdc-theme-text-secondary-on-background);
        font-weight: var(--mdc-typography-body2-font-weight, 400);
        font-size: var(--mdc-typography-body2-font-size, 0.875rem);
      }
    }

    .profile-image {
      width: 60px;
      height: 60px;
      background-size: contain;
      border-radius: 100%;
    }
  }

  .content {
    margin: 8px;
  }

  .menu-button-container {
    display: flex;
    height: 100%;
    align-items: center;
  }

  .divider {
    border-top: 1px solid var(--mdc-theme-text-secondary-on-background);
    margin: 15px 3px;
  }
</style>
