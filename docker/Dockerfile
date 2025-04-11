FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app
COPY . .
RUN dotnet restore BenchmarkingSandbox.sln
RUN dotnet publish -c Release -o out "Src/Benchmarking.Runner/BenchmarkingSandbox.Runner.csproj"

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/out ./
ENTRYPOINT ["dotnet", "BenchmarkingSandbox.Runner.dll"]
