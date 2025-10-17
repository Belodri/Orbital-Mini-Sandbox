
export type ViewModelConfig = {
    id: string, 
    containerOrId: HTMLElement | string, 
    template: string,
}

export interface IViewModel {
    /** The unique identifier of this Viewmodel instance. */
    readonly id: string;
    /** Whether this ViewModel instance has a fixed container that persists for the lifetime of the application. */
    readonly isStatic: boolean;
    /**
     * Updates the ViewModel's content.
     * @param args Update data.
     */
    render(...args: any[]): void;
    /** 
     * Cleans up the component, clearing its container and removing any event listeners.
     * Non-static ViewModels are removed from the DOM entirely.
     */
    destroy(): void;
}

export default abstract class ViewModel implements IViewModel {
    readonly #id: string;
    readonly #isStatic: boolean;

    get id() { return this.#id; }
    get isStatic() { return this.#isStatic; }

    protected container: HTMLElement;

    constructor(cfg: ViewModelConfig) {
        this.#isStatic = typeof cfg.containerOrId === "string";
        this.#id = cfg.id;

        if(typeof cfg.containerOrId === "string") {
            const el = document.getElementById(cfg.containerOrId);
            if(el) this.container = el;
            else throw new Error(`ViewModel: Container element with ID '${cfg.containerOrId}' not found.`);
        } else this.container = cfg.containerOrId;

        const templateEl = document.createElement("template");
        templateEl.innerHTML = cfg.template.trim();
        this.container.appendChild(templateEl.content);
    }

    abstract render(...args: any[]): void;

    /** Removes the ViewModel from the DOM. Subclasses should override this to remove event listeners. */
    destroy(): void {
        if(this.isStatic) this.container.innerHTML = "";
        else this.container?.remove();
    }
}
