<script lang="ts">
  import { goto } from "$app/navigation";
  import { page } from "$app/stores";

  let serverAddress = $page.url.searchParams.get("address")!;
  let serverPort = $page.url.searchParams.get("port")!;
  let tournamentId = $page.url.searchParams.get("tournamentId")!;

  goto(
    `/tournament/match-select?tournamentId=${tournamentId}&address=${serverAddress}&port=${serverPort}`,
    {
      replaceState: true,
    }
  );
</script>
