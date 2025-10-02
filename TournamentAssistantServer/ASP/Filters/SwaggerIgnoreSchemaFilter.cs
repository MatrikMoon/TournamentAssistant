using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Linq;
using System.Reflection;
using TournamentAssistantServer.ASP.Attributes;

/**
 * Created by Moon on 8/12/2025
 * 
 * Ensures that arguments meant to be populated from the context rather than body
 * (see: FromUser) don't appear in the Swagger schema
 */

namespace TournamentAssistantServer.ASP.Filters
{
    public class SwaggerIgnoreParameterFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (operation.Parameters is null) return;

            // Find parameters decorated with [FromUser]
            var ignored = context.ApiDescription.ParameterDescriptions
                .Select(d => d.ParameterDescriptor as ControllerParameterDescriptor)
                .Where(d => d?.ParameterInfo?.GetCustomAttribute<FromUser>() != null)
                .Select(d => d!.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (ignored.Count == 0) return;

            operation.Parameters = operation.Parameters
                .Where(p => !ignored.Contains(p.Name))
                .ToList();
        }
    }
}
