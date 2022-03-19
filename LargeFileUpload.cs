using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ReneWiersma.Chunking;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;

namespace ContentHubLargeFileUpload
{
    public static class LargeFileUpload
    {
        const string _mimeType = "application/json";

        [FunctionName("LargeFileUpload")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            HttpClient client = new HttpClient();

            try
            {
                log.LogInformation("C# HTTP trigger function processed a request.");

                string jsonContent = await new StreamReader(req.Body).ReadToEndAsync();
                FileMetaData data = JsonConvert.DeserializeObject<FileMetaData>(jsonContent);

                if (string.IsNullOrEmpty(data.FileContent))
                {
                    return new BadRequestObjectResult("File data missing");
                }
                string fileName = data.Filename;
                string fileData = data.FileContent;
                //string val = fileData.ToObject<string>();
                client.BaseAddress = new Uri(data.ContentHubHostName);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(_mimeType));
                client.DefaultRequestHeaders.Add("X-Auth-Toklen", data.ContentHubToken);

                var base64EncodedBytes = System.Convert.FromBase64String(fileData);
                var result = System.Text.Encoding.UTF8.GetString(base64EncodedBytes);

                int chunkCounter = 0;
                Dictionary<int, byte[]> fileChunks = new Dictionary<int, byte[]>();
                foreach (var chunk in base64EncodedBytes.ToChunks(1000))
                {
                    fileChunks.Add(chunkCounter, chunk.ToArray()); ;
                    chunkCounter++;
                }

                var upload = await GetUploadUrl(data.ContentHubHostName, data.Filename, data.FileSize, data.ContentHubToken);
                foreach (var fileChunk in fileChunks.OrderBy(o => o.Key))
                {
                    //var uploadUrl = fileChunk.Key == 0 
                    //    ? $"{data.ContentHubUploadUrl}&chunks={fileChunks.Count}" 
                    //    : $"{data.ContentHubUploadUrl}&chunks={fileChunks.Count}&chunk={fileChunk.Key}";

                    var uploadUrl = fileChunk.Key == 0
                        ? $"{upload.Key}&chunks={fileChunks.Count}"
                        : $"{upload.Key}&chunks={fileChunks.Count}&chunk={fileChunk.Key}";


                    var resp = await client.PostAsync(uploadUrl, new ByteArrayContent(fileChunk.Value));

                    if (!resp.IsSuccessStatusCode)
                    {
                        return new BadRequestObjectResult(await resp.Content.ReadAsStringAsync());

                    }
                }

                await FinalizeUpload(data.ContentHubHostName, upload.Value, data.ContentHubToken);
                // log.Info($"file data val : "+result);
                //StringReader stringReader = new StringReader(result);
                //rest you can do yourself
            }
            catch (Exception ex)
            {
                return new StatusCodeResult((int)HttpStatusCode.InternalServerError);
            }

            //log.LogInformation("C# HTTP trigger function processed a request.");

            //string name = req.Query["name"];

            //string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            //dynamic data = JsonConvert.DeserializeObject(requestBody);
            //name = name ?? data?.name;

            string responseMessage = "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response.";
            return new OkObjectResult(responseMessage);
        }

        static async Task<KeyValuePair<string, HttpContent>> GetUploadUrl(string hostUrl, string fileName, long fileSize, string token)
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(hostUrl);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(_mimeType));
            client.DefaultRequestHeaders.Add("Content-Type", _mimeType);
            client.DefaultRequestHeaders.Add("X-Auth-Toklen", token);


            var payload = new { 
                file_name = fileName, 
                file_size = fileSize, 
                upload_configuration = new { 
                    name = "ApprovedAssetUploadConfiguration", 
                    parameters = new { }
                }
            };

            var resp = await client.PostAsync("api/v2.0/upload", new StringContent(JsonConvert.SerializeObject(payload)));
            return resp.IsSuccessStatusCode 
                ? new KeyValuePair<string, HttpContent>(resp.Headers.GetValues("location").FirstOrDefault(), resp.Content) 
                : new KeyValuePair<string, HttpContent>(String.Empty, null);
        }

        static async Task<int> FinalizeUpload(string hostUrl, HttpContent httpContent, string token)
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(hostUrl);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(_mimeType));
            client.DefaultRequestHeaders.Add("Content-Type", _mimeType);
            client.DefaultRequestHeaders.Add("X-Auth-Toklen", token);

            var resp = await client.PostAsync("api/v2.0/upload/Finalize", httpContent);
            return (int)resp.StatusCode;
        }
    }
}
