namespace PmTracker.Web.Services.ProjectDashboard.Zakladni;

/// <summary>Termínové metriky jednoho záznamu (sdílené mezi disciplínovými providery).</summary>
public sealed record RecordDeadline(
    int RecordId,
    int SubsystemId,
    DateTime Puvodni,
    DateTime Posledni,
    DateTime? Dokonceni,
    int PocetPosunu)
{
    public bool MaPosun => PocetPosunu > 0;
    public int DnyPosunu => (int)(Posledni - Puvodni).TotalDays;
    public bool JeDokonceno => Dokonceni.HasValue;
    public bool DodrzelPosledni => Dokonceni is DateTime d && d <= Posledni;
    public bool DodrzelPuvodni => Dokonceni is DateTime d && d <= Puvodni;
}

/// <summary>
/// Spočítá termínovou disciplínu per záznam z <see cref="ZakladniDataset"/>. Původní termín =
/// nejstarší změna (její <see cref="DatasetTerminChange.PuvodniTermin"/>); pokud se neposouvalo,
/// původní == poslední (= <see cref="DatasetRecord.DatumUkonceni"/>). Sdílená čistá funkce.
/// </summary>
public static class DeadlineDiscipline
{
    public static IReadOnlyList<RecordDeadline> PerRecord(ZakladniDataset ds)
    {
        var changesByRecord = ds.TerminChanges
            .GroupBy(c => c.ZaznamId)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.DatumZmeny).ToList());

        return ds.Records.Select(r =>
        {
            var posledni = r.DatumUkonceni;
            var puvodni = changesByRecord.TryGetValue(r.Id, out var changes) && changes.Count > 0
                ? changes[0].PuvodniTermin
                : posledni;
            var pocet = changes?.Count ?? 0;
            return new RecordDeadline(r.Id, r.SubsystemId, puvodni, posledni, r.DatumDokonceni, pocet);
        }).ToList();
    }

    /// <summary>Procento (0–100) zaokrouhlené na 1 desetinné; 0 z 0 = 0.</summary>
    public static double Pct(int part, int total) => total == 0 ? 0d : Math.Round((double)part / total * 100d, 1);
}
