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
using ContentHubLargeFileUpload.Validation;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;

namespace ContentHubLargeFileUpload
{
    public static class LargeFileUpload
    {
        const string MIME_TYPE = "application/json";
        const string TOKEN_HEADER = "X-Auth-Token";
        const string DEFAULT_UPLOAD_CONFIGURATION = "AssetUploadConfiguration";
        const string FINALIZE_UPLOAD_ROUTE = "api/v2.0/upload/finalize";
        const string UPLOAD_ROUTE = "api/v2.0/upload";

        [FunctionName("LargeFileUpload")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "name" })]
        [OpenApiRequestBody("application/json", typeof(LargeUploadRequest))]
        [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(LargeUploadResponse))]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req, ILogger log)
        {
            HttpClient client = new HttpClient();

            try
            {
                log.LogInformation("LargeFileUpload HTTP trigger function recieved a request.");

                string jsonContent = await new StreamReader(req.Body).ReadToEndAsync();
                LargeUploadRequest data = JsonConvert.DeserializeObject<LargeUploadRequest>(jsonContent);

                var mv = new ModelValidator<LargeUploadRequest>();
                var valid = mv.ValidateModel(data);

                if (!valid.Valid)
                {
                    var invalidMsg = valid.Errors.ElementAt(0).ErrorMessage;
                    log.LogError($"Bad request: {invalidMsg}");
                    return new BadRequestObjectResult(new LargeUploadResponse { Success = false, Message = invalidMsg });
                }

                client.BaseAddress = new Uri(data.ContentHubHostName);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MIME_TYPE));
                client.DefaultRequestHeaders.Add(TOKEN_HEADER, data.ContentHubToken);

                Dictionary<int, byte[]> fileChunks = GetFileChunks(data.FileContent, log);

                var upload = await GetUploadUrl(data.ContentHubHostName, data.Filename, data.FileSize, data.ContentHubToken, data.UploadConfiguration, log);

                foreach (var fileChunk in fileChunks.OrderBy(o => o.Key))
                {
                    var uploadUrl = $"{upload.Key}&chunks={fileChunks.Count}&chunk={fileChunk.Key}";

                    MultipartFormDataContent multipartContent = ConstructMultipartData(data, fileChunk);

                    var resp = await client.PostAsync(uploadUrl, multipartContent);
                    if (!resp.IsSuccessStatusCode)
                    {
                        var msg = await resp.Content.ReadAsStringAsync();
                        log.LogError($"Bad request: {msg}");

                        return new BadRequestObjectResult(new LargeUploadResponse { Success = false, Message = msg });
                    }
                }

                await client.PostAsync($"{upload.Key}&chunks={fileChunks.Count}", null);

                var finalResp = await FinalizeUpload(data.ContentHubHostName, upload.Value, data.ContentHubToken, log);

                if (finalResp.Status == (int)HttpStatusCode.OK || finalResp.Status == (int)HttpStatusCode.Created || finalResp.Status == (int)HttpStatusCode.NoContent)
                {
                    log.LogInformation("Function processing complete.");
                    return new OkObjectResult(finalResp.Response);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"Exception: {ex.Message}");
                return new UnprocessableEntityObjectResult(new LargeUploadResponse { Success = false, Message = ex.Message });
            }

            log.LogInformation("Function processing complete. No content.");
            return new NoContentResult();
        }

        /// <summary>
        /// Constructs multipart form-data content from file chunk
        /// </summary>
        /// <param name="data">Incoming request object</param>
        /// <param name="fileChunk">file chunk data</param>
        /// <returns>MultipartFormDataContent</returns>
        static MultipartFormDataContent ConstructMultipartData(LargeUploadRequest data, KeyValuePair<int, byte[]> fileChunk)
        {
            MultipartFormDataContent multipartContent = new MultipartFormDataContent();
            var byteArrayContent = new ByteArrayContent(fileChunk.Value);
            byteArrayContent.Headers.ContentType = new MediaTypeHeaderValue(data.MediaType);
            byteArrayContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
            {
                FileName = data.Filename,
                Name = "file",
            };
            multipartContent.Add(byteArrayContent);
            return multipartContent;
        }

        /// <summary>
        /// Divides file into 1000000 byte chunks
        /// https://docs.stylelabs.com/contenthub/4.1.x/content/integrations/rest-api/upload/upload-large-files.html
        /// </summary>
        /// <param name="fileData">file to chunk</param>
        /// <returns>Dictionary<int, byte[]></returns>
        static Dictionary<int, byte[]> GetFileChunks(string fileData, ILogger log)
        {
            log.LogInformation("Chunking file data...");
            var base64EncodedBytes = System.Convert.FromBase64String(fileData);

            int chunkCounter = 0;
            Dictionary<int, byte[]> fileChunks = new Dictionary<int, byte[]>();
            foreach (var chunk in base64EncodedBytes.ToChunks(1000000)) //(1000000)) approx. 1 Megabyte chunks per https://docs.stylelabs.com/contenthub/4.1.x/content/integrations/rest-api/upload/upload-large-files.html
            {
                fileChunks.Add(chunkCounter, chunk.ToArray()); ;
                chunkCounter++;
            }

            log.LogInformation($"File chunking completed: {chunkCounter++} total");
            return fileChunks;
        }

        /// <summary>
        /// Gets upload URL from Content Hub
        /// https://docs.stylelabs.com/contenthub/4.1.x/content/integrations/rest-api/upload/upload-api-v2.html#request-an-upload
        /// </summary>
        /// <param name="hostUrl">Content Hub instance URL</param>
        /// <param name="fileName">Filename with extension</param>
        /// <param name="fileSize">File size</param>
        /// <param name="token">Content Hub access token</param>
        /// <param name="uploadConfiguration">Content Hub upload configuration</param>
        /// <param name="log">Logger</param>
        /// <returns>KeyValuePair<string, HttpContent></returns>
        static async Task<KeyValuePair<string, HttpContent>> GetUploadUrl(string hostUrl, string fileName, long fileSize, string token, string uploadConfiguration, ILogger log)
        {
            log.LogInformation($"Getting upload url from Content Hub");

            try
            {
                HttpClient client = new HttpClient();
                client.BaseAddress = new Uri(hostUrl);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MIME_TYPE));
                client.DefaultRequestHeaders.Add(TOKEN_HEADER, token);

                var payload = new
                {
                    action = new
                    {
                        name = "NewAsset",
                        parameters = new { }
                    },
                    file_name = fileName,
                    file_size = fileSize.ToString(),
                    upload_configuration = new
                    {
                        name = String.IsNullOrEmpty(uploadConfiguration) ? DEFAULT_UPLOAD_CONFIGURATION : uploadConfiguration,
                        parameters = new { }
                    }
                };

                var jsonPayload = JsonConvert.SerializeObject(payload);

                var resp = await client.PostAsync(UPLOAD_ROUTE, new StringContent(jsonPayload, Encoding.UTF8, MIME_TYPE));
                
                log.LogInformation($"Getting upload url response: {resp.StatusCode}");

                return resp.IsSuccessStatusCode
                    ? new KeyValuePair<string, HttpContent>(resp.Headers.GetValues("Location").FirstOrDefault(), resp.Content)
                    : new KeyValuePair<string, HttpContent>(String.Empty, null);
            }
            catch (Exception ex)
            {
                log.LogInformation($"Error getting upload url:\n {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Finalizes file upload.
        /// https://docs.stylelabs.com/contenthub/4.1.x/content/integrations/rest-api/upload/upload-api-v2.html#finalize-the-upload
        /// </summary>
        /// <param name="hostUrl">Content Hub instance URL</param>
        /// <param name="httpContent">Request payload</param>
        /// <param name="token">Content Hub access token</param>
        /// <param name="log">Logger</param>
        /// <returns>(int Status, LargeUploadResponse Response)</returns>
        static async Task<(int Status, LargeUploadResponse Response)> FinalizeUpload(string hostUrl, HttpContent httpContent, string token, ILogger log)
        {
            try
            {
                LargeUploadResponse largeUploadResponse = new LargeUploadResponse();
                HttpClient client = new HttpClient();
                client.BaseAddress = new Uri(hostUrl);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MIME_TYPE));
                client.DefaultRequestHeaders.Add(TOKEN_HEADER, token);

                log.LogInformation($"Finalizing upload.");

                var resp = await client.PostAsync(FINALIZE_UPLOAD_ROUTE, httpContent);

                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    largeUploadResponse = JsonConvert.DeserializeObject<LargeUploadResponse>(json);

                    log.LogInformation($"Finalized upload.");
                    return ((int)resp.StatusCode, largeUploadResponse);
                }

                
                log.LogInformation($"Finalize upload response: null");
                return ((int)resp.StatusCode, largeUploadResponse);
            }
            catch (Exception ex)
            {
                log.LogInformation($"Error finalizing upload:\n {ex.Message}");
                throw;
            }
        }
    }
}
