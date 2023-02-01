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
        }
    }
</script>

<div class="paper-container">
    <Paper class="log-window">
        {#each $log as line}
            {#if line.message.split}
                {#each line.message.split("\n") as splitLine}
                    <Content style={"color: " + colorFromLogType(line.type)}>
                        {splitLine}
                    </Content>
                {/each}
            {/if}
        {/each}
    </Paper>
</div>

<style lang="scss">
    .log-window {
        max-height: 300px;
        overflow: scroll;

        &.smui-paper {
            .smui-paper__content {
                white-space: break-spaces;
                font-family: monospace;
            }
        }
    }
</style>
