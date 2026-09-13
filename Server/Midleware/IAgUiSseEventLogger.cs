namespace AGUIWebChatServer.Midleware
{
    public interface IAgUiSseEventLogger
    {
        void LogEvent(string sseEvent);
    }
}
