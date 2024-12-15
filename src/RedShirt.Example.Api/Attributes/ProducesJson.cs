using Microsoft.AspNetCore.Mvc;

namespace RedShirt.Example.Api.Attributes;

public class ProducesJsonAttribute() : ProducesAttribute("application/json");