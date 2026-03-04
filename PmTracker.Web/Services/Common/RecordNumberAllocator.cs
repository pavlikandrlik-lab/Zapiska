namespace PmTracker.Web.Services.Common;

public static class RecordNumberAllocator
{
    public static int FindLowestAvailablePositive(IEnumerable<int> existingNumbers)
    {
        ArgumentNullException.ThrowIfNull(existingNumbers);

        var expected = 1;
        foreach (var number in existingNumbers
                     .Where(number => number > 0)
                     .Distinct()
                     .OrderBy(number => number))
        {
            if (number != expected)
            {
                return expected;
            }

            expected++;
        }

        return expected;
    }
}
