ARG REPO=mcr.microsoft.com/dotnet/runtime-deps

# Installer image
FROM arm64v8/buildpack-deps:noble-curl AS installer

# Retrieve .NET Runtime
RUN dotnet_version=10.0.0-preview.5.25277.114 \
    && curl --fail --show-error --location \
        --remote-name https://builds.dotnet.microsoft.com/dotnet/Runtime/$dotnet_version/dotnet-runtime-$dotnet_version-linux-arm64.tar.gz \
        --remote-name https://builds.dotnet.microsoft.com/dotnet/Runtime/$dotnet_version/dotnet-runtime-$dotnet_version-linux-arm64.tar.gz.sha512 \
    && echo "$(cat dotnet-runtime-$dotnet_version-linux-arm64.tar.gz.sha512)  dotnet-runtime-$dotnet_version-linux-arm64.tar.gz" | sha512sum -c - \
    && mkdir --parents /dotnet \
    && tar --gzip --extract --no-same-owner --file dotnet-runtime-$dotnet_version-linux-arm64.tar.gz --directory /dotnet \
    && rm \
        dotnet-runtime-$dotnet_version-linux-arm64.tar.gz \
        dotnet-runtime-$dotnet_version-linux-arm64.tar.gz.sha512


# .NET runtime image
FROM $REPO:10.0.0-preview.5-noble-arm64v8

# .NET Runtime version
ENV DOTNET_VERSION=10.0.0-preview.5.25277.114

COPY --from=installer ["/dotnet", "/usr/share/dotnet"]

RUN ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet
