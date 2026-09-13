namespace AGUIWebChat.Client.Middleware
{
    public interface IAgUiClientSseEventLogger
    {
        void LogEvent(string sseEvent);
    }
}
