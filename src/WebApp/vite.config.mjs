import { defineConfig, normalizePath } from 'vite';
import { resolve, dirname } from 'path';
import { fileURLToPath } from 'url';

/**
 * Dev server logs warnings but I think those can be ignored. 
 * Everything is working just fine.
 */

const __dirname = dirname(fileURLToPath(import.meta.url));

export default defineConfig(({ command }) => {
    const config = {
        root: ".",
        build: {
            sourcemap: true,
            rollupOptions: {
                input: {
                    app: normalizePath(resolve(__dirname, "index.html")),
                },
                output: {
                    entryFileNames: "scripts/main.mjs"
                },
                external: [
                    /^\.\.\/bridge\/Bridge\.mjs$/
                ]
            }
        },
        plugins: [],
    };

    if (command === "serve") {
        config.resolve = {
            alias: {
                '../bridge': resolve(__dirname, '../../dist/bridge')
            }
        };

        config.server = {
            fs: {
                strict: false
            }
        };
    }

    return config;
});