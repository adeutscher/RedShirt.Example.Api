namespace RedShirt.Example.Api.ClientEvents.Domains.Example.Utilities;

internal static class ExampleMessageTopicNames
{
    public static string ForUser(string userId)
    {
        return $"example-message/user/{userId}";
    }
}