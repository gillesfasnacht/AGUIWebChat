using System.Security.Cryptography;
using System.Text;

namespace AGUIWebChat.Server.Services.Chat;

public static class ConversationAccess
{
    public const string Header = "X-Conversation-Key";

    public static string GetOwnerKey(string? token)
    {
        if (!Guid.TryParseExact(token, "N", out var id) || id == Guid.Empty)
            throw new ConversationValidationException("A valid conversation browser key is required.");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token!)));
    }
}
