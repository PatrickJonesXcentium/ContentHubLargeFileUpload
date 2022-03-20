using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ContentHubLargeFileUpload
{
    internal class LargeUploadRequest
    {
        [Required]
        [OpenApiProperty(Description = "File name with extension")]
        public string Filename { get; set; }
        [Required]
        [OpenApiProperty(Description = "Media/Mime type of file")]
        public string MediaType { get; set; }
        [Required]
        [Range(1, long.MaxValue)]
        [OpenApiProperty(Description = "File size in bytes")]
        public long FileSize { get; set; }
        [Required]
        [OpenApiProperty(Description = "Base64 encoded file contents")]
        public string FileContent { get; set; }
        [Required]
        [OpenApiProperty(Description = "Sitecore Content Hub instance URL host name. \n i.e. https://example.com")]
        public string ContentHubHostName { get; set; }
        [Required]
        [OpenApiProperty(Description = "Sitecore Content Hub access token")]
        public string ContentHubToken { get; set; }
        [OpenApiProperty(Description = "Sitecore Content Hub upload configuration. Default is AssetUploadConfiguration \n https://docs.stylelabs.com/contenthub/4.1.x/content/integrations/rest-api/upload/upload-api-v2.html#upload-configurations")]
        public string UploadConfiguration { get; set; }
    }
}
