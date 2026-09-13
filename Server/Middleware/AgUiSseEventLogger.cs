using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace AGUIWebChat.Server.Middleware
{
    public sealed class AgUiSseEventLogger : IAgUiSseEventLogger
    {
        private readonly ILogger<AgUiSseEventLogger> _logger;

        private readonly ConcurrentDictionary<string, MessageBuffer>
            _messages = new();

        public AgUiSseEventLogger(ILogger<AgUiSseEventLogger> logger)
        {
            _logger = logger;

            _logger.LogInformation(
                "AgUiSseEventLogger levels: Trace={TraceEnabled}, Debug={DebugEnabled}, Information={InformationEnabled}",
                _logger.IsEnabled(LogLevel.Trace),
                _logger.IsEnabled(LogLevel.Debug),
                _logger.IsEnabled(LogLevel.Information));

            _logger.LogTrace("TEST TRACE AgUiSseEventLogger");
            _logger.LogDebug("TEST DEBUG AgUiSseEventLogger");
            _logger.LogInformation("TEST INFORMATION AgUiSseEventLogger");
        }

        public void LogEvent(string sseEvent)
        {
            string? data = ExtractData(sseEvent);

            if (string.IsNullOrWhiteSpace(data))
                return;

            try
            {
                using JsonDocument document =
                    JsonDocument.Parse(data);

                JsonElement root = document.RootElement;

                string eventType =
                    GetString(root, "type") ?? "UNKNOWN";

                // Tous les événements restent disponibles en Trace.
                _logger.LogTrace(
                    "AG-UI raw event {EventType}: {SseData}",
                    eventType,
                    data);

                switch (eventType)
                {
                    case "RUN_STARTED":
                        LogRunStarted(root);
                        break;

                    case "RUN_FINISHED":
                        LogRunFinished(root);
                        break;

                    case "TEXT_MESSAGE_START":
                        StartMessage(root, MessageKind.Text);
                        break;

                    case "TEXT_MESSAGE_CONTENT":
                        AppendMessage(root, MessageKind.Text);
                        break;

                    case "TEXT_MESSAGE_END":
                        EndMessage(root, MessageKind.Text);
                        break;

                    case "REASONING_START":
                        LogReasoningStart(root);
                        break;

                    case "REASONING_MESSAGE_START":
                        StartMessage(root, MessageKind.Reasoning);
                        break;

                    case "REASONING_MESSAGE_CONTENT":
                        AppendMessage(root, MessageKind.Reasoning);
                        break;

                    case "REASONING_MESSAGE_END":
                        EndMessage(root, MessageKind.Reasoning);
                        break;

                    case "REASONING_END":
                        LogReasoningEnd(root);
                        break;

                    case "TOOL_CALL_START":
                        LogToolCallStart(root);
                        break;

                    case "TOOL_CALL_ARGS":
                        LogToolCallArgs(root);
                        break;

                    case "TOOL_CALL_END":
                        LogToolCallEnd(root);
                        break;

                    default:
                        LogGenericEvent(eventType, root);
                        break;
                }
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Unable to parse AG-UI SSE event: {SseData}",
                    data);
            }
        }

        // ---------------------------------------------------------
        // RUN
        // ---------------------------------------------------------

        private void LogRunStarted(JsonElement root)
        {
            _logger.LogInformation(
                "AG-UI {EventType} ThreadId={ThreadId} RunId={RunId}",
                "RUN_STARTED",
                GetString(root, "threadId"),
                GetString(root, "runId"));
        }

        private void LogRunFinished(JsonElement root)
        {
            _logger.LogInformation(
                "AG-UI {EventType} ThreadId={ThreadId} RunId={RunId}",
                "RUN_FINISHED",
                GetString(root, "threadId"),
                GetString(root, "runId"));
        }

        // ---------------------------------------------------------
        // MESSAGES
        // ---------------------------------------------------------

        private void StartMessage(
            JsonElement root,
            MessageKind kind)
        {
            string? messageId =
                GetString(root, "messageId");

            if (string.IsNullOrWhiteSpace(messageId))
            {
                _logger.LogWarning(
                    "AG-UI {MessageKind} message started without MessageId",
                    kind);

                return;
            }

            string? role =
                GetString(root, "role");

            _messages[messageId] =
                new MessageBuffer(kind, role);

            _logger.LogTrace(
                "AG-UI {MessageKind}_MESSAGE_START MessageId={MessageId} Role={Role}",
                kind,
                messageId,
                role);
        }

        private void AppendMessage(
            JsonElement root,
            MessageKind kind)
        {
            string? messageId =
                GetString(root, "messageId");

            string? delta =
                GetString(root, "delta");

            if (string.IsNullOrWhiteSpace(messageId))
                return;

            if (delta is null)
                return;

            MessageBuffer buffer =
                _messages.GetOrAdd(
                    messageId,
                    _ => new MessageBuffer(kind, null));

            buffer.Append(delta);

            _logger.LogTrace(
                "AG-UI {MessageKind} fragment MessageId={MessageId} Delta={Delta}",
                kind,
                messageId,
                delta);
        }

        private void EndMessage(
            JsonElement root,
            MessageKind kind)
        {
            string? messageId =
                GetString(root, "messageId");

            if (string.IsNullOrWhiteSpace(messageId))
                return;

            _logger.LogTrace(
                "AG-UI {MessageKind}_MESSAGE_END MessageId={MessageId}",
                kind,
                messageId);

            if (!_messages.TryRemove(
                    messageId,
                    out MessageBuffer? buffer))
            {
                _logger.LogWarning(
                    "AG-UI {MessageKind} message ended but no buffer exists MessageId={MessageId}",
                    kind,
                    messageId);

                return;
            }

            string content =
                buffer.GetContent();

            if (kind == MessageKind.Text)
            {
                _logger.LogInformation(
                    "AG-UI {EventType} MessageId={MessageId} Role={Role} Content={Content}",
                    "TEXT_MESSAGE",
                    messageId,
                    buffer.Role,
                    content);
            }
            else
            {
                _logger.LogDebug(
                    "AG-UI {EventType} MessageId={MessageId} Role={Role} Content={Content}",
                    "REASONING_MESSAGE",
                    messageId,
                    buffer.Role,
                    content);
            }
        }

        // ---------------------------------------------------------
        // REASONING
        // ---------------------------------------------------------

        private void LogReasoningStart(JsonElement root)
        {
            _logger.LogDebug(
                "AG-UI {EventType} MessageId={MessageId}",
                "REASONING_START",
                GetString(root, "messageId"));
        }

        private void LogReasoningEnd(JsonElement root)
        {
            _logger.LogDebug(
                "AG-UI {EventType} MessageId={MessageId}",
                "REASONING_END",
                GetString(root, "messageId"));
        }

        // ---------------------------------------------------------
        // TOOL CALLS
        // ---------------------------------------------------------

        private void LogToolCallStart(JsonElement root)
        {
            _logger.LogInformation(
                "AG-UI {EventType} ToolCallId={ToolCallId} ToolCallName={ToolCallName}",
                "TOOL_CALL_START",
                GetString(root, "toolCallId"),
                GetString(root, "toolCallName"));
        }

        private void LogToolCallArgs(JsonElement root)
        {
            _logger.LogDebug(
                "AG-UI {EventType} ToolCallId={ToolCallId} Delta={Delta}",
                "TOOL_CALL_ARGS",
                GetString(root, "toolCallId"),
                GetString(root, "delta"));
        }

        private void LogToolCallEnd(JsonElement root)
        {
            _logger.LogInformation(
                "AG-UI {EventType} ToolCallId={ToolCallId}",
                "TOOL_CALL_END",
                GetString(root, "toolCallId"));
        }

        // ---------------------------------------------------------
        // GENERIC
        // ---------------------------------------------------------

        private void LogGenericEvent(
            string eventType,
            JsonElement root)
        {
            _logger.LogDebug(
                "AG-UI unhandled event {EventType} Data={Data}",
                eventType,
                root.GetRawText());
        }

        // ---------------------------------------------------------
        // SSE
        // ---------------------------------------------------------

        private static string? ExtractData(
            string sseEvent)
        {
            StringBuilder data = new();

            foreach (string line in sseEvent.Split('\n'))
            {
                string lineValue =
                    line.TrimEnd('\r');

                if (!lineValue.StartsWith(
                        "data:",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string value =
                    lineValue["data:".Length..]
                        .TrimStart();

                if (data.Length > 0)
                    data.Append('\n');

                data.Append(value);
            }

            return data.Length == 0
                ? null
                : data.ToString();
        }

        // ---------------------------------------------------------
        // JSON
        // ---------------------------------------------------------

        private static string? GetString(
            JsonElement element,
            string propertyName)
        {
            if (!element.TryGetProperty(
                    propertyName,
                    out JsonElement property))
            {
                return null;
            }

            return property.ValueKind switch
            {
                JsonValueKind.String =>
                    property.GetString(),

                JsonValueKind.Null =>
                    null,

                _ =>
                    property.GetRawText()
            };
        }

        // ---------------------------------------------------------
        // Internal types
        // ---------------------------------------------------------

        private enum MessageKind
        {
            Text,
            Reasoning
        }

        private sealed class MessageBuffer
        {
            private readonly object _sync = new();
            private readonly StringBuilder _content = new();

            public MessageBuffer(
                MessageKind kind,
                string? role)
            {
                Kind = kind;
                Role = role;
            }

            public MessageKind Kind { get; }

            public string? Role { get; }

            public void Append(string value)
            {
                lock (_sync)
                {
                    _content.Append(value);
                }
            }

            public string GetContent()
            {
                lock (_sync)
                {
                    return _content.ToString();
                }
            }
        }
    }
}
