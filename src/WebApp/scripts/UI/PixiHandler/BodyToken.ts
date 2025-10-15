import { BodyView } from "@webapp/scripts/Data/DataViews";
import { Texture, Renderer, Graphics, Sprite } from "pixi.js";
import { PIXI_HANDLER_CONFIG } from "./PixiHandler";

const BODY_TOKEN_CONFIG = {
    disabledAlpha: 0.5,
    textureRadius: 5
} as const;

export class BodyToken {
    static #texture: Texture;

    /** Must be called at least once before an instance can be created! */
    static init(renderer: Renderer): void {
        const circle = new Graphics()
            .circle(0, 0, BODY_TOKEN_CONFIG.textureRadius)
            .fill("white");
        BodyToken.#texture = renderer.generateTexture(circle);
    }

    #view: BodyView;
    readonly sprite: Sprite = new Sprite(BodyToken.#texture);

    get app() { return this.#view.app; }
    get physics() { return this.#view.physics; }

    constructor(view: BodyView) {
        this.#view = view;
    }

    updateSprite(sceneScale: number) {
        this.sprite.position.set(
            this.physics.posX * PIXI_HANDLER_CONFIG.PIXELS_PER_AU,
            this.physics.posY * PIXI_HANDLER_CONFIG.PIXELS_PER_AU
        );

        this.sprite.tint = this.app.tint;
        this.sprite.alpha = this.physics.enabled ? 1 : BODY_TOKEN_CONFIG.disabledAlpha;

        this.sprite.scale.set(1 / sceneScale);
    }
}
