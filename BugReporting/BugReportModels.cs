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
        public readonly List<BugReportAttachment> Attachments = new List<BugReportAttachment>();
        public DateTime CreatedUtc;
        public string Description;
        public string GameVersion;
        public bool IsWorldActive;
        public string OperatingSystem;
        public string ReportId;
        public string SceneName;
        public byte[] Screenshot;
        public string ScreenshotNote;
        public BugReportSeverity Severity;
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
        public string Error;
        public float RetryAfterSeconds;
        public bool Success;
    }
}