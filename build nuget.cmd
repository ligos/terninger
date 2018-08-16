rem Rebuild
dotnet clean -c Release

SET NUGET_VERSION=0.1.0

rem Build each package. We just use the package which includes debug symbols.
dotnet pack Terninger.Random.Cypher -c Release --include-symbols -o ../releases
del releases\Terninger.Random.Cypher.%NUGET_VERSION%.nupkg
move releases\Terninger.Random.Cypher.%NUGET_VERSION%.symbols.nupkg releases\Terninger.Random.Cypher.%NUGET_VERSION%.nupkg

dotnet pack Terninger.Random.Pooled -c Release --include-symbols -o ../releases
del releases\Terninger.Random.Pooled.%NUGET_VERSION%.nupkg
move releases\Terninger.Random.Pooled.%NUGET_VERSION%.symbols.nupkg releases\Terninger.Random.Pooled.%NUGET_VERSION%.nupkg

dotnet pack Terninger -c Release --include-symbols -o ../releases
del releases\Terninger.%NUGET_VERSION%.nupkg
move releases\Terninger.%NUGET_VERSION%.symbols.nupkg releases\Terninger.%NUGET_VERSION%.nupkg

rem This works, but NuGet is now signing packages after upload, which means these signatures become invalid :-/

rem gpg -a -u bitbucket@ligos.net -b releases\Terninger.%NUGET_VERSION%.nupkg
rem IF ERRORLEVEL 1 pause
rem gpg --verify releases\Terninger.%NUGET_VERSION%.nupkg.asc releases\Terninger.%NUGET_VERSION%.nupkg
rem IF ERRORLEVEL 1 pause

rem keybase sign -d -i releases\Terninger.%NUGET_VERSION%.nupkg -o releases\Terninger.%NUGET_VERSION%.nupkg.keybase.asc
rem IF ERRORLEVEL 1 pause
rem keybase verify -d releases\Terninger.%NUGET_VERSION%.nupkg.keybase.asc -i releases\Terninger.%NUGET_VERSION%.nupkg
rem IF ERRORLEVEL 1 pause

rem "c:\Program Files\7-Zip\7z.exe" h -scrc* releases\Terninger.%NUGET_VERSION%.nupkg > releases\Terninger.%NUGET_VERSION%.nupkg.hashes
rem IF ERRORLEVEL 1 pause

rem copy releases\SignatureTemplate.txt + releases\Terninger.%NUGET_VERSION%.nupkg.hashes + releases\BlanksLines.txt + releases\Terninger.%NUGET_VERSION%.nupkg.asc + releases\BlanksLines.txt + releases\Terninger.%NUGET_VERSION%.nupkg.keybase.asc  releases\Terninger.%NUGET_VERSION%.nupkg.signatures.txt
rem del releases\Terninger.%NUGET_VERSION%.nupkg.asc
rem del releases\Terninger.%NUGET_VERSION%.nupkg.keybase.asc
rem del releases\Terninger.%NUGET_VERSION%.nupkg.hashes




dotnet pack Terninger.EntropySources.Extended -c Release --include-symbols -o ../releases
del releases\Terninger.EntropySources.Extended.%NUGET_VERSION%.nupkg
move releases\Terninger.EntropySources.Extended.%NUGET_VERSION%.symbols.nupkg releases\Terninger.EntropySources.Extended.%NUGET_VERSION%.nupkg

dotnet pack Terninger.EntropySources.Network -c Release --include-symbols -o ../releases
del releases\Terninger.EntropySources.Network.%NUGET_VERSION%.nupkg
move releases\Terninger.EntropySources.Network.%NUGET_VERSION%.symbols.nupkg releases\Terninger.EntropySources.Network.%NUGET_VERSION%.nupkg


pause