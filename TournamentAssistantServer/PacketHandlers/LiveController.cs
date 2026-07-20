using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using TournamentAssistantServer.ASP.Attributes;
using TournamentAssistantServer.Models;
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
        public ActionResult<List<LiveReplayStatus>> GetLiveReplayStatuses()
        {
            Response.Headers["Access-Control-Allow-Origin"] = "*";
            return TAServer.GetLiveReplayStatuses();
        }
    }
}
