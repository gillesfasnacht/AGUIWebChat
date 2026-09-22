using AGUIWebChat.Contracts.AI.Models;
using AGUIWebChat.Server.Domain.AI;
using Mapster;

namespace AGUIWebChat.Server.Mapping
{
    public static class AIModelMappingConfig
    {
        public static void Register(TypeAdapterConfig config)
        {
            config.NewConfig<AIModelReasoningEffort, ReasoningEffortEditModel>();

            config.NewConfig<ReasoningEffortEditModel, AIModelReasoningEffort>()
                .Ignore(dest => dest.AIModel)
                .Ignore(dest => dest.AIModelId);

            config.NewConfig<AIModel, AIModelEditModel>()
                .Map(
                    dest => dest.Temperature,
                    src => src.Defaults == null
                        ? null
                        : src.Defaults.Temperature)
                .Map(
                    dest => dest.TopP,
                    src => src.Defaults == null
                        ? null
                        : src.Defaults.TopP)
                .Map(
                    dest => dest.TopK,
                    src => src.Defaults == null
                        ? null
                        : src.Defaults.TopK)
                .Map(
                    dest => dest.NumCtx,
                    src => src.Defaults == null
                        ? null
                        : src.Defaults.NumCtx);

            config.NewConfig<AIModelEditModel, AIModel>()
                .Ignore(dest => dest.Provider)
                .Ignore(dest => dest.Defaults)
                .Ignore(dest => dest.ReasoningEfforts);
        }
    }
}
