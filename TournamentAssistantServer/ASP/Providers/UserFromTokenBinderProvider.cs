using Microsoft.AspNetCore.Mvc.ModelBinding;
using TournamentAssistantServer.ASP.Binders;
using TournamentAssistantShared.Models;

/**
 * Created by Moon on 5/11/2025
 * This attribute allows auto-populating the User
 * as an endpoint parameter. The UserFromTokenBinder and
 * UserFromTokenBinderProvider are also part of this process
 */

namespace TournamentAssistantServer.ASP.Providers
{
    public class UserFromTokenBinderProvider : IModelBinderProvider
    {
        public IModelBinder? GetBinder(ModelBinderProviderContext context)
        {
            if (context.Metadata.ModelType == typeof(User) &&
                context.BindingInfo.BindingSource == BindingSource.Custom)
            {
                return new UserFromTokenBinder();
            }

            return null;
        }
    }
}
