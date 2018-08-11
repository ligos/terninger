rem Rebuild
dotnet clean -c Debug

rem Runs tests.
dotnet test Terninger.Test -c Debug
dotnet test Terninger.Test.Slow -c Debug

pause