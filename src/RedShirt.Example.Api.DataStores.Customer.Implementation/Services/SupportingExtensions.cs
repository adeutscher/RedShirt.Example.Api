using RedShirt.Example.Api.DataStores.Customer.Core.Models;

namespace RedShirt.Example.Api.DataStores.Customer.Implementation.Services;

internal static class SupportingExtensions
{
    public static bool AreChangesRequested(this CustomerServicePatchRequest subject)
    {
        return !string.IsNullOrWhiteSpace(subject.Email)
               || !string.IsNullOrWhiteSpace(subject.DisplayName);
    }

    public static bool IsTheSameAs(this CustomerDto a, CustomerDto b)
    {
        return a.Email == b.Email
               && a.DisplayName == b.DisplayName;
    }
}