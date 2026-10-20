namespace PmTracker.Web.Services.Security.ViewAs;

public static class ViewAsMessages
{
    public const string ReadOnly =
        "V režimu „Zobrazit jako“ nelze nic ukládat ani měnit. Pro úpravy ukončete pohled tlačítkem „Zrušit pohled jako“ v liště pod hlavičkou.";

    public const string OnlySuperAdmin = "Zobrazit aplikaci jako jiný uživatel může jen superadministrátor.";

    public const string AlreadyActive =
        "Pohled jako už běží. Nejdřív ho ukončete tlačítkem „Zrušit pohled jako“ v liště pod hlavičkou.";

    public const string SelfTarget = "Sám sebe zobrazit nelze — vyberte jiného uživatele.";

    public const string TargetNotFound = "Vybraná osoba v aplikaci neexistuje. Vyberte osobu ze seznamu.";
}
