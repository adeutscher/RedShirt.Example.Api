using System;
using System.Collections.Generic;

namespace RedShirt.Example.Api.Interop.Exceptions;

// Hand-maintained counterpart to NSwag exceptionClass output (see nswag.json, generateExceptionClasses=false).
// EventStreamListener.cs is compiled in the same MSBuild pass as Client.generated.cs.
// Compile inputs are resolved before the NSwag CoreCompile target runs.
// These types must already exist on disk for a cold build from before Client.generated.cs to succeed.
// Do not remove in favour of generated copies without fixing that ordering.
// BeforeTargets=CoreCompile is not enough on its own.
public class SwaggerException : Exception
{
    public int StatusCode { get; }

    public string? Response { get; }

    public IReadOnlyDictionary<string, IEnumerable<string>> Headers { get; }

    // ReSharper disable once ConvertToPrimaryConstructor
    public SwaggerException(
        string message,
        int statusCode,
        string? response,
        IReadOnlyDictionary<string, IEnumerable<string>> headers,
        Exception? innerException)
        : base(
            message + "\n\nStatus: " + statusCode + "\nResponse: \n" +
            (response == null ? "(null)" : response.Substring(0, response.Length >= 512 ? 512 : response.Length)),
            innerException)
    {
        StatusCode = statusCode;
        Response = response;
        Headers = headers;
    }

    public override string ToString()
    {
        return string.Format("HTTP Response: \n\n{0}\n\n{1}", Response, base.ToString());
    }
}

public class SwaggerException<TResult> : SwaggerException
{
    public TResult Result { get; }

    public SwaggerException(
        string message,
        int statusCode,
        string? response,
        IReadOnlyDictionary<string, IEnumerable<string>> headers,
        TResult result,
        Exception? innerException)
        : base(message, statusCode, response, headers, innerException)
    {
        Result = result;
    }
}