using System;
using System.Collections.Generic;
using System.Text;

namespace ContentHubLargeFileUpload
{
    internal class LargeUploadResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public long Asset_id { get; set; }
        public string Asset_identifier { get; set; }
    }
}
