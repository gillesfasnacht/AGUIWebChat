namespace AGUIWebChat.Server.Services.AI;

public sealed class ModelValidationException(string message) : Exception(message);
public sealed class ModelNotFoundException(string message) : Exception(message);
public sealed class ModelConflictException(string message) : Exception(message);
