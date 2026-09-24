┌───────────────────────────────────────────────────────────────────────────────────────────┐
│                                    UTILISATEUR                                            │
└─────────────────────────────────────────┬─────────────────────────────────────────────────┘
                                          │
                                          ▼
┌───────────────────────────────────────────────────────────────────────────────────────────┐
│                         CLIENT BLAZOR WEB + FLUENT UI v5                                  │
│                                                                                           │
│  ┌─────────────────────────────────────────────────────────────────────────────────────┐  │
│  │ Chat.razor                                                                          │  │
│  │                                                                                     │  │
│  │  ┌─────────────────┐   ┌─────────────────────┐   ┌───────────────────────────────┐  │  │
│  │  │ ChatReasoning   │   │ ChatMessage         │   │ ChatMonitoring                │  │  │
│  │  │                 │   │                     │   │                               │  │  │
│  │  │ Hidden          │   │ Markdown            │   │ KPI                           │  │  │
│  │  │ Collapsed       │   │ Streaming           │   │ Tokens                        │  │  │
│  │  │ Expanded        │   │                     │   │ Durations                     │  │  │
│  │  │ Duration        │   │ RunId               │   │ Throughput                    │  │  │
│  │  └─────────────────┘   └─────────────────────┘   │ Advanced                      │  │  │
│  │                                                  └───────────────────────────────┘  │  │
│  └─────────────────────────────────────────────────────────────────────────────────────┘  │
│                                                                                           │
│  ┌─────────────────────────────┐             ┌─────────────────────────────────────────┐  │
│  │ AgUiChatService             │             │ TelemetryService                        │  │
│  │                             │             │                                         │  │
│  │ AGUI.Client                 │             │ SignalR Client                          │  │
│  │ AIAgent proxy               │             │                                         │  │
│  │ AgentSession                │             │ ReasoningCompleted                      │  │
│  │ RunAgentInput.State         │             │ LlmMetricsCompleted                     │  │
│  └──────────────┬──────────────┘             └─────────────────▲───────────────────────┘  │
│                 │                                              │                          │
│  ┌──────────────┴──────────────────────────────────────────────┴───────────────────────┐  │
│  │ ChatSettings                                                                        │  │
│  │                                                                                     │  │
│  │ ReasoningDisplay       MonitoringDisplay                                            │  │
│  │ ThinkingEffort         Monitoring.ShowTokens                                        │  │
│  │ Temperature            Monitoring.ShowDurations                                     │  │
│  │ TopP                   Monitoring.ShowThroughput                                    │  │
│  │ TopK                   Monitoring.ShowModelInfo                                     │  │
│  │ NumCtx                 Monitoring.ShowAdvancedMetrics                               │  │
│  └─────────────────────────────────────────────────────────────────────────────────────┘  │
└───────────────────────────────┬──────────────────────────────────▲────────────────────────┘
                                │                                  │
                                │ AG-UI / SSE                      │ SignalR
                                │                                  │
                                │ Conversation                     │ Telemetry
                                │ Reasoning                        │
                                │ Streaming                        │
                                ▼                                  │
┌───────────────────────────────────────────────────────────────────────────────────────────┐
│                              ASP.NET CORE .NET 10 SERVER                                  │
│                                                                                           │
│  Microsoft.Agents.AI.Hosting.AGUI.AspNetCore                                              │
│  AGUI.Server / AGUI.Abstractions                                                          │
│                                                                                           │
│  ┌─────────────────────────────────────────────────────────────────────────────────────┐  │
│  │                            MapAGUIServer("/ag-ui")                                  │  │
│  └────────────────────────────────────────┬────────────────────────────────────────────┘  │
│                                           │                                               │
│                                           ▼                                               │
│  ┌─────────────────────────────────────────────────────────────────────────────────────┐  │
│  │                         Microsoft Agent Framework                                   │  │
│  │                                                                                     │  │
│  │  CreateAgent()                                                                      │  │
│  │                                                                                     │  │
│  │  RunAgentInput                                                                      │  │
│  │     ├── RunId  ◄──────────────────── CORRELATION ───────────────────────────┐       │  │
│  │     └── State                                                               │       │  │
│  │          ├── thinkingEffort                                                 │       │  │
│  │          ├── temperature                                                    │       │  │
│  │          ├── topP                                                           │       │  │
│  │          ├── topK                                                           │       │  │
│  │          └── numCtx                                                         │       │  │
│  │                                                                             │       │  │
│  │                         Streaming Pipeline                                  │       │  │
│  │                                                                             │       │  │
│  │     ┌───────────────────────────────────────────────────────────────┐       │       │  │
│  │     │ LlmTelemetry.ObserveAsync()                                   │       │       │  │
│  │     │                                                               │       │       │  │
│  │     │  • Stopwatch serveur                                          │       │       │  │
│  │     │  • UsageContent                                               │       │       │  │
│  │     │  • ChatDoneResponseStream                                     │       │       │  │
│  │     └──────────────────────────────┬────────────────────────────────┘       │       │  │
│  │                                    │                                        │       │  │
│  │                                    ▼                                        │       │  │
│  │     ┌───────────────────────────────────────────────────────────────┐       │       │  │
│  │     │ ReasoningTelemetry.ObserveAsync()                             │       │       │  │
│  │     │                                                               │       │       │  │
│  │     │  • TextReasoningContent                                       │       │       │  │
│  │     │  • Duration reasoning                                         │       │       │  │
│  │     │  • ChunkCount                                                 │       │       │  │
│  │     │  • CharacterCount                                             │       │       │  │
│  │     └──────────────────────────────┬────────────────────────────────┘       │       │  │
│  │                                    │                                        │       │  │
│  │                                    ▼                                        │       │  │
│  │                      innerAgent.RunStreamingAsync()                         │       │  │
│  │                                    │                                        │       │  │
│  └────────────────────────────────────┼────────────────────────────────────────┼───────┘  │
│                                       │                                        │          │
│                                       ▼                                        │          │
│  ┌────────────────────────────────────────────────────────────────────────┐    │          │
│  │                         OllamaSharp 5.4.30                             │    │          │
│  │                                                                        │    │          │
│  │  ChatResponseStream                                                    │    │          │
│  │       │                                                                │    │          │
│  │       └── final update → ChatDoneResponseStream                        │    │          │
│  │                           ├── TotalDuration                            │    │          │
│  │                           ├── LoadDuration                             │    │          │
│  │                           ├── PromptEvalCount                          │    │          │
│  │                           ├── PromptEvalDuration                       │    │          │
│  │                           ├── EvalCount                                │    │          │
│  │                           └── EvalDuration                             │    │          │
│  └───────────────────────────────────┬────────────────────────────────────┘    │          │
│                                      │                                         │          │
└──────────────────────────────────────┼─────────────────────────────────────────┼──────────┘
                                       │                                         │
                                       ▼                                         │
                         ┌────────────────────────────┐                          │
                         │          OLLAMA            │                          │
                         │                            │                          │
                         │      granite4.2:8b         │                          │
                         └────────────────────────────┘                          │
                                                                                 │
                    ┌────────────────────────────────────────────────────────────┘
                    │
                    ▼
┌───────────────────────────────────────────────────────────────────────────────────────────┐
│                                   SIGNALR TELEMETRY                                       │
│                                                                                           │
│                               TelemetryHub  /telemetry                                    │
│                                                                                           │
│       ┌──────────────────────────────────┐      ┌─────────────────────────────────────┐   │
│       │ ReasoningCompleted               │      │ LlmMetricsCompleted                 │   │
│       │                                  │      │                                     │   │
│       │ RunId                            │      │ RunId                               │   │
│       │ DurationMs                       │      │ AgentName                           │   │
│       │ ChunkCount                       │      │ Model                               │   │
│       │ CharacterCount                   │      │ Input / Output / Total tokens       │   │
│       │                                  │      │ Server duration                     │   │
│       └──────────────────────────────────┘      │ Ollama duration                     │   │
│                                                 │ Load / Prompt / Generation          │   │
│                                                 │ Tokens/sec                          │   │
│                                                 └─────────────────────────────────────┘   │
│                                                                                           │
│                              same RunId for both events                                   │
└───────────────────────────────────────────────────────────────────────────────────────────┘