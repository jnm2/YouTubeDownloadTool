using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace YouTubeDownloadTool
{
    [DebuggerDisplay("{ToString(),nq}")]
    public readonly struct DownloadResult : IEquatable<DownloadResult>
    {
        private readonly string? errorMessage;
        private readonly int exitCode;

        private DownloadResult(string? errorMessage, int exitCode)
        {
            this.errorMessage = errorMessage;
            this.exitCode = exitCode;
        }

        public static DownloadResult Success { get; }

        public static DownloadResult Error(string message, int exitCode)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Error message must be specified.", nameof(message));

            return new DownloadResult(message, exitCode);
        }

        public bool IsSuccess => errorMessage is null;

        public bool IsError([NotNullWhen(true)] out string? message, out int exitCode)
        {
            message = errorMessage;
            exitCode = this.exitCode;
            return message is { };
        }

        public override string ToString()
        {
            return errorMessage is { }
                ? $"Error({errorMessage}, exit code {exitCode})"
                : "Success";
        }

        public override bool Equals(object? obj)
        {
            return obj is DownloadResult result && Equals(result);
        }

        public bool Equals([AllowNull] DownloadResult other)
        {
            return errorMessage == other.errorMessage
                && exitCode == other.exitCode;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(errorMessage, exitCode);
        }

        public static bool operator ==(DownloadResult left, DownloadResult right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DownloadResult left, DownloadResult right)
        {
            return !(left == right);
        }
    }
}
