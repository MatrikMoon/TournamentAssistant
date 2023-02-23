using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EUCModule
{
    public class EUC
    {
        private static int _createdQualifierScoreId = -1;
        private static string _token;
        private static GraphQLHttpClient _client = new GraphQLHttpClient("https://gql.beatsaberchampionship.eu/graphql", new NewtonsoftJsonSerializer());
        private static Dictionary<string, int> _levelMap = new Dictionary<string, int>();
        private static Dictionary<int, string> _difficultyMap = new Dictionary<int, string>
        {
            {0, "EASY" },
            {1, "NORMAL" },
            {2, "HARD" },
            {3, "EXPERT" },
            {4, "EXPERT_PLUS" },
        };

        private static async Task RefreshTokenIfExpired(string userId)
        {
            Console.WriteLine("Checking if token is expired...");

            if (_token != null)
            {
                Console.WriteLine("Parsing token...");

                var handler = new JwtSecurityTokenHandler();
                var jwtSecurityToken = handler.ReadJwtToken(_token);
                var currentTimeS = DateTimeOffset.Now.ToUnixTimeSeconds();

                var expiry = Convert.ToInt64(jwtSecurityToken.Claims.First(claim => claim.Type == "exp").Value);

                Console.WriteLine($"Expiry: {expiry}, current time: {currentTimeS}");

                if (expiry < currentTimeS)
                {
                    Console.WriteLine("Token expired, fetching new one...");

                    _token = await GetAuthToken(userId);
                    _client.HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);

                    Console.WriteLine($"New token: {_token}");
                }
            }
            else
            {
                Console.WriteLine("Token not yet fetched, fetching...");

                _token = await GetAuthToken(userId);
                _client.HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);

                Console.WriteLine($"New token: {_token}");
            }
        }

        private static async Task<string> GetAuthToken(string userId)
        {
            var request = new GraphQLRequest
            {
                Query = @"
                    mutation Login($userId: String!) {
                      authenticatePlayer(input: {userId: $userId}) {
                        jwtToken
                      }
                    }",
                OperationName = "Login",
                Variables = new
                {
                    userId = userId
                }
            };

            Console.WriteLine("GetAuthToken - SendMutationAsync");

            var response = await _client.SendMutationAsync<dynamic>(request);

            Console.WriteLine($"GetAuthToken - SendMutationAsync response: {response.Data}");

            foreach (var error in response.Errors)
            {
                Console.WriteLine($"GetAuthToken - SendMutationAsync error: {error.Message}");
            }

            return response.Data.authenticatePlayer.jwtToken;
        }

        [Obfuscation(Exclude = true)]
        public static async Task<int> GetLevelKey(string levelId, int difficulty)
        {
            if (levelId.StartsWith("custom_level_"))
            {
                levelId = levelId.Substring(13);
            }

            Console.WriteLine($"Getting level key for {levelId}:{_difficultyMap[difficulty]}...");

            if (!_levelMap.TryGetValue($"{levelId}:{_difficultyMap[difficulty]}", out var rowId))
            {
                Console.WriteLine("Getting levels from api...");

                var request = new GraphQLRequest
                {
                    Query = @"
                   query GetLevels {
                      levels {
                        nodes {
                          id
                          songHash
                          difficulty
                        }
                      }
                    }
                    ",
                    OperationName = "GetLevels"
                };

                var response = await _client.SendQueryAsync<dynamic>(request);

                Console.WriteLine("Got levels!");

                foreach (var level in response.Data.levels.nodes)
                {
                    Console.WriteLine($"Adding {level.songHash}:{level.difficulty} to map with key {level.id}...");

                    _levelMap[$"{level.songHash}:{level.difficulty}"] = level.id;
                }

                Console.WriteLine($"Found key {_levelMap[$"{levelId}:{_difficultyMap[difficulty]}"]} in resopnse!");

                return _levelMap[$"{levelId}:{_difficultyMap[difficulty]}"];
            }

            Console.WriteLine($"Found key {rowId} in map!");

            return rowId;
        }

        public static async Task<int> CheckRemainingAttempts(string userId, string levelId, int difficulty)
        {
            await RefreshTokenIfExpired(userId);

            var levelKey = await GetLevelKey(levelId, difficulty);

            Console.WriteLine($"Checking remaining attempts for {levelKey}");

            var request = new GraphQLRequest
            {
                Query = @"
                   query AttemptsRemaining($levelId: Int!) {
                     level(id: $levelId) {
                       remainingAttempts
                     }
                   }",
                OperationName = "AttemptsRemaining",
                Variables = new
                {
                    levelId = levelKey
                }
            };

            Console.WriteLine($"CheckRemainingAttempts - SendQueryAsync");

            var response = (await _client.SendQueryAsync<dynamic>(request));

            Console.WriteLine($"CheckRemainingAttempts - SendQueryAsync response: {response.Data}");

            foreach (var error in response.Errors)
            {
                Console.WriteLine($"CheckRemainingAttempts - SendQueryAsync error: {error.Message}");
            }

            Console.WriteLine($"Remaining attempts for {levelKey}: {response.Data.level.remainingAttempts}");

            return response.Data.level.remainingAttempts;
        }

        public static async Task CreateScore(string userId, string levelId, int difficulty)
        {
            await RefreshTokenIfExpired(userId);

            var levelKey = await GetLevelKey(levelId, difficulty);

            Console.WriteLine($"Starting song for level {levelKey}");

            var request = new GraphQLRequest
            {
                Query = @"
                    mutation CreateScore($levelId: Int!) {
                      createQualifierScore(input: { qualifierScore: { levelId: $levelId } }) {
                        qualifierScore {
                          id
                        }
                      }
                    }",
                OperationName = "CreateScore",
                Variables = new
                {
                    levelId = levelKey
                }
            };

            Console.WriteLine($"CreateScore - SendMutationAsync");

            var response = await _client.SendMutationAsync<dynamic>(request);

            Console.WriteLine($"CreateScore - SendMutationAsync response: {response.Data}");

            foreach (var error in response.Errors)
            {
                Console.WriteLine($"CreateScore - SendMutationAsync error: {error.Message}");
            }

            _createdQualifierScoreId = response.Data.createQualifierScore.qualifierScore.id;

            Console.WriteLine($"Started attempt!");
        }

        public static async Task SubmitScore(string userId, long score)
        {
            await RefreshTokenIfExpired(userId);

            var path = Path.Combine(Path.GetFullPath(
                        Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "..", "..")), "TA_Helper.exe");

            Console.WriteLine(path);

            if (File.Exists(path))
            {
                Console.WriteLine("TA Fucky wucky we go into uwu.exe");
                var proc = System.Diagnostics.Process.Start(path, $"{_token} {_createdQualifierScoreId} {score}");
            }
            else
            {
                var request = new GraphQLRequest
                {
                    Query = @"
                    mutation SubmitScore($scoreId: Int!, $score: Int!) {
                      updateQualifierScore(input: { id: $scoreId, patch: { score: $score } }) {
                        qualifierScore {
                          id
                          score
                          timeSet
                        }
                      }
                    }",
<<<<<<< Updated upstream
                OperationName = "SubmitScore",
                Variables = new
                {
                    scoreId = _createdQualifierScoreId,
                    score
                }
            };

            Console.WriteLine($"SubmitScore - SendMutationAsync");

            var response = await _client.SendMutationAsync<dynamic>(request);

            Console.WriteLine($"SubmitScore - SendMutationAsync response: {response.Data}");

            foreach (var error in response.Errors)
            {
                Console.WriteLine($"SubmitScore - SendMutationAsync error: {error.Message}");
            }
=======
                    OperationName = "SubmitScore",
                    Variables = new
                    {
                        scoreId = _createdQualifierScoreId,
                        score = score
                    }
                };
>>>>>>> Stashed changes

                await _client.SendMutationAsync<dynamic>(request);
            }
            
            _createdQualifierScoreId = -1;
        }
    }
}
