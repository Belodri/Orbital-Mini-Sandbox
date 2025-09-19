import { defineConfig } from "vite";

export default defineConfig({
    build: {
        sourcemap: true,
        outDir: 'wwwroot',
        // Do NOT clear the output directory on build, as it contains
        // the `_framework` directory managed by .NET.
        emptyOutDir: false,
        lib: {
            entry: 'scripts/Bridge.ts',
            fileName: 'Bridge',
            formats: ['es'],
        },
        rollupOptions: {
            external: [
                './_framework/dotnet.js'
            ],
        },
    },
});