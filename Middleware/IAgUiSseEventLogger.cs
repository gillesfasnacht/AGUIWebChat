namespace AGUIWebChat.Middleware
{
    public interface IAgUiSseEventLogger
    {
        void LogEvent(string sseEvent);
    }
}
