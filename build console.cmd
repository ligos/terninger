rem Rebuild
dotnet clean -c Release
dotnet build -c Release

rem Build ZIP file for .NET 4.5.2
del /q Terninger.Console.net452.zip
cd Terninger.Console\bin\Release\net452
"c:\Program Files\7-Zip\7z.exe" a -mx9 ..\..\..\..\Terninger.Console.net452.zip *
cd ..\..\..\..
"c:\Program Files\7-Zip\7z.exe" u Terninger.Console.net452.zip LICENSE.txt


rem Publish for .NET Core 2.1
dotnet publish Terninger.Console -c Release -f netcoreapp2.1 

rem Build ZIP file for .NET Core 2.1
del /q Terninger.Console.netcoreapp21.zip
cd Terninger.Console\bin\Release\netcoreapp2.1\publish
"c:\Program Files\7-Zip\7z.exe" a -mx9 ..\..\..\..\..\Terninger.Console.netcoreapp21.zip *
cd ..\..\..\..\..
"c:\Program Files\7-Zip\7z.exe" u Terninger.Console.netcoreapp21.zip LICENSE.txt


rem Publish for .NET Core 3.0
dotnet publish Terninger.Console -c Release -f netcoreapp3.0

rem Build ZIP file for .NET Core 3.0
del /q Terninger.Console.netcoreapp30.zip
cd Terninger.Console\bin\Release\netcoreapp3.0\publish
"c:\Program Files\7-Zip\7z.exe" a -mx9 ..\..\..\..\..\Terninger.Console.netcoreapp30.zip *
cd ..\..\..\..\..
"c:\Program Files\7-Zip\7z.exe" u Terninger.Console.netcoreapp30.zip LICENSE.txt


rem TOOD: generate hashes and signatures

pause