rem Rebuild
dotnet clean -c Release
dotnet build -c Release

rem Build ZIP file for .NET 4.8
del /q Terninger.Console.net48.zip
cd Terninger.Console\bin\Release\net48
"c:\Program Files\7-Zip\7z.exe" a -mx9 ..\..\..\..\Terninger.Console.net48.zip *
cd ..\..\..\..
"c:\Program Files\7-Zip\7z.exe" u Terninger.Console.net48.zip LICENSE.txt


rem Publish for .NET 6.0
dotnet publish Terninger.Console -c Release -f net60

rem Build ZIP file for .NET 6.0
del /q Terninger.Console.netcoreapp60.zip
cd Terninger.Console\bin\Release\net60\publish
"c:\Program Files\7-Zip\7z.exe" a -mx9 ..\..\..\..\..\Terninger.Console.net60.zip *
cd ..\..\..\..\..
"c:\Program Files\7-Zip\7z.exe" u Terninger.Console.net60.zip LICENSE.txt
"c:\Program Files\7-Zip\7z.exe" d Terninger.Console.net60.zip Terninger.Console.exe.config
"c:\Program Files\7-Zip\7z.exe" d Terninger.Console.net60.zip Terninger.Console.dll.config


rem Publish for .NET 8.0
dotnet publish Terninger.Console -c Release -f net80

rem Build ZIP file for .NET 8.0
del /q Terninger.Console.netcoreapp80.zip
cd Terninger.Console\bin\Release\net80\publish
"c:\Program Files\7-Zip\7z.exe" a -mx9 ..\..\..\..\..\Terninger.Console.net80.zip *
cd ..\..\..\..\..
"c:\Program Files\7-Zip\7z.exe" u Terninger.Console.net80.zip LICENSE.txt
"c:\Program Files\7-Zip\7z.exe" d Terninger.Console.net80.zip Terninger.Console.exe.config
"c:\Program Files\7-Zip\7z.exe" d Terninger.Console.net80.zip Terninger.Console.dll.config

rem TODO: generate hashes and signatures

pause