using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using System.Text;
using System.Security.Cryptography;
using Azure.Storage.Blobs.Specialized;

namespace GetPdf
{
    public static class GetPdf
    {
        private static readonly string BLOB_STORAGE_CONNECTION_STRING = Environment.GetEnvironmentVariable("AzureWebJobsStorage");

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

        [FunctionName("GetPdf")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "Faktura/{userId}/{documentName}/{key}")] HttpRequest req,
            string userId,
            string documentName,
            string key,
            ILogger log)
        {
            try
            {
                if (key != CreateHash(userId, documentName, GetSecurityKey(GetUsersMagicNumber(userId))))
                    return new HttpResponseMessage(HttpStatusCode.Forbidden);

                var blobServiceClient = new BlobServiceClient(BLOB_STORAGE_CONNECTION_STRING);
                var blobContainterClient = blobServiceClient.GetBlobContainerClient("pdf");
                var blobObj = blobContainterClient.GetBlobBaseClient(documentName);


                if (!blobObj.Exists())
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);

                var existingBlobObj = blobContainterClient.GetBlobBaseClient(documentName);
                BlobDownloadInfo download = await existingBlobObj.DownloadAsync();
                MemoryStream mystream = new MemoryStream();
                download.Content.CopyTo(mystream);

                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new ByteArrayContent(mystream.ToArray());
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                {
                    FileName = "faktura.pdf"
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                return response;
            }
            catch (Exception e)
            {
                log.LogInformation(e.Message);
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

        }
    }
}
