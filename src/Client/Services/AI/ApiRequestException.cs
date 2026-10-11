using System.Net;
using System.Text.Json;

namespace AGUIWebChat.Client.Services.AI;

public sealed class ApiRequestException(string message, HttpStatusCode statusCode,
    IReadOnlyList<string> errors) : HttpRequestException(message, null, statusCode)
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

internal static class ApiResponse
{
    public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        var errors = new List<string>();
        string? detail = null;
        string? title = null;
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("detail", out var value) && value.ValueKind == JsonValueKind.String)
                    detail = value.GetString();
                if (root.TryGetProperty("title", out value) && value.ValueKind == JsonValueKind.String)
                    title = value.GetString();
                if (root.TryGetProperty("errors", out value) && value.ValueKind == JsonValueKind.Object)
                    foreach (var field in value.EnumerateObject())
                        if (field.Value.ValueKind == JsonValueKind.Array)
                            foreach (var error in field.Value.EnumerateArray())
                                if (error.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(error.GetString()))
                                    errors.Add(error.GetString()!);
            }
        }
        catch (JsonException)
        {
            // A proxy or server may return an empty or non-JSON error response.
        }

        var messages = errors.Distinct().ToArray();
        var message = messages.Length > 0 ? string.Join(" ", messages)
            : !string.IsNullOrWhiteSpace(detail) ? detail
            : !string.IsNullOrWhiteSpace(title) ? title
            : $"The API request failed ({(int)response.StatusCode} {response.ReasonPhrase}).";
        throw new ApiRequestException(message, response.StatusCode, messages.Length > 0 ? messages : [message]);
    }
}
