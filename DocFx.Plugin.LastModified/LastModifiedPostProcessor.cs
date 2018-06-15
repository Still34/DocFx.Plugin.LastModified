using System;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using HtmlAgilityPack;
using Microsoft.DocAsCode.Common;
using Microsoft.DocAsCode.Plugins;

namespace DocFx.Plugin.LastModified
{
    [Export(nameof(LastModifiedPostProcessor), typeof(IPostProcessor))]
    public class LastModifiedPostProcessor : IPostProcessor
    {
        public ImmutableDictionary<string, object> PrepareMetadata(ImmutableDictionary<string, object> metadata) =>
            metadata;

        public Manifest Process(Manifest manifest, string outputFolder)
        {
            string versionInfo = Assembly.GetExecutingAssembly()
                                     .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                     ?.InformationalVersion ??
                                 Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Logger.LogInfo($"Version: {versionInfo}");
            Logger.LogInfo("Begin adding last modified date to items...");
            foreach (var manifestItem in manifest.Files.Where(x => x.DocumentType == "Conceptual"))
            {
                foreach (var manifestItemOutputFile in manifestItem.OutputFiles)
                {
                    string sourcePath = Path.Combine(manifest.SourceBasePath, manifestItem.SourceRelativePath);
                    string outputPath = Path.Combine(outputFolder, manifestItemOutputFile.Value.RelativePath);
                    DateTimeOffset lastModified;
                    try
                    {
                        lastModified = GetCommitDate(sourcePath);
                    }
                    catch (Exception e)
                    {
                        Logger.LogVerbose(e.ToString());
                        Logger.LogVerbose("Failed to fetch commit date, falling back to system write time.");
                        lastModified = GetWriteTimeFromFile(sourcePath);
                    }
                    WriteModifiedDate(lastModified, sourcePath, outputPath);
                }
            }
            return manifest;
        }

        private DateTimeOffset GetCommitDate(string sourcePath)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"log -1 --format=%cd --date=iso8601 {sourcePath}",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            var process = System.Diagnostics.Process.Start(processStartInfo);
            string output = process?.StandardOutput.ReadToEnd();
            return DateTimeOffset.Parse(output);
        }

        private DateTimeOffset GetWriteTimeFromFile(string sourcePath) => File.GetLastWriteTimeUtc(sourcePath);

        private void WriteModifiedDate(DateTimeOffset modifiedDate, string sourcePath, string outputPath)
        {
            Logger.LogInfo($"Writing {modifiedDate} from {sourcePath} to {outputPath}");

            var htmlDoc = new HtmlDocument();
            htmlDoc.Load(outputPath);
            var articleNode = htmlDoc.DocumentNode.SelectSingleNode("//article[contains(@class, 'content wrap')]");
            if (articleNode == null)
            {
                Logger.LogDiagnostic("ArticleNode not found, returning.");
                return;
            }

            var paragraphNode = htmlDoc.CreateElement("p");
            paragraphNode.InnerHtml = $"This page was last modified at {modifiedDate} (UTC).";

            var separatorNode = htmlDoc.CreateElement("hr");

            articleNode.AppendChild(separatorNode);
            articleNode.AppendChild(paragraphNode);

            htmlDoc.Save(outputPath);
        }
    }
}