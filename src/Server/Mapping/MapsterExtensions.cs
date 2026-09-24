using Mapster;

namespace AGUIWebChat.Server.Mapping
{
    public static class MapsterExtensions
    {
        public static void RegisterMappings()
        {
            var config = TypeAdapterConfig.GlobalSettings;

            AIModelMappingConfig.Register(config);
        }
    }
}
