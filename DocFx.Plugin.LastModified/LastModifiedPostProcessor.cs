using System;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using HtmlAgilityPack;
using LibGit2Sharp;
using Microsoft.DocAsCode.Common;
using Microsoft.DocAsCode.Plugins;

namespace DocFx.Plugin.LastModified
{
    [Export(nameof(LastModifiedPostProcessor), typeof(IPostProcessor))]
    public class LastModifiedPostProcessor : IPostProcessor
    {
        private int _addedFiles;

        public ImmutableDictionary<string, object> PrepareMetadata(ImmutableDictionary<string, object> metadata)
            => metadata;

        public Manifest Process(Manifest manifest, string outputFolder)
        {
            var versionInfo = Assembly.GetExecutingAssembly()
                                  .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                  ?.InformationalVersion ??
                              Assembly.GetExecutingAssembly().GetName().Version.ToString();
            var gitDirectory = Repository.Discover(manifest.SourceBasePath);
            Logger.LogInfo($"Version: {versionInfo}");
            Logger.LogInfo("Begin adding last modified date to items...");
            foreach (var manifestItem in manifest.Files.Where(x => x.DocumentType == "Conceptual"))
            foreach (var manifestItemOutputFile in manifestItem.OutputFiles)
            {
                var sourcePath = Path.Combine(manifest.SourceBasePath, manifestItem.SourceRelativePath);
                var outputPath = Path.Combine(outputFolder, manifestItemOutputFile.Value.RelativePath);
                var lastModified = default(DateTimeOffset);
                string modifiedReason = null;
                if (gitDirectory != null)
                {
                    var commitInfo = GetCommitInfo(gitDirectory, sourcePath.Replace('/', '\\'));
                    if (commitInfo.Date.HasValue)
                    {
                        lastModified = commitInfo.Date.Value;
                        modifiedReason = commitInfo.Body;
                    }
                }

                if (lastModified == default(DateTimeOffset))
                    lastModified = GetWriteTimeFromFile(sourcePath);

                Logger.LogVerbose(
                    $"Writing {lastModified} for {outputPath} with reason: {(string.IsNullOrEmpty(modifiedReason) ? "Empty" : modifiedReason)}");
                WriteModifiedDate(sourcePath, outputPath, lastModified, modifiedReason);
            }

            Logger.LogInfo($"Added modification date to {_addedFiles} conceptual articles.");
            return manifest;
        }

        private (DateTimeOffset? Date, string Body) GetCommitInfo(string basePath, string srcPath)
        {
            using (var repo = new Repository(basePath))
            {
                // attempt to fetch root dir of repo

                // hacky solution because libgit2sharp does not provide an easy way
                // to get the root dir of the repo
                // and for some reason does not work with forward-slash
                var repoRoot = new DirectoryInfo(basePath).Parent?.FullName;
                if (string.IsNullOrEmpty(repoRoot))
                    throw new DirectoryNotFoundException("Cannot obtain the root directory of the repository.");

                Logger.LogVerbose($"Repository root: {repoRoot}");
                // remove root dir from absolute path to transform into relative path
                var sourcePath = srcPath.Replace(repoRoot, "").Replace('\\', '/').TrimStart('/');
                Logger.LogVerbose($"Obtaining information from {sourcePath}, from repo {basePath}");

                // see libgit2sharp#1520 for sort issue
                var fileCommits = repo.Commits
                    .QueryBy(sourcePath, new CommitFilter {SortBy = CommitSortStrategies.Topological})
                    .ToList();
                if (!fileCommits.Any()) return (null, null);

                Logger.LogVerbose($"File commit history: {fileCommits.Count}");
                var fileCommit = fileCommits.First();
                return (fileCommit.Commit.Author.When, fileCommit.Commit.Message.Truncate(300));
            }
        }

        private static DateTimeOffset GetWriteTimeFromFile(string sourcePath)
            => File.GetLastWriteTimeUtc(sourcePath);

        private void WriteModifiedDate(string sourcePath, string outputPath, DateTimeOffset modifiedDate,
            string modifiedReason = null)
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
        /// <seealso cref="!:https://github.com/jordnkr/collapsible" />
        private void InjectCollapseScript(HtmlDocument htmlDoc)
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