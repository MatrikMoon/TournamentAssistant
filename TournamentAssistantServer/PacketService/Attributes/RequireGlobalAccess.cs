using System;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using TournamentAssistantServer.ASP.Filters;

namespace TournamentAssistantServer.PacketService.Attributes
{
    public enum GlobalAccessRequirement
    {
        EndpointManagement,
        FullAccess,
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class RequireGlobalAccess : Attribute, IFilterFactory
    {
        public GlobalAccessRequirement Requirement { get; }
        public bool IsReusable => false;

        public RequireGlobalAccess(GlobalAccessRequirement requirement)
        {
            Requirement = requirement;
        }

        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
            new RequireGlobalAccessFilter(
                serviceProvider.GetRequiredService<TournamentAssistantServer.Database.DatabaseService>(),
                this);
    }
}
