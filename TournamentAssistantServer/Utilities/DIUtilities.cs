using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

/**
 * Created by Moon on 2/10/2021
 * A collection of utilites to implement dependency injection
 */

namespace TournamentAssistantServer.Utilities
{
    static class DIUtilities
    {
        /// <summary>
        /// Creates an object of the specified type, injecting the provided services to both constructor parameters and public properties.
        /// Similar to ActivatorUtilities.CreateInstance(), but with the added bonus of injecting properties.
        /// </summary>
        /// <typeparam name="T">Type to instantiate</typeparam>
        /// <param name="services">Optional service collection to inject upon instantiation</param>
        /// <param name="extras">Extra objects to inject which aren't included in the service collection. Not useful in standard DI, but a helpful feature for CommandService.</param>
        /// <returns></returns>
        public static T CreateWithServices<T>(IServiceProvider services = null, params object[] extras) => (T)typeof(T).CreateWithServices(services, extras);


        /// <summary>
        /// Creates an object of the specified type, injecting the provided services to both constructor parameters and public properties.
        /// Similar to ActivatorUtilities.CreateInstance(), but with the added bonus of injecting properties.
        /// </summary>
        /// <param name="type">Type to instantiate</typeparam>
        /// <param name="services">Optional service collection to inject upon instantiation</param>
        /// <param name="extras">Extra objects to inject which aren't included in the service collection. Not useful in standard DI, but a helpful feature for CommandService.</param>
        /// <returns></returns>
        public static object CreateWithServices(this Type type, IServiceProvider services = null, params object[] extras)
        {
            // We'll just take the first constructor we can see. Having more than one is undefined behavior
            var constructor = type.GetConstructors().First();
            var providedParameters = new List<object>();

            foreach (var paremeter in constructor.GetParameters())
            {
                var providedService = services.GetService(paremeter.ParameterType);
                if (providedService == null) providedService = extras.FirstOrDefault(x => x.GetType() == paremeter.ParameterType);
                providedParameters.Add(providedService);
            }

            var createdObject = constructor.Invoke(providedParameters.ToArray());

            // Inject properties as well. Inspired by Discord.NET
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var providedService = services.GetService(property.PropertyType);
                if (providedService == null) providedService = extras.FirstOrDefault(x => x.GetType() == property.PropertyType);
                if (providedService != null) property.SetValue(createdObject, providedService);
            }

            return createdObject;
        }
    }
}
