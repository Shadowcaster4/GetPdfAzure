using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using System.Security.Cryptography;

namespace GetPdf
{
    public static class GetSecKey
    {
        private static string HashMyString(string stringToHash)
        {
            var data = Encoding.ASCII.GetBytes(stringToHash);
            var hashData = new SHA1Managed().ComputeHash(data);
            var myHash = string.Empty;
            foreach (var b in hashData)
            {
                myHash += b.ToString("X2");
            }
            return myHash;
        }

        private static int GetUsersMagicNumber(string userName)
        {
            string userHash = HashMyString(userName);
            return (int)userHash[1] + (int)userHash[16] + 5;
        }

        private static string GetSecurityKey(int magicNumber)
        {
            return HashMyString(Math.Ceiling((DateTime.UtcNow.Day * magicNumber + DateTime.UtcNow.Hour + 1 / DateTime.UtcNow.Day + DateTime.UtcNow.Hour * 10.5f)).ToString());
        }
        private static string CreateHash(string userName, string document, string securityKey)
        {
            return HashMyString(document + securityKey + HashMyString(userName));
        }


        [FunctionName("GetSecKey")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "Faktura/{userId}/{documentName}")] HttpRequest req,
            string userId,
            string documentName)
        {            
            string responseMessage = $"Your seckey to users: {userId} file:{documentName} is {CreateHash(userId, documentName, GetSecurityKey(GetUsersMagicNumber(userId)))} ";

            return new OkObjectResult(responseMessage);
        }
    }
}
