/**
 * Created by Moon on 9/13/2020
 * I can't seem to use SystemServer directly in DI
 * because if it *doesn't* exist, DI throws a fit.
 * So I'm using this intermediary class to serve
 * it if it does exist.
 */

namespace TournamentAssistantCore.Discord.Services
{
    public class SystemServerService
    {
        private SystemServer _server;

        public SystemServerService(SystemServer server)
        {
            _server = server;
        }

        public SystemServer GetServer()
        {
            return _server;
        }
    }
}
