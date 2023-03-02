using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Runtime.ConstrainedExecution;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using TournamentAssistantServer.Database.Contexts;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;

namespace TournamentAssistantServer
{
    class AuthorizationManager
    {
        private UserDatabaseContext _userDatabaseContext;
        private X509Certificate2 _certificate;

        public AuthorizationManager(UserDatabaseContext userDatabaseContext, X509Certificate2 cert)
        {
            _userDatabaseContext = userDatabaseContext;
            _certificate = cert;
        }

        public string GenerateToken(User user)
        {
            if (user.discord_info == null)
            {
                throw new ArgumentException("User must have had their Discord info populated before having a token generated");
            }

            // Create the signing credentials with the certificate
            var signingCredentials = new X509SigningCredentials(_certificate);

            // Create a list of claims for the token payload
            var claims = new[]
            {
                new Claim("sub", user.Guid),
                new Claim("name", user.Name),
                new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
                new Claim("exp", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString()),
                new Claim("ta:discord_id", user.discord_info.UserId),
                new Claim("ta:discord_name", user.discord_info.Username),
                new Claim("ta:discord_avatar", user.discord_info.AvatarUrl),
            };

            // Create the JWT token with the claims and signing credentials
            var token = new JwtSecurityToken(
                issuer: "ta_server",
                audience: "ta_users",
                claims: claims,
                signingCredentials: signingCredentials
            );

            // Create a JWT token handler and serialize the token to a string
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public bool VerifyUser(string token)
        {
            //Empty tokens are definitely not valid
            if (string.IsNullOrWhiteSpace(token)) return false;

            try
            {
                // Create a token validation parameters object with the signing credentials
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = "ta_server",
                    ValidAudience = "ta_users",
                    IssuerSigningKey = new X509SecurityKey(_certificate),
                };

                // Verify the token and extract the claims
                var principal = new JwtSecurityTokenHandler().ValidateToken(token, validationParameters, out var validatedToken);
                var claims = ((JwtSecurityToken)validatedToken).Claims;

                foreach (var claim in claims)
                {
                    Console.WriteLine($"{claim.Type}: {claim.Value}");
                }
            }
            catch (Exception e)
            {
                Logger.Error($"Failed to validate token:");
                Logger.Error(e.Message);
            }

            return true;
        }
    }
}
