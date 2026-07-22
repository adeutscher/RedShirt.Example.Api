namespace RedShirt.Example.Api.Core.Exceptions;

/// <summary>
///     Implies that an idempotent operation was requested while a previous instance of the same operation was in motion.
///     Intended to be handled by returning an HTTP 409 status to the caller, as this is apparently the industry standard
///     (I originally thought that HTTP 102 Processing might be a good candidate).
/// </summary>
public class IdempotentConcurrencyException : Exception;