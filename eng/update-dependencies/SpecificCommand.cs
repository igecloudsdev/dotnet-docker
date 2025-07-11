﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LibGit2Sharp;
using Microsoft.DotNet.VersionTools;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using Microsoft.DotNet.VersionTools.Dependencies;
using Microsoft.DotNet.VersionTools.Dependencies.BuildOutput;

namespace Dotnet.Docker
{
    public class SpecificCommand : BaseCommand<SpecificCommandOptions>
    {
        public const string ManifestFilename = "manifest.json";
        public const string VersionsFilename = "manifest.versions.json";

        private static SpecificCommandOptions? s_options;

        private static SpecificCommandOptions Options {
            get => s_options ?? throw new InvalidOperationException($"{nameof(Options)} has not been set.");
            set => s_options = value;
        }

        public static string RepoRoot { get; } = Directory.GetCurrentDirectory();

        public override async Task<int> ExecuteAsync(SpecificCommandOptions options)
        {
            Options = options;

            try
            {
                ErrorTraceListener errorTraceListener = new();
                Trace.Listeners.Add(errorTraceListener);
                Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

                IDependencyInfo[] productBuildInfos = Options.ProductVersions
                    .Select(kvp => CreateDependencyBuildInfo(kvp.Key, kvp.Value))
                    .ToArray();
                IDependencyInfo[] toolBuildInfos =
                    await Task.WhenAll(Options.Tools.Select(Tools.GetToolBuildInfoAsync));

                List<DependencyUpdateResults> updateResults = [];

                if (productBuildInfos.Length != 0)
                {
                    IEnumerable<IDependencyUpdater> productUpdaters = GetProductUpdaters();
                    DependencyUpdateResults productUpdateResults = UpdateFiles(productBuildInfos, productUpdaters);
                    updateResults.Add(productUpdateResults);
                }

                if (toolBuildInfos.Length != 0)
                {
                    IEnumerable<IDependencyUpdater> toolUpdaters = Tools.GetToolUpdaters(RepoRoot);
                    DependencyUpdateResults toolUpdateResults = UpdateFiles(toolBuildInfos, toolUpdaters);
                    updateResults.Add(toolUpdateResults);
                }

                IEnumerable<IDependencyUpdater> generatedContentUpdaters = GetGeneratedContentUpdaters();
                IEnumerable<IDependencyInfo> allBuildInfos = [..productBuildInfos, ..toolBuildInfos];
                UpdateFiles(allBuildInfos, generatedContentUpdaters);

                if (errorTraceListener.Errors.Any())
                {
                    string errors = string.Join(Environment.NewLine, errorTraceListener.Errors);
                    Console.Error.WriteLine("Failed to update dependencies due to the following errors:");
                    Console.Error.WriteLine(errors);
                    Console.Error.WriteLine();
                    Console.Error.WriteLine("You may need to use the --compute-shas option if checksum files are missing.");
                    Environment.Exit(1);
                }

                if (updateResults.Any(result => result.ChangesDetected()))
                {
                    if (Options.UpdateOnly)
                    {
                        Trace.TraceInformation($"Changes made but no GitHub credentials specified, skipping PR creation");
                    }
                    else
                    {
                        await CreatePullRequestAsync();
                    }
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Failed to update dependencies:{Environment.NewLine}{e}");
                Environment.Exit(1);
            }

            return 0;
        }

        private static DependencyUpdateResults UpdateFiles(
            IEnumerable<IDependencyInfo> buildInfos,
            IEnumerable<IDependencyUpdater> updaters)
        {
            DependencyUpdateResults results = DependencyUpdateUtils.Update(updaters, buildInfos);
            Console.WriteLine(results.GetSuggestedCommitMessage());
            return results;
        }

        private static IDependencyInfo CreateDependencyBuildInfo(string name, string? version)
        {
            return new BuildDependencyInfo(
                new BuildInfo()
                {
                    Name = name,
                    LatestReleaseVersion = version,
                    LatestPackages = new Dictionary<string, string>()
                },
                false,
                Enumerable.Empty<string>());
        }

        // Replace slashes with hyphens for use in naming the branch
        private static string FormatBranchName(string branchName) => branchName.Replace('/', '-');

        private static async Task CreatePullRequestAsync()
        {
            string commitMessage = $"[{Options.TargetBranch}] Update dependencies from {Options.VersionSourceName}";

            string branchSuffix = Options.IsInternal
                ? Options.TargetBranch
                : FormatBranchName($"UpdateDependencies-{Options.TargetBranch}-From-{Options.VersionSourceName}");

            PullRequestOptions prOptions = new()
            {
                BranchNamingStrategy = new SingleBranchNamingStrategy(branchSuffix)
            };

            if (Options.IsInternal)
            {
                PushToAzdoBranch(commitMessage, prOptions);
            }
            else
            {
                await CreateGitHubPullRequest(commitMessage, prOptions, branchSuffix);
            }
        }

        private static void PushToAzdoBranch(string commitMessage, PullRequestOptions prOptions)
        {
            using Repository repo = new(RepoRoot);

            // Commit the existing changes
            Commands.Stage(repo, "*");
            Signature signature = new(Options.User, Options.Email, DateTimeOffset.Now);
            repo.Commit(commitMessage, signature, signature);

            PushOptions pushOptions = new()
            {
                CredentialsProvider = (url, user, credTypes) => new UsernamePasswordCredentials
                {
                    Username = Options.Password, // it doesn't make sense but a PAT needs to be set as username
                    Password = string.Empty
                }
            };

            // Create a remote to AzDO
            string remoteName = GetUniqueName(
                repo.Network.Remotes.Select(remote => remote.Name).ToList(),
                Options.AzdoOrganization);
            Remote remote = repo.Network.Remotes.Add(
                remoteName,
                $"https://dev.azure.com/{Options.AzdoOrganization}/{Options.AzdoProject}/_git/{Options.AzdoRepo}");

            try
            {
                // Push the commit to AzDO
                string username = Options.Email.Substring(0, Options.Email.IndexOf('@'));
                string remoteBranch = prOptions.BranchNamingStrategy.Prefix($"testing/{Options.DockerfileVersion}-internal");
                string pushRefSpec = $@"refs/heads/{remoteBranch}";

                Trace.WriteLine($"Pushing to {remoteBranch}");

                // Force push
                repo.Network.Push(remote, "+HEAD", pushRefSpec, pushOptions);
            }
            finally
            {
                // Clean up the AzDO remote that was created
                repo.Network.Remotes.Remove(remote.Name);
            }
        }

        private static string GetUniqueName(IEnumerable<string> existingNames, string suggestedName, int? index = null)
        {
            string name = suggestedName + index?.ToString();
            if (existingNames.Any(val => val == name))
            {
                return GetUniqueName(existingNames, suggestedName, index is null ? 1 : ++index);
            }

            return name;
        }

        private static async Task CreateGitHubPullRequest(string commitMessage, PullRequestOptions prOptions, string branchSuffix)
        {
            GitHubAuth gitHubAuth = new(Options.Password, Options.User, Options.Email);
            PullRequestCreator prCreator = new(gitHubAuth, Options.User);

            GitHubProject upstreamProject = new(Options.GitHubProject, Options.GitHubUpstreamOwner);
            GitHubBranch upstreamBranch = new(Options.TargetBranch, upstreamProject);

            using (GitHubClient client = new(gitHubAuth))
            {
                GitHubPullRequest pullRequestToUpdate = await client.SearchPullRequestsAsync(
                    upstreamProject,
                    upstreamBranch.Name,
                    await client.GetMyAuthorIdAsync());

                if (pullRequestToUpdate == null || pullRequestToUpdate.Head.Ref != $"{upstreamBranch.Name}-{branchSuffix}")
                {
                    Trace.WriteLine("Didn't find a PR to update. Submitting a new one.");
                    await prCreator.CreateOrUpdateAsync(
                        commitMessage,
                        commitMessage,
                        string.Empty,
                        upstreamBranch,
                        new GitHubProject(Options.GitHubProject, gitHubAuth.User),
                        prOptions);
                }
                else
                {
                    UpdateExistingGitHubPullRequest(gitHubAuth, prOptions, commitMessage, upstreamBranch);
                }
            }
        }

        private static void UpdateExistingGitHubPullRequest(
            GitHubAuth gitHubAuth, PullRequestOptions prOptions, string commitMessage, GitHubBranch upstreamBranch)
        {
            // PullRequestCreator ends up force-pushing updates to an existing PR which is not great when the logic
            // gets called on a schedule (see https://github.com/dotnet/dotnet-docker/issues/1114). To avoid this,
            // it needs the ability to only update files that have changed from the existing PR.  Because the
            // PullRequestCreator class doesn't rely on there being a locally cloned repo, it doesn't have an
            // efficient way to determine whether files have changed or not. Update-dependencies would have to
            // implement logic which pulls down each file individually from the API and compare it to what exists
            // in the local repo.  Since that's not an efficient process, this method works by cloning the PR's
            // branch to a temporary repo location, grabbing the whole repo where the original updates from
            // update-dependencies were made and copying it into the temp repo, and committing and pushing
            // those changes in the temp repo back to the PR's branch.

            Trace.WriteLine($"Updating existing pull request for branch {upstreamBranch.Name}.");

            string tempRepoPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

            try
            {
                string branchName = prOptions.BranchNamingStrategy.Prefix(upstreamBranch.Name);
                CloneOptions cloneOptions = new()
                {
                    BranchName = branchName
                };

                // Clone the PR's repo/branch to a temp location
                Repository.Clone($"https://github.com/{gitHubAuth.User}/{Options.GitHubProject}", tempRepoPath, cloneOptions);

                // Remove all existing directories and files from the temp repo
                ClearRepoContents(tempRepoPath);

                // Copy contents of local repo changes to temp repo
                DirectoryCopy(".", tempRepoPath);

                using Repository repo = new(tempRepoPath);
                RepositoryStatus status = repo.RetrieveStatus(new StatusOptions());

                // If there are any changes from what exists in the PR
                if (status.IsDirty)
                {
                    Trace.WriteLine($"Detected changes that don't currently exist in the PR.");

                    Commands.Stage(repo, "*");

                    Signature signature = new(Options.User, Options.Email, DateTimeOffset.Now);
                    repo.Commit(commitMessage, signature, signature);

                    Branch branch = repo.Branches[$"origin/{branchName}"];

                    PushOptions pushOptions = new()
                    {
                        CredentialsProvider = (url, user, credTypes) => new UsernamePasswordCredentials
                        {
                            Username = Options.Password,
                            Password = string.Empty
                        }
                    };

                    Remote remote = repo.Network.Remotes["origin"];
                    string pushRefSpec = $@"refs/heads/{branchName}";

                    repo.Network.Push(remote, pushRefSpec, pushOptions);
                }
                else
                {
                    Trace.WriteLine($"No new changes were detected - skipping PR update.");
                }
            }
            finally
            {
                // Cleanup temp repo
                DeleteRepoDirectory(tempRepoPath);
            }
        }

        private static void DeleteRepoDirectory(string repoPath)
        {
            if (Directory.Exists(repoPath))
            {
                IEnumerable<string> gitFiles = Directory.GetFiles(
                    Path.Combine(repoPath, ".git"), "*", SearchOption.AllDirectories);

                // Ensure all files in .git folder are writable
                foreach (string file in gitFiles)
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(repoPath, true);
            }
        }

        private static void ClearRepoContents(string repoPath)
        {
            foreach (string file in Directory.GetFiles(repoPath))
            {
                File.Delete(file);
            }
            foreach (DirectoryInfo dir in new DirectoryInfo(repoPath).GetDirectories().Where(dir => dir.Name != ".git"))
            {
                Directory.Delete(dir.FullName, true);
            }
        }

        private static void DirectoryCopy(string sourceDirName, string destDirName)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new(sourceDirName);

            DirectoryInfo[] dirs = dir.GetDirectories()
                .Where(dir => dir.Name != ".git")
                .ToArray();

            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            }

            // If copying subdirectories, copy them and their contents to new location.
            foreach (DirectoryInfo subdir in dirs)
            {
                string temppath = Path.Combine(destDirName, subdir.Name);
                DirectoryCopy(subdir.FullName, temppath);
            }
        }

        private static IEnumerable<IDependencyUpdater> GetProductUpdaters()
        {
            // NOTE: The order in which the updaters are returned/invoked is important as there are cross dependencies
            // (e.g. sha updater requires the version numbers to be updated within the Dockerfiles)

            List<IDependencyUpdater> updaters =
            [
                new NuGetConfigUpdater(RepoRoot, Options),
                new BaseUrlUpdater(RepoRoot, Options),
            ];

            foreach (string productName in Options.ProductVersions.Keys)
            {
                updaters.Add(new VersionUpdater(VersionType.Build, productName, Options.DockerfileVersion, RepoRoot, Options));
                updaters.Add(new VersionUpdater(VersionType.Product, productName, Options.DockerfileVersion, RepoRoot, Options));

                foreach (IDependencyUpdater shaUpdater in DockerfileShaUpdater.CreateUpdaters(productName, Options.DockerfileVersion, RepoRoot, Options))
                {
                    updaters.Add(shaUpdater);
                }
            }

            return updaters;
        }

        private static IEnumerable<IDependencyUpdater> GetGeneratedContentUpdaters() =>
        [
            ScriptRunnerUpdater.GetDockerfileUpdater(RepoRoot),
            ScriptRunnerUpdater.GetReadMeUpdater(RepoRoot)
        ];
    }
}
