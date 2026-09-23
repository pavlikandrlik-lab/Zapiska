namespace PmTracker.Web.Services.Vyzvy;

public static class VyzvaCodeGenerator
{
    public static string Generuj(int poradoveVRoce, int rok)
        => $"{poradoveVRoce}/{rok}";
}
