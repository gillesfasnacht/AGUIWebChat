namespace AGUIFluentUIChatClient.Midleware
{
    public interface IAgUiClientSseEventLogger
    {
        void LogEvent(string sseEvent);
    }
}
