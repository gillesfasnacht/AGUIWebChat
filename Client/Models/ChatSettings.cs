namespace AGUIFluentUIChatClient.Models
{
    public enum ReasoningDisplayMode
    {
        Hidden,
        Collapsed,
        Expanded
    }

    public enum ThinkingEffort
    {
        Low,
        Medium,
        High
    }

    public sealed class ChatSettings
    {
        public ReasoningDisplayMode ReasoningDisplay { get; set; }
            = ReasoningDisplayMode.Collapsed;

        public ThinkingEffort ThinkingEffort { get; set; }
            = ThinkingEffort.Medium;

        public MonitoringDisplayMode MonitoringDisplay { get; set; }
            = MonitoringDisplayMode.Collapsed;

        public double Temperature { get; set; } = 0.7;

        public double TopP { get; set; } = 0.9;

        public int TopK { get; set; } = 40;

        public int NumCtx { get; set; } = 16384;

        public MonitoringSettings Monitoring { get; set; } = new();
    }
}
