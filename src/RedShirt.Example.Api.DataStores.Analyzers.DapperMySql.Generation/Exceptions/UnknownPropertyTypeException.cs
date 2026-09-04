using System;

namespace RedShirt.Example.Api.DataStores.Analyzers.DapperMySql.Generation.Exceptions;

public class UnknownPropertyTypeException : Exception
{
    public UnknownPropertyTypeException(string message) : base(message)
    {
    }
}