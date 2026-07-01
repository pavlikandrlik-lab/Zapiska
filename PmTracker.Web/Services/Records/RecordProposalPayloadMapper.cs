using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records;

/// <summary>
/// Datum-model (2026-06-12) — mapování mezi <see cref="SaveRecordCommand"/> a návrhovým payloadem.
/// Kroky identifikovány pořadím (1–10); plán/skutečnost jsou absolutní datumy.
/// </summary>
public sealed class RecordProposalPayloadMapper
{
    private static SaveRecordHarmonogramValueCommand Krok(SaveRecordHarmonogramValueCommand v)
        => new() { Poradi = v.Poradi, PlanDatum = v.PlanDatum, SkutecnostDatum = v.SkutecnostDatum };

    private static ManualActualKrokDto Manual(ManualActualKrokDto x)
        => new() { Poradi = x.Poradi, AbsolutniDatum = x.AbsolutniDatum, PreferredZdroj = x.PreferredZdroj };

    private static HarmonogramVazbaDto Vazba(HarmonogramVazbaDto x)
        => new() { Poradi = x.Poradi, ExterniOdkazIndex = x.ExterniOdkazIndex, HotVyjadreniId = x.HotVyjadreniId, DatumVyjadreni = x.DatumVyjadreni };

    public RecordProposalPayload BuildCreatePayload(SaveRecordCommand command)
    {
        return new RecordProposalPayload
        {
            ProposalType = RecordProposalTypeCodes.CreateRecord,
            CreateRecord = new CreateRecordProposalPayload
            {
                ProjektId = command.ProjektId,
                Kategorie = command.Kategorie,
                TypUkolu = command.TypUkolu,
                Stav = command.Stav,
                Nazev = command.Nazev,
                Cil = command.Cil,
                Popis = command.Popis,
                VlastnikId = command.VlastnikId.GetValueOrDefault(),
                DatumZalozeni = command.DatumZalozeni,
                TerminUkonceni = command.TerminUkonceni,
                Subsystem = command.Subsystem,
                VybraniSpolupracovniciIds = command.VybraniSpolupracovniciIds.ToList(),
                ExterniVazby = command.ExterniVazby
                    .Select(link => new SaveRecordExterniVazbaCommand
                    {
                        Id = link.Id,
                        Typ = link.Typ,
                        Cislo = link.Cislo,
                        PredpokladanaCena = link.PredpokladanaCena,
                        Vyzva = link.Vyzva,
                        DatumObjednani = link.DatumObjednani,
                        PlanDodani = link.PlanDodani,
                        DatumDodani = link.DatumDodani,
                        DatumPrevzeti = link.DatumPrevzeti
                    })
                    .ToList(),
                HarmonogramHodnoty = command.HarmonogramHodnoty.Select(Krok).ToList(),
                JednaniIdProCislo = command.JednaniIdProCislo,
                ManualActualKroky = command.ManualActualKroky.Select(Manual).ToList(),
                HarmonogramVazby = command.HarmonogramVazby.Select(Vazba).ToList()
            }
        };
    }

    public RecordProposalPayload BuildSchedulePayload(SaveRecordCommand command, DateTime originalDeadline)
    {
        var changesTermDeadline = command.TerminUkonceni.Date != originalDeadline.Date;
        var planned = command.HarmonogramHodnoty
            .Where(v => v.PlanDatum.HasValue)
            .Select(v => new SaveRecordHarmonogramValueCommand { Poradi = v.Poradi, PlanDatum = v.PlanDatum })
            .ToList();
        var actual = command.HarmonogramHodnoty
            .Where(v => v.SkutecnostDatum.HasValue)
            .Select(v => new SaveRecordHarmonogramValueCommand { Poradi = v.Poradi, SkutecnostDatum = v.SkutecnostDatum })
            .ToList();

        return new RecordProposalPayload
        {
            ProposalType = RecordProposalTypeCodes.SchedulePlanChange,
            SchedulePlan = new SchedulePlanProposalPayload
            {
                ProjektId = command.ProjektId,
                ZaznamId = command.Id.GetValueOrDefault(),
                TerminUkonceni = command.TerminUkonceni,
                PlannedHarmonogramHodnoty = planned,
                ActualHarmonogramHodnoty = actual,
                ChangesTermDeadline = changesTermDeadline,
                ChangesSchedulePlan = planned.Count > 0,
                ChangesScheduleActual = actual.Count > 0 || command.ManualActualKroky.Count > 0,
                ManualActualKroky = command.ManualActualKroky.Select(Manual).ToList()
            }
        };
    }

    public SaveRecordCommand BuildSaveCommand(CreateRecordProposalPayload payload)
    {
        return new SaveRecordCommand
        {
            ProjektId = payload.ProjektId,
            Kategorie = payload.Kategorie,
            TypUkolu = payload.TypUkolu,
            Stav = payload.Stav,
            Nazev = payload.Nazev,
            Cil = payload.Cil,
            Popis = payload.Popis,
            VlastnikId = payload.VlastnikId,
            DatumZalozeni = payload.DatumZalozeni,
            TerminUkonceni = payload.TerminUkonceni,
            Subsystem = payload.Subsystem,
            VybraniSpolupracovniciIds = payload.VybraniSpolupracovniciIds.ToList(),
            ExterniVazby = payload.ExterniVazby
                .Select(link => new SaveRecordExterniVazbaCommand
                {
                    Id = link.Id,
                    Typ = link.Typ,
                    Cislo = link.Cislo,
                    PredpokladanaCena = link.PredpokladanaCena,
                    Vyzva = link.Vyzva,
                    DatumObjednani = link.DatumObjednani,
                    PlanDodani = link.PlanDodani,
                    DatumDodani = link.DatumDodani,
                    DatumPrevzeti = link.DatumPrevzeti
                })
                .ToList(),
            HarmonogramHodnoty = payload.HarmonogramHodnoty.Select(Krok).ToList(),
            JednaniIdProCislo = payload.JednaniIdProCislo,
            ManualActualKroky = payload.ManualActualKroky.Select(Manual).ToList(),
            HarmonogramVazby = payload.HarmonogramVazby.Select(Vazba).ToList()
        };
    }

    public void ApplyCreatePayload(ZaznamEditViewModel model, CreateRecordProposalPayload payload)
    {
        model.Kategorie = payload.Kategorie;
        model.TypUkolu = payload.TypUkolu;
        model.Stav = payload.Stav;
        model.Nazev = payload.Nazev;
        model.Cil = payload.Cil ?? string.Empty;
        model.Popis = payload.Popis ?? string.Empty;
        model.VlastnikId = payload.VlastnikId;
        model.DatumZalozeni = payload.DatumZalozeni;
        model.TerminUkonceni = payload.TerminUkonceni;
        model.Subsystem = payload.Subsystem;
        model.VybraniSpolupracovniciIds = payload.VybraniSpolupracovniciIds.ToList();
        model.ExterniVazby = payload.ExterniVazby
            .Select(link => new ExterniOdkazEditViewModel
            {
                Id = link.Id,
                Typ = link.Typ,
                Cislo = link.Cislo,
                PredpokladanaCena = decimal.TryParse(link.PredpokladanaCena, out var price) ? price : null,
                VyzvaId = link.VyzvaId,
                VyzvaKod = link.Vyzva,
                ZaradidDoVyzvy = link.ZaradidDoVyzvy,
                DatumObjednani = link.DatumObjednani,
                PlanDodani = link.PlanDodani,
                DatumDodani = link.DatumDodani,
                DatumPrevzeti = link.DatumPrevzeti
            })
            .ToList();
        model.JednaniIdProCislo = payload.JednaniIdProCislo;

        if (payload.HarmonogramHodnoty.Count > 0)
        {
            var planByPoradi = payload.HarmonogramHodnoty
                .GroupBy(v => v.Poradi).ToDictionary(g => g.Key, g => g.Last().PlanDatum);

            var rebuiltSteps = model.HarmonogramBlok.Kroky
                .Select(step => new HarmonogramKrokEditViewModel
                {
                    KrokIndex = step.KrokIndex,
                    Nazev = step.Nazev,
                    BarvaHex = step.BarvaHex,
                    TrvaniDni = step.TrvaniDni,
                    OdchylkaDni = step.OdchylkaDni,
                    BaselineDatum = planByPoradi.TryGetValue(step.KrokIndex, out var pd) && pd.HasValue ? pd.Value : step.BaselineDatum,
                    SkutecneDatum = step.SkutecneDatum,
                    ZdrojSkutecnosti = step.ZdrojSkutecnosti,
                    SourceVyjadreniId = step.SourceVyjadreniId,
                    SourceVyjadreniDatum = step.SourceVyjadreniDatum,
                    SourceExterniOdkazId = step.SourceExterniOdkazId,
                    IsManualKrok = step.IsManualKrok
                })
                .ToList();

            model.HarmonogramBlok = model.HarmonogramBlok with
            {
                TerminUkonceni = payload.TerminUkonceni,
                Kroky = rebuiltSteps
            };
        }
    }

    public void ApplySchedulePayload(ZaznamEditViewModel model, SchedulePlanProposalPayload payload)
    {
        model.TerminUkonceni = payload.TerminUkonceni;

        var planByPoradi = payload.PlannedHarmonogramHodnoty
            .GroupBy(v => v.Poradi).ToDictionary(g => g.Key, g => g.Last().PlanDatum);
        var actualByPoradi = payload.ActualHarmonogramHodnoty
            .GroupBy(v => v.Poradi).ToDictionary(g => g.Key, g => g.Last().SkutecnostDatum);

        var rebuiltSteps = model.HarmonogramBlok.Kroky
            .Select(step => new HarmonogramKrokEditViewModel
            {
                KrokIndex = step.KrokIndex,
                Nazev = step.Nazev,
                BarvaHex = step.BarvaHex,
                TrvaniDni = step.TrvaniDni,
                OdchylkaDni = step.OdchylkaDni,
                BaselineDatum = planByPoradi.TryGetValue(step.KrokIndex, out var pd) && pd.HasValue ? pd.Value : step.BaselineDatum,
                SkutecneDatum = actualByPoradi.TryGetValue(step.KrokIndex, out var sd) && sd.HasValue ? sd.Value : step.SkutecneDatum,
                ZdrojSkutecnosti = step.ZdrojSkutecnosti,
                SourceVyjadreniId = step.SourceVyjadreniId,
                SourceVyjadreniDatum = step.SourceVyjadreniDatum,
                SourceExterniOdkazId = step.SourceExterniOdkazId,
                IsManualKrok = step.IsManualKrok
            })
            .ToList();

        // `with`: zachová OverviewLayout/Today/ScheduleVersion/lock state — dřív object-initializer
        // zahazoval vše krom explicitně uvedeného (marker přilepený na left:0 + ztráta lock stavu).
        model.HarmonogramBlok = model.HarmonogramBlok with
        {
            TerminUkonceni = payload.TerminUkonceni,
            Kroky = rebuiltSteps
        };
    }
}
