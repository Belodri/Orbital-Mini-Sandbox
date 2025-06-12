# PDD Revisions

- 11/06/2025 
    - Bridge -> pure WASM library without using Blazor
- 12/06/2025
    - Bridge -> add JS api for WebApp so WebApp never needs to interact with any C# stuff directly

# Resources

https://webassembly.org/features/

# Day Log

- 11/06/2025 - Basic repo setup; after some experimentation realized Blazor is unnecessary for the Bridge; investigated alternatives
- 12/06/2025 - Reworked Bridge; refined build tools; created basic JS consumer; 1st test of JS <-> C# communication SUCCESSFUL