using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace ContentHubLargeFileUpload
{
    internal class LargeUploadResponse
    {
        [OpenApiProperty(Description = "Status of file upload")]
        public bool Success { get; set; }
        [OpenApiProperty(Description = "File upload response message")]
        public string Message { get; set; }
        [OpenApiProperty(Description = "Sitecore Content Hub asset id")]
        public long Asset_id { get; set; }
        [OpenApiProperty(Description = "Sitecore Content Hub asset identifier")]
        public string Asset_identifier { get; set; }
    }
}
