---
name: ".NET Docker Release - New Windows Version"
about: "Checklist for releasing .NET container images for new Windows major versions"
title: ".NET Container Images Release - New Windows Version - <new Windows version>"
labels: docker
assignees: lbussell
---

## Main Branch Tasks

1. - [ ] Ensure a ["New Windows Release" issue](https://github.com/dotnet/docker-tools/blob/main/.github/ISSUE_TEMPLATE/releases/new-windows-release.md) exists for docker-tools repo
1. - [ ] Copy the Dockerfiles of the most recent published Windows version for all supported .NET versions and place them in a version-specific folder under their respective variants (runtime, aspnet, sdk)
1. - [ ] Modify the Dockerfile templates if there are any specific changes needed that are related to the new Windows version
1. - [ ] Update [manifest.json](https://github.com/dotnet/dotnet-docker/blob/nightly/manifest.json) to reference the new set of Dockerfiles with the appropriate tags
1. - [ ] Update the [test data](https://github.com/dotnet/dotnet-docker/blob/nightly/tests/Microsoft.DotNet.Docker.Tests/TestData.cs) to include the new Windows version
1. - [ ] Update the [tags metadata templates](https://github.com/dotnet/dotnet-docker/tree/main/eng/mcr-tags-metadata-templates) to include the new Windows version
1. - [ ] Run the command to update the READMEs: `.\eng\readme-templates\Get-GeneratedReadmes.ps1`
1. - [ ] Inspect generated changes for correctness
1. - [ ] Test the images
      1. - [ ] Create a local VM of the new Windows version
      1. - [ ] Clone this repo with the above changes onto the VM
      1. - [ ] Run `.\build-and-test.ps1 -OS nanoserver-<VERSION>` to build and test your changes for Nano Server
      1. - [ ] Run `.\build-and-test.ps1 -OS windowsservercore-<VERSION>` to build and test your changes for Windows Server Core
1. - [ ] Create PR
1. - [ ] Get PR signoff
1. - [ ] Merge PR and build/publish as part of the main branch [release process](https://github.com/dotnet/release/blob/main/.github/ISSUE_TEMPLATE/dotnet-docker-servicing-release.md) for the next .NET release
1. - [ ] Create an announcement (example: [Nano Server, version 20H2](https://github.com/dotnet/dotnet-docker/issues/2322))
1. - [ ] Update the samples to reference the new Windows version:
      - [ ] [Nano Server and Windows Server Core sample Dockerfiles](https://github.com/dotnet/dotnet-docker/tree/main/samples)
      - [ ] [manifest.samples.json](https://github.com/dotnet/dotnet-docker/blob/main/manifest.samples.json)

## Nightly Branch Tasks

- [ ] Merge these changes to the nightly branch as part of the nightly branch [release process](./dotnet-release-lifecycle.md) for the next .NET release.
