rem Copyright Murray Grant
rem Apache License

rem Signs and generates hashes for a file

rem PGP
gpg -a -u github@ligos.net -b %1
IF ERRORLEVEL 1 pause
gpg --verify %1.asc %1
IF ERRORLEVEL 1 pause

rem KeyBase
keybase sign -d -i %1 -o %1.keybase.asc
IF ERRORLEVEL 1 pause
keybase verify -d %1.keybase.asc -i %1
IF ERRORLEVEL 1 pause

rem Hashes via 7-zip
"c:\Program Files\7-Zip\7z.exe" h -scrc* %1 > %1.hashes
IF ERRORLEVEL 1 pause

copy SignatureTemplate.txt + %1.hashes + BlanksLines.txt + %1.asc + BlanksLines.txt + %1.keybase.asc  %1.signatures.txt
del %1.asc
del %1.keybase.asc
del %1.hashes


