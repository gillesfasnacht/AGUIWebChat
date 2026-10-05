using Mapster;

namespace AGUIWebChat.Server.Mapping
{
    public static class MapsterExtensions
    {
        private static readonly Lazy<bool> MappingsRegistered = new(() =>
        {
            AIModelMappingConfig.Register(TypeAdapterConfig.GlobalSettings);
            return true;
        });

        public static void RegisterMappings()
        {
            _ = MappingsRegistered.Value;
        }
    }
}
