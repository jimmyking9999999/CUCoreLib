using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CUCoreLib.BugReporting
{
    internal static class BugReportCollector
    {
        private const int BepInExLogLimitBytes = 1024 * 1024;
        private const int ConsoleLogLimitBytes = 256 * 1024;
        private static readonly Regex TmpTagPattern = new Regex("<[^>]+>", RegexOptions.Compiled);

        internal static BugReportPackage Collect(BugReportRequest request)
        {
            var report = new BugReportPackage
            {
                ReportId = Guid.NewGuid().ToString("N").Substring(0, 12),
                CreatedUtc = DateTime.UtcNow,
                Description = request.Description,
                Severity = request.Severity,
                GameVersion = Application.version,
                OperatingSystem = SystemInfo.operatingSystem,
                SceneName = SceneManager.GetActiveScene().name,
                IsWorldActive = WorldGeneration.world != null &&
                                PlayerCamera.main != null &&
                                PlayerCamera.main.body != null
            };

            report.Attachments.Add(new BugReportAttachment(
                "modlist.txt", "text/plain", Encoding.UTF8.GetBytes(BuildModList())));

            var log = ReadBepInExLog();
            if (log != null)
                report.Attachments.Add(new BugReportAttachment("bepinex-log.txt", "text/plain", log));

            var console = ReadConsoleLog();
            if (console != null)
                report.Attachments.Add(new BugReportAttachment("console-log.txt", "text/plain", console));

            return report;
        }

        private static string BuildModList()
        {
            var plugins = Chainloader.PluginInfos.Values
                .Where(plugin => plugin != null)
                .OrderBy(plugin => plugin.Metadata?.Name ?? plugin.Metadata?.GUID ?? string.Empty)
                .Select(plugin =>
                {
                    var name = plugin.Metadata?.Name ?? plugin.Metadata?.GUID ?? "Unknown Plugin";
                    var version = plugin.Metadata?.Version?.ToString() ?? "unknown";
                    var guid = plugin.Metadata?.GUID ?? "unknown.guid";
                    return name + " v" + version + " (" + guid + ")";
                }).ToList();

            return "Loaded mods (" + plugins.Count + "):" + Environment.NewLine +
                   string.Join(Environment.NewLine, plugins);
        }

        private static byte[] ReadBepInExLog()
        {
            try
            {
                var path = Path.Combine(Paths.BepInExRootPath, "LogOutput.log");
                if (!File.Exists(path)) return null;

                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                           FileShare.ReadWrite | FileShare.Delete))
                {
                    var length = (int)Math.Min(stream.Length, BepInExLogLimitBytes);
                    if (stream.Length > length) stream.Seek(-length, SeekOrigin.End);

                    var bytes = new byte[length];
                    var offset = 0;
                    while (offset < bytes.Length)
                    {
                        var read = stream.Read(bytes, offset, bytes.Length - offset);
                        if (read == 0) break;
                        offset += read;
                    }

                    if (offset == bytes.Length) return bytes;
                    return bytes.Take(offset).ToArray();
                }
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("Could not collect the BepInEx log for a bug report: " + ex.Message);
                return null;
            }
        }

        private static byte[] ReadConsoleLog()
        {
            try
            {
                var logs = ConsoleScript.instance?.logs;
                if (logs == null || logs.Count == 0) return null;

                var plainText = string.Join(Environment.NewLine,
                    logs.Select(line => TmpTagPattern.Replace(line ?? string.Empty, string.Empty)));
                var bytes = Encoding.UTF8.GetBytes(plainText);
                if (bytes.Length <= ConsoleLogLimitBytes) return bytes;

                return bytes.Skip(bytes.Length - ConsoleLogLimitBytes).ToArray();
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("Could not collect the in-game console log for a bug report: " +
                                                ex.Message);
                return null;
            }
        }
    }
}
