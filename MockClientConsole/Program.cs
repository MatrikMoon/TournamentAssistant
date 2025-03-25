using Microsoft.IdentityModel.Tokens;
using MockClientConsole;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using TournamentAssistantShared;

public static class Program
{
    private static X509Certificate2 MockCert { get; } = new("files/mock.pfx", "password");
    private static List<MockClient> MockClients { get; set; } = new List<MockClient>();

    private static readonly Random random = new();

    private static string _qualifierName;
    private static string _levelId;
    private static int _multipliedScore;
    private static int _modifiedScore;
    private static int _maxPossibleScore;
    private static double _accuracy;
    private static int _notesMissed;
    private static int _badCuts;
    private static int _goodCuts;
    private static int _maxCombo;
    private static bool _fullCombo;
    private static bool _isPlaceholder;

    public static void Main(string[] args)
    {
        var argString = string.Join(" ", args);
        var addressArg = Utilities.ParseArgs(argString, "address");
        var portArg = Utilities.ParseArgs(argString, "port");
        var tournamentNameArg = Utilities.ParseArgs(argString, "tournamentName");
        var countArg = Utilities.ParseArgs(argString, "count");
        var idsArg = Utilities.ParseArgs(argString, "userIds");
        var namesArg = Utilities.ParseArgs(argString, "userNames");

        var qualfierNameArg = Utilities.ParseArgs(argString, "qualifierName");
        var levelIdArg = Utilities.ParseArgs(argString, "levelId");
        var multipliedScoreArg = Utilities.ParseArgs(argString, "multipliedScore");
        var modifiedScoreArg = Utilities.ParseArgs(argString, "modifiedScore");
        var maxPossibleScoreArg = Utilities.ParseArgs(argString, "maxPossibleScore");
        var accuracyArg = Utilities.ParseArgs(argString, "accuracy");
        var notesMissedArg = Utilities.ParseArgs(argString, "notesMissed");
        var badCutsArgArg = Utilities.ParseArgs(argString, "badCuts");
        var goodCutsArg = Utilities.ParseArgs(argString, "goodCuts");
        var maxComboArg = Utilities.ParseArgs(argString, "maxCombo");
        var fullComboArg = Utilities.ParseArgs(argString, "fullCombo");
        var isPlaceholderArg = Utilities.ParseArgs(argString, "isPlaceholder");

        var address = string.IsNullOrEmpty(addressArg) ? "server.tournamentassistant.net" : addressArg;
        var port = string.IsNullOrEmpty(portArg) ? 8675 : int.Parse(portArg);
        var tournamentName = string.IsNullOrEmpty(tournamentNameArg) ? "Moon's Test Tourney" : tournamentNameArg;
        var count = string.IsNullOrEmpty(countArg) ? 1 : int.Parse(countArg);
        var idList = new List<string>();
        var nameList = new List<string>();

        _qualifierName = qualfierNameArg;
        _levelId = levelIdArg;
        _ = int.TryParse(multipliedScoreArg, out _multipliedScore);
        _ = int.TryParse(modifiedScoreArg, out _modifiedScore);
        _ = int.TryParse(maxPossibleScoreArg, out _maxPossibleScore);
        _ = double.TryParse(accuracyArg, out _accuracy);
        _ = int.TryParse(notesMissedArg, out _notesMissed);
        _ = int.TryParse(badCutsArgArg, out _badCuts);
        _ = int.TryParse(goodCutsArg, out _goodCuts);
        _ = int.TryParse(maxComboArg, out _maxCombo);
        _fullCombo = fullComboArg != null;
        _isPlaceholder = isPlaceholderArg != null;

        if (!string.IsNullOrEmpty(idsArg))
        {
            idList.AddRange(idsArg.Split(","));
        }

        if (!string.IsNullOrEmpty(namesArg))
        {
            nameList.AddRange(namesArg.Split(","));
        }

        ConnectClients(count, address, port, tournamentName, idList, nameList);

        Console.ReadLine();
    }

    private static void ConnectClients(int count, string address, int port, string tournamentName, List<string> userIds = null, List<string> userNames = null)
    {
        var promises = new List<Task>();
        for (int i = 0; i < count; i++)
        {
            var userId = $"{random.Next(int.MaxValue)}";
            var userName = GenerateName();

            if (userIds != null && userIds.Count > 0)
            {
                userId = userIds[i];
            }

            if (userNames != null && userNames.Count > 0)
            {
                userName = userNames[i];
            }

            var client = new MockClient(address, port, tournamentName);
            var token = GenerateMockToken(userId, userName);
            client.SetAuthToken(token);
            client.ServerDisconnected += Client_ServerDisconnected;
            client.JoinedTournament += async (joinResponse) =>
            {
                // If we have some of this data set, let's try to submit a qualifier score
                if (!string.IsNullOrWhiteSpace(_qualifierName))
                {
                    await client.SubmitQualifierScore(tournamentName, _qualifierName, _levelId, _multipliedScore, _modifiedScore, _maxPossibleScore, _accuracy, _notesMissed, _badCuts, _goodCuts, _maxCombo, _fullCombo, _isPlaceholder);
                }
            };

            promises.Add(client.Connect());

            MockClients.Add(client);
        }

        Task.WaitAll(promises.ToArray());
    }

    private static Task Client_ServerDisconnected()
    {
        Logger.Error("DISCONNECTED");
        return Task.CompletedTask;
    }

    private static string GenerateName(int desiredLength = -1)
    {
        string[] consonants = { "b", "c", "d", "f", "g", "h", "j", "k", "l", "m", "l", "n", "p", "q", "r", "s", "sh", "zh", "t", "v", "w", "x" };
        string[] vowels = { "a", "e", "i", "o", "u", "ae", "y" };

        if (desiredLength < 0) desiredLength = random.Next(6, 20);

        string name = string.Empty;

        for (int i = 0; i < desiredLength; i++)
        {
            name += i % 2 == 0 ? consonants[random.Next(consonants.Length)] : vowels[random.Next(vowels.Length)];
            if (i == 0) name = name.ToUpper();
        }

        return name;
    }

    private static string GenerateMockToken(string userId, string name)
    {
        // Create the signing credentials with the certificate
        var signingCredentials = new X509SigningCredentials(MockCert);

        // Create a list of claims for the token payload
        var claims = new[]
        {
            new Claim("sub", Guid.NewGuid().ToString()),
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            new Claim("exp", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString()),
            new Claim("ta:platform_id", userId),
            new Claim("ta:platform_username", name),
        };

        // Create the JWT token with the claims and signing credentials
        var token = new JwtSecurityToken(
            issuer: "ta_plugin_mock",
            audience: "ta_users",
            claims: claims,
            signingCredentials: signingCredentials
        );

        // Create a JWT token handler and serialize the token to a string
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}