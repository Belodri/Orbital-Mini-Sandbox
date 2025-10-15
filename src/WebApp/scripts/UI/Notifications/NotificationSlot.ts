import { NotificationTypes, NotificationData } from "./Notifications";

export class NotificationSlot {
    static readonly CSS_CLASSES = {
        element: "notification",
        closeIcon: "fa-solid fa-xmark",
        hidden: "hidden"
    };

    static readonly #allTypeElementCssClasses = Object.values(NotificationTypes).map(t => t.elementClass);

    readonly index: number;
    readonly element: HTMLDivElement;

    #data?: NotificationData;
    #icon: HTMLElement;
    #span: HTMLSpanElement;
    #boundCloseListener = this.#closeIconListener.bind(this);
    #onUserCloseCallback: () => void;

    get data() { return this.#data; }
    get isCleared() { return !this.#data; }

    constructor(index: number, onUserCloseCallback: () => void) {
        this.index = index;
        this.#onUserCloseCallback = onUserCloseCallback;

        const { element, hidden, closeIcon } = NotificationSlot.CSS_CLASSES;

        const div = document.createElement("div");
        div.classList.add(element, hidden);

        const icon = document.createElement("i");
        const span = document.createElement("span");

        const closeI = document.createElement("i");
        closeI.className = closeIcon;
        closeI.addEventListener("pointerdown", this.#boundCloseListener);

        div.appendChild(icon);
        div.appendChild(span);
        div.appendChild(closeI);

        this.element = div;
        this.#icon = icon;
        this.#span = span;
    }

    set(data: NotificationData) {
        const { elementClass, iconClass } = NotificationTypes[data.type];

        this.element.classList.remove(NotificationSlot.CSS_CLASSES.hidden,
            ...NotificationSlot.#allTypeElementCssClasses);

        this.element.classList.add(elementClass);
        this.#icon.className = iconClass;
        this.#span.textContent = data.msg;

        this.#data = data;
    }

    clear() {
        if (!this.#data) return;
        this.#data = undefined;
        this.element.classList.add(NotificationSlot.CSS_CLASSES.hidden);
    }

    #closeIconListener(event: PointerEvent) {
        event.stopPropagation();
        event.preventDefault();
        this.clear();
        this.#onUserCloseCallback();
    }
}
