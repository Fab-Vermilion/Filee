using System.Text.Json;

namespace Filee.Web.Sso;

/// <summary>
/// B-channel response validator. A resolved A-channel wb.reload() call has no code/message and is never
/// treated as this B-channel success event.
/// </summary>
public static class ReloadMessageParser
{
    public static bool IsValidBMessage(string json, string expectedRequestId, string expectedOrigin, bool sourceMatches)
    {
        if (!sourceMatches)
            return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return root.TryGetProperty("type", out var type)
                && type.GetString() == "wb:reload:response"
                && root.TryGetProperty("requestId", out var requestId)
                && requestId.GetString() == expectedRequestId
                && root.TryGetProperty("origin", out var origin)
                && origin.GetString() == expectedOrigin
                && root.TryGetProperty("code", out var code)
                && code.ValueKind == JsonValueKind.Number
                && code.GetInt32() == 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
