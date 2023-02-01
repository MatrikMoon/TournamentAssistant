<script lang="ts">
  import { goto } from "$app/navigation";
  import TournamentSelection from "../lib/pages/TournamentSelection.svelte";

  function onTournamentSelected(id: string) {
    goto(`/tournament?id=${id}`);
  }
</script>

<div>
  <TournamentSelection {onTournamentSelected} />
</div>
