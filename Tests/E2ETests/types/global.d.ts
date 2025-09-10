// 1. Import the App class type directly from its auto-generated file.
//    We use the '@webapp' alias configured in jsconfig.json.
//    'typeof' is used because App.mjs exports the class itself.
import type AppClass from '@webapp/types/generated/App.mjs';

// 2. Tell TypeScript that the global 'window' object will have
//    a property named 'App' whose type is the App class constructor.
declare global {
  interface Window {
    App: typeof AppClass;
  }
}

// This export statement is required to make this file a module.
export {};