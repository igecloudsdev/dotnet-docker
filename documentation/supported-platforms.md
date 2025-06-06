# Supported Platforms

This document describes the platforms (OS and architectures) supported by the official .NET Docker images.

## Operating Systems

.NET supports [a broad set of operating systems and versions](https://github.com/dotnet/core/blob/main/os-lifecycle-policy.md). When producing container images, it’s impractical to support the full matrix of OS, arch, and .NET version combinations. In practice, images are produced for a select set of operating systems and versions. If official .NET container images aren't provided for your preferred OS, [let us know by opening a discussion](https://github.com/dotnet/dotnet-docker/discussions). Alternatively, you can [author your own .NET images](scenarios/installing-dotnet.md).

- Images for new OS versions are typically released within one month of the new OS release, with a goal to release same-day when possible.
- New OS versions are available in [`dotnet/nightly` repositories](https://github.com/dotnet/dotnet-docker/blob/nightly/README.md) first, and are added to the officially supported repos afterwards.
- All new OS releases will be accompanied by an [announcement](https://github.com/dotnet/dotnet-docker/discussions/categories/announcements).

These policies are specific to .NET container images. For more information on overall .NET OS support, see [.NET OS Support Tracking](https://github.com/dotnet/core/issues/9638).

### Linux

Each distribution (distro) has a unique approach to releasing, schedule, and end-of life (EOL). This prohibits the definition of a one-size-fits-all policy. Instead, a policy is defined for each supported distro.

- Alpine — support latest and retain support for the previous version one quarter (3 months) after a new version is released.
- Azure Linux — support the latest *stable* version at the time a `major.minor` version of .NET is released. As new *stable* versions are released, support is added to the latest .NET version and latest LTS (if they differ).
- Debian — support the latest *stable* version at the time a `major.minor` version of .NET is released. As new *stable* versions are released, support is added to the latest .NET version and latest LTS (if they differ) via an [OS-specific tag](supported-tags.md#os-tags-and-base-image-updates).
- Ubuntu — support the latest *LTS* version at the time a `major.minor` version of .NET is released. As new *LTS* versions are released, support is added to the latest .NET version and latest LTS (if they differ).

Pre-release versions of the supported distros will be made available within the [nightly repositories](https://github.com/dotnet/dotnet-docker/blob/nightly/README.md) based on the availability of pre-release OS base images.

#### FedRAMP Compliance

For [.NET appliance images](./supported-tags.md#net-appliance-images) based on Azure Linux, base image OS upgrades will be delayed until the new version of Azure Linux has FedRAMP approval.

### Windows

The official .NET images support Nano Server as well as LTS versions of Windows Server Core for .NET 5.0 and higher. Nano Server is the best Windows SKU to run .NET apps from a performance perspective. In order for Nano Server to perform well and remain lightweight, it doesn't have support for every scenario. In case your scenario isn't supported by Nano Server, you may need to use one of the .NET images based on Windows Server Core. For scenarios where the official .NET images don't meet your needs, you will need to manage your own custom .NET images based on [Windows Server Core](https://mcr.microsoft.com/en-us/product/windows/servercore/about) or [Windows](https://mcr.microsoft.com/en-us/product/windows/about).

- Nano Server - support all supported versions with each .NET version.
- Windows Server Core - support all LTS versions (Windows Server 2019 and above) starting with .NET 5.0.

## Architectures

.NET images are provided for the following architectures.

- Linux/Windows x86-64 (amd64)
- Linux ARMv7 32-bit (arm32v7)
- Linux ARMv8 64-bit (arm64v8)
