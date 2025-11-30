rem Rebuild
dotnet clean -c Release

rem Build each package. We just use the package which includes debug symbols.
dotnet pack Terninger.Random.Cypher -c Release /p:RefNugets=True /p:ContinuousIntegrationBuild=true --include-symbols -o releases
dotnet pack Terninger.Random.Pooled -c Release /p:RefNugets=True /p:ContinuousIntegrationBuild=true --include-symbols -o releases
dotnet pack Terninger -c Release /p:RefNugets=True /p:ContinuousIntegrationBuild=true --include-symbols -o releases
dotnet pack Terninger.EntropySources.Extended -c Release /p:RefNugets=True /p:ContinuousIntegrationBuild=true --include-symbols -o releases
dotnet pack Terninger.EntropySources.Network -c Release /p:RefNugets=True /p:ContinuousIntegrationBuild=true --include-symbols -o releases


pause