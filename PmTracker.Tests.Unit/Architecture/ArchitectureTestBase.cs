using System.IO;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Sdílené helpers pro architecture tests — prevent copy-paste RepoRoot/ResolvePath
/// napříč všemi split test třídami (DashboardPriority, ExportTemplate, OpenXmlWord,
/// PdfTemplate, RecordEditorJs, atd.).
/// </summary>
internal static class ArchitectureTestBase
{
    public static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PmTracker.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Nepodařilo se najít kořen repozitáře (PmTracker.sln).");
        }

        return directory.FullName;
    }

    public static string ResolvePath(string relative) =>
        Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar));
}
