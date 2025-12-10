namespace LLMClient.Component.Utility;

public static class MessageEventBus
{
    public static event Action<string>? MessageReceived;

    public static void Publish<T>(T message)
    {
        if (message == null)
        {
            return;
        }

        var s = message.ToString();
        if (string.IsNullOrEmpty(s))
        {
            return;
        }

        MessageReceived?.Invoke(s);
    }
}