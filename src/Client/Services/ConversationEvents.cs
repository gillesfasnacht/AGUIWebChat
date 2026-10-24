namespace AGUIWebChat.Client.Services;

public sealed class ConversationEvents
{
    public event Func<Task>? Changed;

    public async Task NotifyAsync()
    {
        if (Changed is null) return;
        foreach (var callback in Changed.GetInvocationList().Cast<Func<Task>>())
            await callback();
    }
}
