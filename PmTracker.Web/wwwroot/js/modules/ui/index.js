import { getContrastTextColor, pickSegmentLabel } from "../utils.js";

export * from "./print.js";
export * from "./floating.js";

export function renderRainbowSegmentLabel(segment) {
    if (!(segment instanceof HTMLElement)) {
        return;
    }

    const fullLabel = segment.dataset.rainbowSegmentLabelFull || "";
    const shortLabel = segment.dataset.rainbowSegmentLabelShort || "";
    if (!fullLabel && !shortLabel) {
        return;
    }

    const width = segment.getBoundingClientRect().width;
    const computed = window.getComputedStyle(segment);
    const fontSpec = `${computed.fontWeight} ${computed.fontSize} ${computed.fontFamily}`;
    const selected = pickSegmentLabel(fullLabel, shortLabel, width, fontSpec);

    segment.textContent = selected;
    if (!selected) {
        segment.style.removeProperty("color");
        return;
    }

    segment.style.color = getContrastTextColor(computed.backgroundColor);
}

export function renderAllRainbowSegmentLabels(scope) {
    const root = scope instanceof HTMLElement || scope instanceof Document ? scope : document;
    root
        .querySelectorAll(
            ".schedule-overview-segment[data-rainbow-segment-label-short], "
            + ".schedule-layered-segment[data-rainbow-segment-label-short], "
            + ".schedule-mini-gantt-segment[data-rainbow-segment-label-short]")
        .forEach((segment) => {
            renderRainbowSegmentLabel(segment);
        });
}

export function queueRainbowSegmentRender(scope) {
    window.requestAnimationFrame(() => renderAllRainbowSegmentLabels(scope));
}
