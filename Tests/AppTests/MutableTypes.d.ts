import {
  BodyView as _BodyView,
  SimView as _SimView,
  BodyFrameData as _BodyFrameData,
  SimFrameData as _SimFrameData,
} from "@webapp/scripts/Data/DataViews";

import {
  AppStateBody as _AppStateBody,
  AppStateSim as _AppStateSim,
  AppState as _AppState,
  AppDiff as _AppDiff,
} from "@webapp/scripts/Data/AppData";

import {
  BodyId as _BodyId,
  PhysicsStateBody as _PhysicsStateBody,
  PhysicsStateSim as _PhysicsStateSim,
  PhysicsState as _PhysicsState,
  PhysicsDiff as _PhysicsDiff,
} from "@bridge";

import { ColorSource } from "pixi.js";

type ExcludeFromMutable =
  | ColorSource
  | Promise<any>

type Mutable<T, TNot = ExcludeFromMutable> =
    T extends TNot ? T :
    // T extends (...args: any[]) => any ? T :
    // T extends new (...args: any[]) => any ? T :
    T extends ReadonlyMap<infer K, infer V> ? Map<Mutable<K>, Mutable<V>> :
    T extends ReadonlySet<infer U> ? Set<Mutable<U>> :
    T extends ReadonlyArray<infer E> ? Array<Mutable<E>> :
    T extends object ? { -readonly [P in keyof T]: Mutable<T[P]> } :
    T;

export type BodyView = Mutable<_BodyView>;
export type SimView = Mutable<_SimView>;
export type BodyFrameData = Mutable<_BodyFrameData>;
export type SimFrameData = Mutable<_SimFrameData>;

export type AppStateBody = Mutable<_AppStateBody>;
export type AppStateSim = Mutable<_AppStateSim>;
export type AppState = Mutable<_AppState>;
export type AppDiff = Mutable<_AppDiff>;

export type BodyId = Mutable<_BodyId>;
export type PhysicsStateBody = Mutable<_PhysicsStateBody>;
export type PhysicsStateSim = Mutable<_PhysicsStateSim>;
export type PhysicsState = Mutable<_PhysicsState>;
export type PhysicsDiff = Mutable<_PhysicsDiff>;