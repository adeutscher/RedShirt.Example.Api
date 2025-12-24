using Microsoft.AspNetCore.Mvc;

namespace RedShirt.Example.Api.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class ProducesJsonAttribute() : ProducesAttribute("application/json");