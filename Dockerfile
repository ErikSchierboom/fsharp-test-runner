FROM mcr.microsoft.com/dotnet/core/sdk:3.1-alpine AS build
WORKDIR /app

COPY run.sh /opt/test-runner/bin/

# Copy fsproj and restore as distinct layers
COPY src/Exercism.TestRunner.FSharp/Exercism.TestRunner.FSharp.fsproj ./
RUN dotnet restore

# Copy everything else and build
COPY src/Exercism.TestRunner.FSharp/ ./
RUN dotnet publish -r linux-musl-x64 -c Release -o /opt/test-runner

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/sdk:3.1-alpine AS runtime
WORKDIR /opt/test-runner
COPY --from=build /opt/test-runner/ . 
ENTRYPOINT ["sh", "/opt/test-runner/bin/run.sh"]
