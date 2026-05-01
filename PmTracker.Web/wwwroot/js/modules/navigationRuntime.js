export const navigationRuntime = {
    initRecordFormEnhancements: null,
    prepareRecordEditorFormNavigation: null,
    refreshProjectIndexFilters: null
};

export function configureNavigationRuntime(runtime = {}) {
    if (typeof runtime.initRecordFormEnhancements === "function") {
        navigationRuntime.initRecordFormEnhancements = runtime.initRecordFormEnhancements;
    }
    if (typeof runtime.prepareRecordEditorFormNavigation === "function") {
        navigationRuntime.prepareRecordEditorFormNavigation = runtime.prepareRecordEditorFormNavigation;
    }
    if (typeof runtime.refreshProjectIndexFilters === "function") {
        navigationRuntime.refreshProjectIndexFilters = runtime.refreshProjectIndexFilters;
    }
}
