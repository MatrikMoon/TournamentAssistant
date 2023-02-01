<script>
  import { page } from "$app/stores";
  import Tournament from "$lib/pages/Tournament.svelte";
</script>

<div>
  <Tournament tournamentId={$page.url.searchParams.get("id")} />
</div>
