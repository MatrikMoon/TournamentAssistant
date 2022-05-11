using System;
using System.Security.Cryptography;
using System.Text;

namespace TournamentAssistantUI.Misc
{
    public class Hashing
    {
        public static string CreateSha1FromString(string input)
        {
            using (SHA1 sha = SHA1.Create())
            {
                byte[] value = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(value).Replace("-", string.Empty);
            }
        }
    }
}
