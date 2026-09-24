namespace AGUIWebChat.Server.Inference
{
    public sealed class InferenceSettings
    {
        public string ThinkingEffort { get; init; } = "low";
        public double Temperature { get; init; } = 0.7;
        public double TopP { get; init; } = 0.9;
        public int TopK { get; init; } = 40;
        public int NumCtx { get; init; } = 16384;
    }
}
