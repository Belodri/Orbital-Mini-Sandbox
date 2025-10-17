import template from "./template.html?raw";
import ViewModel from "../../abstract/ViewModel";
import { SimFrameData, SimView } from "@webapp/scripts/Data/DataViews";
import { IController } from "@webapp/scripts/Controller/Controller";

const IDs = {
    container: "time-controls",
    simulationTime: "simulation-time",
    togglePause: "toggle-pause"
} as const;

export default class TimeControls extends ViewModel<[SimFrameData]> {
    readonly #controller: IController;
    readonly #view: SimView;

    #togglePauseEl: HTMLButtonElement;
    #simulationTimeEl: HTMLInputElement;

    constructor(id: string, controller: IController, view: SimView) {
        super({id, containerOrId: IDs.container, template});
        this.#controller = controller;
        this.#view = view;

        this.#simulationTimeEl = this.container.querySelector(`#${IDs.simulationTime}`) as HTMLInputElement;
        this.#togglePauseEl = this.container.querySelector(`#${IDs.togglePause}`) as HTMLButtonElement;

        this.#togglePauseEl.addEventListener("pointerdown", () => this.#controller.togglePaused());
    }

    onRender(data: SimFrameData): void {
        if(data.app.has("paused")) this.#setPaused();
        if(data.physics.has("simulationTime")) this.#setSimulationTime();
    }

    onFirstRender(data: SimFrameData): void {
        this.#setPaused();
        this.#setSimulationTime();
    }

    #setPaused() {
        this.#togglePauseEl.textContent =  this.#view.app.paused ? "Play" : "Pause"
    }

    #setSimulationTime() {
        this.#simulationTimeEl.value = this.#view.physics.simulationTime.toFixed(2);
    }
}