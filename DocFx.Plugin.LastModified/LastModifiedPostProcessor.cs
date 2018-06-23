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
        private int _addedFiles;

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
                    string modifiedReason = null;
                    try
                    {
                        lastModified = DateTimeOffset.Parse(GetCommitInfo(sourcePath, CommitDataType.Date)).ToUniversalTime();
                        modifiedReason = GetCommitInfo(sourcePath, CommitDataType.Body);
                    }
                    catch (Exception e)
                    {
                        Logger.LogVerbose(e.ToString());
                        Logger.LogVerbose("Failed to fetch commit date, falling back to system write time.");
                        lastModified = GetWriteTimeFromFile(sourcePath);
                    }
                    WriteModifiedDate(sourcePath, outputPath, lastModified, modifiedReason);
                }
            }
            Logger.LogInfo($"Added modification date to {_addedFiles} conceptual articles.");
            return manifest;
        }

        private string GetCommitInfo(string sourcePath, CommitDataType dataType)
        {
            string formatString = null;
            switch (dataType)
            {
                case CommitDataType.Date:
                    formatString = "%cd --date=iso8601";
                    break;
                case CommitDataType.Body:
                    formatString = "%B";
                    break;
            }
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"log -1 --format={formatString} {sourcePath}",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            var process = System.Diagnostics.Process.Start(processStartInfo);
            return process?.StandardOutput.ReadToEnd();
        }

        private DateTimeOffset GetWriteTimeFromFile(string sourcePath) => File.GetLastWriteTimeUtc(sourcePath);

        private void WriteModifiedDate(string sourcePath, string outputPath, DateTimeOffset modifiedDate, string modifiedReason = null)
        {
            Logger.LogVerbose($"Writing {modifiedDate} from {sourcePath} to {outputPath}");

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

            if (!string.IsNullOrEmpty(modifiedReason))
            {
                InjectCollapseScript(htmlDoc);

                var collapsibleNode = htmlDoc.CreateElement("div");
                collapsibleNode.SetAttributeValue("class", "collapse-container last-modified");
                collapsibleNode.SetAttributeValue("id", "accordion");
                var reasonHeaderNode = htmlDoc.CreateElement("span");
                reasonHeaderNode.InnerHtml = "<span class=\"arrow-r\"></span>Commit Message";
                var reasonContainerNode = htmlDoc.CreateElement("div");
                var preCodeBlockNode = htmlDoc.CreateElement("pre");
                var codeBlockNode = htmlDoc.CreateElement("code");
                codeBlockNode.SetAttributeValue("class", "xml");
                codeBlockNode.InnerHtml = modifiedReason.Trim();

                preCodeBlockNode.AppendChild(codeBlockNode);
                reasonContainerNode.AppendChild(preCodeBlockNode);
                collapsibleNode.AppendChild(reasonHeaderNode);
                collapsibleNode.AppendChild(reasonContainerNode);
                articleNode.AppendChild(collapsibleNode);
            }

            htmlDoc.Save(outputPath);
            _addedFiles++;
        }

        /// <summary>
        ///     Injects script required for collapsible dropdown menu.
        /// </summary>
        /// <seealso cref="https://github.com/jordnkr/collapsible"/>
        private void InjectCollapseScript(HtmlDocument htmlDoc)
        {
            var bodyNode = htmlDoc.DocumentNode.SelectSingleNode("//body");

            var accordionNode = htmlDoc.CreateElement("script");
            accordionNode.InnerHtml = @"
  $( function() {
    $( ""#accordion"" ).collapsible();
  } );";
            bodyNode.AppendChild(accordionNode);

            var collpasibleScriptNode = htmlDoc.CreateElement("script");
            collpasibleScriptNode.SetAttributeValue("type", "text/javascript");
            collpasibleScriptNode.SetAttributeValue("src",
                "https://cdn.rawgit.com/jordnkr/collapsible/master/jquery.collapsible.min.js");
            bodyNode.AppendChild(collpasibleScriptNode);

            var headNode = htmlDoc.DocumentNode.SelectSingleNode("//head");
            var collpasibleCssNode = htmlDoc.CreateElement("link");
            collpasibleCssNode.SetAttributeValue("rel", "stylesheet");
            collpasibleCssNode.SetAttributeValue("href",
                "https://cdn.rawgit.com/jordnkr/collapsible/master/collapsible.css");
            headNode.AppendChild(collpasibleCssNode);
        }
    }
}