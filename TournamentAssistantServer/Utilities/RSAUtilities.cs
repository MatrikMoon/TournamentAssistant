using System;
using System.IO;
using System.Security.Cryptography;

namespace TournamentAssistantServer.Utilities
{
    internal class RSAUtilities
    {
        public static RSA CreateRsaFromPublicKeyPem(string pemFilePath)
        {
            var pem = File.ReadAllText(pemFilePath);

            // Remove header and footer
            var publicKey = pem.Replace("-----BEGIN PUBLIC KEY-----", "")
                               .Replace("-----END PUBLIC KEY-----", "")
                               .Replace("\n", "")
                               .Replace("\r", "")
                               .Trim();

            // Decode Base64 content
            var keyBytes = Convert.FromBase64String(publicKey);

            // Parse the key bytes into an RSAParameters structure
            var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(keyBytes, out var bytesRead);

            return rsa;
        }
    }
}
