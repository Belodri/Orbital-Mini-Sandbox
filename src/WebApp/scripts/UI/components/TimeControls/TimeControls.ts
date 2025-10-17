import template from "./template.html?raw";
import ViewModel from "../../abstract/ViewModel";
import { SimFrameData, SimView } from "@webapp/scripts/Data/DataViews";
import { IController } from "@webapp/scripts/Controller/Controller";

const IDs = {
    container: "time-controls",
    simulationTime: "simulation-time",
    togglePause: "toggle-pause"
} as const;


export default class TimeControls extends ViewModel {
    readonly #controller: IController;
    readonly #view: SimView;

    #togglePauseEl: HTMLButtonElement;
    #simeTimeEl: HTMLInputElement;

    constructor(id: string, controller: IController, view: SimView) {
        super({id, containerOrId: IDs.container, template});
        this.#controller = controller;
        this.#view = view;

        this.#simeTimeEl = document.getElementById(IDs.simulationTime) as HTMLInputElement;
        this.#togglePauseEl = document.getElementById(IDs.togglePause) as HTMLButtonElement;

        this.#togglePauseEl.addEventListener("pointerdown", () => this.#controller.togglePaused());

        this.#setPaused();
        this.#setSimulationTime();
    }

    render(data: SimFrameData): void {
        if(data.app.has("paused")) this.#setPaused();
        if(data.physics.has("simulationTime")) this.#setSimulationTime();
    }

    #setPaused() {
        const { paused } = this.#view.app;
        this.#togglePauseEl.textContent = paused ? "Play" : "Pause"
    }

    #setSimulationTime() {
        this.#simeTimeEl.value = this.#view.physics.simulationTime.toFixed(2);
    }
}