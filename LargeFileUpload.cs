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
using System.Text;

namespace ContentHubLargeFileUpload
{
    public static class LargeFileUpload
    {
        const string _mimeType = "application/json";
        const string TOKEN_HEADER = "X-Auth-Token";

        [FunctionName("LargeFileUpload")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req, ILogger log)
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
                client.DefaultRequestHeaders.Add(TOKEN_HEADER, data.ContentHubToken);

                var base64EncodedBytes = System.Convert.FromBase64String(fileData);
                var result = System.Text.Encoding.UTF8.GetString(base64EncodedBytes);

                int chunkCounter = 0;
                Dictionary<int, byte[]> fileChunks = new Dictionary<int, byte[]>();
                foreach (var chunk in base64EncodedBytes.ToChunks(2 * 1024))
                {
                    fileChunks.Add(chunkCounter, chunk.ToArray()); ;
                    chunkCounter++;
                }

                var upload = await GetUploadUrl(data.ContentHubHostName, data.Filename, data.FileSize, data.ContentHubToken);
                
                foreach (var fileChunk in fileChunks.OrderBy(o => o.Key))
                {
                    using (var ms = new MemoryStream())
                    {
                        ms.Write(fileChunk.Value);

                        //var uploadUrl = $"{data.ContentHubUploadUrl}&chunks={fileChunks.Count}&chunk={fileChunk.Key}";
                        var uploadUrl = $"{upload.Key}&chunks={fileChunks.Count}&chunk={fileChunk.Key}";

                        //var resp = await client.PostAsync(uploadUrl, new StreamContent(ms));

                        var multipartContent = new MultipartFormDataContent();
                        var byteArrayContent = new ByteArrayContent(fileChunk.Value);
                        byteArrayContent.Headers.ContentType = new MediaTypeHeaderValue(data.MediaType);
                        byteArrayContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                        {
                            FileName = data.Filename,
                            Name = "file",
                        };
                        multipartContent.Add(byteArrayContent);
                        var resp = await client.PostAsync(uploadUrl, multipartContent);
                        if (!resp.IsSuccessStatusCode)
                        {
                            return new BadRequestObjectResult(await resp.Content.ReadAsStringAsync());

                        }
                    }
                }

                await client.PostAsync($"{upload.Key}&chunks={fileChunks.Count}", null);
                var finalResp = await FinalizeUpload(data.ContentHubHostName, upload.Value, data.ContentHubToken);
            }
            catch (Exception)
            {
                return new StatusCodeResult((int)HttpStatusCode.InternalServerError);
            }

            string responseMessage = "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response.";
            return new OkObjectResult(responseMessage);
        }

        static async Task<KeyValuePair<string, HttpContent>> GetUploadUrl(string hostUrl, string fileName, long fileSize, string token)
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(hostUrl);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(_mimeType));
            client.DefaultRequestHeaders.Add(TOKEN_HEADER, token);

            var payload = new {
                action = new { 
                    name = "NewAsset", 
                    parameters = new { }
                },
                file_name = fileName, 
                file_size = fileSize.ToString(), 
                upload_configuration = new { 
                    name = "ApprovedAssetUploadConfiguration", 
                    parameters = new { }
                }
            };

            var jsonPayload = JsonConvert.SerializeObject(payload);

            var resp = await client.PostAsync("api/v2.0/upload", new StringContent(jsonPayload, Encoding.UTF8, _mimeType));
            return resp.IsSuccessStatusCode 
                ? new KeyValuePair<string, HttpContent>(resp.Headers.GetValues("Location").FirstOrDefault(), resp.Content) 
                : new KeyValuePair<string, HttpContent>(String.Empty, null);
        }

        static async Task<int> FinalizeUpload(string hostUrl, HttpContent httpContent, string token)
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(hostUrl);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(_mimeType));
            client.DefaultRequestHeaders.Add(TOKEN_HEADER, token);

            var resp = await client.PostAsync("api/v2.0/upload/finalize", httpContent);
            var json = await resp.Content.ReadAsStringAsync();
            return (int)resp.StatusCode;
        }
    }
}
