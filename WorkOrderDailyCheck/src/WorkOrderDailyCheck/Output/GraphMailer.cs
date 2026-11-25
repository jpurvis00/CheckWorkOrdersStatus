using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WorkOrderDailyCheck.Output;

/// <summary>
/// Sends mail through Microsoft Graph with an app registration (client credentials).
/// The app needs the Mail.Send *application* permission with admin consent, and the
/// From address must be a real mailbox in the tenant.
/// </summary>
public static class GraphMailer
{
    public static void Send(EmailSettings settings, string subject, string html, HttpMessageHandler? handler = null)
    {
        var g = settings.Graph;
        if (string.IsNullOrWhiteSpace(g.TenantId) || string.IsNullOrWhiteSpace(g.ClientId) || string.IsNullOrWhiteSpace(g.ClientSecret))
            throw new InvalidOperationException("Email:Provider is Graph but Email:Graph TenantId, ClientId or ClientSecret is empty.");

        using var http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        http.Timeout = TimeSpan.FromSeconds(100);

        // 1. App-only access token.
        var tokenResponse = http.PostAsync(
            $"https://login.microsoftonline.com/{Uri.EscapeDataString(g.TenantId)}/oauth2/v2.0/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = g.ClientId,
                ["client_secret"] = g.ClientSecret,
                ["scope"] = "https://graph.microsoft.com/.default",
                ["grant_type"] = "client_credentials",
            })).GetAwaiter().GetResult();
        var tokenBody = tokenResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!tokenResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"Could not get a Microsoft Graph token ({(int)tokenResponse.StatusCode}): {tokenBody}");
        var token = JsonNode.Parse(tokenBody)?["access_token"]?.GetValue<string>()
                    ?? throw new InvalidOperationException("Graph token response had no access_token.");

        // 2. Send as the From mailbox.
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(settings.From)}/sendMail")
        {
            Content = JsonContent.Create(BuildMessage(settings, subject, html)),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var sendResponse = http.SendAsync(request).GetAwaiter().GetResult();
        if (!sendResponse.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Graph sendMail failed ({(int)sendResponse.StatusCode}): {sendResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()}");
    }

    internal static JsonObject BuildMessage(EmailSettings settings, string subject, string html)
    {
        var message = new JsonObject
        {
            ["subject"] = subject,
            ["body"] = new JsonObject { ["contentType"] = "HTML", ["content"] = html },
            ["toRecipients"] = new JsonArray(settings.To
                .Select(a => (JsonNode)new JsonObject { ["emailAddress"] = new JsonObject { ["address"] = a } })
                .ToArray()),
        };
        return new JsonObject { ["message"] = message, ["saveToSentItems"] = true };
    }
}
