﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.VersionTools.Dependencies;
using Newtonsoft.Json.Linq;

namespace Dotnet.Docker;

/// <summary>
/// Updates the baseUrl variables in the manifest.versions.json file.
/// </summary>
internal class BaseUrlUpdater : FileRegexUpdater
{
    private const string BaseUrlGroupName = "BaseUrlValue";
    private readonly SpecificCommandOptions _options;
    private readonly JObject _manifestVariables;

    public BaseUrlUpdater(string repoRoot, SpecificCommandOptions options)
    {
        Path = System.IO.Path.Combine(repoRoot, SpecificCommand.VersionsFilename);
        VersionGroupName = BaseUrlGroupName;
        Regex = ManifestHelper.GetManifestVariableRegex(
            ManifestHelper.GetBaseUrlVariableName(options.DockerfileVersion, options.SourceBranch, options.VersionSourceName),
            $"(?<{BaseUrlGroupName}>.+)");
        _options = options;

        _manifestVariables = (JObject?)ManifestHelper.LoadManifest(SpecificCommand.VersionsFilename)["variables"] ??
            throw new InvalidOperationException($"'{SpecificCommand.VersionsFilename}' property missing in '{SpecificCommand.VersionsFilename}'"); ;
    }

    protected override string TryGetDesiredValue(IEnumerable<IDependencyInfo> dependencyInfos, out IEnumerable<IDependencyInfo> usedDependencyInfos)
    {
        usedDependencyInfos = Enumerable.Empty<IDependencyInfo>();

        string baseUrlVersionVarName = ManifestHelper.GetBaseUrlVariableName(_options.DockerfileVersion, _options.SourceBranch, _options.VersionSourceName);
        string unresolvedBaseUrl = _manifestVariables[baseUrlVersionVarName]?.ToString() ??
            throw new InvalidOperationException($"Variable with name '{baseUrlVersionVarName}' is missing.");

        if (_options.IsInternal)
        {
            if (string.IsNullOrEmpty(_options.InternalBaseUrl))
            {
                throw new InvalidOperationException("InternalBaseUrl must be set in order to update base url for internal builds");
            }

            unresolvedBaseUrl = _options.InternalBaseUrl;
        }
        else if (_options.ReleaseState.HasValue)
        {
            unresolvedBaseUrl = $"$({ManifestHelper.GetBaseUrlVariableName(_options.ReleaseState.Value, _options.TargetBranch)})";
        }
        else
        {
            // Modifying the URL from internal to public is not suppported because it's not possible to know
            // what common variable it was originally referencing when it was last public.
        }

        return unresolvedBaseUrl;
    }
}
