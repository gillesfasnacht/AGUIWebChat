using System.Text.Json;
using AGUIWebChat.Server.Inference;
using Xunit;

namespace AGUIWebChat.Tests;

public class InferenceTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"thinkingEffort\":12,\"temperature\":\"bad\",\"topP\":false,\"topK\":{},\"numCtx\":null}")]
    [InlineData("{\"temperature\":3,\"topP\":-1,\"topK\":1001,\"numCtx\":131073}")]
    public void InvalidSettingsFallBackWithoutThrowing(string json)
    {
        var defaults = new InferenceSettings();
        var result = InferenceSettingsParser.Parse(JsonSerializer.Deserialize<JsonElement>(json), defaults);
        Assert.Equal(defaults.ThinkingEffort, result.ThinkingEffort);
        Assert.Equal(defaults.Temperature, result.Temperature);
        Assert.Equal(defaults.TopP, result.TopP);
        Assert.Equal(defaults.TopK, result.TopK);
        Assert.Equal(defaults.NumCtx, result.NumCtx);
    }

    [Fact]
    public void ValidSettingsAreApplied()
    {
        var result = InferenceSettingsParser.Parse(JsonSerializer.Deserialize<JsonElement>(
            """{"thinkingEffort":"HIGH","temperature":0,"topP":1,"topK":1000,"numCtx":131072}"""), new());
        Assert.Equal("high", result.ThinkingEffort);
        Assert.Equal(0, result.Temperature);
        Assert.Equal(1, result.TopP);
        Assert.Equal(1000, result.TopK);
        Assert.Equal(131072, result.NumCtx);
    }
}
