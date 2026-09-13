namespace AGUIWebChat.Server.Middleware
{
    public interface IAgUiSseEventLogger
    {
        void LogEvent(string sseEvent);
    }
}
