namespace PmTracker.Web.Services.Schedules;

/// <summary>
/// Zapouzdřuje oprávnění pro editaci harmonogramu záznamu.
/// Použij factory metody místo přímého nastavení properties.
/// </summary>
public sealed record ScheduleEditorPermissionSet
{
    /// <summary>Uživatel může editovat trvání (plánová část).</summary>
    public bool CanEditDuration { get; init; }
    /// <summary>Uživatel může editovat odchylku (skutečná část).</summary>
    public bool CanEditDelay { get; init; }
    /// <summary>Uživatel může editovat datum zahájení kroku.</summary>
    public bool CanEditDate { get; init; }
    /// <summary>Plánová část je uzamčena (aktivní návrh).</summary>
    public bool IsPlanLocked { get; init; }
    /// <summary>Celý harmonogram je uzamčen (aktivní návrh).</summary>
    public bool IsScheduleLocked { get; init; }
    /// <summary>Záznam je kategorie "úkol" (jinak se harmonogram nezobrazuje).</summary>
    public bool IsTaskCategory { get; init; }
    /// <summary>Pouze přidávání — editovat lze jen kroky s TrvaniDni == 0.</summary>
    public bool IsAddOnlyMode { get; init; }

    /// <summary>Pouze pro čtení — žádné editace nejsou možné.</summary>
    public static ScheduleEditorPermissionSet ForReadOnly() => new()
    {
        CanEditDuration = false,
        CanEditDelay = false,
        CanEditDate = false,
        IsPlanLocked = false,
        IsScheduleLocked = false,
        IsTaskCategory = false
    };

    /// <summary>Plný edit — uživatel může editovat trvání i odchylku.</summary>
    public static ScheduleEditorPermissionSet ForFullEdit(bool isTaskCategory = true) => new()
    {
        CanEditDuration = isTaskCategory,
        CanEditDelay = isTaskCategory,
        CanEditDate = isTaskCategory,
        IsPlanLocked = false,
        IsScheduleLocked = false,
        IsTaskCategory = isTaskCategory
    };

    /// <summary>Jen přidávání — uživatel může editovat pouze kroky s trvání == 0.</summary>
    public static ScheduleEditorPermissionSet ForAddOnly(bool isTaskCategory = true) => new()
    {
        CanEditDuration = isTaskCategory,
        CanEditDelay = isTaskCategory,
        CanEditDate = isTaskCategory,
        IsPlanLocked = false,
        IsScheduleLocked = false,
        IsTaskCategory = isTaskCategory,
        IsAddOnlyMode = true
    };

    /// <summary>Pouze plán — uživatel může editovat jen plánovou část.</summary>
    public static ScheduleEditorPermissionSet ForPlanOnly(bool isTaskCategory = true) => new()
    {
        CanEditDuration = isTaskCategory,
        CanEditDelay = false,
        CanEditDate = isTaskCategory,
        IsPlanLocked = false,
        IsScheduleLocked = false,
        IsTaskCategory = isTaskCategory
    };

    /// <summary>Aktivní návrh — plánová část uzamčena.</summary>
    public static ScheduleEditorPermissionSet ForActivePlanProposal(bool isTaskCategory = true) => new()
    {
        CanEditDuration = false,
        CanEditDelay = isTaskCategory,
        CanEditDate = false,
        IsPlanLocked = true,
        IsScheduleLocked = false,
        IsTaskCategory = isTaskCategory
    };

    /// <summary>Aktivní návrh — celý harmonogram uzamčen.</summary>
    public static ScheduleEditorPermissionSet ForActiveScheduleProposal(bool isTaskCategory = true) => new()
    {
        CanEditDuration = false,
        CanEditDelay = false,
        CanEditDate = false,
        IsPlanLocked = false,
        IsScheduleLocked = true,
        IsTaskCategory = isTaskCategory
    };
}
