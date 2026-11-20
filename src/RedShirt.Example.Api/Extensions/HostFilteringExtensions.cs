using Microsoft.AspNetCore.HostFiltering;
using Microsoft.Extensions.Options;

namespace RedShirt.Example.Api.Extensions;

internal static class HostFilteringExtensions
{
    internal const string AllHostsAllowedWarning =
        "AllowedHosts is unset or '*', so requests are accepted for any Host header. Set AllowedHosts (environment " +
        "variable ALLOWED_HOSTS, ';'-separated) to the host names this API is served under, so links and redirects " +
        "built from the Host header cannot point at an attacker-chosen host. See docs/patterns/hosting.md.";

    /// <summary>
    ///     Logs a warning at startup when host filtering accepts any host outside the Development environment.
    ///     <para>
    ///         This is a warning rather than a startup failure: a template cannot know the ingress host names, and
    ///         health probes or load balancers that call the pod by IP would be rejected by a list that leaves them
    ///         out.
    ///     </para>
    /// </summary>
    internal static WebApplication WarnIfAllHostsAllowed(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            return app;
        }

        // When AllowedHosts is unset, ASP.NET Core falls back to "*", so check the resolved options.
        var allowedHosts = app.Services.GetRequiredService<IOptions<HostFilteringOptions>>().Value.AllowedHosts;
        if (allowedHosts.Count == 0 || allowedHosts.Any(host => host.Trim() == "*"))
        {
            app.Logger.LogWarning(AllHostsAllowedWarning);
        }

        return app;
    }
}