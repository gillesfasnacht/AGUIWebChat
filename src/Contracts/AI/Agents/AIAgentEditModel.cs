using System.ComponentModel.DataAnnotations;

﻿namespace AGUIWebChat.Contracts.AI.Agents
{
    public sealed class AIAgentEditModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Agent name is required.")]
        [StringLength(200, ErrorMessage = "Agent name cannot exceed 200 characters.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "System prompt is required.")]
        public string SystemPrompt { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "An AI model is required.")]
        public int AIModelId { get; set; }

        public string AIModelDisplayName { get; set; } = string.Empty;

        // Runtime overrides
        [Range(0.0, 2.0, ErrorMessage = "Temperature must be between 0 and 2.")]
        public double? Temperature { get; set; }

        [Range(0.0, 1.0, ErrorMessage = "TopP must be between 0 and 1.")]
        public double? TopP { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "TopK must be greater than zero.")]
        public int? TopK { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "NumCtx must be greater than zero.")]
        public int? NumCtx { get; set; }

        [StringLength(100, ErrorMessage = "ReasoningEffort cannot exceed 100 characters.")]
        public string? ReasoningEffort { get; set; }

        public bool IsEnabled { get; set; } = true;
    }
}
