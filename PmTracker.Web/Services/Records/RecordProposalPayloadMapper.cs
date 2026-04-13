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
                JednaniIdProCislo = command.JednaniIdProCislo
            }
        };
    }

    public RecordProposalPayload BuildSchedulePayload(SaveRecordCommand command, IReadOnlyCollection<int> plannedTypeIds)
    {
        var plannedTypeIdSet = plannedTypeIds.ToHashSet();

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
            JednaniIdProCislo = payload.JednaniIdProCislo
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
                Vyzva = link.Vyzva,
                DatumObjednani = link.DatumObjednani,
                PlanDodani = link.PlanDodani,
                DatumDodani = link.DatumDodani,
                DatumPrevzeti = link.DatumPrevzeti
            })
            .ToList();
        model.JednaniIdProCislo = payload.JednaniIdProCislo;
    }
}
