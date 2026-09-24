using AGUI.Abstractions;
using AGUI.Server;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace AGUIWebChat.Server.Inference;

public static class InferenceSettingsParser
{
    public static InferenceSettings GetInferenceSettings(AgentRunOptions? options, InferenceSettings defaults)
    {
        if (options is not ChatClientAgentRunOptions
            {
                ChatOptions: { } chatOptions
            })
        {
            return defaults;
        }

        if (!chatOptions.TryGetRunAgentInput(out RunAgentInput? input))
        {
            return defaults;
        }

        if (input.State is not
            {
                ValueKind: JsonValueKind.Object
            } state)
        {
            return defaults;
        }

        return Parse(state, defaults);
    }

    public static InferenceSettings Parse(JsonElement state, InferenceSettings defaults)
    {
        if (state.ValueKind != JsonValueKind.Object)
            return defaults;

        string thinkingEffort = defaults.ThinkingEffort;
        double temperature = defaults.Temperature;
        double topP = defaults.TopP;
        int topK = defaults.TopK;
        int numCtx = defaults.NumCtx;

        if (state.TryGetProperty("thinkingEffort", out var thinkingElement) && thinkingElement.ValueKind == JsonValueKind.String)
        {
            thinkingEffort = thinkingElement.GetString()?.ToLowerInvariant() switch
            {
                "low" => "low",
                "medium" => "medium",
                "high" => "high",
                _ => defaults.ThinkingEffort
            };
        }

        if (state.TryGetProperty("temperature", out var temperatureElement) &&
            temperatureElement.ValueKind == JsonValueKind.Number &&
            temperatureElement.TryGetDouble(out var parsedTemperature) &&
            parsedTemperature is >= 0 and <= 2)
        {
            temperature = parsedTemperature;
        }

        if (state.TryGetProperty("topP", out var topPElement) &&
            topPElement.ValueKind == JsonValueKind.Number &&
            topPElement.TryGetDouble(out var parsedTopP) &&
            parsedTopP is >= 0 and <= 1)
        {
            topP = parsedTopP;
        }

        if (state.TryGetProperty("topK", out var topKElement) &&
            topKElement.ValueKind == JsonValueKind.Number &&
            topKElement.TryGetInt32(out var parsedTopK) &&
            parsedTopK is >= 1 and <= 1000)
        {
            topK = parsedTopK;
        }

        if (state.TryGetProperty("numCtx", out var numCtxElement) &&
            numCtxElement.ValueKind == JsonValueKind.Number &&
            numCtxElement.TryGetInt32(out var parsedNumCtx) &&
            parsedNumCtx is >= 1024 and <= 131072)
        {
            numCtx = parsedNumCtx;
        }

        return new InferenceSettings
        {
            ThinkingEffort = thinkingEffort,
            Temperature = temperature,
            TopP = topP,
            TopK = topK,
            NumCtx = numCtx
        };
    }

    public static AgentRunOptions ApplyInferenceSettings(AgentRunOptions? options, InferenceSettings settings)
    {
        ChatClientAgentRunOptions runOptions;

        if (options is ChatClientAgentRunOptions chatClientOptions)
        {
            runOptions = (ChatClientAgentRunOptions)chatClientOptions.Clone();
        }
        else
        {
            runOptions = new ChatClientAgentRunOptions();
        }

        runOptions.ChatOptions ??= new ChatOptions();
        runOptions.ChatOptions.AdditionalProperties ??= new();
        runOptions.ChatOptions.AdditionalProperties["think"] = settings.ThinkingEffort;
        runOptions.ChatOptions.AdditionalProperties["temperature"] = settings.Temperature;
        runOptions.ChatOptions.AdditionalProperties["top_p"] = settings.TopP;
        runOptions.ChatOptions.AdditionalProperties["top_k"] = settings.TopK;
        runOptions.ChatOptions.AdditionalProperties["num_ctx"] = settings.NumCtx;

        return runOptions;
    }

}
