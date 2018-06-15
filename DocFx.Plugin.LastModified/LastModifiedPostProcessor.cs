using System;
using System.Collections.Immutable;
using System.Composition;
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
                    AddModifiedDate(Path.Combine(manifest.SourceBasePath, manifestItem.SourceRelativePath),
                        Path.Combine(outputFolder, manifestItemOutputFile.Value.RelativePath));
            }

            return manifest;
        }

        private void AddModifiedDate(string sourcePath, string outputPath)
        {
            var lastWrittenDate = File.GetLastWriteTimeUtc(sourcePath);
            if (lastWrittenDate == default(DateTimeOffset)) return;
            Logger.LogInfo($"Writing {lastWrittenDate} from {sourcePath} to {outputPath}");

            var htmlDoc = new HtmlDocument();
            htmlDoc.Load(outputPath);
            var articleNode = htmlDoc.DocumentNode.SelectSingleNode("//article[contains(@class, 'content wrap')]");
            if (articleNode == null)
            {
                Logger.LogDiagnostic("ArticleNode not found, returning.");
                return;
            }

            var paragraphNode = htmlDoc.CreateElement("p");
            paragraphNode.InnerHtml = $"This page was last modified at {lastWrittenDate} (UTC).";

            var separatorNode = htmlDoc.CreateElement("hr");

            articleNode.AppendChild(separatorNode);
            articleNode.AppendChild(paragraphNode);

            htmlDoc.Save(outputPath);
        }
    }
}