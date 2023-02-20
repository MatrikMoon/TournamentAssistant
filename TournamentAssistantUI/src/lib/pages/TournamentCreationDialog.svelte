<script lang="ts">
    import Dialog, { Header, Title, Content, Actions } from "@smui/dialog";
    import Button, { Label } from "@smui/button";
    import { client } from "$lib/stores";
    import type { CoreServer } from "tournament-assistant-client/dist/models/models";
    import Select, { Option } from "@smui/select";
    import Textfield from "@smui/textfield";
    import IconButton from "@smui/icon-button";
    import FormField from "@smui/form-field";
    import Switch from "@smui/switch";
    import LayoutGrid, { Cell } from "@smui/layout-grid";
    import type { Files } from "$lib/components/FileDrop/file";
    import type { FileDropOptions } from "$lib/components/FileDrop/options";
    import filedrop from "$lib/components/FileDrop/filedrop";

    export let open = false;
    let knownHosts: CoreServer[] = $client.stateManager.getKnownServers();

    let modalCloseResult: string;
    let tournamentName: string = "";
    let enableTeams = false;
    let host: CoreServer;

    function closeHandler(event: CustomEvent<{ action: string }>) {
        switch (event.detail.action) {
            case "cancel":
                modalCloseResult = "Closed without response.";
                break;
            case "create":
                modalCloseResult = "Accepted.";
                break;
        }
    }

    //If we don't yet have any host information
    $: if (open && knownHosts.length == 0) {
        console.log("Getting known servers for modal");

        $client.connect();
        $client.on("connectedToServer", () => {
            knownHosts = $client.stateManager.getKnownServers();
            $client.disconnect();
        });
    }

    //File Drop
    let files: Files;
    let options: FileDropOptions = { windowDrop: false, hideInput: false };
</script>

<Dialog
    bind:open
    fullscreen
    aria-labelledby="fullscreen-title"
    aria-describedby="fullscreen-content"
    on:SMUIDialog:closed={closeHandler}
>
    <Header>
        <Title>Create a Tournament</Title>
        <IconButton action="cancel" class="material-icons">close</IconButton>
    </Header>
    <Content>
        <LayoutGrid>
            <Cell span={12}>
                <Select
                    bind:value={host}
                    key={(test) => `${test?.address}:${test?.websocketPort}`}
                    label="Server"
                >
                    {#each knownHosts as host}
                        <Option value={host}>
                            {`${host.address}:${host.websocketPort}`}
                        </Option>
                    {/each}
                </Select>
            </Cell>
            <Cell span={4}>
                <Textfield
                    bind:value={tournamentName}
                    variant="outlined"
                    label="Tournament Name"
                />
            </Cell>
            <Cell span={4}>
                <FormField>
                    <Switch bind:checked={enableTeams} />
                    <span slot="label">Enable Teams</span>
                </FormField>
            </Cell>
            <Cell span={4}>
                <div
                    use:filedrop={options}
                    on:filedrop={(e) => {
                        files = e.detail.files;
                    }}
                    on:filedragenter={(filedragenter) =>
                        console.log({ filedragenter })}
                    on:filedragleave={(filedragleave) =>
                        console.log({ filedragleave })}
                    on:filedragover={(filedragover) =>
                        console.log({ filedragover })}
                    on:filedialogcancel={(filedialogcancel) =>
                        console.log({ filedialogcancel })}
                    on:filedialogclose={(filedialogclose) =>
                        console.log({ filedialogclose })}
                    on:filedialogopen={(filedialogopen) =>
                        console.log({ filedialogopen })}
                    on:windowfiledragenter={(windowfiledragenter) =>
                        console.log({ windowfiledragenter })}
                    on:windowfiledragleave={(windowfiledragleave) =>
                        console.log({ windowfiledragleave })}
                    on:windowfiledragover={(windowfiledragover) =>
                        console.log({ windowfiledragover })}
                >
                    Drag &amp; drop files
                    {files}
                </div>

                {#if files}
                    <h3>Accepted files</h3>
                    <ul>
                        {#each files.accepted as file}
                            <li>{file.name} - {file.size}</li>
                        {/each}
                    </ul>
                    <h3>Rejected files</h3>
                    <ul>
                        {#each files.rejected as rejected}
                            <li>
                                {rejected.file.name} - {rejected.error.message}
                            </li>
                        {/each}
                    </ul>
                {/if}
            </Cell>
        </LayoutGrid>
    </Content>
    <Actions>
        <Button action="cancel" defaultAction>
            <Label>Cancel</Label>
        </Button>
        <Button action="create">
            <Label>Create</Label>
        </Button>
    </Actions>
</Dialog>
