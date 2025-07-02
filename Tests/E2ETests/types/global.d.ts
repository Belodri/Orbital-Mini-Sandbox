// 1. Import the AppShell class type directly from its auto-generated file.
//    We use the '@webapp' alias configured in jsconfig.json.
//    'typeof' is used because AppShell.mjs exports the class itself.
import type AppShellClass from '@webapp/types/generated/AppShell.mjs';

// 2. Tell TypeScript that the global 'window' object will have
//    a property named 'AppShell' whose type is the AppShell class constructor.
declare global {
  interface Window {
    AppShell: typeof AppShellClass;
  }
}

// This export statement is required to make this file a module.
export {};