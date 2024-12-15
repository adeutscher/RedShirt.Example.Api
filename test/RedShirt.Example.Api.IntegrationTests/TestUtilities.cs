namespace RedShirt.Example.Api.IntegrationTests;

public static class TestUtilities
{
    public static void WrapEnvironment(Dictionary<string, string> environment, Action callback)
    {
        var backup = new Dictionary<string, string?>();

        try
        {
            foreach (var kvp in environment)
            {
                backup[kvp.Key] = Environment.GetEnvironmentVariable(kvp.Key);
                Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
            }

            callback();
        }
        finally
        {
            foreach (var kvp in backup)
            {
                Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
            }
        }
    }
}