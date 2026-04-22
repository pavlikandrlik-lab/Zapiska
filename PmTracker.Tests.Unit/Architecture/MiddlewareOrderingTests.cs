using System.IO;
using FluentAssertions;

namespace PmTracker.Tests.Unit.Architecture;

/// <summary>
/// Regression guard for HIGH-1: UserContextMiddleware must execute AFTER
/// UseAuthentication() and BEFORE UseAuthorization() in Program.cs.
/// If someone reorders the pipeline during a refactor, this test fails immediately.
/// </summary>
public sealed class MiddlewareOrderingTests
{
    [Fact]
    public void Program_ShouldPlaceUserContextMiddleware_BetweenAuthenticationAndAuthorization()
    {
        // Arrange
        var programPath = Path.Combine(ArchitectureTestBase.RepoRoot(), "PmTracker.Web", "Program.cs");
        var lines = File.ReadAllLines(programPath);

        int? authnLine = null, userCtxLine = null, authzLine = null;

        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("app.UseAuthentication()")) authnLine = i;
            if (lines[i].Contains("UseMiddleware<UserContextMiddleware>")) userCtxLine = i;
            if (lines[i].Contains("app.UseAuthorization()")) authzLine = i;
        }

        // Assert — presence
        authnLine.Should().NotBeNull("app.UseAuthentication() must be present in Program.cs");
        userCtxLine.Should().NotBeNull("app.UseMiddleware<UserContextMiddleware>() must be present in Program.cs");
        authzLine.Should().NotBeNull("app.UseAuthorization() must be present in Program.cs");

        // Assert — ordering (HIGH-1 regression guard)
        (authnLine < userCtxLine).Should().BeTrue(
            "UserContextMiddleware must run AFTER UseAuthentication() — " +
            "otherwise HttpContext.User is not yet populated when the middleware executes (HIGH-1)");
        (userCtxLine < authzLine).Should().BeTrue(
            "UserContextMiddleware must run BEFORE UseAuthorization() — " +
            "it must populate HttpContext.Items[OsobaId] before the authorization handlers run (HIGH-1)");
    }
}
