<script lang="ts">
    import Paper, { Content } from "@smui/paper";
    import { log } from "../stores";

    function colorFromLogType(logType: string) {
        switch (logType) {
            case "info":
                return "cornflowerblue";
            case "warn":
                return "yellow";
            case "error":
                return "red";
            case "success":
                return "green";
            default:
                return "var(--mdc-theme-text-primary-on-background)";
        }
    }
</script>

<div class="paper-container">
    <Paper class="paper">
        {#each $log as line}
            {#if line.message}
                {#if line.message.split}
                    {#each line.message.split("\n") as splitLine}
                        <Content
                            class="paper-content"
                            style={"color: " + colorFromLogType(line.type)}
                        >
                            {splitLine}
                        </Content>
                    {/each}
                {/if}
            {/if}
        {/each}
    </Paper>
</div>

<style lang="scss">
    .paper-container {
        overflow: auto;

        :global(.paper) {
            :global(.paper-content) {
                white-space: break-spaces;
                font-family: monospace;
            }
        }
    }
</style>
