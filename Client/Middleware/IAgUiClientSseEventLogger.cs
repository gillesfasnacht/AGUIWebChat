namespace AGUIFluentUIChatClient.Middleware
{
    public interface IAgUiClientSseEventLogger
    {
        void LogEvent(string sseEvent);
    }
}
