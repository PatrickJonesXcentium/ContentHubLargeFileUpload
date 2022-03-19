using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ContentHubLargeFileUpload
{
    internal class LargeUploadRequest
    {
        [Required]
        public string Filename { get; set; }
        [Required]
        public string MediaType { get; set; }
        [Required]
        [MinLength(1)]
        public long FileSize { get; set; }
        [Required]
        public string FileContent { get; set; }
        [Required]
        public string ContentHubHostName { get; set; }
        [Required]
        public string ContentHubToken { get; set; }
        public string UploadConfiguration { get; set; }
    }
}
