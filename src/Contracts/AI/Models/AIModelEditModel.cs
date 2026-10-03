using System.ComponentModel.DataAnnotations;

namespace AGUIWebChat.Contracts.AI.Models
{
    public sealed class AIModelEditModel
    {
        public int Id { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "A provider must be selected.")]
        public int ProviderId { get; set; }

        public string ProviderName { get; set; } = string.Empty;

        [Required(ErrorMessage = "ModelId is required.")]
        [StringLength(200, ErrorMessage = "ModelId cannot exceed 200 characters.")]
        public string ModelId { get; set; } = string.Empty;

        [Required(ErrorMessage = "DisplayName is required.")]
        [StringLength(200, ErrorMessage = "DisplayName cannot exceed 200 characters.")]
        public string DisplayName { get; set; } = string.Empty;

        [EnumDataType(typeof(ThinkingMode), ErrorMessage = "ThinkingMode must be a valid thinking mode.")]
        public ThinkingMode ThinkingMode { get; set; }

        public bool SupportsVision { get; set; }

        public bool SupportsTools { get; set; }

        public bool SupportsStreaming { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "ContextWindow must be greater than zero.")]
        public int? ContextWindow { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "MaxOutputTokens must be greater than zero.")]
        public int? MaxOutputTokens { get; set; }

        [Range(0.0, 2.0, ErrorMessage = "Temperature must be between 0 and 2.")]
        public double? Temperature { get; set; }

        [Range(0.0, 1.0, ErrorMessage = "TopP must be between 0 and 1.")]
        public double? TopP { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "TopK must be greater than zero.")]
        public int? TopK { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "NumCtx must be greater than zero.")]
        public int? NumCtx { get; set; }

        [Required(ErrorMessage = "ReasoningEfforts is required.")]
        public List<ReasoningEffortEditModel> ReasoningEfforts { get; set; } = [];

        public bool IsEnabled { get; set; } = true;

    }
}
