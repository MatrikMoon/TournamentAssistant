using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System;

/**
 * Created by Moon on 5/11/2025
 * This activator allows regular, constructor-based DI,
 * then goes back and also does Property injection.
 * Yes, yes, I know it's not encouraged, but my old code
 * had property injection and I don't wanna change it. Yet.
 * Not until it bites me, at least.
 */

namespace TournamentAssistantServer.ASP.Activators
{
    public class PropertyInjectionActivator : IControllerActivator
    {
        private readonly IServiceProvider _services;

        public PropertyInjectionActivator(IServiceProvider services)
        {
            _services = services;
        }

        public object Create(ControllerContext context)
        {
            var controller = ActivatorUtilities.CreateInstance(_services, context.ActionDescriptor.ControllerTypeInfo.AsType());

            foreach (var prop in controller.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.CanWrite && prop.PropertyType != null && _services.GetService(prop.PropertyType) is { } value)
                {
                    prop.SetValue(controller, value);
                }
            }

            return controller;
        }

        public void Release(ControllerContext context, object controller) { }
    }
}
