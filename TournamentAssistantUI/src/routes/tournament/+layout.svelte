<script lang="ts">
  import { goto } from "$app/navigation";
  import { page } from "$app/stores";
  import TaDrawer from "$lib/components/TADrawer.svelte";
  import { MockPlayer } from "$lib/mockPlayer";

  $: serverAddress = $page.url.searchParams.get("address")!;
  $: serverPort = $page.url.searchParams.get("port")!;
  $: tournamentId = $page.url.searchParams.get("tournamentId")!;
</script>

<TaDrawer
  items={[
    {
      name: "Matches",
      isActive: $page.url.pathname === "/tournament/match-select",
      onClick: () => {
        goto(
          `/tournament/match-select?tournamentId=${tournamentId}&address=${serverAddress}&port=${serverPort}`,
        );
      },
    },
    {
      name: "Qualifiers",
      isActive: $page.url.pathname === "/tournament/qualifier-select",
      onClick: () => {
        goto(
          `/tournament/qualifier-select?tournamentId=${tournamentId}&address=${serverAddress}&port=${serverPort}`,
        );
      },
    },
    {
      name: "[DEBUG] - Add mock players",
      isActive: false,
      onClick: async () => {
        var mockPlayer = new MockPlayer();
        await mockPlayer.connect(serverAddress, serverPort);
        await mockPlayer.join(tournamentId);

        var mockPlayer2 = new MockPlayer();
        await mockPlayer2.connect(serverAddress, serverPort);
        await mockPlayer2.join(tournamentId);
      },
    },
  ]}
>
  <slot />
</TaDrawer>
