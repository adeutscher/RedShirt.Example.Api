using System;

namespace RedShirt.Example.Api.Common.Analyzers.Database.Generation.Exceptions;

public class UnknownPropertyTypeException : Exception
{
    public UnknownPropertyTypeException(string message) : base(message)
    {
    }
}