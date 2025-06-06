#!/usr/bin/env pwsh
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

[cmdletbinding()]
param(
    [Parameter(ParameterSetName = "Version")]
    [string]$Version = "*",

    [Parameter(ParameterSetName = "Paths")]
    [string[]]$Paths = @(),

    [string]$Architecture,

    [string[]]$OSVersions,

    [string]$Registry,

    [string]$CacheRegistry,

    [string]$RepoPrefix,

    [switch]$DisableHttpVerification,

    [switch]$PullImages,

    [string]$ImageInfoPath,

    [ValidateSet("runtime", "runtime-deps", "aspnet", "sdk", "pre-build", "sample", "monitor", "aspire-dashboard")]
    [string[]]$TestCategories = @("runtime", "runtime-deps", "aspnet", "sdk", "monitor", "aspire-dashboard"),

    [string]$CustomTestFilter,

    [string]$InternalAccessToken
)

Import-Module -force $PSScriptRoot/../eng/DependencyManagement.psm1

function Log {
    param ([string] $Message)

    Write-Output $Message
}

function Exec {
    param ([string] $Cmd)

    Log "Executing: '$Cmd'"
    Invoke-Expression $Cmd
    if ($LASTEXITCODE -ne 0) {
        throw "Failed: '$Cmd'"
    }
}

function GetPath {
    param ([string] $osVersion)

    return "src/*/$Version/$osVersion/$Architecture"
}

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$EngCommonDir = "$PSScriptRoot/../eng/common"

$DotnetInstallDir = "$PSScriptRoot/../.dotnet"
& $EngCommonDir/Install-DotNetSdk.ps1 -InstallPath $DotnetInstallDir

# Ensure that ImageBuilder image is pulled because some tests require it
& $EngCommonDir/Get-ImageBuilder.ps1

$activeOS = docker version -f "{{ .Server.Os }}"

Push-Location "$PSScriptRoot\Microsoft.DotNet.Docker.Tests"

# Store the original set of environment variables before we start modifying them
$origEnvVars = Get-ChildItem env:

Try {
    # Run Tests
    if ([string]::IsNullOrWhiteSpace($Architecture)) {
        $Architecture = "amd64"
    }

    if ($PSCmdlet.ParameterSetName -eq "Version") {
        if ($OSVersions -and $OSVersions.Count -gt 0) {
            foreach ($osVersion in $OSVersions) {
                $Paths += $(GetPath $osVersion)
            }
        }
        else {
            $Paths += GetPath "*"
        }
    }

    if ($DisableHttpVerification) {
        $env:DISABLE_HTTP_VERIFICATION = 1
    }
    else {
        $env:DISABLE_HTTP_VERIFICATION = $null
    }

    if ($PullImages) {
        $env:PULL_IMAGES = 1
    }
    else {
        $env:PULL_IMAGES = $null
    }

    $env:DOCKERFILE_PATHS = $($Paths -Join ",")
    $env:IMAGE_ARCH = $Architecture
    $env:IMAGE_OS_NAMES = $($OSVersions -Join ",")
    $env:REGISTRY = $Registry
    $env:CACHE_REGISTRY = $CacheRegistry
    $env:REPO_PREFIX = $RepoPrefix
    $env:IMAGE_INFO_PATH = $ImageInfoPath
    $env:SOURCE_REPO_ROOT = (Get-Item "$PSScriptRoot").Parent.FullName
    $env:SOURCE_BRANCH = Get-Branch

    $env:DOTNET_CLI_TELEMETRY_OPTOUT = 1
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = 1
    $env:DOTNET_MULTILEVEL_LOOKUP = '0'

    if ($InternalAccessToken) {
        $env:INTERNAL_ACCESS_TOKEN = $InternalAccessToken
        $env:INTERNAL_TESTING = 1
    }

    $testFilter = ""
    if ($TestCategories) {
        # Construct an expression that filters the test to each of the
        # selected TestCategories (using an OR operator between each category).
        # See https://docs.microsoft.com/en-us/dotnet/core/testing/selective-unit-tests
        $TestCategories | ForEach-Object {
            if ($testFilter) {
                $testFilter += "|"
            }

            $testFilter += "Category=$_"
        }

        if (-not $testFilter) {
            exit;
        }

        if ($CustomTestFilter)
        {
            $testFilter = "$CustomTestFilter&($testFilter)"
        }

        $testFilter = "--filter '$testFilter'"
    }

    Write-Host "`nRunning tests with $testFilter`n"
    Exec "$DotnetInstallDir/dotnet test $testFilter --logger:trx"
}
Finally {
    Pop-Location

    # Delete any newly added environment variables
    Get-ChildItem env: | Where-Object { $_.Name -notin ($origEnvVars | Select-Object -ExpandProperty Name) } | Remove-Item

    # Restore the original values of any modified environment variables
    $origEnvVars | ForEach-Object { Set-Item "env:$($_.Name)" $_.Value }
}
