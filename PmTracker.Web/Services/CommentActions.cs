namespace PmTracker.Web.Services;

/// <summary>Smí uživatel vyjádření upravit / smazat — pro tužku a koš u vyjádření (spec 2026-11-10 R6).</summary>
public sealed record CommentActions(bool CanEdit, bool CanDelete);
