/**
* Created by Moon on 9/13/2020
* I can't seem to use SystemServer directly in DI
* because if it *doesn't* exist, DI throws a fit.
* So I'm using this intermediary class to serve
* it if it does exist.
*/
namespace TournamentAssistantServer.Discord.Services
{
    public class TAServerService
    {
        private TAServer _server;

        public TAServerService(TAServer server)
        {
            _server = server;
        }

        public TAServer GetServer()
        {
            return _server;
        }
    }
}
