using System;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DocFx.Plugin.LastModified.Helpers;
using HtmlAgilityPack;
using LibGit2Sharp;
using Microsoft.DocAsCode.Common;
using Microsoft.DocAsCode.Plugins;

namespace DocFx.Plugin.LastModified
{
    /// <summary>
    ///     Post-processor responsible for injecting last modified date according to commit or file modified date.
    /// </summary>
    [Export(nameof(LastModifiedPostProcessor), typeof(IPostProcessor))]
    public class LastModifiedPostProcessor : IPostProcessor
    {
        private int _addedFiles;
        private Repository _repo;

        public ImmutableDictionary<string, object> PrepareMetadata(ImmutableDictionary<string, object> metadata)
            => metadata;

        public Manifest Process(Manifest manifest, string outputFolder)
        {
            var versionInfo = Assembly.GetExecutingAssembly()
                                  .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                  ?.InformationalVersion ??
                              Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Logger.LogInfo($"Version: {versionInfo}");
            Logger.LogInfo("Begin adding last modified date to items...");

            // attempt to fetch git repo from the current project
            var gitDirectory = Repository.Discover(manifest.SourceBasePath);
            if (gitDirectory != null) _repo = new Repository(gitDirectory);

            foreach (var manifestItem in manifest.Files.Where(x => x.DocumentType == "Conceptual"))
            foreach (var manifestItemOutputFile in manifestItem.OutputFiles)
            {
                var sourcePath = Path.Combine(manifest.SourceBasePath, manifestItem.SourceRelativePath);
                var outputPath = Path.Combine(outputFolder, manifestItemOutputFile.Value.RelativePath);
                if (_repo != null)
                {
                    var commitInfo = _repo.GetCommitInfo(sourcePath);
                    if (commitInfo != null)
                    {
                        Logger.LogVerbose("Assigning commit date...");
                        var lastModified = commitInfo.Author.When;

                        var commitHeaderBuilder = new StringBuilder();
                        Logger.LogVerbose("Appending commit author and email...");
                        commitHeaderBuilder.AppendLine($"Author:    {commitInfo.Author.Name}");
                        Logger.LogVerbose("Appending commit SHA...");
                        commitHeaderBuilder.AppendLine($"Commit:    {commitInfo.Sha}");
                        
                        var commitHeader = commitHeaderBuilder.ToString();
                        // truncate to 200 in case of huge commit body
                        var commitBody = commitInfo.Message.Truncate(300);
                        Logger.LogVerbose($"Writing {lastModified} with reason for {outputPath}...");
                        WriteModifiedDate(outputPath, lastModified, commitHeader, commitBody);
                        continue;
                    }
                }

                var fileLastModified = File.GetLastWriteTimeUtc(sourcePath);
                Logger.LogVerbose($"Writing {fileLastModified} for {outputPath}...");
                WriteModifiedDate(outputPath, fileLastModified);
            }

            // dispose repo after usage
            _repo?.Dispose();

            Logger.LogInfo($"Added modification date to {_addedFiles} conceptual articles.");
            return manifest;
        }

        private void WriteModifiedDate(string outputPath, DateTimeOffset modifiedDate, string commitHeader = null,
            string commitBody = null)
        {
            if (outputPath == null) throw new ArgumentNullException(nameof(outputPath));

            // load the document
            var htmlDoc = new HtmlDocument();
            htmlDoc.Load(outputPath);

            // check for article container
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

            if (!string.IsNullOrEmpty(commitHeader))
            {
                // inject collapsible container script
                InjectCollapseScript(htmlDoc);

                // create collapse container
                var collapsibleNode = htmlDoc.CreateElement("div");
                collapsibleNode.SetAttributeValue("class", "collapse-container last-modified");
                collapsibleNode.SetAttributeValue("id", "accordion");
                var reasonHeaderNode = htmlDoc.CreateElement("span");
                reasonHeaderNode.InnerHtml = "<span class=\"arrow-r\"></span>Commit Message";
                var reasonContainerNode = htmlDoc.CreateElement("div");

                // inject header
                var preCodeBlockNode = htmlDoc.CreateElement("pre");
                var codeBlockNode = htmlDoc.CreateElement("code");
                codeBlockNode.InnerHtml = commitHeader;
                preCodeBlockNode.AppendChild(codeBlockNode);
                reasonContainerNode.AppendChild(preCodeBlockNode);
                
                // inject body
                preCodeBlockNode = htmlDoc.CreateElement("pre");
                codeBlockNode = htmlDoc.CreateElement("code");
                codeBlockNode.SetAttributeValue("class", "xml");
                codeBlockNode.InnerHtml = commitBody;
                preCodeBlockNode.AppendChild(codeBlockNode);
                reasonContainerNode.AppendChild(preCodeBlockNode);

                // inject the entire block
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
        /// <seealso cref="!:https://github.com/jordnkr/collapsible" />
        private static void InjectCollapseScript(HtmlDocument htmlDoc)
        {
            var bodyNode = htmlDoc.DocumentNode.SelectSingleNode("//body");

            var accordionNode = htmlDoc.CreateElement("script");
            accordionNode.InnerHtml = @"
  $( function() {
    $( ""#accordion"" ).collapsible();
  } );";
            bodyNode.AppendChild(accordionNode);

            var collapsibleScriptNode = htmlDoc.CreateElement("script");
            collapsibleScriptNode.SetAttributeValue("type", "text/javascript");
            collapsibleScriptNode.SetAttributeValue("src",
                "https://cdn.rawgit.com/jordnkr/collapsible/master/jquery.collapsible.min.js");
            bodyNode.AppendChild(collapsibleScriptNode);

            var headNode = htmlDoc.DocumentNode.SelectSingleNode("//head");
            var collapsibleCssNode = htmlDoc.CreateElement("link");
            collapsibleCssNode.SetAttributeValue("rel", "stylesheet");
            collapsibleCssNode.SetAttributeValue("href",
                "https://cdn.rawgit.com/jordnkr/collapsible/master/collapsible.css");
            headNode.AppendChild(collapsibleCssNode);
        }
    }
}