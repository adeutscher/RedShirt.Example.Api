namespace RedShirt.Example.Api.Core.Services;

/// <summary>
///     Identifies one idempotent operation attempt.
///     A client-supplied key is only meaningful for the caller that sent it and the operation it was sent to,
///     so all three parts make up the cache identity.
/// </summary>
/// <param name="Operation">Name of the operation, normally the command type name.</param>
/// <param name="CallerId">Identity of the caller (for example the JWT subject).</param>
/// <param name="Key">The client-supplied idempotency key.</param>
public sealed record IdempotencyScope(string Operation, string CallerId, string Key)
{
    /// <summary>
    ///     Builds an unambiguous cache key fragment.
    ///     Each part is escaped so that a ':' inside one part cannot shift the boundary between parts
    ///     (for example caller "a:b" with key "c" vs caller "a" with key "b:c").
    /// </summary>
    public string ToCacheKeyFragment()
    {
        return
            $"{Uri.EscapeDataString(Operation)}:{Uri.EscapeDataString(CallerId)}:{Uri.EscapeDataString(Key)}";
    }
}
