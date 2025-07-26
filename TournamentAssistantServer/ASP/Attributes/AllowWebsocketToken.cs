using System;

/**
 * Created by Moon on 7/14/2025
 * Sometimes, in rare cases (right now only for file
 * upload and download, ie: the tournament image server),
 * we don't need to bother the user with getting the a REST
 * token, since this is really the only call they'll be making with it...
 * So we'll be nice and allow websocket tokens in this one controller.
 */

namespace TournamentAssistantServer.ASP.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class AllowWebsocketToken : Attribute
    {
    }
}
