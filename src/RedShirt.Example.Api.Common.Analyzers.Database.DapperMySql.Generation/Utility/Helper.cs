using System;
using System.Threading.Tasks;

namespace RedShirt.Example.Api.Common.Analyzers.Database.DapperMySql.Generation.Utility;

public static class Helper
{
    /// <summary>
    ///     Lazy helper method for making a list type
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static string Listify(string type)
    {
        return $"System.Collections.Generic.List<{type}>";
    }

    /// <summary>
    ///     Lazy helper method for making a list type
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static string Listify(Type type)
    {
        return Listify(type.FullName!);
    }

    /// <summary>
    ///     Lazy helper method for making a Task
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static string Taskify(string type)
    {
        return $"{typeof(Task).FullName}<{type}>";
    }
}