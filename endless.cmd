@ECHO OFF
setlocal EnableDelayedExpansion
set /a counter=0
:A
if !counter! LSS 100 (
ECHO | set /p=Testrun !counter!: 
dotnet MemoryTest.dll --shutup
set /a counter+=1
GOTO A
)
endlocal