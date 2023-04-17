import { EventEmitter } from 'events';

export class CustomEventEmitter<T extends Record<string, any>> {
    private emitter = new EventEmitter();

    public get on(): <K extends Extract<keyof T, string | symbol>>(eventName: K, fn: (params: T[K]) => void) => void {
        return this.emitter.on.bind(this.emitter);
    }

    public get once(): <K extends Extract<keyof T, string | symbol>>(eventName: K, fn: (params: T[K]) => void) => void {
        return this.emitter.once.bind(this.emitter);
    }

    public get emit(): <K extends Extract<keyof T, string | symbol>>(eventName: K, params: T[K]) => void {
        return this.emitter.emit.bind(this.emitter);
    }

    public get removeListener(): <K extends Extract<keyof T, string | symbol>>(eventName: K, fn: (params: T[K]) => void) => void {
        return this.emitter.removeListener.bind(this.emitter);
    }
}
