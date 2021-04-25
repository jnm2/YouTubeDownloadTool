using System;
using System.Collections.Generic;
using System.Text;

namespace YouTubeDownloadTool
{
    public struct DownloadDetails
    {
        public string? Name { get; set; }
        public DateTimeOffset? UploadDate { get; set; }
        public TimeSpan? Duration { get; set; }
        public string? Author { get; set; }
    }
}
