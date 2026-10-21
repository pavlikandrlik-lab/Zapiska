// Detail uzavřeného jednání (plán 2026-10-21): iframe se zápisem dostane výšku podle obsahu,
// aby stránka měla jeden posuvník. srcdoc dokument je stejného původu → contentDocument je
// čitelný; při chybě zůstane min-height z CSS.
function fitMinutesFrame(frame) {
    try {
        const doc = frame.contentDocument;
        if (!doc || !doc.documentElement) {
            return;
        }
        frame.style.height = `${doc.documentElement.scrollHeight}px`;
    } catch {
        // zablokovaný přístup — ponechat min-height z CSS
    }
}

export function initMeetingMinutesFrames(root = document) {
    root.querySelectorAll("iframe[data-meeting-minutes-frame]").forEach((frame) => {
        if (frame.dataset.meetingMinutesBound === "true") {
            return;
        }
        frame.dataset.meetingMinutesBound = "true";
        frame.addEventListener("load", () => fitMinutesFrame(frame));
        if (frame.contentDocument?.readyState === "complete") {
            fitMinutesFrame(frame);
        }
    });
}

initMeetingMinutesFrames();
