<script lang="ts">
  import { Icon } from "@smui/button";
  import { filedrop } from "filedrop-svelte";
  import type { Files } from "filedrop-svelte/file";
  import type { FileDropOptions } from "filedrop-svelte/options";

  export let onFileSelected: (file: File) => void = (a) => {};
  export let disabled = false;

  let timer: NodeJS.Timeout | undefined;
  let hoveredWithFile = false;
  let error = false;
  let dropzoneClass = "";

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
    border: 1px solid var(--mdc-theme-text-secondary-on-background);
    border-radius: 5px;
    padding: 0 16px;
    background: var(--background-color);
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
      background: var(--background-color-shaded-1);
      transition: 0.2s;
    }
  }

  .hovered-with-file {
    background: var(--mdc-theme-primary-shaded);
    border: 1px dashed var(--mdc-theme-text-primary-on-background);
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
    //border: 3px dashed var(--mdc-theme-primary);
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
      transform: scale(1.2, 1.2);
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
