/* eslint-disable @typescript-eslint/no-var-requires */
import pkg from '../package.json' assert { type: 'json' };
import esbuild from 'esbuild';
import GlobalsPlugin from 'esbuild-plugin-globals';
import { polyfillNode } from "esbuild-plugin-polyfill-node";

esbuild.build({
    entryPoints: ['src/index.ts'],
    bundle: true,
    outfile: pkg.exports['.'].default,
    globalName: 'TournamentAssistantClient',
    platform: 'browser',
    define: {
        global: 'window',
    },
    target: ['chrome80'],
    format: 'esm',
    plugins: [
        GlobalsPlugin({
            ws: 'WebSocket',
        }),
        polyfillNode(),
    ],
});