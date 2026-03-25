export const navigationRuntime = {
    initRecordFormEnhancements: null,
    prepareRecordEditorFormNavigation: null
};

export function configureNavigationRuntime(runtime = {}) {
    if (typeof runtime.initRecordFormEnhancements === "function") {
        navigationRuntime.initRecordFormEnhancements = runtime.initRecordFormEnhancements;
    }
    if (typeof runtime.prepareRecordEditorFormNavigation === "function") {
        navigationRuntime.prepareRecordEditorFormNavigation = runtime.prepareRecordEditorFormNavigation;
    }
}
