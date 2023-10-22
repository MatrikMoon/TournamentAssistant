<script lang="ts">
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

  type DrawerItem = {
    name: string;
    isActive: boolean;
    onClick: () => void | boolean;
  };

  export let items: DrawerItem[] = [];

  let open = false;
</script>

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

<AppContent class="app-content">
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

  :global(.app-content) {
    // display: flex;
    // flex-direction: column;
    // align-items: center;

    .content {
      margin: 8px;
      //max-width: 800px;
    }
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
