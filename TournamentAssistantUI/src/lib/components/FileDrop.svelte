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

  let files: Files;
  let options: FileDropOptions = {
    windowDrop: false,
    hideInput: true,
    multiple: false,
    accept: [".jpg", ".png", ".svg", ".gif"],
    maxSize: 5000000, // 5MB max
    disabled,
  };

  $: if ((img && img.length > 1) || files?.accepted?.length > 0) {
    if (imageUrl) {
      URL.revokeObjectURL(imageUrl);
    }

    if (img) {
      const blob = new Blob([img], { type: "image/jpeg" });
      imageUrl = URL.createObjectURL(blob);
    } else {
      imageUrl = URL.createObjectURL(files.accepted[0]);
    }
  } else if (imageUrl) {
    URL.revokeObjectURL(imageUrl);
    imageUrl = undefined;
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

  // Only debounce going from hovered to not hovered
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
  {#if imageUrl}
    <img alt="" class={"selected-image"} src={imageUrl} />
  {:else}
    <Icon class="material-icons">photo_camera</Icon>
  {/if}
</div>

<style lang="scss">
  .dropzone {
    cursor: default;
    display: flex;
    align-items: center;
    justify-content: center;
    border-radius: 50%;
    background-color: rgba($color: #000000, $alpha: 0.1);
    box-shadow: 5px 5px 5px rgba($color: #000000, $alpha: 0.2);
    height: 55px; // To match mdc-text-field (55 + 1 border)
    width: 55px;

    // Transition back from hovered if it was hovered
    transform: scale(1, 1);

    // Hover color transition time
    transition: 0.3s;

    .selected-image {
      width: 100%;
      height: 100%;
      border-radius: 50%;
      object-fit: cover;
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
