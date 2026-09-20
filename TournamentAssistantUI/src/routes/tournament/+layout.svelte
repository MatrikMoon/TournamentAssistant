<script lang="ts">
  import { goto } from "$app/navigation";
  import { page } from "$app/stores";
  import TaDrawer from "$lib/components/TADrawer.svelte";
  import { taService } from "$lib/stores";
  import { onMount } from "svelte";

  $: serverAddress = $page.url.searchParams.get("address")!;
  $: serverPort = $page.url.searchParams.get("port")!;
  $: tournamentId = $page.url.searchParams.get("tournamentId")!;

  let items = [
    {
      name: "Matches",
      isActive: $page.url.pathname === "/tournament/match-select",
      onClick: () => {
        goto(
          `/tournament/match-select?tournamentId=${tournamentId}&address=${serverAddress}&port=${serverPort}`
        );
      },
    },
    {
      name: "Qualifiers",
      isActive: $page.url.pathname === "/tournament/qualifier-select",
      onClick: () => {
        goto(
          `/tournament/qualifier-select?tournamentId=${tournamentId}&address=${serverAddress}&port=${serverPort}`
        );
      },
    },
    {
      name: "Tournament Settings",
      isActive: $page.url.pathname === "/tournament/edit",
      onClick: () => {
        goto(
          `/tournament/edit?tournamentId=${tournamentId}&address=${serverAddress}&port=${serverPort}`
        );
      },
    },
    {
      name: "Bot Tokens",
      isActive: $page.url.pathname === "/applications",
      onClick: () => {
        goto(`/applications`);
      },
    },
    {
      name: "Webhooks",
      isActive: $page.url.pathname === "/tournament/webhooks",
      onClick: () => {
        goto(
          `/tournament/webhooks?tournamentId=${tournamentId}&address=${serverAddress}&port=${serverPort}`
        );
      },
    },
    {
      name: "Server Access",
      isActive: $page.url.pathname === "/server-access",
      onClick: () => {
        goto(`/server-access?address=${serverAddress}&port=${serverPort}`);
      },
    },
  ];

  onMount(async () => {
    try {
      const response = await $taService.getGlobalConfiguration(serverAddress, serverPort);
      if (response.details.oneofKind === "getGlobalConfiguration") {
        items = [...items, {
          name: "Debug Page",
          isActive: $page.url.pathname === "/tournament/debug",
          onClick: () => {
            goto(`/tournament/debug?tournamentId=${tournamentId}&address=${serverAddress}&port=${serverPort}`);
          },
        }];
      }
    } catch {
      // Users without server-management access simply do not see this item.
    }
  });
</script>

<TaDrawer
  onHomeClicked={() => {
    goto(`/`);
  }}
  {items}
>
  <slot />
</TaDrawer>
