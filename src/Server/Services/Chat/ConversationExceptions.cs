namespace AGUIWebChat.Server.Services.Chat;

public sealed class ConversationValidationException(string message) : Exception(message);
public sealed class ConversationNotFoundException() : Exception("The conversation was not found.");
public sealed class ConversationConflictException(string message) : Exception(message);
