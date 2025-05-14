using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Threading.Tasks;
using TournamentAssistantShared.Models;

/**
 * Created by Moon on 5/11/2025
 * This attribute allows auto-populating the User
 * as an endpoint parameter. The UserFromTokenBinder and
 * UserFromTokenBinderProvider are also part of this process
 */

namespace TournamentAssistantServer.ASP.Binders
{
    public class UserFromTokenBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var httpContext = bindingContext.HttpContext;

            if (httpContext.Items.TryGetValue("UserFromToken", out var userObj) && userObj is User user)
            {
                bindingContext.Result = ModelBindingResult.Success(user);
            }
            else
            {
                bindingContext.Result = ModelBindingResult.Failed();
            }

            return Task.CompletedTask;
        }
    }
}
