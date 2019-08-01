' Simple1.vbs
' Sample VBScript Echo to display the ComputerName
' Author Guy Thomas http://computerperformance.co.uk/
' Version 1.5 - November 2010
' --------------------------------------------------' 
Option Explicit
Dim strComputer 
strComputer = "LocalHost"
WScript.Echo "Computer: " _
& strComputer
WScript.Quit 
' End of VBScript example.