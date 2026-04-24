namespace PmTracker.Web.Services.ServiceDesk;

/// <summary>
/// Sestaví odkaz na detail ticketu v reálném ServiceDesku (Hotline webové UI).
/// Jediné kanonické místo pro formát URL — dříve duplikát v
/// <c>ProjectService.RecordComposition.BuildServiceDeskUrl</c>.
/// Memory: project_servicedesk_infosystem_binding (URL builder reuse).
/// </summary>
public static class ServiceDeskUrlBuilder
{
    private const string HotlineTicketDetailBase = "https://servicedesk.fis.acr/Hotline/Ticket/Details/";

    /// <summary>
    /// Vrací URL pro ticket s daným <paramref name="ticketId"/>. NULL nebo prázdný
    /// vstup → <c>null</c> (ticket bez ID je mimo scope PM Trackeru).
    /// </summary>
    public static string? ForTicket(string? ticketId)
    {
        if (string.IsNullOrWhiteSpace(ticketId))
        {
            return null;
        }

        return HotlineTicketDetailBase + ticketId;
    }

    /// <summary>Convenience overload pro int ticket ID.</summary>
    public static string ForTicket(int ticketId) => HotlineTicketDetailBase + ticketId;
}
