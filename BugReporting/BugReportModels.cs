using System;
using System.Collections.Generic;

namespace CUCoreLib.BugReporting
{
    internal sealed class BugReportRequest
    {
        public string Description;
        public bool IncludeScreenshot;
        public BugReportSeverity Severity;
    }

    internal sealed class BugReportPackage
    {
        public string ReportId;
        public DateTime CreatedUtc;
        public string Description;
        public BugReportSeverity Severity;
        public string GameVersion;
        public string OperatingSystem;
        public string SceneName;
        public bool IsWorldActive;
        public byte[] Screenshot;
        public string ScreenshotNote;
        public readonly List<BugReportAttachment> Attachments = new List<BugReportAttachment>();
    }

    internal sealed class BugReportAttachment
    {
        public BugReportAttachment(string fileName, string contentType, byte[] data)
        {
            FileName = fileName;
            ContentType = contentType;
            Data = data;
        }

        public string FileName { get; }
        public string ContentType { get; }
        public byte[] Data { get; }
    }

    internal enum BugReportSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    internal sealed class BugReportSendResult
    {
        public bool Success;
        public string Error;
        public float RetryAfterSeconds;
    }
}
