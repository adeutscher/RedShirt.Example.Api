namespace RedShirt.Example.Api.Core.Exceptions;

/// <summary>
///     Implies that an idempotent operation was requested while a previous instance of the same operation was in motion.
///     Mapped to an HTTP 409 ProblemDetails response by the API exception handler (industry-common for this case;
///     HTTP 102 Processing was considered but is a poorer fit).
/// </summary>
public class IdempotentConcurrencyException() : Exception(string.Empty);