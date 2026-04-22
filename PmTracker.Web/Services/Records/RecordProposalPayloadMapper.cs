using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Records;

public sealed class RecordProposalPayloadMapper
{
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
                HarmonogramHodnoty = command.HarmonogramHodnoty
                    .Select(value => new SaveRecordHarmonogramValueCommand
                    {
                        TypId = value.TypId,
                        Hodnota = value.Hodnota
                    })
                    .ToList(),
                JednaniIdProCislo = command.JednaniIdProCislo,
                ManualActualKroky = command.ManualActualKroky
                    .Select(x => new ManualActualKrokDto
                    {
                        KrokKey = x.KrokKey,
                        AbsolutniDatum = x.AbsolutniDatum
                    })
                    .ToList(),
                HarmonogramVazby = command.HarmonogramVazby
                    .Select(x => new HarmonogramVazbaDto
                    {
                        KrokKey = x.KrokKey,
                        ExterniOdkazIndex = x.ExterniOdkazIndex,
                        HotVyjadreniId = x.HotVyjadreniId,
                        DatumVyjadreni = x.DatumVyjadreni
                    })
                    .ToList()
            }
        };
    }

    public RecordProposalPayload BuildSchedulePayload(
        SaveRecordCommand command,
        IReadOnlyCollection<int> plannedTypeIds,
        IReadOnlyCollection<int> actualTypeIds,
        DateTime originalDeadline,
        IReadOnlyDictionary<int, int> existingValues)
    {
        var plannedTypeIdSet = plannedTypeIds.ToHashSet();
        var actualTypeIdSet = actualTypeIds.ToHashSet();
        var submittedByType = command.HarmonogramHodnoty
            .GroupBy(value => value.TypId)
            .ToDictionary(group => group.Key, group => group.Last().Hodnota);
        var changesTermDeadline = command.TerminUkonceni.Date != originalDeadline.Date;
        var changesSchedulePlan = plannedTypeIdSet.Any(typeId =>
            NormalizeScheduleValue(submittedByType.GetValueOrDefault(typeId)) != NormalizeScheduleValue(existingValues.GetValueOrDefault(typeId)));
        var changesScheduleActual = actualTypeIdSet.Any(typeId =>
            NormalizeScheduleValue(submittedByType.GetValueOrDefault(typeId)) != NormalizeScheduleValue(existingValues.GetValueOrDefault(typeId)));

        return new RecordProposalPayload
        {
            ProposalType = RecordProposalTypeCodes.SchedulePlanChange,
            SchedulePlan = new SchedulePlanProposalPayload
            {
                ProjektId = command.ProjektId,
                ZaznamId = command.Id.GetValueOrDefault(),
                TerminUkonceni = command.TerminUkonceni,
                PlannedHarmonogramHodnoty = command.HarmonogramHodnoty
                    .Where(value => plannedTypeIdSet.Contains(value.TypId))
                    .Select(value => new SaveRecordHarmonogramValueCommand
                    {
                        TypId = value.TypId,
                        Hodnota = value.Hodnota
                    })
                    .ToList(),
                ActualHarmonogramHodnoty = command.HarmonogramHodnoty
                    .Where(value => actualTypeIdSet.Contains(value.TypId))
                    .Select(value => new SaveRecordHarmonogramValueCommand
                    {
                        TypId = value.TypId,
                        Hodnota = value.Hodnota
                    })
                    .ToList(),
                ChangesTermDeadline = changesTermDeadline,
                ChangesSchedulePlan = changesSchedulePlan,
                ChangesScheduleActual = changesScheduleActual,
                ManualActualKroky = command.ManualActualKroky
                    .Select(x => new ManualActualKrokDto
                    {
                        KrokKey = x.KrokKey,
                        AbsolutniDatum = x.AbsolutniDatum
                    })
                    .ToList()
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
            HarmonogramHodnoty = payload.HarmonogramHodnoty
                .Select(value => new SaveRecordHarmonogramValueCommand
                {
                    TypId = value.TypId,
                    Hodnota = value.Hodnota
                })
                .ToList(),
            JednaniIdProCislo = payload.JednaniIdProCislo,
            ManualActualKroky = payload.ManualActualKroky
                .Select(x => new ManualActualKrokDto
                {
                    KrokKey = x.KrokKey,
                    AbsolutniDatum = x.AbsolutniDatum
                })
                .ToList(),
            HarmonogramVazby = payload.HarmonogramVazby
                .Select(x => new HarmonogramVazbaDto
                {
                    KrokKey = x.KrokKey,
                    ExterniOdkazIndex = x.ExterniOdkazIndex,
                    HotVyjadreniId = x.HotVyjadreniId,
                    DatumVyjadreni = x.DatumVyjadreni
                })
                .ToList()
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
    }

    public void ApplySchedulePayload(ZaznamEditViewModel model, SchedulePlanProposalPayload payload)
    {
        model.TerminUkonceni = payload.TerminUkonceni;

        var scheduleValuesByType = payload.PlannedHarmonogramHodnoty
            .Concat(payload.ActualHarmonogramHodnoty)
            .GroupBy(value => value.TypId)
            .ToDictionary(group => group.Key, group => group.Last().Hodnota);

        var rebuiltSteps = model.HarmonogramBlok.Kroky
            .Select(step => new HarmonogramKrokEditViewModel
            {
                KrokIndex = step.KrokIndex,
                Nazev = step.Nazev,
                BarvaHex = step.BarvaHex,
                TrvaniTypId = step.TrvaniTypId,
                ZpozdeniTypId = step.ZpozdeniTypId,
                TrvaniDni = scheduleValuesByType.TryGetValue(step.TrvaniTypId, out var duration) ? Math.Max(0, duration) : step.TrvaniDni,
                OdchylkaDni = scheduleValuesByType.TryGetValue(step.ZpozdeniTypId, out var delay) ? delay : step.OdchylkaDni,
                BaselineDatum = step.BaselineDatum,
                SkutecneDatum = step.SkutecneDatum
            })
            .ToList();

        model.HarmonogramBlok = new HarmonogramBlockViewModel
        {
            RecordId = model.HarmonogramBlok.RecordId,
            Mode = model.HarmonogramBlok.Mode,
            DatumZalozeni = model.HarmonogramBlok.DatumZalozeni,
            TerminUkonceni = payload.TerminUkonceni,
            DelayBarvaHex = model.HarmonogramBlok.DelayBarvaHex,
            Souhrn = model.HarmonogramBlok.Souhrn,
            Kroky = rebuiltSteps,
            Permissions = model.HarmonogramBlok.Permissions
        };
    }

    private static int NormalizeScheduleValue(int value) => value;
}
