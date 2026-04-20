namespace PmTracker.Web.Services.Vyzvy;

public static class VyzvaCodeGenerator
{
    public static string Generuj(int poradoveVRoce, int rok)
        => $"{poradoveVRoce}/{rok}";

    public static int DalsiPoradoveVRoce(IReadOnlyCollection<int> existujiciPoradove)
        => existujiciPoradove.Count == 0 ? 1 : existujiciPoradove.Max() + 1;
}
