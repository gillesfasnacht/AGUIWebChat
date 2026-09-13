namespace AGUIWebChat.Client.Models
{
    public enum MonitoringDisplayMode
    {
        Hidden,
        Collapsed,
        Expanded
    }

    public sealed class MonitoringSettings
    {
        public bool ShowTokens { get; set; } = true;

        public bool ShowDurations { get; set; } = true;

        public bool ShowThroughput { get; set; } = true;

        public bool ShowModelInfo { get; set; } = true;

        public bool ShowAdvancedMetrics { get; set; } = false;
    }
}
