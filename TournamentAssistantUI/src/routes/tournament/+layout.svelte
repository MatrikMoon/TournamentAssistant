<script lang="ts">
  import { goto } from "$app/navigation";
  import { page } from "$app/stores";
  import TaDrawer from "$lib/components/TADrawer.svelte";

  let serverAddress = $page.url.searchParams.get("address")!;
  let serverPort = $page.url.searchParams.get("port")!;
  let tournamentId = $page.url.searchParams.get("tournamentId")!;

  console.log($page.url);
</script>

<TaDrawer
  items={[
    {
      name: "Matches",
      isActive: () => $page.url.pathname === "/tournament/match-select",
      onClick: () => {
        goto(
          `/tournament/match-select?tournamentId=${tournamentId}&address=${serverAddress}&port=${serverPort}`
        );
      },
    },
    {
      name: "Qualifiers",
      isActive: () => $page.url.pathname === "/tournament/qualifier-select",
      onClick: () => {
        goto(
          `/tournament/qualifier-select?tournamentId=${tournamentId}&address=${serverAddress}&port=${serverPort}`
        );
      },
    },
  ]}
>
  <slot />
</TaDrawer>
