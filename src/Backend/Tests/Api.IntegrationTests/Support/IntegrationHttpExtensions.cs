using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Filters;
using Api.Models;

namespace Api.IntegrationTests.Support;

internal static class IntegrationHttpExtensions
{
    public static HttpClient WithBearer(this HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public static HttpClient ClearBearer(this HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = null;
        return client;
    }

    public static HttpRequestMessage WithIdempotencyKey(this HttpRequestMessage request, string? key = null)
    {
        request.Headers.TryAddWithoutValidation(RequiresIdempotencyKeyAttribute.HeaderName, key ?? Guid.NewGuid().ToString("N"));
        return request;
    }

    public static async Task<ApiResponse<T>?> ReadApiAsync<T>(this HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ApiResponse<T>>();
    }

    public static async Task<ApiResponse<T>?> ReadApiAllowFailureAsync<T>(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<ApiResponse<T>>();
}
