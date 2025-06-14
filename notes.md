# Resources

https://webassembly.org/features/
https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop/?view=aspnetcore-9.0#type-mappings
https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop/wasm-browser-app?view=aspnetcore-9.0#javascript-interop-on-

# PDD Revisions

- 11/06/2025 
    - Bridge -> pure WASM library without using Blazor
- 12/06/2025
    - Bridge -> add JS api for WebApp so WebApp never needs to interact with any C# stuff directly
- 13/06/2025
    - Shared Memory -> All shared buffer fields standardized to `Float64`; Passing `BodyStateBuffer` pointer within `SimStateBuffer`; Changed BufferLayout to simple string arrays.

# Day Log

- 11/06/2025 - Basic repo setup; after some experimentation realized Blazor is unnecessary for the Bridge; investigated alternatives
- 12/06/2025 - Reworked Bridge; refined build tools; created basic JS consumer; 1st test of JS <-> C# communication SUCCESSFUL
- 13/06/2025 - Added dual shared memory buffers and dynamic memory reallocation; moved bridge.js into EngineBridge project;