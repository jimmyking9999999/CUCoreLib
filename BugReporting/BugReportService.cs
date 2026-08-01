using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CUCoreLib.Helpers;
using UnityEngine;

namespace CUCoreLib.BugReporting
{
    internal static class BugReportService
    {
        private const int CooldownSeconds = 60;
        private static readonly string[] BuiltInWebhookParts =
        {
            /* Hey! This is open source, so anyone can realistically take this webhook link and 
            * delete it or send whatever in it.
            *
            * I trust the modding community enough though, I would hate to have to obfuscate it more
            * or host it on google bucket or my own server.
            *
            * Thanks in advance ^^
            */
            "https://ptb.dis",
            "cord.com/api/web",
            "hooks/1531032922969346299/",
            "M2yjvU7X7otXredLGQbnzSdJZIi7-",
            "ksEMKXgBjSVlRFHtjWFf21GvM5SF_8MkqDr1kTA"
        };

        private const int MaxTotalUploadBytes = 12 * 1024 * 1024; // 12mb
        private const int MaxScreenshotBytes = 5 * 1024 * 1024; // 5mb

        private static bool _sending;
        private static float _lastStartedAt = float.NegativeInfinity;

        internal static void RunCommand(string[] args)
        {
            if (!TryParseCommand(args, out var reportRequest, out var error))
                throw new Exception(error);

          
            if (_sending)
                throw new Exception("A bug report is already being sent.");

            var remaining = CooldownSeconds - (Time.realtimeSinceStartup - _lastStartedAt);
            if (remaining > 0f)
                throw new Exception("Please wait " + Mathf.CeilToInt(remaining) +
                                    " seconds before sending another bug report.");

            _sending = true;
            _lastStartedAt = Time.realtimeSinceStartup;
            WriteConsole("Sending! Thanks for contributing!"); // Cheers~
            CUCoreUtils.StartCoroutine(SendRoutine(reportRequest));
        }

        internal static bool TryParseCommand(string[] args, out BugReportRequest request, out string error)
        {
            request = null;
            error = null;
            if (args == null || args.Length == 0)
            {
                error = GetUsage();
                return false;
            }

            var description = "-";
            var nextArgument = 1;
            if (args.Length > 1 &&
                !TryReadDescription(args, out description, out nextArgument, out error))
                return false;

            var screenshot = false;
            if (args.Length > nextArgument && !bool.TryParse(args[nextArgument], out screenshot))
            {
                error = "Screenshot must be 'true' or 'false'.";
                return false;
            }

            var severity = BugReportSeverity.Medium;
            if (args.Length > nextArgument + 1 && !TryParseSeverity(args[nextArgument + 1], out severity))
            {
                error = "Severity must be low, medium, high, or critical.";
                return false;
            }

            if (args.Length > nextArgument + 2)
            {
                error = GetUsage();
                return false;
            }

            request = new BugReportRequest
            {
                Description = description,
                IncludeScreenshot = screenshot,
                Severity = severity
            };
            return true;
        }

        private static bool TryReadDescription(string[] args, out string description, out int nextArgument,
            out string error)
        {
            description = args[1];
            nextArgument = 2;
            error = null;

            if (!description.StartsWith("\"", StringComparison.Ordinal)) return true;

            var parts = new List<string>();
            for (var index = 1; index < args.Length; index++)
            {
                var part = args[index];
                if (index == 1) part = part.Substring(1);

                var closesQuote = part.EndsWith("\"", StringComparison.Ordinal);
                if (closesQuote) part = part.Substring(0, part.Length - 1);
                parts.Add(part);

                if (!closesQuote) continue;

                description = string.Join(" ", parts);
                nextArgument = index + 1;
                return true;
            }

            error = "The bug report description has an opening quote but no closing quote. " + GetUsage();
            return false;
        }

        private static string GetUsage()
        {
            return "Usage: bug-report [\"description text\"] [bool screenshot] [severity]";
        }

        private static IEnumerator SendRoutine(BugReportRequest request)
        {
            BugReportPackage report = null;
            try
            {
                report = BugReportCollector.Collect(request);
                if (request.IncludeScreenshot)
                {
                    yield return new WaitForEndOfFrame();
                    CaptureScreenshot(report);
                }

                EnforceTotalSizeLimit(report);

                var destination = new DiscordWebhookDestination(string.Concat(BuiltInWebhookParts));
                BugReportSendResult result = null;
                yield return destination.Send(report, value => result = value);

                if (result != null && !result.Success && result.RetryAfterSeconds > 0f)
                {
                    yield return new WaitForSecondsRealtime(Math.Min(result.RetryAfterSeconds, 30f));
                    result = null;
                    yield return destination.Send(report, value => result = value);
                }

                if (result?.Success == true)
                {
                    var message = "Bug report sent successfully. ID: " + report.ReportId;
                    CUCoreLibPlugin.Log?.LogInfo(message);
                    WriteConsole("<color=#66FF66>" + message + "</color>");
                }
                else
                {
                    var message = result?.Error ?? "The bug report did not send for some reason! Uh-oh.";
                    CUCoreLibPlugin.Log?.LogWarning("Bug report " + report.ReportId + " failed: " + message);
                    WriteConsole("<color=orange>Bug report failed: " + message + "</color>");
                }
            }
            finally
            {
                _sending = false;
            }
        }

        private static void CaptureScreenshot(BugReportPackage report)
        {
            Texture2D texture = null;
            try
            {
                texture = ScreenCapture.CaptureScreenshotAsTexture();
                if (texture == null)
                {
                    report.ScreenshotNote = "Capture failed";
                    return;
                }

                foreach (var quality in new[] { 85, 70, 55, 40 })
                {
                    var encoded = texture.EncodeToJPG(quality);
                    if (encoded == null || encoded.Length > MaxScreenshotBytes) continue;
                    report.Screenshot = encoded;
                    report.ScreenshotNote = "Attached";
                    return;
                }

                report.ScreenshotNote = "Omitted because someone's playing CU on a 16K TV";
            }
            catch (Exception ex)
            {
                report.ScreenshotNote = "Capture failed: " + ex.Message;
                CUCoreLibPlugin.Log?.LogWarning("Could not capture a bug report screenshot: " + ex.Message);
            }
            finally
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
            }
        }

        private static void EnforceTotalSizeLimit(BugReportPackage report)
        {
            var total = report.Attachments.Sum(attachment => attachment?.Data?.Length ?? 0) +
                        (report.Screenshot?.Length ?? 0);
            if (total <= MaxTotalUploadBytes) return;

            report.Screenshot = null;
            report.ScreenshotNote = "Omitted because the total report was too large (!)";
        }

        private static bool TryParseSeverity(string value, out BugReportSeverity severity)
        {
            return Enum.TryParse((value ?? string.Empty).Trim(), true, out severity) &&
                   Enum.IsDefined(typeof(BugReportSeverity), severity);
        }

        private static void WriteConsole(string message)
        {
            if (ConsoleScript.instance != null) CUCoreUtils.ConsoleLog(ConsoleScript.instance, message);
        }
    }
}
