using Microsoft.AspNetCore.Mvc;
using TournamentAssistantServer.ASP.Attributes;
using TournamentAssistantServer.PacketService.Attributes;

namespace TournamentAssistantServer.PacketHandlers
{
    [AllowWebsocketToken]
    [ApiController]
    [Route("api/live")]
    public sealed class LiveController : ControllerBase
    {
        public TAServer TAServer { get; set; }

        [HttpGet]
        [AllowUnauthorized]
        public ContentResult GetLiveReplayStatuses()
        {
            Response.Headers["Access-Control-Allow-Origin"] = "*";
            return Content(TAServer.GetLiveReplayStatusesJson(), "application/json");
        }
    }
}
