using System.Reflection;
using System.Threading;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using PmTracker.Web.Controllers;

namespace PmTracker.Tests.Unit.Search;

/// <summary>
/// Reflection testy ověřující existenci a signaturu Suggest endpointu na SearchController.
/// </summary>
public sealed class SuggestEndpointExistsTests
{
    [Fact]
    public void SearchController_MaMetodu_Suggest()
    {
        var method = typeof(SearchController).GetMethod(
            "Suggest",
            BindingFlags.Public | BindingFlags.Instance);

        method.Should().NotBeNull("SearchController musí mít veřejnou metodu Suggest");
    }

    [Fact]
    public void Suggest_MaParametr_Q_String()
    {
        var method = typeof(SearchController).GetMethod(
            "Suggest",
            BindingFlags.Public | BindingFlags.Instance);

        method.Should().NotBeNull();

        var parameters = method!.GetParameters();
        var qParam = parameters.FirstOrDefault(p =>
            string.Equals(p.Name, "q", StringComparison.OrdinalIgnoreCase));

        qParam.Should().NotBeNull("metoda Suggest musí mít parametr q");
        qParam!.ParameterType.Should().Be(typeof(string),
            "parametr q musí být typu string");
    }

    [Fact]
    public void Suggest_MaParametr_CancellationToken()
    {
        var method = typeof(SearchController).GetMethod(
            "Suggest",
            BindingFlags.Public | BindingFlags.Instance);

        method.Should().NotBeNull();

        var parameters = method!.GetParameters();
        var ctParam = parameters.FirstOrDefault(p => p.ParameterType == typeof(CancellationToken));

        ctParam.Should().NotBeNull("metoda Suggest musí přijímat CancellationToken pro async zrušení");
    }

    [Fact]
    public void Suggest_VraciTask_OfIActionResult()
    {
        var method = typeof(SearchController).GetMethod(
            "Suggest",
            BindingFlags.Public | BindingFlags.Instance);

        method.Should().NotBeNull();

        var returnType = method!.ReturnType;
        returnType.Should().NotBeNull();

        // Task<IActionResult> nebo Task<JsonResult>
        var isTaskOfActionResult = returnType.IsGenericType &&
            returnType.GetGenericTypeDefinition() == typeof(Task<>) &&
            typeof(IActionResult).IsAssignableFrom(returnType.GetGenericArguments()[0]);

        isTaskOfActionResult.Should().BeTrue(
            $"Suggest musí vracet Task<IActionResult> nebo kompatibilní typ, ale vrací {returnType.Name}");
    }

    [Fact]
    public void Suggest_MaAtribut_HttpGet()
    {
        var method = typeof(SearchController).GetMethod(
            "Suggest",
            BindingFlags.Public | BindingFlags.Instance);

        method.Should().NotBeNull();

        var hasHttpGet = method!.GetCustomAttributes(typeof(HttpGetAttribute), inherit: false).Length > 0;
        hasHttpGet.Should().BeTrue("Suggest musí být označen [HttpGet]");
    }
}
