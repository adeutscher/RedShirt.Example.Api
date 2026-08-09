using System;

namespace RedShirt.Example.Api.Common.Analyzers.Database.DapperMySql.Generation.Exceptions;

public class UnknownPropertyTypeException : Exception
{
    public UnknownPropertyTypeException(string message) : base(message)
    {
    }
}