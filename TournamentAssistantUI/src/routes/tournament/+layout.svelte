<script lang="ts">
  import { goto } from "$app/navigation";
  import { page } from "$app/stores";
  import TaDrawer from "$lib/components/TADrawer.svelte";
  import { getUserIdFromToken } from "$lib/services/jwtService";
  import { authToken } from "$lib/stores";

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
  ];

  if (getUserIdFromToken($authToken) === "229408465787944970") {
    items = [
      ...items,
      {
        name: "Debug Page",
        isActive: $page.url.pathname === "/tournament/debug",
        onClick: () => {
          goto(
            `/tournament/debug?tournamentId=${tournamentId}&address=${serverAddress}&port=${serverPort}`
          );
        },
      },
    ];
  }
</script>

<TaDrawer
  onHomeClicked={() => {
    goto(`/`);
  }}
  {items}
>
  <slot />
</TaDrawer>
