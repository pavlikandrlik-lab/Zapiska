using PmTracker.Web.Models.Entities;

namespace PmTracker.Web.Services.Vyzvy;

public static class VyzvaStateMachine
{
    public static bool JePovolenyPrechod(VyzvaStav z, VyzvaStav na)
        => (z, na) switch
        {
            (VyzvaStav.Priprava, VyzvaStav.Odeslano) => true,
            (VyzvaStav.Priprava, VyzvaStav.Zruseno) => true,
            (VyzvaStav.Odeslano, VyzvaStav.Priprava) => true,
            (VyzvaStav.Odeslano, VyzvaStav.Zruseno) => true,
            (VyzvaStav.Zruseno, VyzvaStav.Priprava) => true,
            _ => false,
        };
}
