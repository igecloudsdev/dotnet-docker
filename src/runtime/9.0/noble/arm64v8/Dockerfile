ARG REPO=mcr.microsoft.com/dotnet/runtime-deps

# Installer image
FROM arm64v8/buildpack-deps:noble-curl AS installer

# Retrieve .NET Runtime
RUN dotnet_version=9.0.6 \
    && curl -fSL --output dotnet.tar.gz https://builds.dotnet.microsoft.com/dotnet/Runtime/$dotnet_version/dotnet-runtime-$dotnet_version-linux-arm64.tar.gz \
    && dotnet_sha512='24ab41d34be7f6718d7a9489e21983e857e7348c914157e8fa04a6fdf010d43a942a9757c059d88c863efd3aeaed6781f928da355021c50b965e8d0915021ea0' \
    && echo "$dotnet_sha512  dotnet.tar.gz" | sha512sum -c - \
    && mkdir -p /dotnet \
    && tar -oxzf dotnet.tar.gz -C /dotnet \
    && rm dotnet.tar.gz


# .NET runtime image
FROM $REPO:9.0.6-noble-arm64v8

# .NET Runtime version
ENV DOTNET_VERSION=9.0.6

COPY --from=installer ["/dotnet", "/usr/share/dotnet"]

RUN ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet
