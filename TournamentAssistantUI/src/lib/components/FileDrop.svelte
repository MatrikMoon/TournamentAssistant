<script lang="ts">
  import { Icon } from "@smui/button";
  import { filedrop } from "filedrop-svelte";
  import type { Files } from "filedrop-svelte/file";
  import type { FileDropOptions } from "filedrop-svelte/options";
  import { onDestroy } from "svelte";

  export let img: Uint8Array | undefined = undefined;
  export let onFileSelected: (file: File) => void = (a) => {};
  export let disabled = false;

  let timer: NodeJS.Timeout | undefined;
  let hoveredWithFile = false;
  let error = false;
  let dropzoneClass = "";
  let imageUrl: string | undefined;

  $: if (img && img.length > 1) {
    if (imageUrl) {
      URL.revokeObjectURL(imageUrl);
    }

    const blob = new Blob([img], { type: "image/jpeg" });
    imageUrl = URL.createObjectURL(blob);
  }

  onDestroy(() => {
    if (imageUrl) {
      URL.revokeObjectURL(imageUrl);
    }
  });

  $: {
    dropzoneClass = hoveredWithFile ? " hovered-with-file" : "";
    dropzoneClass += error ? " error" : "";
  }

  //Only debounce going from hovered to not hovered
  const debounceHoveredWithFile = (newValue: boolean) => {
    if (!hoveredWithFile) {
      hoveredWithFile = newValue;
    } else {
      clearTimeout(timer);
      timer = setTimeout(() => {
        hoveredWithFile = newValue;
      }, 100);
    }
  };

  let files: Files;
  let options: FileDropOptions = {
    windowDrop: false,
    hideInput: true,
    multiple: false,
    accept: [".jpg", ".png", ".svg", ".gif"],
    maxSize: 5000000, //5MB max
    disabled,
  };
</script>

<div
  class="dropzone{dropzoneClass}"
  use:filedrop={options}
  on:filedrop={(filedrop) => {
    debounceHoveredWithFile(false);

    files = filedrop.detail.files;

    if (files?.rejected.length > 0) {
      error = true;
    } else if (files?.accepted) {
      onFileSelected(files?.accepted[0] ?? "");
    }
  }}
  on:filedragenter={(filedragenter) => {
    error = false;
    debounceHoveredWithFile(true);
  }}
  on:filedragleave={(filedragleave) => {
    debounceHoveredWithFile(false);
  }}
  on:filedragover={(filedragover) => {
    debounceHoveredWithFile(true);
  }}
>
  {#if files?.accepted[0]}
    <img
      alt=""
      class={"selected-image"}
      src={URL.createObjectURL(files?.accepted[0])}
    />
  {:else if imageUrl}
    <img alt="" class={"selected-image"} src={imageUrl} />
  {:else}
    <Icon class="material-icons">add</Icon>
    <div class="dropzone-label">Add an Image</div>
  {/if}
</div>

<style lang="scss">
  .dropzone {
    cursor: default;
    display: flex;
    align-items: center;
    border-radius: 5px;
    padding: 0 16px;
    background-color: rgba($color: #000000, $alpha: 0.1);
    box-shadow: 5px 5px 5px rgba($color: #000000, $alpha: 0.2);
    min-height: 55px; // To match mdc-text-field (55 + 1 border)

    //Transition back from hovered if it was hovered
    transform: scale(1, 1);

    //Hover color transition time
    transition: 0.3s;

    .dropzone-label {
      padding-left: 1vmin;
      color: var(--mdc-theme-text-secondary-on-background);
    }

    .selected-image {
      height: fit-content;
      width: fit-content;
      max-width: 200px;
      max-height: 200px;
    }

    :global(.material-icons) {
      color: var(--mdc-theme-text-secondary-on-background);
    }

    &:hover {
      background-color: rgba($color: #000000, $alpha: 0.1);
      transition: 0.2s;
    }
  }

  .hovered-with-file {
    box-shadow: 0 0 10px rgba($color: red, $alpha: 0.2);
    animation: grow 0.5s ease forwards;

    .dropzone-label {
      padding-left: 1vmin;
      color: var(--mdc-theme-text-primary-on-background);
    }

    :global(.material-icons) {
      color: var(--mdc-theme-text-primary-on-background);
    }
  }

  .error {
    animation: shake 0.2s linear;
  }

  @keyframes shake {
    0% {
      transform: translate(1px, 1px) rotate(0deg);
    }
    10% {
      transform: translate(-1px, -2px) rotate(-1deg);
    }
    20% {
      transform: translate(-3px, 0px) rotate(1deg);
    }
    30% {
      transform: translate(3px, 2px) rotate(0deg);
    }
    40% {
      transform: translate(1px, -1px) rotate(1deg);
    }
    50% {
      transform: translate(-1px, 2px) rotate(-1deg);
    }
    60% {
      transform: translate(-3px, 1px) rotate(0deg);
    }
    70% {
      transform: translate(3px, 1px) rotate(-1deg);
    }
    80% {
      transform: translate(-1px, -1px) rotate(1deg);
    }
    90% {
      transform: translate(1px, 2px) rotate(0deg);
    }
    100% {
      transform: translate(1px, -2px) rotate(-1deg);
    }
  }

  @keyframes grow {
    from {
      transform: scale(1, 1);
    }

    to {
      transform: scale(1.1, 1.1);
    }
  }

  @keyframes shrink {
    to {
      transform: scale(1, 1);
    }

    from {
      transform: scale(1.2, 1.2);
    }
  }
</style>
