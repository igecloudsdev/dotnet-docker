ARG REPO=mcr.microsoft.com/dotnet/runtime-deps

# Installer image
FROM mcr.microsoft.com/azurelinux/base/core:3.0 AS installer

RUN tdnf install -y \
        ca-certificates \
        gzip \
        tar \
    && tdnf clean all

# Retrieve ASP.NET Composite Runtime
RUN aspnetcore_version=8.0.16  \
    && curl -fSL --output dotnet.tar.gz https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/$aspnetcore_version/aspnetcore-runtime-composite-$aspnetcore_version-linux-x64.tar.gz \
    && dotnet_sha512='d22f82e34d57dd8138aa662308ccca380d5d25b971f92fe9861dc5dadb5ee64f4b450d22f50c656e2f358eccd914905e4126e577dd02d3c150c5905e35e4e4ed' \
    && echo "$dotnet_sha512  dotnet.tar.gz" | sha512sum -c - \
    && mkdir -p /usr/share/dotnet \
    && tar -oxzf dotnet.tar.gz -C /usr/share/dotnet \
    && rm dotnet.tar.gz

RUN mkdir /dotnet-symlink \
    && ln -s /usr/share/dotnet/dotnet /dotnet-symlink/dotnet


# ASP.NET Composite Image
FROM $REPO:8.0.16-azurelinux3.0-distroless-extra-amd64

ENV \
    # .NET Runtime version
    DOTNET_VERSION=8.0.16 \
    # ASP.NET Core version
    ASPNET_VERSION=8.0.16

COPY --from=installer ["/usr/share/dotnet", "/usr/share/dotnet"]
COPY --from=installer ["/dotnet-symlink", "/usr/bin"]

ENTRYPOINT ["/usr/bin/dotnet"]
CMD ["--info"]
