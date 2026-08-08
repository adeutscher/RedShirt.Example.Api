using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Polly;
using Polly.Retry;

namespace RedShirt.Example.Api.Common.Database.DapperMySql.Utility;

public static class PolicyHelper
{
    public static AsyncRetryPolicy GetRetryPolicy(ILogger logger)
    {
        return Policy.Handle<MySqlException>(e => e.IsTransient)
            .WaitAndRetryAsync(3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    logger.LogWarning("Transient SQL Exception: {EMessage}", exception.Message);
                }
            );
    }
}