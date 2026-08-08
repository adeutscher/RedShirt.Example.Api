using System;

namespace RedShirt.Example.Api.Common.Analyzers.Database.Generation.Exceptions;

public class UnsupportedKeyTypeForPost : Exception
{
    public UnsupportedKeyTypeForPost(string message) : base(message)
    {
    }
}