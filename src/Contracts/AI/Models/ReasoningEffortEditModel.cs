using System.ComponentModel.DataAnnotations;

namespace AGUIWebChat.Contracts.AI.Models
{
    public sealed class ReasoningEffortEditModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Every reasoning effort must have a display name.")]
        [StringLength(100, ErrorMessage = "Reasoning effort display names cannot exceed 100 characters.")]
        public string DisplayName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Every reasoning effort must have a value.")]
        [StringLength(100, ErrorMessage = "Reasoning effort values cannot exceed 100 characters.")]
        public string Value { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public bool IsDefault { get; set; }
    }
}
