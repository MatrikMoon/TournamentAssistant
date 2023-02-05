<script lang="ts">
    import Dialog, { Header, Title, Content, Actions } from "@smui/dialog";
    import Button, { Label } from "@smui/button";
    import { client } from "$lib/stores";
    import type { CoreServer } from "tournament-assistant-client/dist/models/models";
    import Autocomplete from "@smui-extra/autocomplete";
    import Textfield from "@smui/textfield";
    import IconButton from "@smui/icon-button";
    import FormField from "@smui/form-field";
    import Switch from "@smui/switch";

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
        <Textfield bind:value={tournamentName} label="Tournament Name" />
        <FormField>
            <Switch bind:checked={enableTeams} />
            <span slot="label"> Enable Teams </span>
        </FormField>
        <Autocomplete
            options={knownHosts}
            getOptionLabel={(option) =>
                option ? `${option.address}:${option.port}` : ""}
            bind:value={host}
            label="Server"
        />
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
