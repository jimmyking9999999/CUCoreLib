using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace CUCoreLib.BugReporting
{
    internal sealed class DiscordWebhookDestination
    {
        private const int RequestTimeoutSeconds = 30;
        // Surely...
        private readonly string _webhookUrl;

        internal DiscordWebhookDestination(string webhookUrl)
        {
            _webhookUrl = webhookUrl;
        }

        public IEnumerator Send(BugReportPackage report, Action<BugReportSendResult> completed)
        {
            var sections = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("payload_json", BuildPayload(report))
            };

            foreach (var attachment in report.Attachments)
            {
                if (attachment?.Data == null || attachment.Data.Length == 0) continue;
                sections.Add(new MultipartFormFileSection(
                    "files[" + (sections.Count - 1) + "]",
                    attachment.Data,
                    attachment.FileName,
                    attachment.ContentType));
            }

            if (report.Screenshot != null && report.Screenshot.Length > 0)
            {
                sections.Add(new MultipartFormFileSection(
                    "files[" + (sections.Count - 1) + "]",
                    report.Screenshot,
                    "screenshot.jpg",
                    "image/jpeg"));
            }

            var url = _webhookUrl.IndexOf('?') >= 0
                ? _webhookUrl + "&wait=true"
                : _webhookUrl + "?wait=true";

            using (var request = UnityWebRequest.Post(url, sections))
            {
                request.timeout = RequestTimeoutSeconds;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    completed(new BugReportSendResult { Success = true });
                    yield break;
                }

                completed(new BugReportSendResult
                {
                    Success = false,
                    Error = "Please check your internet connection! | HTTP " + request.responseCode + ": " + request.error,
                    RetryAfterSeconds = request.responseCode == 429
                        ? ReadRetryAfterSeconds(request.downloadHandler?.text)
                        : request.responseCode >= 500 ? 1f : 0f
                });
            }
        }

        private static string BuildPayload(BugReportPackage report)
        {
            var embed = new JObject
            {
                ["title"] = "CUCoreLib bug report (" + report.ReportId + ')',
                ["description"] = string.IsNullOrWhiteSpace(report.Description) ||
                                  string.Equals(report.Description, "-", StringComparison.Ordinal)
                    ? "No description :("
                    : SanitizeText(FormatDescription(report.Description), 4096),
                ["color"] = GetSeverityColor(report.Severity),
                ["timestamp"] = report.CreatedUtc.ToString("o"),
                ["fields"] = new JArray
                {
                    Field("Severity", report.Severity.ToString(), true),
                    Field("CUCoreLib", CUCoreLibPlugin.VERSION, true),
                    Field("Game", string.IsNullOrWhiteSpace(report.GameVersion) ? "unknown" : report.GameVersion, true),
                    //Field("Scene", string.IsNullOrWhiteSpace(report.SceneName) ? "unknown" : report.SceneName, true),
                    //Field("World active", report.IsWorldActive ? "Yes" : "No", true),
                    Field("Operating system",
                        string.IsNullOrWhiteSpace(report.OperatingSystem) ? "unknown" : report.OperatingSystem, false),
                    //Field("Screenshot", string.IsNullOrWhiteSpace(report.ScreenshotNote)
                    //    ? (report.Screenshot != null ? "Attached" : "Not requested")
                    //    : report.ScreenshotNote, false)
                }
            };

            var payload = new JObject
            {
                ["username"] = "Bug Reports",
                ["embeds"] = new JArray { embed },
                ["allowed_mentions"] = new JObject
                {
                    ["parse"] = new JArray()
                }
            };
            // I better not have to update the google bucket for this
            return payload.ToString();
        }

        private static JObject Field(string name, string value, bool inline)
        {
            return new JObject
            {
                ["name"] = name,
                ["value"] = SanitizeText(value, 1024),
                ["inline"] = inline
            };
        }

        private static string SanitizeText(string value, int maximumLength)
        {
            var sanitized = (value ?? string.Empty).Replace("@", "@\u200B");
            return sanitized.Length <= maximumLength
                ? sanitized
                : sanitized.Substring(0, maximumLength - 1) + "…";
        }

        private static string FormatDescription(string value)
        {
            var words = (value ?? string.Empty)
                .Replace('_', ' ')
                .Replace('-', ' ')
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", words);
        }

        private static int GetSeverityColor(BugReportSeverity severity)
        {
            switch (severity)
            {
                case BugReportSeverity.Low:
                    return 5763719;
                case BugReportSeverity.High:
                    return 15105570;
                case BugReportSeverity.Critical:
                    return 15548997;
                default:
                    return 16776960;
            }
        }

        private static float ReadRetryAfterSeconds(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText)) return 1f;
            try
            {
                return Math.Max(1f, JObject.Parse(responseText).Value<float?>("retry_after") ?? 1f);
            }
            catch
            {
                return 1f;
            }
        }
    }
}
