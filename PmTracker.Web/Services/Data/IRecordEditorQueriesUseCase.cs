using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Web.Services.Data;

public interface IRecordEditorQueriesUseCase
{
    ZaznamEditViewModel BuildZaznamEdit(int id, IRecordEditorQueriesComposition composition);

    ZaznamEditViewModel BuildZaznamCreate(int projektId, int? jednaniId, IRecordEditorQueriesComposition composition);

    DeleteRecordModalViewModel BuildDeleteRecordModal(int projektId, int zaznamId);
}
