using AGUI.Abstractions;
using Microsoft.Agents.AI;

namespace AGUIWebChatServer.Midleware
{
    public class CustomEventExample
    {
        //private readonly IAGUISession _session;
        //private readonly AgentResponseUpdate toto;

        //public CustomEventExample(IAGUISession session)
        //{
        //    _session = session ?? throw new ArgumentNullException(nameof(session));
        //}

        //public async Task SendCustomEventAsync()
        //{
        //    // Create a custom event with your own payload
        //    var customEvent = new CustomEvent
        //    {
        //        Name = "UserLevelUp", // Your custom event name
        //        Data = new
        //        {
        //            UserId = 42,
        //            NewLevel = 5,
        //            Achievements = new[] { "FirstWin", "SharpShooter" }
        //        }
        //    };

        //    // Send the event to the connected client
        //    await _session.SendEventAsync(customEvent);
        //    Console.WriteLine("CustomEvent sent to client.");
        //}

        //public void HandleIncomingEvent(CustomEvent evt)
        //{
        //    Console.WriteLine($"Received CustomEvent: {evt.Name}");
        //    Console.WriteLine($"Payload: {evt.Data}");
        //    // Handle according to your application logic
        //}
    }
}
