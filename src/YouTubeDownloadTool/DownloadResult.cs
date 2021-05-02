using System;
using System.Diagnostics.CodeAnalysis;

namespace YouTubeDownloadTool
{
    public readonly struct DownloadResult
    {
        [MemberNotNullWhen(false, nameof(Message))]
        public bool IsSuccess { get; }
        public string? Message { get; }
        public int ExitCode { get; }

        private DownloadResult(bool isSuccess, string? message, int exitCode)
        {
            IsSuccess = isSuccess;
            Message = message;
            ExitCode = exitCode;
        }

        public static DownloadResult Success(string? message)
        {
            return new DownloadResult(isSuccess: true, message, exitCode: 0);
        }

        public static DownloadResult Error(string message, int exitCode)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Error message must be specified.", nameof(message));

            return new DownloadResult(isSuccess: false, message, exitCode);
        }
    }
}
