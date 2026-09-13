using AGUIFluentUIChatClient.Models;
using Microsoft.JSInterop;
using System.Text.Json;

namespace AGUIFluentUIChatClient.Services
{
    public sealed class ChatSettingsService
    {
        private const string StorageKey = "agui-chat-settings";

        private readonly IJSRuntime _jsRuntime;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public ChatSettings Settings { get; private set; } = new();

        public bool IsInitialized { get; private set; }

        public event Action? SettingsChanged;

        public ChatSettingsService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task InitializeAsync()
        {
            if (IsInitialized)
                return;

            try
            {
                var json = await _jsRuntime.InvokeAsync<string?>(
                    "localStorage.getItem",
                    StorageKey);

                if (!string.IsNullOrWhiteSpace(json))
                {
                    Settings =
                        JsonSerializer.Deserialize<ChatSettings>(
                            json,
                            JsonOptions)
                        ?? new ChatSettings();
                }
            }
            catch (JSException)
            {
                Settings = new ChatSettings();
            }

            IsInitialized = true;

            //await ApplyThemeAsync();

            SettingsChanged?.Invoke();
        }

        //public async Task ApplyThemeAsync()
        //{
        //    await _jsRuntime.InvokeVoidAsync(
        //        "chatTheme.apply",
        //        Settings.Theme.ToString());
        //}

        public async Task SaveAsync()
        {
            var json = JsonSerializer.Serialize(
                Settings,
                JsonOptions);

            await _jsRuntime.InvokeVoidAsync(
                "localStorage.setItem",
                StorageKey,
                json);

            SettingsChanged?.Invoke();
        }

        public async Task ResetAsync()
        {
            Settings = new ChatSettings();

            await _jsRuntime.InvokeVoidAsync(
                "localStorage.removeItem",
                StorageKey);

            //await ApplyThemeAsync();

            SettingsChanged?.Invoke();
        }
    }
}
