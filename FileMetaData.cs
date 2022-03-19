using System;
using System.Collections.Generic;
using System.Text;

namespace ContentHubLargeFileUpload
{
    internal class FileMetaData
    {
        public string Filename { get; set; }
        public string MediaType { get; set; }
        public long FileSize { get; set; }
        public string FileContent { get; set; }

        public string ContentHubHostName { get; set; }
        public string ContentHubUploadUrl { get; set; }
        public string ContentHubToken { get; set; }
    }
}
