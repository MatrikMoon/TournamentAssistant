import dts from 'rollup-plugin-dts';
import path from 'path';
import { join } from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url); // get the resolved path to the file
const __dirname = path.dirname(__filename); // get the name of the directory

export default {
    input: join(__dirname, './dist/index.d.ts'), // Entry point of your type definitions
    output: {
        file: join(__dirname, './dist/index.d.ts'), // Output file
        format: 'es'
    },
    plugins: [dts()]
};