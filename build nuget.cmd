rem Rebuild
dotnet clean -c Release

SET NUGET_VERSION=0.2.0

rem Build each package. We just use the package which includes debug symbols.
dotnet pack Terninger.Random.Cypher -c Release /p:RefNugets=True --include-symbols -o ../releases
del releases\Terninger.Random.Cypher.%NUGET_VERSION%.nupkg
move releases\Terninger.Random.Cypher.%NUGET_VERSION%.symbols.nupkg releases\Terninger.Random.Cypher.%NUGET_VERSION%.nupkg

dotnet pack Terninger.Random.Pooled -c Release /p:RefNugets=True --include-symbols -o ../releases
del releases\Terninger.Random.Pooled.%NUGET_VERSION%.nupkg
move releases\Terninger.Random.Pooled.%NUGET_VERSION%.symbols.nupkg releases\Terninger.Random.Pooled.%NUGET_VERSION%.nupkg

dotnet pack Terninger -c Release /p:RefNugets=True --include-symbols -o ../releases
del releases\Terninger.%NUGET_VERSION%.nupkg
move releases\Terninger.%NUGET_VERSION%.symbols.nupkg releases\Terninger.%NUGET_VERSION%.nupkg


dotnet pack Terninger.EntropySources.Extended -c Release /p:RefNugets=True --include-symbols -o ../releases
del releases\Terninger.EntropySources.Extended.%NUGET_VERSION%.nupkg
move releases\Terninger.EntropySources.Extended.%NUGET_VERSION%.symbols.nupkg releases\Terninger.EntropySources.Extended.%NUGET_VERSION%.nupkg

dotnet pack Terninger.EntropySources.Network -c Release /p:RefNugets=True --include-symbols -o ../releases
del releases\Terninger.EntropySources.Network.%NUGET_VERSION%.nupkg
move releases\Terninger.EntropySources.Network.%NUGET_VERSION%.symbols.nupkg releases\Terninger.EntropySources.Network.%NUGET_VERSION%.nupkg


pause