using System;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using HtmlAgilityPack;
using Microsoft.DocAsCode.Common;
using Microsoft.DocAsCode.Plugins;

namespace DocFx.Plugin.LastModified
{
    [Export(nameof(LastModifiedPostProcessor), typeof(IPostProcessor))]
    public class LastModifiedPostProcessor : IPostProcessor
    {
        public ImmutableDictionary<string, object> PrepareMetadata(ImmutableDictionary<string, object> metadata)
        {
            return metadata;
        }

        public Manifest Process(Manifest manifest, string outputFolder)
        {
            Logger.LogInfo("Begin adding last modified date to items...");
            foreach (var manifestItem in manifest.Files.Where(x=>x.DocumentType == "Conceptual"))
            {
                foreach (var manifestItemOutputFile in manifestItem.OutputFiles)
                {
                    AddModifiedDate(Path.Combine(outputFolder, manifestItemOutputFile.Value.RelativePath));
                }
            }
            return manifest;
        }

        private void AddModifiedDate(string path)
        {
            var lastWrittenDate = File.GetLastWriteTimeUtc(path);
            if (lastWrittenDate == default(DateTimeOffset)) return;

            var htmlDoc = new HtmlDocument();
            htmlDoc.Load(path);
            var articleNode = htmlDoc.DocumentNode.SelectSingleNode("//article[contains(@class, 'content wrap')]");
            if (articleNode == null)
            {
                Logger.LogDiagnostic("ArticleNode not found, returning.");
                return;
            }

            var separator = htmlDoc.CreateElement("hr");

            var noteNode = htmlDoc.CreateElement("div");
            noteNode.SetAttributeValue("class", "NOTE");

            var titleNode = htmlDoc.CreateElement("h5");
            titleNode.InnerHtml = "Last Updated";
            noteNode.AppendChild(titleNode);

            var paragraphNode = htmlDoc.CreateElement("p");
            paragraphNode.InnerHtml = $"This page was last modified at {lastWrittenDate} (UTC).";
            noteNode.AppendChild(paragraphNode);

            articleNode.AppendChild(separator);
            articleNode.AppendChild(noteNode);

            htmlDoc.Save(path);
        }
    }
}
