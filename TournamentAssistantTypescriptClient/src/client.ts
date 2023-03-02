import { w3cwebsocket } from 'websocket';
import { CustomEventEmitter } from './custom-event-emitter';
import { Packet, ForwardingPacket } from './models/packets';

// Created by Moon on 6/11/2022

export type ClientEvents = {
    packetReceived: Packet;
    connectedToServer: {};
    failedToConnectToServer: {};
    disconnectedFromServer: {};
};

export class Client extends CustomEventEmitter<ClientEvents> {
    private address: string;
    private port: string;
    private token: string;
    private websocket: w3cwebsocket | undefined;
    private websocketWasConnected = false;

    constructor(address: string, port: string, token?: string) {
        super();
        this.address = address;
        this.port = port;
        this.token = token ?? '';
    }

    public get isConnected() {
        return this.websocket?.readyState === w3cwebsocket.OPEN
    }

    public connect() {
        this.websocket = new w3cwebsocket(`wss://${this.address}:${this.port}`);
        this.websocket.binaryType = 'arraybuffer';

        if (!this.websocket) {
            this.emit('failedToConnectToServer', {});
            return;
        }

        this.websocket.onopen = () => {
            this.websocketWasConnected = true;
            this.emit('connectedToServer', {});
        };

        this.websocket.onmessage = (event) => {
            if (event.data instanceof ArrayBuffer) {
                const packet = Packet.fromBinary(new Uint8Array(event.data));
                this.emit('packetReceived', packet);
            }
        };

        this.websocket.onclose = () => {
            if (this.websocketWasConnected) {
                this.emit('disconnectedFromServer', {});
            } else {
                this.emit('failedToConnectToServer', {});
            }
        };

        this.websocket.onerror = (error) => {
            console.error(error);
        };
    }

    public disconnect() {
        this.websocket?.close();
    }

    public setToken(token: string) {
        this.token = token;
    }

    public send(packet: Packet, ids?: string[]): void {
        let toSend = packet;

        if (ids) {
            const toForward: ForwardingPacket = {
                packet,
                forwardTo: ids,
            };

            toSend = {
                token: this.token,
                from: packet.from,
                id: packet.id,
                packet: {
                    oneofKind: 'forwardingPacket',
                    forwardingPacket: toForward,
                },
            };
        }

        this.websocket?.send(Packet.toBinary(toSend));
    }
}
