import fs from 'fs';
import path from 'path'
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url); // get the resolved path to the file
const __dirname = path.dirname(__filename); // get the name of the directory
const directoryPath = path.join(__dirname, '../src/models');

fs.readdir(directoryPath, (err, files) => {
    if (err) {
        return console.error('Unable to scan directory: ' + err);
    }

    files.forEach((file) => {
        const filePath = path.join(directoryPath, file);

        fs.readFile(filePath, 'utf8', (err, data) => {
            if (err) {
                return console.error('Unable to read file: ' + err);
            }

            const updatedData = data
                .replace(/import\s+(.*?)\s+from\s+['"](\.\/.+?)['"]/g, (match, p1, p2) => {
                    if (!p2.endsWith('.js')) {
                        return `import ${p1} from '${p2}.js'`;
                    }
                    return match;
                })
                .replace(/export\s+.*?from\s+['"](\.\/.+?)['"]/g, (match, p1) => {
                    if (!p1.endsWith('.js')) {
                        return match.replace(p1, `${p1}.js`);
                    }
                    return match;
                });

            fs.writeFile(filePath, updatedData, 'utf8', (err) => {
                if (err) {
                    return console.error('Unable to write file: ' + err);
                }
                console.log(`Updated file: ${filePath}`);
            });
        });
    });
});