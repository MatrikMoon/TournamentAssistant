<script lang="ts">
  import { goto } from "$app/navigation";
  import TournamentSelection from "../lib/pages/TournamentSelection.svelte";
</script>

<div>
  <TournamentSelection
    onTournamentSelected={(id) => goto(`/tournament?id=${id}`)}
  />
</div>
