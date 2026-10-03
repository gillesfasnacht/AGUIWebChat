using System.ComponentModel.DataAnnotations;
using AGUIWebChat.Contracts.AI;
using AGUIWebChat.Contracts.AI.Models;
using Xunit;

namespace AGUIWebChat.Contracts.Tests;

public sealed class AIModelValidationTests
{
    [Theory]
    [InlineData("ProviderId", 0, "A provider must be selected.")]
    [InlineData("ModelId", "", "ModelId is required.")]
    [InlineData("DisplayName", "   ", "DisplayName is required.")]
    [InlineData("ThinkingMode", 999, "ThinkingMode must be a valid thinking mode.")]
    [InlineData("ContextWindow", 0, "ContextWindow must be greater than zero.")]
    [InlineData("MaxOutputTokens", -1, "MaxOutputTokens must be greater than zero.")]
    [InlineData("Temperature", -0.1, "Temperature must be between 0 and 2.")]
    [InlineData("Temperature", 2.1, "Temperature must be between 0 and 2.")]
    [InlineData("TopP", -0.1, "TopP must be between 0 and 1.")]
    [InlineData("TopP", 1.1, "TopP must be between 0 and 1.")]
    [InlineData("TopK", 0, "TopK must be greater than zero.")]
    [InlineData("NumCtx", -1, "NumCtx must be greater than zero.")]
    [InlineData("ReasoningEfforts", null, "ReasoningEfforts is required.")]
    public void InvalidModelProperty_ShouldReturnItsPreciseMessage(string propertyName, object? value, string message)
    {
        var model = CreateModel();
        var property = typeof(AIModelEditModel).GetProperty(propertyName)!;
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        property.SetValue(model, value is null ? null : type.IsEnum
            ? Enum.ToObject(type, value) : Convert.ChangeType(value, type));

        var error = Assert.Single(Validate(model));
        Assert.Equal(message, error.ErrorMessage);
        Assert.Contains(propertyName, error.MemberNames);
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(2.0, 1.0)]
    public void InclusiveSamplingBoundaries_ShouldBeAccepted(double temperature, double topP)
    {
        var model = CreateModel();
        model.Temperature = temperature;
        model.TopP = topP;

        Assert.Empty(Validate(model));
    }

    [Fact]
    public void OptionalSettingsNullAndEmptyEfforts_ShouldBeAcceptedByAnnotations()
    {
        var model = CreateModel();
        Assert.Null(model.ContextWindow);
        Assert.Null(model.MaxOutputTokens);
        Assert.Null(model.Temperature);
        Assert.Null(model.TopP);
        Assert.Null(model.TopK);
        Assert.Null(model.NumCtx);
        Assert.Empty(model.ReasoningEfforts);
        Assert.Empty(Validate(model));
    }

    [Theory]
    [InlineData("ModelId", "ModelId cannot exceed 200 characters.")]
    [InlineData("DisplayName", "DisplayName cannot exceed 200 characters.")]
    public void ModelTextLength_ShouldAcceptDatabaseLimitAndRejectOverflow(string propertyName, string message)
    {
        var model = CreateModel();
        var property = typeof(AIModelEditModel).GetProperty(propertyName)!;
        property.SetValue(model, new string('x', 200));
        Assert.Empty(Validate(model));

        property.SetValue(model, new string('x', 201));
        Assert.Equal(message, Assert.Single(Validate(model)).ErrorMessage);
    }

    [Theory]
    [InlineData("DisplayName", "Every reasoning effort must have a display name.", "Reasoning effort display names cannot exceed 100 characters.")]
    [InlineData("Value", "Every reasoning effort must have a value.", "Reasoning effort values cannot exceed 100 characters.")]
    public void EffortText_ShouldEnforceRequiredValuesAndDatabaseLimits(string propertyName, string requiredMessage, string lengthMessage)
    {
        var effort = new ReasoningEffortEditModel { DisplayName = "Low", Value = "low" };
        var property = typeof(ReasoningEffortEditModel).GetProperty(propertyName)!;
        property.SetValue(effort, "   ");
        Assert.Equal(requiredMessage, Assert.Single(Validate(effort)).ErrorMessage);

        property.SetValue(effort, new string('x', 100));
        Assert.Empty(Validate(effort));

        property.SetValue(effort, new string('x', 101));
        Assert.Equal(lengthMessage, Assert.Single(Validate(effort)).ErrorMessage);
    }

    private static AIModelEditModel CreateModel() => new()
    {
        ProviderId = 1,
        ModelId = "test-model",
        DisplayName = "Test model",
        ThinkingMode = ThinkingMode.None
    };

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
