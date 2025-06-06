﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Dotnet.Docker;

/// <summary>
/// Helper class for interacting with manifest files.
/// </summary>
public static partial class ManifestHelper
{
    private const string VariableGroupName = "variable";
    private const string VariablePattern = $"\\$\\((?<{VariableGroupName}>[\\w:\\-.|]+)\\)";

    /// <summary>
    /// Gets the base URL based on the configured context.
    /// </summary>
    /// <param name="manifestVariables">JSON object of the variables from the manifest.</param>
    /// <param name="options">Configured options from the app.</param>
    public static string GetBaseUrl(JObject manifestVariables, SpecificCommandOptions options) =>
        ResolveVariableValue(GetBaseUrlVariableName(options.DockerfileVersion, options.SourceBranch, options.VersionSourceName), manifestVariables);

    /// <summary>
    /// Constructs the name of the product version base URL variable.
    /// </summary>
    /// <param name="dockerfileVersion">Dockerfile version.</param>
    /// <param name="branch">Name of the branch.</param>
    public static string GetBaseUrlVariableName(string dockerfileVersion, string branch, string? versionSourceName)
    {
        string product = versionSourceName switch
        {
            string v when v.Contains("dotnet-monitor") => $"monitor",
            string v when v.Contains("aspire-dashboard") => $"aspire-dashboard",
            _ => "dotnet",
        };

        return $"{product}|{dockerfileVersion}|base-url|{branch}";
    }

    /// <summary>
    /// Constructs the name of the shared base URL variable.
    /// </summary>
    /// <param name="releaseState">Release state of the product assets.</param>
    /// <param name="branch">Name of the branch.</param>
    public static string GetBaseUrlVariableName(ReleaseState releaseState, string branch)
    {
        string qualityString = releaseState switch
        {
            ReleaseState.Prerelease => "preview",
            ReleaseState.Release => "maintenance",
            _ => throw new NotSupportedException()
        };

        return $"base-url|public|{qualityString}|{branch}";
    }

    public static string GetVersionVariableName(VersionType versionType, string productName, string dockerfileVersion) =>
        $"{productName}|{dockerfileVersion}|{versionType.ToString().ToLowerInvariant()}-version";

    /// <summary>
    /// Gets the value of a manifest variable, returns an empty string if it is not defined.
    /// </summary>
    /// <param name="variableName">Name of the variable.</param>
    /// <param name="variables">JSON object of the variables from the manifest.</param>
    public static string TryResolveVariableValue(string variableName, JObject variables)
        => variables.ContainsKey(variableName) ? ResolveVariableValue(variableName, variables) : "";

    /// <summary>
    /// Resolves the value of a manifest variable, recursively resolving any variables referenced in the value
    /// </summary>
    /// <param name="variableName">Name of the variable.</param>
    /// <param name="variables">JSON object of the variables from the manifest.</param>
    public static string ResolveVariableValue(string variableName, JObject variables)
    {
        string variableValue = GetVariableValue(variableName, variables);
        return ResolveVariables(variableValue, variables);
    }

    public static string TryGetVariableValue(string variableName, JObject variables)
        => variables.ContainsKey(variableName) ? GetVariableValue(variableName, variables) : "";

    public static string GetVariableValue(string variableName, JObject variables)
        => (string?) variables[variableName]
            ?? throw new ArgumentException($"Manifest does not contain a value for {variableName}");

    /// <summary>
    /// Loads the manifest from the given filename.
    /// </summary>
    /// <param name="filename">Name, not path, of the manifest file located at the root of the repo.</param>
    public static JObject LoadManifest(string filename)
    {
        string path = Path.Combine(SpecificCommand.RepoRoot, filename);
        string contents = File.ReadAllText(path);
        return JObject.Parse(contents);
    }

    /// <summary>
    /// Gets the regex that identifies a manifest variable.
    /// </summary>
    /// <param name="variableName">Name of the variable.</param>
    /// <param name="valuePattern">Regex pattern that identifies the value of the variable.</param>
    /// <param name="options">Configured options from the app.</param>
    public static Regex GetManifestVariableRegex(string variableName, string valuePattern, RegexOptions options = RegexOptions.None) =>
        new($"\"{Regex.Escape(variableName)}\": \"{valuePattern}\"", options);

    /// <summary>
    /// Determines if the given value matches the pattern manifest variable. Does not check if the variable is defined
    /// in the manifest.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>True if the value is a manifest variable, false otherwise.</returns>
    public static bool IsManifestVariable(string value) => AnyVariableRegex().IsMatch(value);

    /// <summary>
    /// Resolves the value of a variable, recursively resolving any variables referenced in the value.
    /// </summary>
    /// <param name="value">Variable value to be resolved.</param>
    /// <param name="variables">JSON object of the variables from the manifest.</param>
    private static string ResolveVariables(string value, JObject variables)
    {
        MatchCollection matches = Regex.Matches(value, VariablePattern);
        foreach (Match match in matches)
        {
            string variableName = match.Groups[VariableGroupName].Value;
            string variableValue = ResolveVariableValue(variableName, variables);
            value = value.Replace(match.Value, variableValue);
        }

        return value;
    }

    [GeneratedRegex(@"^\$\(.*\)$")]
    private static partial Regex AnyVariableRegex();
}
