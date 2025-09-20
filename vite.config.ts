import { defineConfig, type UserConfig } from 'vite';
import { resolve, dirname } from "path";
import { fileURLToPath } from 'url';
import tsconfigPaths from "vite-tsconfig-paths";

const __dirname = dirname(fileURLToPath(import.meta.url));

export default defineConfig(({ command }) => {
    const config: UserConfig = {
        root: 'src/WebApp', // required for the dev server to work correctly
        build: {
            // The output directory is relative to the project root, not the `root` option.
            outDir: '../../dist',
            emptyOutDir: false,     // Already contains the WASM _framework
            
            rollupOptions: {
                output: {
                    entryFileNames: "scripts/main.js",
                    paths: {
                        'dotnet-runtime': "../_framework/dotnet.js"
                    }
                },
                external: [
                    'dotnet-runtime'
                ],
            },
            
            sourcemap: true,        
        },
        plugins: [
            tsconfigPaths(),
        ]
    }

    if (command === "serve") {
        config.resolve = {
            alias: {
                '@bridge': resolve(__dirname, './src/Bridge/scripts/Bridge.ts'),
                './_framework/dotnet.js': resolve(__dirname, "./dist/_framework/dotnet.js")
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