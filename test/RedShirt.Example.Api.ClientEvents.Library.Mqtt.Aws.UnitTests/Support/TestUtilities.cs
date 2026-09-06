namespace RedShirt.Example.Api.ClientEvents.Library.Mqtt.Aws.UnitTests.Support;

internal static class TestUtilities
{
    internal static void WrapEnvironment(IReadOnlyDictionary<string, string> values, Action action)
    {
        var previous = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var (key, value) in values)
        {
            previous[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }

        try
        {
            action();
        }
        finally
        {
            foreach (var (key, value) in previous)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    internal static void WrapLocalAwsEnvironment(Action action)
    {
        WrapEnvironment(new Dictionary<string, string>
        {
            ["AWS_SERVICE_URL"] = "http://localhost:4566",
            ["AWS_ACCESS_KEY_ID"] = "foo",
            ["AWS_SECRET_ACCESS_KEY"] = "bar",
            ["AWS_REGION"] = "us-east-1"
        }, action);
    }
}