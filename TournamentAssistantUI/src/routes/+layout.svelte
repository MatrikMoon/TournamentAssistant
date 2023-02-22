<script>
  import "../app.scss";
  import Color from "color";

  const root = window.document.querySelector(":root");

  const primaryColor = new Color("#d60000");
  const backgroundColor = new Color("#424242");

  function getTextColorForBackground() {
    const perspectiveLuminence =
      1 -
      (0.299 * backgroundColor.red() +
        0.587 * backgroundColor.green() +
        0.114 * backgroundColor.blue()) /
        255;

    return perspectiveLuminence < 0.5 ? new Color("black") : new Color("white");
  }

  //Base
  root.style.setProperty("--mdc-theme-primary", primaryColor.toString());
  root.style.setProperty("--mdc-theme-primary-shaded", primaryColor.alpha(0.5));
  root.style.setProperty("--background-color", backgroundColor.toString());
  root.style.setProperty("--mdc-theme-surface", backgroundColor.toString());
  root.style.setProperty(
    "--background-color-shaded",
    backgroundColor.darken(0.1)
  );

  //Text
  root.style.setProperty(
    "--mdc-theme-text-primary-on-background",
    getTextColorForBackground()
  );
  root.style.setProperty(
    "--mdc-theme-text-secondary-on-background",
    getTextColorForBackground().alpha(0.7)
  );
  root.style.setProperty(
    "--mdc-theme-text-hint-on-background",
    getTextColorForBackground().alpha(0.7)
  );

  //Switch
  root.style.setProperty(
    "--mdc-switch-selected-track-color",
    primaryColor.alpha(0.4)
  );
  root.style.setProperty(
    "--mdc-switch-selected-hover-track-color",
    primaryColor.alpha(0.4)
  );
  root.style.setProperty(
    "--mdc-switch-selected-focus-track-color",
    primaryColor.alpha(0.4)
  );
  root.style.setProperty(
    "--mdc-switch-selected-pressed-track-color",
    primaryColor.alpha(0.4)
  );
  root.style.setProperty(
    "--mdc-switch-selected-hover-handle-color",
    primaryColor.alpha(0.7)
  );
  root.style.setProperty(
    "--mdc-switch-selected-focus-handle-color",
    primaryColor
  );
  root.style.setProperty(
    "--mdc-switch-selected-pressed-handle-color",
    primaryColor.alpha(0.7)
  );
  root.style.setProperty(
    "--mdc-switch-selected-pressed-handle-color",
    primaryColor.alpha(0.7)
  );
</script>

<main class="container">
  <slot />
</main>
