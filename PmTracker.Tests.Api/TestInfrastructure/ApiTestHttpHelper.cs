using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using PmTracker.Web.Models.ViewModels;

namespace PmTracker.Tests.Api.TestInfrastructure;

internal static class ApiTestHttpHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static FormUrlEncodedContent BuildForm(params (string Key, string Value)[] fields)
    {
        return new FormUrlEncodedContent(fields.Select(field => new KeyValuePair<string, string>(field.Key, field.Value)));
    }

    public static HttpRequestMessage BuildAjaxPost(string url, HttpContent content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };

        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    public static HttpRequestMessage BuildAjaxGet(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    public static async Task<ModalSubmitResultViewModel> ReadModalResultAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        var payload = JsonSerializer.Deserialize<ModalSubmitResultViewModel>(json, JsonOptions);
        payload.Should().NotBeNull();
        return payload!;
    }
}
