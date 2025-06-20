# Resources

https://webassembly.org/features/
https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop/?view=aspnetcore-9.0#type-mappings
https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop/wasm-browser-app?view=aspnetcore-9.0#javascript-interop-on-


# Day Log

- 11/06/2025 - Basic repo setup; after some experimentation realized Blazor is unnecessary for the Bridge; investigated alternatives
- 12/06/2025 - Reworked Bridge; refined build tools; created basic JS consumer; 1st test of JS <-> C# communication SUCCESSFUL
- 13/06/2025 - Added dual shared memory buffers and dynamic memory reallocation; moved bridge.js into EngineBridge project;
- 14/06/2025 - Added dynamic layout aware JavaScript reader; Added custom Vector2D math library; Scaffolded simulation & body models; Defined the data transfer contracts between `PhysicsEngine` and `EngineBridge`
- 16/06/2025 - Added C# data writer with self-configuring layout; added GC-efficient, self-configuring JavaScript reader with primitive delta reporting; first successful end-to-end data pipeline test
- 17/06/2025 - Refactored memory layout to a C# SSOT; created build tool (TsTypeGen) for automatic TypeScript type generation; completed major PDD update.