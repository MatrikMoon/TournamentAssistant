using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;

/**
 * Created by Moon on 5/11/2025
 * This attribute allows auto-populating the User
 * as an endpoint parameter. The UserFromTokenBinder and
 * UserFromTokenBinderProvider are also part of this process
 */

namespace TournamentAssistantServer.ASP.Attributes
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public class FromUser : Attribute, IBindingSourceMetadata
    {
        public BindingSource BindingSource => BindingSource.Custom;
    }
}
