<script lang="ts">
  import { goto } from "$app/navigation";
  import { page } from "$app/stores";
  import LayoutGrid, { Cell } from "@smui/layout-grid";
  import { onMount } from "svelte";
  import Button, { Label } from "@smui/button";
  import type { Tournament } from "tournament-assistant-client";
  import { taService } from "$lib/stores";
  import QualifierList from "$lib/components/QualifierList.svelte";

  let serverAddress = $page.url.searchParams.get("address")!;
  let serverPort = $page.url.searchParams.get("port")!;
  let tournamentId = $page.url.searchParams.get("tournamentId")!;

  let tournament: Tournament;

  onMount(async () => {
    console.log("onMount joinTournament/getTournament");

    await $taService.joinTournament(serverAddress, serverPort, tournamentId);

    tournament = (await $taService.getTournament(
      serverAddress,
      serverPort,
      tournamentId,
    ))!;
  });

  function onCreateQualifierClick() {
    goto(
      `/tournament/qualifier?tournamentId=${tournamentId}&address=${serverAddress}&port=${serverPort}`,
    );
  }

  function onQualifierSelected(id: string) {
    goto(
      `/tournament/qualifier?tournamentId=${tournamentId}&address=${serverAddress}&port=${serverPort}&qualifierId=${id}`,
    );
  }
</script>

<div class="page">
  <div class="qualifier-title">Create or edit a qualifier</div>
  <div class="qualifier-list">
    <div class="qualifier-list-title">Qualifiers</div>
    <div class="shaded-box">
      <QualifierList {tournamentId} {onQualifierSelected} />
      <div class="button">
        <Button variant="raised" on:click={onCreateQualifierClick}>
          <Label>Create Qualifier</Label>
        </Button>
      </div>
    </div>
  </div>
</div>

<style lang="scss">
  .page {
    display: flex;
    flex-direction: column;
    align-items: center;

    .qualifier-title {
      color: var(--mdc-theme-text-primary-on-background);
      background-color: rgba($color: #000000, $alpha: 0.1);
      border-radius: 2vmin;
      text-align: center;
      font-size: 2rem;
      font-weight: 100;
      line-height: 1.1;
      padding: 2vmin;
      width: -webkit-fill-available;
      width: -moz-available;
    }

    .qualifier-list {
      width: -webkit-fill-available;
      width: -moz-available;

      // mimics other pages' use of margin, cells, and padding
      max-width: 690px;
      margin-top: 10px;

      .qualifier-list-title {
        color: var(--mdc-theme-text-primary-on-background);
        background-color: rgba($color: #000000, $alpha: 0.1);
        border-radius: 2vmin 2vmin 0 0;
        text-align: center;
        font-size: 2rem;
        font-weight: 100;
        line-height: 1.1;
        padding: 2vmin;
      }
    }

    .shaded-box {
      background-color: rgba($color: #000000, $alpha: 0.1);

      .button {
        text-align: center;
        padding-bottom: 2vmin;
      }
    }
  }
</style>
