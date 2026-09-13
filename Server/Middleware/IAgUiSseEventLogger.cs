namespace AGUIWebChatServer.Middleware
{
    public interface IAgUiSseEventLogger
    {
        void LogEvent(string sseEvent);
    }
}
