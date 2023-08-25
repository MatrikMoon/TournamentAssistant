<script lang="ts">
  import { page } from "$app/stores";
  import LayoutGrid, { Cell } from "@smui/layout-grid";
  import AddSong from "$lib/components/AddSong.svelte";
  import FormField from "@smui/form-field";
  import Textfield from "@smui/textfield";
  import FileDrop from "$lib/components/FileDrop.svelte";
  import { taService } from "$lib/stores";
  import type { QualifierEvent } from "tournament-assistant-client";
  import Switch from "@smui/switch";
  import { onMount } from "svelte";

  let serverAddress = $page.url.searchParams.get("address")!;
  let serverPort = $page.url.searchParams.get("port")!;
  let tournamentId = $page.url.searchParams.get("tournamentId")!;
  let qualifierId = $page.url.searchParams.get("qualifierId")!;

  $: editDisabled = qualifierId == null;

  let qualifier: QualifierEvent = {
    guid: "",
    name: "",
    guild: {
      id: BigInt(0),
      name: "dummy",
    },
    infoChannel: {
      id: BigInt(0),
      name: "dummy",
    },
    qualifierMaps: [],
    sendScoresToInfoChannel: false,
    flags: 0,
    image: new Uint8Array([1]),
  };

  onMount(async () => {});

  //Don't allow creation unless we have all the required fields
  let canCreate = false;
  $: if (qualifier.name.length > 0) {
    canCreate = true;
  }

  const onCreateClicked = async () => {
    await $taService.createQualifier(
      serverAddress,
      serverPort,
      tournamentId,
      qualifier
    );
  };
</script>

<div>
  <div class="qualifier-title">
    Select a song, difficulty, and characteristic
  </div>
  <LayoutGrid>
    <Cell span={4}>
      <Textfield
        bind:value={qualifier.name}
        variant="outlined"
        label="Qualifier Name"
        disabled={editDisabled}
      />
    </Cell>

    <Cell span={4}>
      <FileDrop
        onFileSelected={async (file) => {
          const loadedImage = await file?.arrayBuffer();

          qualifier.image = loadedImage
            ? new Uint8Array(loadedImage)
            : new Uint8Array([1]);
        }}
        disabled={editDisabled}
      />
    </Cell>
    <Cell span={4}>
      <FormField>
        <Switch />
        <span slot="label">Hide scores from players</span>
      </FormField>
      <FormField>
        <Switch />
        <span slot="label">Disable Scoresaber submission</span>
      </FormField>
      <FormField>
        <Switch />
        <span slot="label">Enable discord bot leaderboard message</span>
      </FormField>
    </Cell>
    <Cell span={4}>
      <AddSong {serverAddress} {serverPort} {tournamentId} />
    </Cell>
  </LayoutGrid>
</div>

<style lang="scss">
  .grid-cell {
    background-color: rgba($color: #000000, $alpha: 0.1);
  }

  .qualifier-title {
    color: var(--mdc-theme-text-primary-on-background);
    background-color: rgba($color: #000000, $alpha: 0.1);
    border-radius: 2vmin;
    text-align: center;
    font-size: 2rem;
    font-weight: 100;
    line-height: 1.1;
    padding: 2vmin;
  }
</style>
