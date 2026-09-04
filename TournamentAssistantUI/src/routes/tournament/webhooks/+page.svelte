<script lang="ts">
	import { onMount } from "svelte";
	import { page } from "$app/stores";
	import { taService } from "$lib/stores";
	import {
		Response_ResponseType,
		Webhook_Trigger,
		type Webhook,
	} from "tournament-assistant-client";

	const serverAddress = $page.url.searchParams.get("address")!;
	const serverPort = $page.url.searchParams.get("port")!;
	const tournamentId = $page.url.searchParams.get("tournamentId")!;

	const triggerOptions = [
		[
			Webhook_Trigger.TournamentUpdated,
			"Tournament updated",
			"Tournament settings, teams, pools, and roles change",
		],
		[
			Webhook_Trigger.TournamentDeleted,
			"Tournament deleted",
			"The tournament is deleted",
		],
		[
			Webhook_Trigger.UserAdded,
			"User joined",
			"A user joins the tournament",
		],
		[
			Webhook_Trigger.UserUpdated,
			"User updated",
			"A user's state or information changes",
		],
		[Webhook_Trigger.UserLeft, "User left", "A user leaves or disconnects"],
		[
			Webhook_Trigger.MatchCreated,
			"Match created",
			"A match is created in this tournament",
		],
		[
			Webhook_Trigger.MatchUpdated,
			"Match updated",
			"Players, map, leader, or match state changes",
		],
		[Webhook_Trigger.MatchDeleted, "Match deleted", "A match is deleted"],
		[
			Webhook_Trigger.QualifierCreated,
			"Qualifier created",
			"A qualifier is created",
		],
		[
			Webhook_Trigger.QualifierUpdated,
			"Qualifier updated",
			"Qualifier settings or maps change",
		],
		[
			Webhook_Trigger.QualifierDeleted,
			"Qualifier deleted",
			"A qualifier is deleted",
		],
		[
			Webhook_Trigger.QualifierScoreSubmitted,
			"Qualifier score submitted",
			"A qualifier attempt is submitted",
		],
		[
			Webhook_Trigger.SongFinished,
			"Song finished",
			"A match player reports a final result",
		],
	] as const;

	let webhooks: Webhook[] = [];
	let secrets: Record<string, string> = {};
	let replaceSecrets: Record<string, boolean> = {};
	let loading = true;
	let saving = "";
	let status = "";
	let error = "";
	let newUrl = "";
	let newSecret = "";
	let newTriggers = BigInt(Webhook_Trigger.All);

	const hasTrigger = (triggers: bigint, trigger: Webhook_Trigger) =>
		(triggers & BigInt(trigger)) !== BigInt(0);

	function isValidHttpsUrl(value: string) {
		try {
			const url = new URL(value);
			return (
				url.protocol === "https:" &&
				!!url.hostname &&
				!url.username &&
				!url.password
			);
		} catch {
			return false;
		}
	}

	function toggleTrigger(webhook: Webhook, trigger: Webhook_Trigger) {
		const value = BigInt(trigger);
		webhook.triggers = hasTrigger(webhook.triggers, trigger)
			? webhook.triggers & ~value
			: webhook.triggers | value;
		webhooks = [...webhooks];
	}

	function toggleNewTrigger(trigger: Webhook_Trigger) {
		const value = BigInt(trigger);
		newTriggers = hasTrigger(newTriggers, trigger)
			? newTriggers & ~value
			: newTriggers | value;
	}

	async function loadWebhooks() {
		loading = true;
		error = "";
		try {
			await $taService.joinTournament(
				serverAddress,
				serverPort,
				tournamentId,
			);
			const response = await $taService.getWebhooks(
				serverAddress,
				serverPort,
				tournamentId,
			);
			if (
				response.type === Response_ResponseType.Success &&
				response.details.oneofKind === "getWebhooks"
			) {
				webhooks = response.details.getWebhooks.webhooks;
			} else {
				error =
					response.details.oneofKind === "permissionError"
						? `Missing permission: ${response.details.permissionError.requiredPermission}`
						: "Could not load webhooks.";
			}
		} catch (reason) {
			error = reason instanceof Error ? reason.message : String(reason);
		} finally {
			loading = false;
		}
	}

	async function createWebhook() {
		if (!isValidHttpsUrl(newUrl.trim())) {
			error = "Enter a valid HTTPS endpoint URL.";
			return;
		}
		saving = "new";
		error = "";
		status = "";
		try {
			const response = await $taService.createWebhook(
				serverAddress,
				serverPort,
				tournamentId,
				newUrl.trim(),
				newTriggers,
				newSecret,
			);
			if (
				response.type === Response_ResponseType.Success &&
				response.details.oneofKind === "createWebhook"
			) {
				newUrl = "";
				newSecret = "";
				newTriggers = BigInt(Webhook_Trigger.All);
				status = "Webhook created.";
				await loadWebhooks();
			} else {
				error =
					response.details.oneofKind === "createWebhook"
						? response.details.createWebhook.message
						: "Could not create webhook.";
			}
		} catch (reason) {
			error = reason instanceof Error ? reason.message : String(reason);
		} finally {
			saving = "";
		}
	}

	async function saveWebhook(webhook: Webhook) {
		if (!isValidHttpsUrl(webhook.url.trim())) {
			error = "Enter a valid HTTPS endpoint URL.";
			return;
		}
		saving = webhook.guid;
		error = "";
		status = "";
		try {
			const response = await $taService.updateWebhook(
				serverAddress,
				serverPort,
				tournamentId,
				webhook.guid,
				webhook.url.trim(),
				webhook.triggers,
				replaceSecrets[webhook.guid] ?? false,
				secrets[webhook.guid] ?? "",
			);
			if (
				response.type === Response_ResponseType.Success &&
				response.details.oneofKind === "updateWebhook"
			) {
				secrets[webhook.guid] = "";
				replaceSecrets[webhook.guid] = false;
				status = "Webhook updated.";
				await loadWebhooks();
			} else {
				error =
					response.details.oneofKind === "updateWebhook"
						? response.details.updateWebhook.message
						: "Could not update webhook.";
			}
		} catch (reason) {
			error = reason instanceof Error ? reason.message : String(reason);
		} finally {
			saving = "";
		}
	}

	async function deleteWebhook(webhook: Webhook) {
		if (!confirm(`Delete webhook ${webhook.url}?`)) return;
		saving = webhook.guid;
		error = "";
		status = "";
		try {
			const response = await $taService.deleteWebhook(
				serverAddress,
				serverPort,
				tournamentId,
				webhook.guid,
			);
			if (
				response.type === Response_ResponseType.Success &&
				response.details.oneofKind === "deleteWebhook"
			) {
				webhooks = webhooks.filter(
					(item) => item.guid !== webhook.guid,
				);
				status = "Webhook deleted.";
			} else {
				error =
					response.details.oneofKind === "deleteWebhook"
						? response.details.deleteWebhook.message
						: "Could not delete webhook.";
			}
		} catch (reason) {
			error = reason instanceof Error ? reason.message : String(reason);
		} finally {
			saving = "";
		}
	}

	onMount(loadWebhooks);
</script>

<svelte:head><title>Webhooks | TournamentAssistant</title></svelte:head>

<div class="page">
	<header>
		<div>
			<h1>Webhooks</h1>
			<p>Send tournament activity to your HTTPS endpoints.</p>
		</div>
		<button class="secondary" on:click={loadWebhooks} disabled={loading}
			>Refresh</button>
	</header>

	{#if error}<div class="message error">{error}</div>{/if}
	{#if status}<div class="message success">{status}</div>{/if}

	<section class="information">
		<h2>Request format</h2>
		<p>
			Every selected trigger sends an <strong>HTTPS POST</strong> with JSON.
			Realtime scores and replay-stream packets cannot be subscribed to, and
			will not be delivered.
		</p>
		<pre>{`{
  "id": "delivery-guid",
  "timestamp": "2026-09-04T12:00:00.0000000Z",
  "tournamentId": "tournament-guid",
  "oneOfKind": "matchCreated",
  "data": {
    "matchCreated": {
      "tournamentId": "tournament-guid",
      "match": { "guid": "match-guid", "associatedUsers": [] }
    }
  }
}`}</pre>
		<p>
			If a signing secret is configured, verify the lowercase hexadecimal
			HMAC-SHA256 of the exact request body from 
      <code>X-TA-Signature-256</code>. 
      It is prefixed with <code>sha256=</code>. The event name and
			delivery ID are also sent as <code>X-TA-Webhook-Event</code> and
			<code>X-TA-Webhook-Delivery</code>.
		</p>
    <p>
      While the webhooks created will be visible to other admins and those
      with the tournament:webhooks:manage permission, the secret is 
      <strong>NEVER</strong> returned. So once you set and save a webhook secret,
      you may only change it later. 
    </p>
	</section>

	<section class="editor new-webhook">
		<div class="section-title">
			<div>
				<h2>Add endpoint</h2>
				<p>URLs must be absolute and use HTTPS.</p>
			</div>
		</div>
		<label
			>Endpoint URL<input
				type="url"
				bind:value={newUrl}
				placeholder="https://api.beatkhana.com/webhooks/tournamentassistant" /></label>
		<label
			>HMAC signing secret <span>optional</span><input
				type="password"
				bind:value={newSecret}
				placeholder="Secret used to sign request bodies. YOU WILL NOT BE ABLE TO VIEW THIS LATER!" /></label>
		<div class="triggers">
			<h3>Triggers</h3>
			<div class="trigger-grid">
				{#each triggerOptions as option}
					<label class="trigger"
						><input
							type="checkbox"
							checked={hasTrigger(newTriggers, option[0])}
							on:change={() =>
								toggleNewTrigger(option[0])} /><span
							><strong>{option[1]}</strong><small
								>{option[2]}</small
							></span
						></label>
				{/each}
			</div>
		</div>
		<button
			class="primary"
			on:click={createWebhook}
			disabled={saving === "new" ||
				!isValidHttpsUrl(newUrl.trim()) ||
				newTriggers === BigInt(0)}
			>{saving === "new" ? "Creating…" : "Create webhook"}</button>
	</section>

	<div class="section-title list-heading">
		<div>
			<h2>Configured endpoints</h2>
			<p>{webhooks.length} webhook{webhooks.length === 1 ? "" : "s"}</p>
		</div>
	</div>
	{#if loading}
		<div class="empty">Loading webhooks…</div>
	{:else if webhooks.length === 0}
		<div class="empty">No webhook endpoints have been configured.</div>
	{:else}
		<div class="webhook-list">
			{#each webhooks as webhook}
				<section class="editor">
					<div class="webhook-heading">
						<code>{webhook.guid}</code
						>{#if webhook.hasSigningSecret}<span>Signed</span>{/if}
					</div>
					<label
						>Endpoint URL<input
							type="url"
							bind:value={webhook.url} /></label>
					<label
						>HMAC signing secret <span
							>{webhook.hasSigningSecret
								? "leave blank to keep current secret"
								: "optional"}</span
						><input
							type="password"
							bind:value={secrets[webhook.guid]}
							on:input={() =>
								(replaceSecrets[webhook.guid] = true)}
							placeholder={webhook.hasSigningSecret
								? "Current secret is hidden"
								: "Add a signing secret"} /></label>
					{#if webhook.hasSigningSecret}
						<label class="remove-secret"
							><input
								type="checkbox"
								bind:checked={replaceSecrets[webhook.guid]}
								on:change={() => {
									if (replaceSecrets[webhook.guid])
										secrets[webhook.guid] = "";
								}} /> Replace or remove the existing signing secret</label>
					{/if}
					<div class="triggers">
						<h3>Triggers</h3>
						<div class="trigger-grid">
							{#each triggerOptions as option}
								<label class="trigger"
									><input
										type="checkbox"
										checked={hasTrigger(
											webhook.triggers,
											option[0],
										)}
										on:change={() =>
											toggleTrigger(
												webhook,
												option[0],
											)} /><span
										><strong>{option[1]}</strong><small
											>{option[2]}</small
										></span
									></label>
							{/each}
						</div>
					</div>
					<div class="actions">
						<button
							class="danger"
							on:click={() => deleteWebhook(webhook)}
							disabled={saving === webhook.guid}>Delete</button
						><button
							class="primary"
							on:click={() => saveWebhook(webhook)}
							disabled={saving === webhook.guid ||
								!isValidHttpsUrl(webhook.url.trim()) ||
								webhook.triggers === BigInt(0)}
							>{saving === webhook.guid
								? "Saving…"
								: "Save changes"}</button>
					</div>
				</section>
			{/each}
		</div>
	{/if}
</div>

<style lang="scss">
	.page {
		max-width: 1180px;
		margin: 0 auto;
		padding: 2rem;
		color: var(--mdc-theme-text-primary-on-background);
	}
	header,
	.section-title,
	.webhook-heading,
	.actions {
		display: flex;
		align-items: center;
		justify-content: space-between;
		gap: 1rem;
	}
	h1,
	h2,
	h3,
	p {
		margin: 0;
	}
	header p,
	.section-title p,
	label span,
	.information p,
	small {
		color: var(--mdc-theme-text-secondary-on-background);
	}
	header {
		margin-bottom: 1.5rem;
	}
	header h1 {
		font-size: 2rem;
		font-weight: 500;
	}
	.information,
	.editor,
	.empty {
		background: rgba(0, 0, 0, 0.12);
		border-radius: 1rem;
		padding: 1.4rem;
		margin-bottom: 1.25rem;
	}
	.information {
		display: grid;
		gap: 1rem;
	}
	pre {
		margin: 0;
		padding: 1rem;
		overflow: auto;
		border-radius: 0.65rem;
		background: rgba(0, 0, 0, 0.3);
		color: #f0dce0;
		line-height: 1.5;
	}
	code {
		color: var(--mdc-theme-primary);
	}
	.editor {
		display: grid;
		gap: 1rem;
	}
	label {
		display: grid;
		gap: 0.45rem;
		font-weight: 500;
	}
	input[type="url"],
	input[type="password"] {
		box-sizing: border-box;
		width: 100%;
		border: 1px solid rgba(255, 255, 255, 0.15);
		border-radius: 0.6rem;
		padding: 0.8rem 0.9rem;
		background: rgba(0, 0, 0, 0.2);
		color: inherit;
		font: inherit;
	}
	input:focus {
		outline: 2px solid var(--mdc-theme-primary);
		outline-offset: 1px;
	}
	.triggers {
		display: grid;
		gap: 0.75rem;
	}
	.trigger-grid {
		display: grid;
		grid-template-columns: repeat(2, minmax(0, 1fr));
		gap: 0.55rem;
	}
	.trigger {
		display: flex;
		grid-template-columns: none;
		align-items: flex-start;
		gap: 0.7rem;
		padding: 0.75rem;
		border-radius: 0.65rem;
		background: rgba(0, 0, 0, 0.12);
		cursor: pointer;
	}
	.trigger input {
		margin-top: 0.15rem;
		accent-color: var(--mdc-theme-primary);
	}
	.trigger span {
		display: grid;
		gap: 0.2rem;
	}
	.trigger strong {
		color: var(--mdc-theme-text-primary-on-background);
	}
	.trigger small {
		font-weight: 400;
	}
	button {
		border: 0;
		border-radius: 999px;
		padding: 0.7rem 1.1rem;
		font: inherit;
		font-weight: 600;
		cursor: pointer;
	}
	button:disabled {
		cursor: not-allowed;
		opacity: 0.55;
	}
	.primary {
		justify-self: end;
		color: var(--mdc-theme-on-primary);
		background: var(--mdc-theme-primary);
	}
	.secondary {
		color: var(--mdc-theme-primary);
		background: rgba(0, 0, 0, 0.14);
	}
	.danger {
		color: #fff;
		background: #922f3b;
	}
	.list-heading {
		margin: 2rem 0 1rem;
	}
	.webhook-heading code {
		overflow: hidden;
		text-overflow: ellipsis;
	}
	.webhook-heading span {
		border-radius: 999px;
		padding: 0.25rem 0.6rem;
		color: #9ee6bb;
		background: rgba(45, 140, 83, 0.2);
	}
	.remove-secret {
		display: flex;
		grid-template-columns: none;
		align-items: center;
		gap: 0.55rem;
		font-weight: 400;
	}
	.actions {
		justify-content: flex-end;
	}
	.empty {
		text-align: center;
		color: var(--mdc-theme-text-secondary-on-background);
	}
	.message {
		border-radius: 0.7rem;
		padding: 0.85rem 1rem;
		margin-bottom: 1rem;
	}
	.message.error {
		color: #ffc3ca;
		background: rgba(146, 47, 59, 0.28);
	}
	.message.success {
		color: #a8e8bf;
		background: rgba(45, 140, 83, 0.22);
	}
	@media (max-width: 760px) {
		.page {
			padding: 1rem;
		}
		.trigger-grid {
			grid-template-columns: 1fr;
		}
		header {
			align-items: flex-start;
		}
	}
</style>
