﻿'    Display driver Uninstaller (DDU) a driver uninstaller / Cleaner for Windows
'    Copyright (C) <2013>  <DDU dev team>

'    This program is free software: you can redistribute it and/or modify
'    it under the terms of the GNU General Public License as published by
'    the Free Software Foundation, either version 3 of the License, or
'    (at your option) any later version.

'    This program is distributed in the hope that it will be useful,
'    but WITHOUT ANY WARRANTY; without even the implied warranty of
'    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
'    GNU General Public License for more details.

'    You should have received a copy of the GNU General Public License
'    along with DDU.  If not, see <http://www.gnu.org/licenses/>.
Option Strict On

Imports System.DirectoryServices
Imports Microsoft.Win32
Imports System.IO
Imports System.Security.AccessControl
Imports System.Threading
Imports System.Security.Principal
Imports System.Management
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports System.Reflection
Imports System.Text


Public Class frmMain
	Private WithEvents BackgroundWorker1 As New System.ComponentModel.BackgroundWorker
	Dim arg As String

	Dim backgroundworkcomplete As Boolean = True

	Dim silent As Boolean = False
	Dim argcleanamd As Boolean = False
	Dim argcleanintel As Boolean = False
	Dim argcleannvidia As Boolean = False
	Dim nbclean As Integer = 0
	Dim restart As Boolean = False
	Dim MyIdentity As WindowsIdentity = WindowsIdentity.GetCurrent()
	Dim identity As WindowsIdentity = WindowsIdentity.GetCurrent()
	Dim principal As WindowsPrincipal = New WindowsPrincipal(identity)
	Dim isElevated As Boolean = principal.IsInRole(WindowsBuiltInRole.Administrator)
	Dim processinfo As New ProcessStartInfo
	Dim process As New Process

	Dim reboot As Boolean = False
	Dim shutdown As Boolean = False
	Public Shared baseDir As String = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
	Public Shared win8higher As Boolean = False
	Public win10 As Boolean = False
	Public Shared winxp As Boolean = False
	Dim stopme As Boolean = False

	Dim sysdrv As String = System.Environment.GetEnvironmentVariable("systemdrive").ToLower
	Dim userpth As String = CStr(My.Computer.Registry.LocalMachine.OpenSubKey("software\microsoft\windows nt\currentversion\profilelist").GetValue("ProfilesDirectory")) & "\"
	Dim reply As String = Nothing
	Dim reply2 As String = Nothing
	Dim version As String = Nothing
	Dim position2 As Integer = Nothing
	Dim currentdriverversion As String = Nothing
	Dim safemode As Boolean = False
	Dim CleanupEngine As New CleanupEngine
	Dim enduro As Boolean = False
	Public Shared preventclose As Boolean = False
	Dim closeapp As Boolean = False
	Public ddudrfolder As String
	Public Shared donotremoveamdhdaudiobusfiles As Boolean = True


	Private Sub Checkupdates2()
		If Not Me.Dispatcher.CheckAccess() Then
			Dispatcher.Invoke(New MethodInvoker(AddressOf Checkupdates2))
		Else
			lblUpdate.Content = Languages.GetTranslation(Me.Name, "Label11", "Text")
			Dim updates As Integer = HasUpdates()

			If updates = 1 Then
				lblUpdate.Content = Languages.GetTranslation(Me.Name, "Label11", "Text2")

			ElseIf updates = 2 Then
				lblUpdate.Content = Languages.GetTranslation(Me.Name, "Label11", "Text3")

				If Not MyIdentity.IsSystem Then	 'we dont want to open a webpage when the app is under "System" user.
					Select Case MessageBox.Show(Languages.GetTranslation(Me.Name, "Messages", "Text1"), Application.Current.MainWindow.GetType().Assembly.GetName().Name, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information)
						Case Windows.Forms.DialogResult.Yes
							process.Start("http://www.wagnardmobile.com")
							closeapp = True
							closeddu()
							Exit Sub
						Case Windows.Forms.DialogResult.No
							MessageBox.Show(Languages.GetTranslation(Me.Name, "Messages", "Text2"), Application.Current.MainWindow.GetType().Assembly.GetName().Name, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information)
						Case Windows.Forms.DialogResult.Cancel
							closeapp = True
							closeddu()
							Exit Sub
					End Select

				End If

			ElseIf updates = 3 Then
				lblUpdate.Content = Languages.GetTranslation(Me.Name, "Label11", "Text4")
			End If
		End If
	End Sub

	Private Function HasUpdates() As Integer
		Return 3  'TODO: REMOVE THIS LINE!!!! Blocked updates for faster debugging

		Try
			If Not My.Computer.Network.IsAvailable Then
				Return 3
			End If
		Catch ex As Exception
		End Try

		Try
			Dim request2 As System.Net.HttpWebRequest = CType(System.Net.HttpWebRequest.Create("http://www.wagnardmobile.com/DDU/currentversion2.txt"), Net.HttpWebRequest)
			Dim response2 As System.Net.HttpWebResponse = Nothing
			request2.Timeout = 2500

			Try
				response2 = CType(request2.GetResponse(), Net.HttpWebResponse)
			Catch ex As Exception
				request2 = CType(System.Net.HttpWebRequest.Create("http://archive.sunet.se/pub/games/PC/guru3d/ddu/currentversion2.txt"), Net.HttpWebRequest)
			End Try

			request2.Timeout = 2500
			response2 = CType(request2.GetResponse(), Net.HttpWebResponse)

			Dim newestversion2 As String = ""

			Using sr As System.IO.StreamReader = New System.IO.StreamReader(response2.GetResponseStream())
				newestversion2 = sr.ReadToEnd()

				sr.Close()
			End Using


			Dim newestversion2int As Integer = CInt(newestversion2.Replace(".", ""))
			Dim exeversion As String = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString().Replace(".", "")
			Dim applicationversion As Integer = CInt(exeversion)

			If newestversion2int <= applicationversion Then
				Return 1
			Else
				Return 2
			End If

		Catch ex As Exception
			MsgBox(ex)
			Return 3
		End Try
	End Function

	Private Sub cleandriverstore(ByVal config As ThreadSettings)
		Dim catalog As String = ""
		Dim CurrentProvider As String = ""
		UpdateTextMethod("-Executing Driver Store cleanUP(finding OEM step)...")
		Application.Log.AddMessage("Executing Driver Store cleanUP(Find OEM)...")
		'Check the driver from the driver store  ( oemxx.inf)

		Dim deloem As New Diagnostics.ProcessStartInfo
		deloem.FileName = baseDir & "\" & ddudrfolder & "\ddudr.exe"
		Dim proc3 As New Diagnostics.Process
		
		UpdateTextMethod(UpdateTextMethodmessagefn(0))

		Select Case config.SelectedGPU
			Case GPUVendor.Nvidia
				CurrentProvider = "NVIDIA"
			Case GPUVendor.AMD
				CurrentProvider = "AdvancedMicroDevices"
			Case GPUVendor.Intel
				CurrentProvider = "Intel"
		End Select

		For Each oem As OemINF In GetOemInfList(Application.Paths.WinDir & "inf\")

			If Not oem.IsValid Then
				Continue For
			End If

			If StrContainsAny(oem.Provider, True, CurrentProvider) Or
			   oem.Provider.ToLower.StartsWith("atitech") Or
			   StrContainsAny(oem.Provider, True, "amd") Then

				deloem.Arguments = "dp_delete " + oem.FileName

				'We can force the OEMs removal if they are of Display or Media class.
				If StrContainsAny(oem.Class, True, "display") Or
				   StrContainsAny(oem.Class, True, "media") Then
					deloem.Arguments = "-f dp_delete " + oem.FileName
				End If
			Else
				Continue For
			End If

			'before removing the oem we try to get the original inf name (win8+)
			If win8higher Then
				Try
				catalog = My.Computer.Registry.LocalMachine.OpenSubKey("DRIVERS\DriverDatabase\DriverInfFiles\" & oem.FileName) .GetValue("Active").ToString
					catalog = catalog.Substring(0, catalog.IndexOf("inf_") + 3)
				Catch ex As Exception
					catalog = ""
				End Try
			End If

			'Uninstall Driver from driver store  delete from (oemxx.inf)

			Application.Log.AddMessage(deloem.Arguments)

			deloem.UseShellExecute = False
			deloem.CreateNoWindow = True
			deloem.RedirectStandardOutput = True
			'creation dun process fantome pour le wait on exit.

			proc3.StartInfo = deloem
			proc3.Start()
			reply2 = proc3.StandardOutput.ReadToEnd
			'proc3.WaitForExit()
			proc3.StandardOutput.Close()
			proc3.Close()
			UpdateTextMethod(reply2)
			Application.Log.AddMessage(reply2)
			'check if the oem was removed to process to the pnplockdownfile if necessary
			If win8higher AndAlso (Not System.IO.File.Exists(Environment.GetEnvironmentVariable("windir") & "\inf\" + oem.FileName)) AndAlso (Not IsNullOrWhitespace(catalog)) Then
				CleanupEngine.prePnplockdownfiles(catalog)
			End If
		Next
		UpdateTextMethod("-Driver Store cleanUP complete.")

		Application.Log.AddMessage("Driver Store CleanUP Complete.")

	End Sub

	Private Sub cleanamdserviceprocess()


		CleanupEngine.cleanserviceprocess(IO.File.ReadAllLines(baseDir & "\settings\AMD\services.cfg"))	'// add each line as String Array.

		Dim killpid As New ProcessStartInfo
		killpid.FileName = "cmd.exe"
		killpid.Arguments = " /C" & "taskkill /f /im CLIStart.exe"
		killpid.UseShellExecute = False
		killpid.CreateNoWindow = True
		killpid.RedirectStandardOutput = False

		Dim processkillpid As New Process
		processkillpid.StartInfo = killpid
		processkillpid.Start()
		processkillpid.WaitForExit()
		processkillpid.Close()

		Dim appproc = process.GetProcessesByName("MOM")
		For i As Integer = 0 To appproc.Length - 1
			appproc(i).Kill()
		Next i

		appproc = process.GetProcessesByName("CLIStart")
		For i As Integer = 0 To appproc.Length - 1
			appproc(i).Kill()
		Next i

		appproc = process.GetProcessesByName("CLI")
		For i As Integer = 0 To appproc.Length - 1
			appproc(i).Kill()
		Next i

		appproc = process.GetProcessesByName("CCC")
		For i As Integer = 0 To appproc.Length - 1
			appproc(i).Kill()
		Next i

		appproc = process.GetProcessesByName("Cnext")
		For i As Integer = 0 To appproc.Length - 1
			appproc(i).Kill()
		Next i

		appproc = process.GetProcessesByName("HydraDM")
		For i As Integer = 0 To appproc.Length - 1
			appproc(i).Kill()
		Next i

		appproc = process.GetProcessesByName("HydraDM64")
		For i As Integer = 0 To appproc.Length - 1
			appproc(i).Kill()
		Next i

		appproc = process.GetProcessesByName("HydraGrd")
		For i As Integer = 0 To appproc.Length - 1
			appproc(i).Kill()
		Next i

		appproc = process.GetProcessesByName("Grid64")
		For i As Integer = 0 To appproc.Length - 1
			appproc(i).Kill()
		Next i

		appproc = process.GetProcessesByName("HydraMD64")
		For i As Integer = 0 To appproc.Length - 1
			appproc(i).Kill()
		Next i

		appproc = process.GetProcessesByName("HydraMD")
		For i As Integer = 0 To appproc.Length - 1
			appproc(i).Kill()
		Next i

		appproc = process.GetProcessesByName("ThumbnailExtractionHost")
		For i As Integer = 0 To appproc.Length - 1
			appproc(i).Kill()
		Next i

		appproc = process.GetProcessesByName("jusched")
		For i As Integer = 0 To appproc.Length - 1
			appproc(i).Kill()
		Next i

		System.Threading.Thread.Sleep(10)
	End Sub

	Private Sub cleanamdfolders(ByVal config As ThreadSettings)
		Dim filePath As String = Nothing
		Dim removedxcache As Boolean = config.RemoveCrimsonCache
		'Delete AMD data Folders
		UpdateTextMethod(UpdateTextMethodmessagefn(1))

		Application.Log.AddMessage("Cleaning Directory (Please Wait...)")


		If config.RemoveAMDDirs Then
			filePath = sysdrv + "\AMD"

			Try
				deletedirectory(filePath)
			Catch ex As Exception
				Application.Log.AddException(ex)
				TestDelete(filePath)
			End Try
		End If

		'Delete driver files
		'delete OpenCL

		CleanupEngine.folderscleanup(IO.File.ReadAllLines(baseDir & "\settings\AMD\driverfiles.cfg")) '// add each line as String Array.



		filePath = Environment.GetEnvironmentVariable("windir")
		Try
			deletefile(filePath + "\atiogl.xml")
		Catch ex As Exception
		End Try

		filePath = Environment.GetEnvironmentVariable("windir")
		Try
			deletefile(filePath + "\ativpsrm.bin")
		Catch ex As Exception
		End Try


		filePath = Environment.GetFolderPath _
		 (Environment.SpecialFolder.ProgramFiles) + "\ATI Technologies"
		If Directory.Exists(filePath) Then

			For Each child As String In Directory.GetDirectories(filePath)
				If IsNullOrWhitespace(child) = False Then
					If child.ToLower.Contains("ati.ace") Or
					   child.ToLower.Contains("ati catalyst control center") Or
					   child.ToLower.Contains("application profiles") Or
					   child.ToLower.EndsWith("\px") Or
					   child.ToLower.Contains("hydravision") Then
						Try
							deletedirectory(child)
						Catch ex As Exception
							Application.Log.AddException(ex)
							TestDelete(child)
						End Try
					End If
				End If
			Next
			If Directory.GetDirectories(filePath).Length = 0 Then
				Try
					deletedirectory(filePath)
				Catch ex As Exception
					Application.Log.AddException(ex)
					TestDelete(filePath)
				End Try
			Else
				For Each data As String In Directory.GetDirectories(filePath)
					Application.Log.AddWarningMessage("Remaining folders found " + " : " + data)
				Next

			End If
		End If



		filePath = Environment.GetFolderPath _
		 (Environment.SpecialFolder.ProgramFiles) + "\ATI"
		If Directory.Exists(filePath) Then
			For Each child As String In Directory.GetDirectories(filePath)
				If IsNullOrWhitespace(child) = False Then
					If child.ToLower.Contains("cim") Then
						Try
							deletedirectory(child)
						Catch ex As Exception
							Application.Log.AddException(ex)
							TestDelete(child)
						End Try
					End If
				End If
			Next
			If Directory.GetDirectories(filePath).Length = 0 Then
				Try
					deletedirectory(filePath)
				Catch ex As Exception
					Application.Log.AddException(ex)
					TestDelete(filePath)
				End Try
			Else
				For Each data As String In Directory.GetDirectories(filePath)
					Application.Log.AddWarningMessage("Remaining folders found " + " : " + data)
				Next

			End If
		End If


		filePath = Environment.GetFolderPath _
		  (Environment.SpecialFolder.ProgramFiles) + "\Common Files" + "\ATI Technologies"
		If Directory.Exists(filePath) Then
			For Each child As String In Directory.GetDirectories(filePath)
				If IsNullOrWhitespace(child) = False Then
					If child.ToLower.Contains("multimedia") Then
						Try
							deletedirectory(child)
						Catch ex As Exception
							Application.Log.AddException(ex)
							TestDelete(child)
						End Try
					End If
				End If
			Next
			If Directory.GetDirectories(filePath).Length = 0 Then
				Try
					deletedirectory(filePath)
					'on success, do this

				Catch ex As Exception
					Application.Log.AddException(ex)
					TestDelete(filePath)
				End Try
			Else
				For Each data As String In Directory.GetDirectories(filePath)
					Application.Log.AddWarningMessage("Remaining folders found " + " : " + data)
				Next

			End If
		End If

		filePath = Environment.GetFolderPath _
		 (Environment.SpecialFolder.ProgramFiles) + "\AMD APP"
		If Directory.Exists(filePath) Then
			Try
				deletedirectory(filePath)
			Catch ex As Exception
				Application.Log.AddException(ex)
				TestDelete(filePath)
			End Try
		End If

		If IntPtr.Size = 8 Then

			filePath = Environment.GetFolderPath _
				 (Environment.SpecialFolder.ProgramFiles) + " (x86)" + "\AMD AVT"
			If Directory.Exists(filePath) Then
				Try
					deletedirectory(filePath)
				Catch ex As Exception
					Application.Log.AddException(ex)
					TestDelete(filePath)
				End Try
			End If

			filePath = Environment.GetFolderPath _
			 (Environment.SpecialFolder.ProgramFiles) + " (x86)" + "\ATI Technologies"
			If Directory.Exists(filePath) Then
				Try
					For Each child As String In Directory.GetDirectories(filePath)
						If IsNullOrWhitespace(child) = False Then
							If child.ToLower.Contains("ati.ace") Or
							 child.ToLower.Contains("ati catalyst control center") Or
							 child.ToLower.Contains("application profiles") Or
							 child.ToLower.EndsWith("\px") Or
							 child.ToLower.Contains("hydravision") Then
								Try
									deletedirectory(child)
								Catch ex As Exception
									Application.Log.AddException(ex)
									TestDelete(child)
								End Try
							End If
						End If
					Next
					If Directory.GetDirectories(filePath).Length = 0 Then
						Try
							deletedirectory(filePath)
						Catch ex As Exception
							Application.Log.AddException(ex)
							TestDelete(filePath)
						End Try
					Else
						For Each data As String In Directory.GetDirectories(filePath)
							Application.Log.AddWarningMessage("Remaining folders found " + " : " + data)
						Next

					End If
				Catch ex As Exception
				End Try
			End If

			filePath = System.Environment.SystemDirectory
			Dim files() As String = IO.Directory.GetFiles(filePath + "\", "coinst_*.*")
			For i As Integer = 0 To files.Length - 1
				If Not IsNullOrWhitespace(files(i)) Then
					Try
						deletefile(files(i))
					Catch ex As Exception
					End Try
				End If
			Next

			filePath = Environment.GetFolderPath _
			   (Environment.SpecialFolder.ProgramFiles) + " (x86)" + "\AMD APP"
			If Directory.Exists(filePath) Then
				Try
					deletedirectory(filePath)
				Catch ex As Exception
					Application.Log.AddException(ex)
					TestDelete(filePath)
				End Try
			End If

			filePath = Environment.GetFolderPath _
		 (Environment.SpecialFolder.ProgramFiles) + " (x86)" + "\AMD\SteadyVideo"
			If Directory.Exists(filePath) Then
				Try
					TestDelete(filePath)
				Catch ex As Exception
					Application.Log.AddException(ex)
				End Try
			End If

			filePath = Environment.GetFolderPath _
			(Environment.SpecialFolder.ProgramFiles) + " (x86)" + "\AMD\SteadyVideoFirefox"
			If Directory.Exists(filePath) Then
				Try
					TestDelete(filePath)
				Catch ex As Exception
					Application.Log.AddException(ex)
				End Try
			End If

			filePath = Environment.GetFolderPath _
		 (Environment.SpecialFolder.ProgramFiles) + " (x86)" + "\AMD\SteadyVideoChrome"
			If Directory.Exists(filePath) Then
				Try
					TestDelete(filePath)
				Catch ex As Exception
					Application.Log.AddException(ex)
				End Try
			End If

			filePath = Environment.GetFolderPath _
			 (Environment.SpecialFolder.ProgramFiles) + " (x86)" + "\Common Files" + "\ATI Technologies"
			If Directory.Exists(filePath) Then
				For Each child As String In Directory.GetDirectories(filePath)
					If IsNullOrWhitespace(child) = False Then
						If child.ToLower.Contains("multimedia") Then
							Try
								deletedirectory(child)
							Catch ex As Exception
								Application.Log.AddException(ex)
								TestDelete(child)
							End Try
						End If
					End If
				Next
				Try
					If Directory.GetDirectories(filePath).Length = 0 Then
						Try
							deletedirectory(filePath)
						Catch ex As Exception
							Application.Log.AddException(ex)
							TestDelete(filePath)
						End Try
					Else
						For Each data As String In Directory.GetDirectories(filePath)
							Application.Log.AddWarningMessage("Remaining folders found " + " : " + data)
						Next

					End If
				Catch ex As Exception
				End Try
			End If
		End If


		filePath = Environment.GetFolderPath _
	  (Environment.SpecialFolder.CommonApplicationData) + "\Microsoft\Windows\Start Menu\Programs\Catalyst Control Center"
		If Directory.Exists(filePath) Then
			Try
				deletedirectory(filePath)
			Catch ex As Exception
				TestDelete(filePath)
			End Try
		End If


		filePath = Environment.GetFolderPath _
	  (Environment.SpecialFolder.CommonApplicationData) + "\Microsoft\Windows\Start Menu\Programs\AMD Catalyst Control Center"
		If Directory.Exists(filePath) Then
			Try
				deletedirectory(filePath)
			Catch ex As Exception
				TestDelete(filePath)
			End Try
		End If

		filePath = Environment.GetFolderPath _
	  (Environment.SpecialFolder.CommonApplicationData) + "\ATI"
		If Directory.Exists(filePath) Then
			For Each child As String In My.Computer.FileSystem.GetDirectories(filePath)
				If IsNullOrWhitespace(child) = False Then
					If child.ToLower.Contains("ace") Then
						Try
							deletedirectory(child)
						Catch ex As Exception
							Application.Log.AddException(ex)
							TestDelete(child)
						End Try
					End If
				End If
			Next
			If Directory.GetDirectories(filePath).Length = 0 Then
				Try
					deletedirectory(filePath)
				Catch ex As Exception
					Application.Log.AddException(ex)
					TestDelete(filePath)
				End Try
			Else
				For Each data As String In Directory.GetDirectories(filePath)
					Application.Log.AddWarningMessage("Remaining folders found " + " : " + data)
				Next

			End If
		End If

		filePath = Environment.GetFolderPath _
	  (Environment.SpecialFolder.CommonApplicationData) + "\AMD"
		If Directory.Exists(filePath) Then
			For Each child As String In My.Computer.FileSystem.GetDirectories(filePath)
				If IsNullOrWhitespace(child) = False Then
					If child.ToLower.Contains("kdb") Or _
					   child.ToLower.Contains("fuel") Then
						Try
							deletedirectory(child)
						Catch ex As Exception
							Application.Log.AddException(ex)
							TestDelete(child)
						End Try
					End If
				End If
			Next
			If Directory.GetDirectories(filePath).Length = 0 Then
				Try
					deletedirectory(filePath)
				Catch ex As Exception
					Application.Log.AddException(ex)
					TestDelete(filePath)
				End Try
			Else
				For Each data As String In Directory.GetDirectories(filePath)
					Application.Log.AddWarningMessage("Remaining folders found " + " : " + data)
				Next

			End If
		End If

		For Each filepaths As String In Directory.GetDirectories(IO.Path.GetDirectoryName(userpth))
			filePath = filepaths + "\AppData\Roaming\ATI"
			If winxp Then
				filePath = filepaths + "\Application Data\ATI"
			End If
			If Directory.Exists(filePath) Then
				Try
					For Each child As String In My.Computer.FileSystem.GetDirectories(filePath)
						If IsNullOrWhitespace(child) = False Then
							If child.ToLower.Contains("ace") Then
								Try
									deletedirectory(child)
								Catch ex As Exception
									Application.Log.AddException(ex)
									TestDelete(child)
								End Try
							End If
						End If
					Next
					If Directory.GetDirectories(filePath).Length = 0 Then
						Try
							deletedirectory(filePath)
						Catch ex As Exception
							Application.Log.AddException(ex)
							TestDelete(filePath)
						End Try
					Else
						For Each data As String In Directory.GetDirectories(filePath)
							Application.Log.AddWarningMessage("Remaining folders found " + " : " + data)
						Next

					End If
				Catch ex As Exception
					Application.Log.AddMessage("Possible permission issue detected on : " + filePath)
				End Try
			End If


			filePath = filepaths + "\AppData\Local\ATI"
			If winxp Then
				filePath = filepaths + "\Local Settings\Application Data\ATI"
			End If
			If Directory.Exists(filePath) Then
				Try
					For Each child As String In My.Computer.FileSystem.GetDirectories(filePath)
						If IsNullOrWhitespace(child) = False Then
							If child.ToLower.Contains("ace") Then
								Try
									deletedirectory(child)
								Catch ex As Exception
									Application.Log.AddException(ex)
									TestDelete(child)
								End Try
							End If
						End If
					Next
					If Directory.GetDirectories(filePath).Length = 0 Then
						Try
							deletedirectory(filePath)
						Catch ex As Exception
							Application.Log.AddException(ex)
							TestDelete(filePath)
						End Try
					Else
						For Each data As String In Directory.GetDirectories(filePath)
							Application.Log.AddWarningMessage("Remaining folders found " + " : " + data)
						Next

					End If
				Catch ex As Exception
					Application.Log.AddMessage("Possible permission issue detected on : " + filePath)
				End Try
			End If

			filePath = filepaths + "\AppData\Local\AMD"
			If winxp Then
				filePath = filepaths + "\Local Settings\Application Data\AMD"
			End If
			If Directory.Exists(filePath) Then
				Try
					For Each child As String In My.Computer.FileSystem.GetDirectories(filePath)
						If IsNullOrWhitespace(child) = False Then
							If child.ToLower.Contains("cn") Or
							 child.ToLower.Contains("fuel") Or _
							 removedxcache AndAlso child.ToLower.Contains("dxcache") Or _
							 removedxcache AndAlso child.ToLower.Contains("glcache") Then
								Try
									deletedirectory(child)
								Catch ex As Exception
									Application.Log.AddException(ex)
									TestDelete(child)
								End Try
							End If
						End If
					Next
					If Directory.GetDirectories(filePath).Length = 0 Then
						Try
							deletedirectory(filePath)
						Catch ex As Exception
							Application.Log.AddException(ex)
							TestDelete(filePath)
						End Try
					Else
						For Each data As String In Directory.GetDirectories(filePath)
							Application.Log.AddWarningMessage("Remaining folders found " + " : " + data)
						Next

					End If
				Catch ex As Exception
					Application.Log.AddMessage("Possible permission issue detected on : " + filePath)
				End Try
			End If

		Next

		'starting with AMD  14.12 Omega driver folders

		filePath = Environment.GetFolderPath _
		 (Environment.SpecialFolder.ProgramFiles) + "\AMD"
		If Directory.Exists(filePath) Then
			For Each child As String In Directory.GetDirectories(filePath)
				If IsNullOrWhitespace(child) = False Then
					If child.ToLower.Contains("amdkmpfd") Or
					 child.ToLower.Contains("cnext") Or
					 child.ToLower.Contains("steadyvideo") Or
					 child.ToLower.Contains("920dec42-4ca5-4d1d-9487-67be645cddfc") Or
					   child.ToLower.Contains("cim") Then
						Try
							deletedirectory(child)
						Catch ex As Exception
							Application.Log.AddException(ex)
							TestDelete(child)
						End Try
					End If
				End If
			Next
			Try
				If Directory.GetDirectories(filePath).Length = 0 Then
					Try
						deletedirectory(filePath)
					Catch ex As Exception
						Application.Log.AddException(ex)
						TestDelete(filePath)
					End Try
				Else
					For Each data As String In Directory.GetDirectories(filePath)
						Application.Log.AddWarningMessage("Remaining folders found " + " : " + data)
					Next

				End If
			Catch ex As Exception
			End Try
		End If

		filePath = Environment.GetFolderPath _
	   (Environment.SpecialFolder.ProgramFiles) + " (x86)" + "\AMD"
		If Directory.Exists(filePath) Then

			For Each child As String In Directory.GetDirectories(filePath)
				If IsNullOrWhitespace(child) = False Then
					If child.ToLower.Contains("ati.ace") Or _
					   child.ToLower.Contains("cnext") Then
						Try
							deletedirectory(child)
						Catch ex As Exception
							Application.Log.AddException(ex)
							TestDelete(child)
						End Try
					End If
				End If
			Next
			If Directory.GetDirectories(filePath).Length = 0 Then
				Try
					deletedirectory(filePath)
				Catch ex As Exception
					Application.Log.AddException(ex)
					TestDelete(filePath)
				End Try
			Else
				For Each data As String In Directory.GetDirectories(filePath)
					Application.Log.AddWarningMessage("Remaining folders found " + " : " + data)
				Next

			End If
		End If

		'Cleaning the CCC assemblies.


		filePath = Environment.GetEnvironmentVariable("windir") + "\assembly\NativeImages_v4.0.30319_64"
		If Directory.Exists(filePath) Then
			For Each child As String In Directory.GetDirectories(filePath)
				If IsNullOrWhitespace(child) = False Then
					If child.ToLower.EndsWith("\mom") Or
					 child.ToLower.Contains("\mom.") Or
					 child.ToLower.Contains("newaem.foundation") Or
					 child.ToLower.Contains("fuel.foundation") Or
					 child.ToLower.Contains("\localizatio") Or
					 child.ToLower.EndsWith("\log") Or
					 child.ToLower.Contains("log.foundat") Or
					 child.ToLower.EndsWith("\cli") Or
					 child.ToLower.Contains("\cli.") Or
					 child.ToLower.Contains("ace.graphi") Or
					 child.ToLower.Contains("adl.foundation") Or
					 child.ToLower.Contains("64\aem.") Or
					 child.ToLower.Contains("aticccom") Or
					 child.ToLower.EndsWith("\ccc") Or
					 child.ToLower.Contains("\ccc.") Or
					 child.ToLower.Contains("\pckghlp.") Or
					 child.ToLower.Contains("\resourceman") Or
					 child.ToLower.Contains("\apm.") Or
					 child.ToLower.Contains("\a4.found") Or
					 child.ToLower.Contains("\atixclib") Or
					   child.ToLower.Contains("\dem.") Then
						Try
							deletedirectory(child)
						Catch ex As Exception
							Application.Log.AddException(ex)
							TestDelete(child)
						End Try
					End If
				End If
			Next
		End If

		filePath = Environment.GetEnvironmentVariable("windir") + "\assembly\GAC_MSIL"
		If Directory.Exists(filePath) Then
			For Each child As String In Directory.GetDirectories(filePath)
				If IsNullOrWhitespace(child) = False Then
					If child.ToLower.EndsWith("\mom") Or
					 child.ToLower.Contains("\mom.") Or
					 child.ToLower.Contains("newaem.foundation") Or
					 child.ToLower.Contains("fuel.foundation") Or
					 child.ToLower.Contains("\localizatio") Or
					 child.ToLower.EndsWith("\log") Or
					 child.ToLower.Contains("log.foundat") Or
					 child.ToLower.EndsWith("\cli") Or
					 child.ToLower.Contains("\cli.") Or
					 child.ToLower.Contains("ace.graphi") Or
					 child.ToLower.Contains("adl.foundation") Or
					 child.ToLower.Contains("64\aem.") Or
					 child.ToLower.Contains("msil\aem.") Or
					 child.ToLower.Contains("aticccom") Or
					 child.ToLower.EndsWith("\ccc") Or
					 child.ToLower.Contains("\ccc.") Or
					 child.ToLower.Contains("\pckghlp.") Or
					 child.ToLower.Contains("\resourceman") Or
					 child.ToLower.Contains("\apm.") Or
					 child.ToLower.Contains("\a4.found") Or
					 child.ToLower.Contains("\atixclib") Or
					 child.ToLower.Contains("\dem.") Then
						Try
							deletedirectory(child)
						Catch ex As Exception
							Application.Log.AddException(ex)
							TestDelete(child)
						End Try
					End If
				End If
			Next
		End If

	End Sub

	Private Sub cleanamd(ByVal config As ThreadSettings)

		Dim regkey As RegistryKey = Nothing
		Dim subregkey As RegistryKey = Nothing
		Dim subregkey2 As RegistryKey = Nothing
		Dim wantedvalue As String = Nothing
		Dim wantedvalue2 As String = Nothing
		Dim superkey As RegistryKey = Nothing
		Dim filePath As String = Nothing
		Dim packages As String()

		UpdateTextMethod(UpdateTextMethodmessagefn(2))
		Application.Log.AddMessage("Cleaning known Regkeys")


		'Delete AMD regkey
		'Deleting DCOM object

		Application.Log.AddMessage("Starting dcom/clsid/appid/typelib cleanup")

		CleanupEngine.classroot(IO.File.ReadAllLines(baseDir & "\settings\AMD\classroot.cfg")) '// add each line as String Array.


		'-----------------
		'interface cleanup
		'-----------------



		CleanupEngine.interfaces(IO.File.ReadAllLines(baseDir & "\settings\AMD\interface.cfg"))	'// add each line as String Array.

		Application.Log.AddMessage("Instance class cleanUP")
		Try
			regkey = My.Computer.Registry.ClassesRoot.OpenSubKey("CLSID", False)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames()
					If IsNullOrWhitespace(child) = False Then
						subregkey = My.Computer.Registry.ClassesRoot.OpenSubKey("CLSID\" & child, False)
						If subregkey IsNot Nothing Then
							subregkey2 = My.Computer.Registry.ClassesRoot.OpenSubKey("CLSID\" & child & "\Instance", False)
							If subregkey2 IsNot Nothing Then
								For Each child2 As String In subregkey2.GetSubKeyNames()
									If IsNullOrWhitespace(child2) = False Then
										superkey = My.Computer.Registry.ClassesRoot.OpenSubKey("CLSID\" & child & "\Instance\" & child2)
										If superkey IsNot Nothing Then
											If IsNullOrWhitespace(CStr(superkey.GetValue("FriendlyName"))) = False Then
												wantedvalue2 = superkey.GetValue("FriendlyName").ToString
												If wantedvalue2.ToLower.Contains("ati mpeg") Or
												 wantedvalue2.ToLower.Contains("amd mjpeg") Or
												 wantedvalue2.ToLower.Contains("ati ticker") Or
												 wantedvalue2.ToLower.Contains("mmace softemu") Or
												 wantedvalue2.ToLower.Contains("mmace deinterlace") Or
												 wantedvalue2.ToLower.Contains("amd video") Or
												 wantedvalue2.ToLower.Contains("mmace procamp") Or
												 wantedvalue2.ToLower.Contains("ati video") Then
													Try
														deletesubregkey(My.Computer.Registry.ClassesRoot, "CLSID\" & child & "\Instance\" & child2)
													Catch ex As Exception
													End Try
												End If
											End If
										End If
									End If
								Next
							End If
						End If
					End If
				Next
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		If IntPtr.Size = 8 Then
			Try
				regkey = My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\CLSID", False)
				If regkey IsNot Nothing Then
					For Each child As String In regkey.GetSubKeyNames()
						If IsNullOrWhitespace(child) = False Then
							subregkey = My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\CLSID\" & child, False)
							If subregkey IsNot Nothing Then
								subregkey2 = My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\CLSID\" & child & "\Instance", False)
								If subregkey2 IsNot Nothing Then
									For Each child2 As String In subregkey2.GetSubKeyNames()
										If IsNullOrWhitespace(child2) = False Then
											superkey = My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\CLSID\" & child & "\Instance\" & child2)
											If superkey IsNot Nothing Then
												If IsNullOrWhitespace(CStr(superkey.GetValue("FriendlyName"))) = False Then
													wantedvalue2 = superkey.GetValue("FriendlyName").ToString
													If wantedvalue2.ToLower.Contains("ati mpeg") Or
													wantedvalue2.ToLower.Contains("amd mjpeg") Or
													wantedvalue2.ToLower.Contains("ati ticker") Or
													wantedvalue2.ToLower.Contains("mmace softemu") Or
													wantedvalue2.ToLower.Contains("mmace deinterlace") Or
													wantedvalue2.ToLower.Contains("mmace procamp") Or
													wantedvalue2.ToLower.Contains("amd video") Or
													wantedvalue2.ToLower.Contains("ati video") Then
														Try
															deletesubregkey(My.Computer.Registry.ClassesRoot, "Wow6432Node\CLSID\" & child & "\Instance\" & child2)
														Catch ex As Exception
														End Try
													End If
												End If
											End If
										End If
									Next
								End If
							End If
						End If
					Next
				End If
			Catch ex As Exception
				Application.Log.AddException(ex)
			End Try
		End If

		Application.Log.AddMessage("MediaFoundation cleanUP")
		Try
			regkey = My.Computer.Registry.ClassesRoot.OpenSubKey("MediaFoundation\Transforms", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames()
					If IsNullOrWhitespace(child) = False Then

						If Not IsNullOrWhitespace(CStr(regkey.OpenSubKey(child).GetValue(""))) Then
							If regkey.OpenSubKey(child).GetValue("").ToString.ToLower.Contains("amd d3d11 hardware mft") Or
							  regkey.OpenSubKey(child).GetValue("").ToString.ToLower.Contains("amd fast (dnd) decoder") Or
							   regkey.OpenSubKey(child).GetValue("").ToString.ToLower.Contains("amd h.264 hardware mft encoder") Or
							  regkey.OpenSubKey(child).GetValue("").ToString.ToLower.Contains("amd playback decoder mft") Then

								For Each child2 As String In regkey.OpenSubKey("Categories", False).GetSubKeyNames
									Try
										deletesubregkey(regkey.OpenSubKey("Categories\" & child2, True), child)
									Catch ex As Exception
									End Try
								Next

								Try
									deletesubregkey(regkey, child)
								Catch ex As Exception
								End Try
							End If
						End If
					End If
				Next
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		If IntPtr.Size = 8 Then
			Try
				regkey = My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\MediaFoundation\Transforms", True)
				If regkey IsNot Nothing Then
					For Each child As String In regkey.GetSubKeyNames()
						If IsNullOrWhitespace(child) = False Then

							If Not IsNullOrWhitespace(CStr(regkey.OpenSubKey(child).GetValue(""))) Then
								If regkey.OpenSubKey(child).GetValue("").ToString.ToLower.Contains("amd d3d11 hardware mft") Or
								 regkey.OpenSubKey(child).GetValue("").ToString.ToLower.Contains("amd fast (dnd) decoder") Or
								 regkey.OpenSubKey(child).GetValue("").ToString.ToLower.Contains("amd h.264 hardware mft encoder") Or
								 regkey.OpenSubKey(child).GetValue("").ToString.ToLower.Contains("amd playback decoder mft") Then

									For Each child2 As String In regkey.OpenSubKey("Categories", False).GetSubKeyNames
										Try
											deletesubregkey(regkey.OpenSubKey("Categories\" & child2, True), child)
										Catch ex As Exception
										End Try
									Next

									Try
										deletesubregkey(regkey, child)
									Catch ex As Exception
									End Try
								End If
							End If
						End If
					Next
				End If
			Catch ex As Exception
				Application.Log.AddException(ex)
			End Try
		End If

		Application.Log.AddMessage("AppID and clsidleftover cleanUP")
		'old dcom 

		CleanupEngine.clsidleftover(IO.File.ReadAllLines(baseDir & "\settings\AMD\clsidleftover.cfg")) '// add each line as String Array.

		Application.Log.AddMessage("Record CleanUP")

		'--------------
		'Record cleanup
		'--------------
		Try
			regkey = My.Computer.Registry.ClassesRoot.OpenSubKey("Record", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames()
					If IsNullOrWhitespace(child) = False Then
						subregkey = regkey.OpenSubKey(child)
						If subregkey IsNot Nothing Then
							For Each childs As String In subregkey.GetSubKeyNames()
								If IsNullOrWhitespace(childs) = False Then
									Try
										If IsNullOrWhitespace(CStr(subregkey.OpenSubKey(childs, False).GetValue("Assembly"))) = False Then
											If subregkey.OpenSubKey(childs, False).GetValue("Assembly").ToString.ToLower.Contains("aticccom") Then
												deletesubregkey(regkey, child)
											End If
										End If
									Catch ex As Exception
										Continue For
									End Try
								End If
							Next
						End If
					End If
				Next
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try
		Application.Log.AddMessage("Assembly CleanUP")

		'------------------
		'Assemblies cleanUP
		'------------------
		Try
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey("Software\Classes\Installer\Assemblies", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames()
					If IsNullOrWhitespace(child) = False Then
						If child.ToLower.Contains("ati.ace") Then
							Try
								deletesubregkey(regkey, child)
							Catch ex As Exception
							End Try

						End If
					End If
				Next
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		'----------------------
		'End Assemblies cleanUP
		'----------------------


		'end of decom?

		Try
			deletesubregkey(My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Controls Folder\" &
						"Display\shellex\PropertySheetHandlers", True), "ATIACE")
		Catch ex As Exception
		End Try


		'remove opencl registry Khronos

		Try
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey("Software\Khronos\OpenCL\Vendors", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetValueNames()
					If IsNullOrWhitespace(child) = False Then
						If child.ToLower.Contains("amdocl") Then
							Try
								deletevalue(regkey, child)
							Catch ex As Exception
							End Try
						End If
					End If
				Next
				If regkey.GetValueNames().Length = 0 Then
					Try
						deletesubregkey(My.Computer.Registry.LocalMachine, "Software\Khronos\OpenCL")
					Catch ex As Exception
					End Try
				End If
				CleanVulkan(config)
				regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Khronos", True)
				If regkey IsNot Nothing Then
					If regkey.GetSubKeyNames().Length = 0 Then
						Try
							deletesubregkey(My.Computer.Registry.LocalMachine, "Software\Khronos")
						Catch ex As Exception
						End Try
					End If
				End If
			End If
		Catch ex As Exception
		End Try

		If IntPtr.Size = 8 Then

			Try
				regkey = My.Computer.Registry.LocalMachine.OpenSubKey("Software\Wow6432Node\Khronos\OpenCL\Vendors", True)
				If regkey IsNot Nothing Then
					For Each child As String In regkey.GetValueNames()
						If IsNullOrWhitespace(child) = False Then
							If child.ToLower.Contains("amdocl") Then
								Try
									deletevalue(regkey, child)
								Catch ex As Exception
								End Try
							End If
						End If
					Next
					If regkey.GetValueNames().Length = 0 Then
						Try
							deletesubregkey(My.Computer.Registry.LocalMachine, "Software\WOW6432Node\Khronos\OpenCL")
						Catch ex As Exception
						End Try
					End If
					regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\WOW6432Node\Khronos", True)
					If regkey IsNot Nothing Then
						If regkey.GetSubKeyNames().Length = 0 Then
							Try
								deletesubregkey(My.Computer.Registry.LocalMachine, "Software\WOW6432Node\Khronos")
							Catch ex As Exception
							End Try
						End If
					End If
				End If
			Catch ex As Exception
			End Try
		End If

		Application.Log.AddMessage("ngenservice Clean")

		'----------------------
		'.net ngenservice clean
		'----------------------
		Try
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\.NETFramework\v2.0.50727\NGenService\Roots", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames()
					If IsNullOrWhitespace(child) = False Then
						If child.ToLower.Contains("ati.ace") Then
							Try
								deletesubregkey(regkey, child)
							Catch ex As Exception
							End Try
						End If
					End If
				Next
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		'-----------------------------
		'End of .net ngenservice clean
		'-----------------------------

		'-----------------------------
		'Shell extensions\aprouved
		'-----------------------------
		Try
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetValueNames()
					If IsNullOrWhitespace(child) = False Then
						If regkey.GetValue(child).ToString.ToLower.Contains("catalyst context menu extension") Or
						 regkey.GetValue(child).ToString.ToLower.Contains("display cpl extension") Then
							Try
								deletevalue(regkey, child)
							Catch ex As Exception
							End Try
						End If
					End If
				Next
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		If IntPtr.Size = 8 Then
			Try
				regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved", True)
				If regkey IsNot Nothing Then
					For Each child As String In regkey.GetValueNames()
						If IsNullOrWhitespace(child) = False Then
							If regkey.GetValue(child).ToString.ToLower.Contains("catalyst context menu extension") Or
							 regkey.GetValue(child).ToString.ToLower.Contains("display cpl extension") Then
								Try
									deletevalue(regkey, child)
								Catch ex As Exception
								End Try
							End If
						End If
					Next
				End If
			Catch ex As Exception
				Application.Log.AddException(ex)
			End Try
		End If
		'-----------------------------
		'End Shell extensions\aprouved
		'-----------------------------

		Application.Log.AddMessage("Pnplockdownfiles region cleanUP")

		CleanupEngine.Pnplockdownfiles(IO.File.ReadAllLines(baseDir & "\settings\AMD\driverfiles.cfg"))	'// add each line as String Array.

		Try

			deletesubregkey(My.Computer.Registry.LocalMachine, "SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpResources\Registry\HKLM\SOFTWARE\Khronos")
		Catch ex As Exception
		End Try

		Try

			deletesubregkey(My.Computer.Registry.LocalMachine, "SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpResources\Registry\HKLM\SOFTWARE\AMD")
		Catch ex As Exception
		End Try

		Try

			deletesubregkey(My.Computer.Registry.LocalMachine, "SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpResources\Registry\HKLM\SOFTWARE\ATI Technologies")
		Catch ex As Exception
		End Try

		Try

			deletesubregkey(My.Computer.Registry.LocalMachine, "SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpResources\Registry\HKLM\SYSTEM\CurrentControlSet\Services\Atierecord")
		Catch ex As Exception
		End Try

		Try

			deletesubregkey(My.Computer.Registry.LocalMachine, "SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpResources\Registry\HKLM\SYSTEM\CurrentControlSet\Services\amdkmdap")
		Catch ex As Exception
		End Try

		If IntPtr.Size = 8 Then
			Try

				deletesubregkey(My.Computer.Registry.LocalMachine, "SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpResources\Registry\HKLM\SOFTWARE\Wow6432Node\Khronos")
			Catch ex As Exception
			End Try

			Try

				deletesubregkey(My.Computer.Registry.LocalMachine, "SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpResources\Registry\HKLM\SOFTWARE\Wow6432Node\ATI\ACE")
			Catch ex As Exception
			End Try
		End If

		Try

			deletesubregkey(My.Computer.Registry.LocalMachine, "SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpResources\Registry\HKLM\SOFTWARE\AMD\EEU")
		Catch ex As Exception
		End Try

		Try

			deletesubregkey(My.Computer.Registry.LocalMachine, "SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpResources\Registry\HKLM\SYSTEM\CurrentControlSet\Services\Atierecord\eRecordEnable")
		Catch ex As Exception
		End Try

		Try

			deletesubregkey(My.Computer.Registry.LocalMachine, "SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpResources\Registry\HKLM\SYSTEM\CurrentControlSet\Services\Atierecord\eRecordEnablePopups")
		Catch ex As Exception
		End Try


		'---------------------------------------------
		'Cleaning of Legacy_AMDKMDAG+ on win7 and lower
		'---------------------------------------------

		Try
			If version < "6.2" And System.Windows.Forms.SystemInformation.BootMode <> BootMode.Normal Then 'win 7 and lower + safemode only
				Application.Log.AddMessage("Cleaning LEGACY_AMDKMDAG")
				subregkey = My.Computer.Registry.LocalMachine.OpenSubKey _
				 ("SYSTEM")
				If subregkey IsNot Nothing Then
					For Each childs As String In subregkey.GetSubKeyNames()
						If IsNullOrWhitespace(childs) = False Then
							If childs.ToLower.Contains("controlset") Then
								regkey = My.Computer.Registry.LocalMachine.OpenSubKey _
								  ("SYSTEM\" & childs & "\Enum\Root")
								If regkey IsNot Nothing Then
									For Each child As String In regkey.GetSubKeyNames()
										If IsNullOrWhitespace(child) = False Then
											If child.ToLower.Contains("legacy_amdkmdag") Or _
											 (child.ToLower.Contains("legacy_amdkmpfd") AndAlso config.RemoveAMDKMPFD) Or _
											 child.ToLower.Contains("legacy_amdacpksd") Then

												Try
													deletesubregkey(My.Computer.Registry.LocalMachine, "SYSTEM\" & childs & "\Enum\Root\" & child)
												Catch ex As Exception
													Application.Log.AddException(ex)
												End Try
											End If
										End If
									Next
								End If
							End If
						End If
					Next
				End If
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		'----------------------------------------------------
		'End of Cleaning of Legacy_AMDKMDAG on win7 and lower
		'----------------------------------------------------


		'--------------------------------
		'System environement path cleanup
		'--------------------------------
		Application.Log.AddMessage("System environement cleanUP")
		Try
			subregkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM", False)
			If subregkey IsNot Nothing Then
				For Each child2 As String In subregkey.GetSubKeyNames()
					If child2.ToLower.Contains("controlset") Then
						regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\" & child2 & "\Control\Session Manager\Environment", True)
						If regkey IsNot Nothing Then
							For Each child As String In regkey.GetValueNames()
								If IsNullOrWhitespace(child) = False Then
									If child.Contains("AMDAPPSDKROOT") Then
										Try
											deletesubregkey(regkey, child)
										Catch ex As Exception
										End Try
									End If
									If child.Contains("Path") Then
										If IsNullOrWhitespace(CStr(regkey.GetValue(child))) = False Then
											wantedvalue = regkey.GetValue(child).ToString.ToLower
											Try
												Select Case True
													Case wantedvalue.Contains(";" + sysdrv & "\program files (x86)\amd app\bin\x86_64")
														wantedvalue = wantedvalue.Replace(";" + sysdrv & "\program files (x86)\amd app\bin\x86_64", "")
														regkey.SetValue(child, wantedvalue)

													Case wantedvalue.Contains(sysdrv & "\program files (x86)\amd app\bin\x86_64;")
														wantedvalue = wantedvalue.Replace(sysdrv & "\program files (x86)\amd app\bin\x86_64;", "")
														regkey.SetValue(child, wantedvalue)

													Case wantedvalue.Contains(";" + sysdrv & "\program files (x86)\amd app\bin\x86")
														wantedvalue = wantedvalue.Replace(";" + sysdrv & "\program files (x86)\amd app\bin\x86", "")
														regkey.SetValue(child, wantedvalue)

													Case wantedvalue.Contains(sysdrv & "\program files (x86)\amd app\bin\x86;")
														wantedvalue = wantedvalue.Replace(sysdrv & "\program files (x86)\amd app\bin\x86;", "")
														regkey.SetValue(child, wantedvalue)

													Case wantedvalue.Contains(";" + sysdrv & "\program Files (x86)\ati technologies\ati.ace\core-static")
														wantedvalue = wantedvalue.Replace(";" + sysdrv & "\program Files (x86)\ati technologies\ati.ace\core-static", "")
														regkey.SetValue(child, wantedvalue)

													Case wantedvalue.Contains(sysdrv & "\program Files (x86)\ati technologies\ati.ace\core-static;")
														wantedvalue = wantedvalue.Replace(sysdrv & "\program Files (x86)\ati technologies\ati.ace\core-static;", "")
														regkey.SetValue(child, wantedvalue)

													Case wantedvalue.Contains(";" + sysdrv & "\program Files (x86)\amd\ati.ace\core-static")
														wantedvalue = wantedvalue.Replace(";" + sysdrv & "\program Files (x86)\ati technologies\ati.ace\core-static", "")
														regkey.SetValue(child, wantedvalue)

													Case wantedvalue.Contains(sysdrv & "\program Files (x86)\amd\ati.ace\core-static;")
														wantedvalue = wantedvalue.Replace(sysdrv & "\program Files (x86)\ati technologies\ati.ace\core-static;", "")
														regkey.SetValue(child, wantedvalue)

												End Select
											Catch ex As Exception
											End Try
										End If
									End If
								End If
							Next
						End If
					End If
				Next
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		'end system environement patch cleanup

		'-----------------------
		'remove event view stuff
		'-----------------------
		Application.Log.AddMessage("Remove eventviewer stuff")
		Try
			subregkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM", False)
			If subregkey IsNot Nothing Then
				For Each child2 As String In subregkey.GetSubKeyNames()
					If IsNullOrWhitespace(child2) = False Then
						If child2.ToLower.Contains("controlset") Then
							regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\" & child2 & "\Services\eventlog", True)
							If regkey IsNot Nothing Then
								For Each child As String In regkey.GetSubKeyNames()
									If IsNullOrWhitespace(child) = False Then
										If child.ToLower.Contains("aceeventlog") Then
											deletesubregkey(regkey, child)
										End If
									End If
								Next


								Try
									deletesubregkey(regkey.OpenSubKey("Application", True), "ATIeRecord")
								Catch ex As Exception
								End Try

								Try
									deletesubregkey(regkey.OpenSubKey("System", True), "amdkmdag")
								Catch ex As Exception
								End Try

								Try
									deletesubregkey(regkey.OpenSubKey("System", True), "amdkmdap")
								Catch ex As Exception
								End Try
							End If
							Try
								deletesubregkey(My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\" & child2 & "\Services", True), "Atierecord")
							Catch ex As Exception
							End Try
						End If
					End If
				Next
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try


		'--------------------------------
		'end of eventviewer stuff removal
		'--------------------------------
		Try
			regkey = My.Computer.Registry.ClassesRoot.OpenSubKey _
			 ("Directory\background\shellex\ContextMenuHandlers", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames()
					If IsNullOrWhitespace(child) = False Then
						If child.Contains("ACE") Then

							deletesubregkey(regkey, child)

						End If
					End If

				Next
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try


		' to fix later, the range is too large and could lead to problems.
		Try
			For Each users As String In My.Computer.Registry.Users.GetSubKeyNames()
				If Not IsNullOrWhitespace(users) Then
					regkey = My.Computer.Registry.Users.OpenSubKey(users & "\Software", True)
					If regkey IsNot Nothing Then
						For Each child As String In regkey.GetSubKeyNames()
							If IsNullOrWhitespace(child) = False Then
								If child.StartsWith("ATI") Then
									deletesubregkey(regkey, child)
								End If
							End If
						Next
					End If
				End If
			Next
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		' to fix later, the range is too large and could lead to problems.
		Try
			For Each users As String In My.Computer.Registry.Users.GetSubKeyNames()
				If Not IsNullOrWhitespace(users) Then
					regkey = My.Computer.Registry.Users.OpenSubKey(users & "\Software", True)
					If regkey IsNot Nothing Then
						For Each child As String In regkey.GetSubKeyNames()
							If IsNullOrWhitespace(child) = False Then
								If child.StartsWith("AMD") Then
									deletesubregkey(regkey, child)
								End If
							End If
						Next
					End If
				End If
			Next
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		Try
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey("Software\ATI", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames()
					If IsNullOrWhitespace(child) = False Then
						If child.ToLower.Contains("ace") Or
						 child.ToLower.Contains("appprofiles") Or
						   child.ToLower.Contains("install") Then
							Try
								deletesubregkey(regkey, child)
							Catch ex As Exception
							End Try
						End If
					End If
				Next
				If regkey.SubKeyCount = 0 Then
					Try
						deletesubregkey(My.Computer.Registry.LocalMachine.OpenSubKey("Software", True), "ATI")
					Catch ex As Exception
					End Try
				End If
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		Try
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey("Software\ATI Technologies", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames()
					If IsNullOrWhitespace(child) = False Then
						If child.ToLower.Contains("cbt") Then
							Try
								deletesubregkey(regkey, child)
							Catch ex As Exception
							End Try
						End If
						If child.ToLower.Contains("ati catalyst control center") Then
							Try
								deletesubregkey(regkey, child)
							Catch ex As Exception
							End Try
						End If
						If child.ToLower.Contains("cds") Then
							Try
								deletesubregkey(regkey, child)
							Catch ex As Exception
							End Try
						End If
						If child.ToLower.Contains("install") Then
							'here we check the install path location in case CCC is not installed on the system drive.  A kill to explorer must be made
							'to help cleaning in normal mode.
							If System.Windows.Forms.SystemInformation.BootMode = BootMode.Normal Then
								Application.Log.AddMessage("Killing Explorer.exe")
								Dim appproc = process.GetProcessesByName("explorer")
								For i As Integer = 0 To appproc.Length - 1
									appproc(i).Kill()
								Next i
							End If
							Try
								If Not IsNullOrWhitespace(CStr(regkey.OpenSubKey(child).GetValue("InstallDir"))) Then
									filePath = regkey.OpenSubKey(child).GetValue("InstallDir").ToString
									If Not IsNullOrWhitespace(filePath) AndAlso My.Computer.FileSystem.DirectoryExists(filePath) Then
										For Each childf As String In Directory.GetDirectories(filePath)
											If IsNullOrWhitespace(childf) = False Then
												If childf.ToLower.Contains("ati.ace") Or
											 childf.ToLower.Contains("cnext") Or
											 childf.ToLower.Contains("amdkmpfd") Or
											 childf.ToLower.Contains("cim") Then
													Try
														deletedirectory(childf)
													Catch ex As Exception
														Application.Log.AddException(ex)
														TestDelete(childf)
													End Try
												End If
											End If
										Next
										If Directory.GetDirectories(filePath).Length = 0 Then
											Try
												deletedirectory(filePath)
											Catch ex As Exception
												Application.Log.AddException(ex)
												TestDelete(filePath)
											End Try
										End If
									End If
								End If

							Catch ex As Exception
								Application.Log.AddException(ex)
							End Try
							For Each child2 As String In regkey.OpenSubKey(child).GetSubKeyNames()
								If Not IsNullOrWhitespace(child2) Then
									If child2.ToLower.Contains("ati catalyst") Or
									 child2.ToLower.Contains("ati mcat") Or
									 child2.ToLower.Contains("avt") Or
									 child2.ToLower.Contains("ccc") Or
									 child2.ToLower.Contains("cnext") Or
									 child2.ToLower.Contains("amd app sdk") Or
									 child2.ToLower.Contains("packages") Or
									 child2.ToLower.Contains("wirelessdisplay") Or
									 child2.ToLower.Contains("hydravision") Or
									 child2.ToLower.Contains("avivo") Or
									 child2.ToLower.Contains("ati display driver") Or
									 child2.ToLower.Contains("installed drivers") Or
									 child2.ToLower.Contains("steadyvideo") Then
										Try
											deletesubregkey(regkey.OpenSubKey(child, True), child2)
										Catch ex As Exception
										End Try
									End If
								End If
							Next
							For Each values As String In regkey.OpenSubKey(child).GetValueNames()
								Try
									deletevalue(regkey.OpenSubKey(child, True), values)	'This is for windows 7, it prevent removing the South Bridge and fix the Catalyst "Upgrade"
								Catch ex As Exception
								End Try
							Next
							If regkey.OpenSubKey(child).SubKeyCount = 0 Then
								Try
									deletesubregkey(regkey, child)
								Catch ex As Exception
								End Try
							End If
						End If
					End If
				Next
				If regkey.SubKeyCount = 0 Then
					Try
						deletesubregkey(My.Computer.Registry.LocalMachine.OpenSubKey("Software", True), "ATI Technologies")
					Catch ex As Exception
					End Try
				End If
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		Try
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey("Software\AMD", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames()
					If IsNullOrWhitespace(child) = False Then
						If child.ToLower.Contains("eeu") Or
						   child.ToLower.Contains("fuel") Or
						   child.ToLower.Contains("cn") Or
						   child.ToLower.Contains("mftvdecoder") Then
							Try
								deletesubregkey(regkey, child)
							Catch ex As Exception
							End Try
						End If
					End If
				Next
				If regkey.SubKeyCount = 0 Then
					Try
						deletesubregkey(My.Computer.Registry.LocalMachine.OpenSubKey("Software", True), "AMD")
					Catch ex As Exception
					End Try
				End If
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		If IntPtr.Size = 8 Then
			Try
				regkey = My.Computer.Registry.LocalMachine.OpenSubKey("Software\Wow6432Node\ATI", True)
				If regkey IsNot Nothing Then
					For Each child As String In regkey.GetSubKeyNames()
						If IsNullOrWhitespace(child) = False Then
							If child.ToLower.Contains("ace") Or
							   child.ToLower.Contains("appprofiles") Then
								Try
									deletesubregkey(regkey, child)
								Catch ex As Exception
								End Try
							End If
						End If
					Next
					If regkey.SubKeyCount = 0 Then
						Try
							deletesubregkey(My.Computer.Registry.LocalMachine.OpenSubKey("Software\Wow6432Node", True), "ATI")
						Catch ex As Exception
						End Try
					End If
				End If

				regkey = My.Computer.Registry.LocalMachine.OpenSubKey("Software\Wow6432Node\AMD", True)
				If regkey IsNot Nothing Then
					For Each child As String In regkey.GetSubKeyNames()
						If IsNullOrWhitespace(child) = False Then
							If child.ToLower.Contains("eeu") Or
							   child.ToLower.Contains("mftvdecoder") Then

								deletesubregkey(regkey, child)

							End If
						End If
					Next
					If regkey.SubKeyCount = 0 Then
						deletesubregkey(My.Computer.Registry.LocalMachine.OpenSubKey("Software\Wow6432Node", True), "AMD")
					End If
				End If
			Catch ex As Exception
				Application.Log.AddException(ex)
			End Try

			Try
				regkey = My.Computer.Registry.LocalMachine.OpenSubKey("Software\Wow6432Node\ATI Technologies", True)
				If regkey IsNot Nothing Then
					For Each child As String In regkey.GetSubKeyNames()
						If IsNullOrWhitespace(child) = False Then
							If child.ToLower.Contains("system wide settings") Then
								Try
									deletesubregkey(regkey, child)
								Catch ex As Exception
								End Try
							End If
							If child.ToLower.Contains("install") Then
								For Each child2 As String In regkey.OpenSubKey(child).GetSubKeyNames()
									If child2.ToLower.Contains("ati catalyst") Or
									 child2.ToLower.Contains("ati mcat") Or
									 child2.ToLower.Contains("avt") Or
									 child2.ToLower.Contains("ccc") Or
									 child2.ToLower.Contains("cnext") Or
									 child2.ToLower.Contains("packages") Or
									 child2.ToLower.Contains("wirelessdisplay") Or
									 child2.ToLower.Contains("hydravision") Or
									 child2.ToLower.Contains("dndtranscoding64") Or
									 child2.ToLower.Contains("avivo") Or
									 child2.ToLower.Contains("steadyvideo") Then
										Try
											deletesubregkey(regkey.OpenSubKey(child, True), child2)
										Catch ex As Exception
										End Try
									End If
								Next
								If regkey.OpenSubKey(child).SubKeyCount = 0 Then
									Try
										deletesubregkey(regkey, child)
									Catch ex As Exception
									End Try
								End If
							End If
						End If
					Next
					If regkey.SubKeyCount = 0 Then
						Try
							deletesubregkey(My.Computer.Registry.LocalMachine.OpenSubKey("Software\Wow6432Node", True), "ATI Technologies")
						Catch ex As Exception
						End Try
					End If
				End If
			Catch ex As Exception
				Application.Log.AddException(ex)
			End Try
		End If

		Try
			For Each users As String In My.Computer.Registry.Users.GetSubKeyNames()
				If IsNullOrWhitespace(users) Then
					regkey = My.Computer.Registry.Users.OpenSubKey(users & "\Software\Microsoft\Windows\CurrentVersion\Run", True)
					If regkey IsNot Nothing Then
						Try
							deletevalue(regkey, "HydraVisionDesktopManager")
						Catch ex As Exception

							Application.Log.AddException(ex)
						End Try

						Try
							deletevalue(regkey, "Grid")
						Catch ex As Exception

							Application.Log.AddException(ex)
						End Try

						Try
							deletevalue(regkey, "HydraVisionMDEngine")
						Catch ex As Exception

							Application.Log.AddException(ex)
						End Try

					End If
				End If
			Next
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		Application.Log.AddMessage("Removing known Packages")

		packages = IO.File.ReadAllLines(baseDir & "\settings\AMD\packages.cfg")	'// add each line as String Array.
		Try
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey _
			("Software\Microsoft\Windows\CurrentVersion\Uninstall", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames()
					If IsNullOrWhitespace(child) = False Then

						subregkey = My.Computer.Registry.LocalMachine.OpenSubKey _
					   ("Software\Microsoft\Windows\CurrentVersion\Uninstall\" & child)

						If subregkey IsNot Nothing Then
							If IsNullOrWhitespace(CStr(subregkey.GetValue("DisplayName"))) = False Then
								wantedvalue = subregkey.GetValue("DisplayName").ToString
								If IsNullOrWhitespace(wantedvalue) = False Then
									For i As Integer = 0 To packages.Length - 1
										If Not IsNullOrWhitespace(packages(i)) Then
											If wantedvalue.ToLower.Contains(packages(i).ToLower) Then
												Try
													If Not (config.RemoveVulkan = False AndAlso StrContainsAny(wantedvalue, True, "vulkan")) Then
														deletesubregkey(regkey, child)
													End If
												Catch ex As Exception
												End Try
											End If
										End If
									Next
								End If
							End If
						End If
					End If
				Next
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		If IntPtr.Size = 8 Then
			packages = IO.File.ReadAllLines(baseDir & "\settings\AMD\packages.cfg")	'// add each line as String Array.
			Try
				regkey = My.Computer.Registry.LocalMachine.OpenSubKey _
				 ("Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall", True)
				If regkey IsNot Nothing Then
					For Each child As String In regkey.GetSubKeyNames()
						If IsNullOrWhitespace(child) = False Then
							subregkey = My.Computer.Registry.LocalMachine.OpenSubKey _
							 ("Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\" & child, True)
							If subregkey IsNot Nothing Then
								If IsNullOrWhitespace(CStr(subregkey.GetValue("DisplayName"))) = False Then
									wantedvalue = subregkey.GetValue("DisplayName").ToString
									If IsNullOrWhitespace(wantedvalue) = False Then
										For i As Integer = 0 To packages.Length - 1
											If Not IsNullOrWhitespace(packages(i)) Then
												If wantedvalue.ToLower.Contains(packages(i).ToLower) Then
													Try
														deletesubregkey(regkey, child)
													Catch ex As Exception
													End Try
												End If
											End If
										Next
									End If
								End If
							End If
						End If
					Next
				End If
			Catch ex As Exception
				Application.Log.AddException(ex)
			End Try
		End If

		CleanupEngine.installer(IO.File.ReadAllLines(baseDir & "\settings\AMD\packages.cfg"), config)

		Try
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey _
			 ("Software\Microsoft\Windows\CurrentVersion\Run", True)
			If regkey IsNot Nothing Then
				Try
					deletevalue(regkey, "StartCCC")

				Catch ex As Exception

					Application.Log.AddException(ex)
				End Try
				Try
					deletevalue(regkey, "StartCN")

				Catch ex As Exception

					Application.Log.AddException(ex)
				End Try
				Try

					deletevalue(regkey, "AMD AVT")

				Catch ex As Exception

					Application.Log.AddException(ex)
				End Try
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try


		If IntPtr.Size = 8 Then
			Try
				regkey = My.Computer.Registry.LocalMachine.OpenSubKey _
				 ("Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run", True)
				If regkey IsNot Nothing Then
					Try
						deletevalue(regkey, "StartCCC")
					Catch ex As Exception
						Application.Log.AddException(ex)
					End Try

					Try
						deletevalue(regkey, "StartCN")
					Catch ex As Exception
						Application.Log.AddException(ex)
					End Try

					Try
						deletevalue(regkey, "AMD AVT")
					Catch ex As Exception
						Application.Log.AddException(ex)
					End Try
				End If
			Catch ex As Exception
				Application.Log.AddException(ex)
			End Try
		End If

		Try
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey _
			 ("Software\Microsoft\Windows\CurrentVersion\Installer\Folders", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetValueNames()
					If IsNullOrWhitespace(child) = False Then
						If child.Contains("ATI\CIM\") Or
						   child.Contains("AMD\CNext\") Or
						   child.Contains("AMD APP\") Or
						   child.Contains("AMD\SteadyVideo\") Or
						   child.Contains("HydraVision\") Then

							Try
								deletevalue(regkey, child)
							Catch ex As Exception
							End Try
						End If
					End If
				Next
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		'prevent CCC reinstalltion (comes from drivers installed from windows updates)
		Try
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetValueNames()
					If Not IsNullOrWhitespace(child) Then
						If child.ToLower.Contains("launchwuapp") Then
							deletevalue(regkey, child)
						End If
					End If
				Next
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		If IntPtr.Size = 8 Then
			Try
				regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\RunOnce", True)
				If regkey IsNot Nothing Then
					For Each child As String In regkey.GetValueNames()
						If Not IsNullOrWhitespace(child) Then
							If child.ToLower.Contains("launchwuapp") Then
								deletevalue(regkey, child)
							End If
						End If
					Next
				End If
			Catch ex As Exception
				Application.Log.AddException(ex)
			End Try
		End If

		'Saw on Win 10 cat 15.7
		Application.Log.AddMessage("AudioEngine CleanUP")
		Try
			regkey = My.Computer.Registry.ClassesRoot.OpenSubKey("AudioEngine\AudioProcessingObjects", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames()
					If Not IsNullOrWhitespace(child) Then
						Try
							If Not IsNullOrWhitespace(CStr(regkey.OpenSubKey(child).GetValue("FriendlyName"))) Then
								If regkey.OpenSubKey(child).GetValue("FriendlyName").ToString.ToLower.Contains("cdelayapogfx") Then
									deletesubregkey(regkey, child)
								End If
							End If
						Catch ex As Exception
							Application.Log.AddException(ex)
						End Try
					End If
				Next
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		'SteadyVideo stuff

		regkey = My.Computer.Registry.LocalMachine.OpenSubKey _
		 ("Software\Microsoft\Windows\CurrentVersion\Explorer\Browser Helper Objects", True)
		If regkey IsNot Nothing Then
			For Each child As String In regkey.GetSubKeyNames()
				If IsNullOrWhitespace(child) = False Then
					subregkey = regkey.OpenSubKey(child, False)
					If subregkey IsNot Nothing Then
						If IsNullOrWhitespace(CStr(subregkey.GetValue(""))) = False Then
							wantedvalue = subregkey.GetValue("").ToString
							If IsNullOrWhitespace(wantedvalue) = False Then
								If wantedvalue.ToLower.Contains("steadyvideo") Then
									Try
										deletesubregkey(regkey, child)
									Catch ex As Exception
									End Try
								End If
							End If
						End If
					End If
				End If
			Next
		End If


		Try
			regkey = My.Computer.Registry.ClassesRoot.OpenSubKey("PROTOCOLS\Filter", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames()
					If Not IsNullOrWhitespace(child) Then
						subregkey = regkey.OpenSubKey(child, False)
						If subregkey IsNot Nothing Then
							If Not IsNullOrWhitespace(CStr(subregkey.GetValue(""))) Then
								wantedvalue = CStr(subregkey.GetValue(""))
								If Not IsNullOrWhitespace(wantedvalue) Then
									If wantedvalue.ToLower.Contains("steadyvideo") Then
										Try
											deletesubregkey(regkey, child)
										Catch ex As Exception
											Application.Log.AddException(ex)
										End Try
									End If
								End If
							End If
						End If
					End If
				Next
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		If IntPtr.Size = 8 Then
			'SteadyVideo stuff

			regkey = My.Computer.Registry.LocalMachine.OpenSubKey _
		 ("Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Explorer\Browser Helper Objects", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames()
					If IsNullOrWhitespace(child) = False Then
						subregkey = regkey.OpenSubKey(child, False)
						If subregkey IsNot Nothing Then
							If IsNullOrWhitespace(CStr(subregkey.GetValue(""))) = False Then
								wantedvalue = subregkey.GetValue("").ToString
								If IsNullOrWhitespace(wantedvalue) = False Then
									If wantedvalue.ToLower.Contains("steadyvideo") Then
										Try
											deletesubregkey(regkey, child)
										Catch ex As Exception
										End Try
									End If
								End If
							End If
						End If
					End If
				Next
			End If



			Try
				regkey = My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\PROTOCOLS\Filter", True)
				If regkey IsNot Nothing Then
					For Each child As String In regkey.GetSubKeyNames()
						If Not IsNullOrWhitespace(child) Then
							subregkey = regkey.OpenSubKey(child, False)
							If subregkey IsNot Nothing Then
								If Not IsNullOrWhitespace(CStr(subregkey.GetValue(""))) Then
									wantedvalue = CStr(subregkey.GetValue(""))
									If Not IsNullOrWhitespace(wantedvalue) Then
										If wantedvalue.ToLower.Contains("steadyvideo") Then
											Try
												deletesubregkey(regkey, child)
											Catch ex As Exception
												Application.Log.AddException(ex)
											End Try
										End If
									End If
								End If
							End If
						End If
					Next
				End If
			Catch ex As Exception
				Application.Log.AddException(ex)
			End Try

		End If

	End Sub

	Private Sub rebuildcountercache()
		Application.Log.AddMessage("Rebuilding the Perf.Counter cache X2")
		Try

			For i = 0 To 1
				processinfo.FileName = "lodctr"
				processinfo.Arguments = "/R"
				processinfo.WindowStyle = ProcessWindowStyle.Hidden
				processinfo.UseShellExecute = False
				processinfo.CreateNoWindow = True
				processinfo.RedirectStandardOutput = True

				process.StartInfo = processinfo
				process.Start()
				reply2 = process.StandardOutput.ReadToEnd
				process.StandardOutput.Close()
				process.Close()
				Application.Log.AddMessage(reply2)
			Next

		Catch ex As Exception
			Application.log.AddException(ex)
		End Try
	End Sub

	Private Sub fixregistrydriverstore()
		'Windows 8 + only
		'This should fix driver installation problem reporting that a file is not found.
		'It is usually caused by Windows somehow losing track of the driver store , This intend to help it a bit.
		If win8higher Then
			Application.Log.AddMessage("Fixing registry driverstore if necessary")
			Try
				Dim regkey As RegistryKey = Nothing
				Dim infslist As String = ""
				For Each infs As String In My.Computer.FileSystem.GetFiles(Environment.GetEnvironmentVariable("windir") & "\inf", FileIO.SearchOption.SearchTopLevelOnly, "oem*.inf")
					If Not IsNullOrWhitespace(infs) Then
						infslist = infslist + infs
					End If
				Next
				regkey = My.Computer.Registry.LocalMachine.OpenSubKey("DRIVERS\DriverDatabase\DriverInfFiles", True)
				If regkey IsNot Nothing Then
					For Each child As String In regkey.GetSubKeyNames()
						If Not IsNullOrWhitespace(child) Then
							If child.ToLower.StartsWith("oem") AndAlso child.ToLower.EndsWith(".inf") Then
								If Not infslist.ToLower.Contains(child) Then
									Try
										deletesubregkey(My.Computer.Registry.LocalMachine.OpenSubKey("DRIVERS\DriverDatabase\DriverPackages", True), CStr(regkey.OpenSubKey(child).GetValue("Active")))
									Catch ex As Exception
										Application.log.AddException(ex)
									End Try

									Try
										deletesubregkey(regkey, child)
									Catch ex As Exception
										Application.log.AddException(ex)
									End Try
								End If
							End If
						End If
					Next
				End If

				regkey = My.Computer.Registry.LocalMachine.OpenSubKey("DRIVERS\DriverDatabase\DriverPackages", True)
				If regkey IsNot Nothing Then
					For Each child As String In regkey.GetSubKeyNames()
						If Not IsNullOrWhitespace(child) Then
							If CStr(regkey.OpenSubKey(child).GetValue("")).ToLower.StartsWith("oem") AndAlso
								CStr(regkey.OpenSubKey(child).GetValue("")).ToLower.EndsWith(".inf") AndAlso
								Not infslist.ToLower.Contains(CStr(regkey.OpenSubKey(child).GetValue(""))) Then
								Try
									deletesubregkey(regkey, child)
								Catch ex As Exception
									Application.Log.AddException(ex)
								End Try
							End If
						End If
					Next
				End If

			Catch ex As Exception
				Application.log.AddException(ex)
			End Try
		End If
	End Sub
	Private Sub CleanVulkan(ByRef config As ThreadSettings)
		Dim regkey As RegistryKey = Nothing
		Dim FilePath As String = Nothing
		Dim files() As String = Nothing

		If config.RemoveVulkan Then
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Khronos", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames
					If Not IsNullOrWhitespace(child) Then
						If StrContainsAny(child, True, "vulkan") Then
							deletesubregkey(regkey, child)
						End If
					End If
				Next
			End If
			If IntPtr.Size = 8 Then
				regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\WOW6432Node\Khronos", True)
				If regkey IsNot Nothing Then
					For Each child As String In regkey.GetSubKeyNames
						If Not IsNullOrWhitespace(child) Then
							If StrContainsAny(child, True, "vulkan") Then
								deletesubregkey(regkey, child)
							End If
						End If
					Next
				End If
			End If

			If config.RemoveVulkan Then
				filePath = System.Environment.SystemDirectory
				files = IO.Directory.GetFiles(filePath + "\", "vulkan-1*.dll")
				For i As Integer = 0 To files.Length - 1
					If Not IsNullOrWhitespace(files(i)) Then
						Try
							deletefile(files(i))
						Catch ex As Exception
						End Try
					End If
				Next

				files = IO.Directory.GetFiles(filePath + "\", "vulkaninfo*.*")
				For i As Integer = 0 To files.Length - 1
					If Not IsNullOrWhitespace(files(i)) Then
						Try
							deletefile(files(i))
						Catch ex As Exception
						End Try
					End If
				Next
			End If

			If IntPtr.Size = 8 Then
				If config.RemoveVulkan Then
					filePath = Environment.GetEnvironmentVariable("windir") + "\SysWOW64"
					files = IO.Directory.GetFiles(filePath + "\", "vulkan-1*.dll")
					For i As Integer = 0 To files.Length - 1
						If Not IsNullOrWhitespace(files(i)) Then
							Try
								deletefile(files(i))
							Catch ex As Exception
							End Try
						End If
					Next

					files = IO.Directory.GetFiles(filePath + "\", "vulkaninfo*.*")
					For i As Integer = 0 To files.Length - 1
						If Not IsNullOrWhitespace(files(i)) Then
							Try
								deletefile(files(i))
							Catch ex As Exception
							End Try
						End If
					Next
				End If
			End If
		End If
	End Sub
	Private Sub cleannvidiaserviceprocess(ByVal config As ThreadSettings)
		CleanupEngine.cleanserviceprocess(IO.File.ReadAllLines(baseDir & "\settings\NVIDIA\services.cfg"))
		If config.RemoveGFE Then
			CleanupEngine.cleanserviceprocess(IO.File.ReadAllLines(baseDir & "\settings\NVIDIA\gfeservice.cfg"))
		End If

		'kill process NvTmru.exe and special kill for Logitech Keyboard(Lcore.exe) 
		'holding files in the NVIDIA folders sometimes.
		Try
			Dim processes As String() =
			 New String() {
			 "Lcore",
			 "nvgamemonitor",
			 "nvstreamsvc",
			 "NvTmru",
			 "nvxdsync",
			 "dwm",
			 "WWAHost",
			 "nvspcaps64",
			 "nvspcaps",
			 "NvBackend"}

			For Each pname As String In processes
				For Each p As Process In process.GetProcessesByName(pname)
					p.Kill()
				Next
			Next

			If config.RemoveGFE Then
				Dim appproc = process.GetProcessesByName("nvtray")

				For i As Integer = 0 To appproc.Length - 1
					appproc(i).Kill()
				Next i
			End If

		Catch ex As Exception
		End Try
	End Sub

	Private Sub cleannvidiafolders(ByVal config As ThreadSettings)
		Dim regkey As RegistryKey = Nothing
		Dim subregkey As RegistryKey = Nothing
		Dim filePath As String = Nothing
		Dim removephysx As Boolean = config.RemovePhysX
		'Delete NVIDIA data Folders
		'Here we delete the Geforce experience / Nvidia update user it created. This fail sometime for no reason :/

		UpdateTextMethod(UpdateTextMethodmessagefn(3))
		Application.Log.AddMessage("Cleaning UpdatusUser users ac if present")

		Dim AD As DirectoryEntry = New DirectoryEntry("WinNT://" + Environment.MachineName.ToString())
		Dim users As DirectoryEntries = AD.Children
		Dim newuser As DirectoryEntry = Nothing

		Try
			newuser = users.Find("UpdatusUser")
			users.Remove(newuser)
		Catch ex As Exception
		End Try

		UpdateTextMethod(UpdateTextMethodmessagefn(4))

		Application.Log.AddMessage("Cleaning Directory")


		If config.RemoveNvidiaDirs = True Then
			filePath = sysdrv + "\NVIDIA"
			Try
				deletedirectory(filePath)
			Catch ex As Exception

				Application.Log.AddException(ex)
				TestDelete(filePath)
			End Try

		End If

		' here I erase the folders / files of the nvidia GFE / update in users.
		filePath = IO.Path.GetDirectoryName(userpth)
		For Each child As String In Directory.GetDirectories(filePath)
			If IsNullOrWhitespace(child) = False Then
				If child.ToLower.Contains("updatususer") Then
					Try
						TestDelete(child)
					Catch ex As Exception
					End Try

					Try
						deletedirectory(child)
					Catch ex As Exception

						Application.Log.AddException(ex)
					End Try

					'Yes we do it 2 times. This will workaround a problem on junction/sybolic/hard link
					Try
						TestDelete(child)
					Catch ex As Exception
						Application.Log.AddException(ex)
					End Try
					Try
						deletedirectory(child)
					Catch ex As Exception
						Application.Log.AddException(ex)
					End Try
				End If
			End If
		Next


		For Each filepaths As String In Directory.GetDirectories(IO.Path.GetDirectoryName(userpth))

			filePath = filepaths + "\AppData\Local\NVIDIA"

			If config.RemoveGFE Then
				Try
					For Each child As String In My.Computer.FileSystem.GetDirectories(filePath)
						If IsNullOrWhitespace(child) = False Then
							If (child.ToLower.Contains("nvbackend") AndAlso config.RemoveGFE) Or
							 (child.ToLower.Contains("nvosc.") AndAlso config.RemoveGFE) Or
							 (child.ToLower.Contains("shareconnect") AndAlso config.RemoveGFE) Or
							 (child.ToLower.Contains("gfexperience") AndAlso config.RemoveGFE) Then
								Try
									deletedirectory(child)
								Catch ex As Exception
									Application.Log.AddException(ex)
									TestDelete(child)
								End Try
							End If
						End If
					Next
					Try
						If Directory.GetDirectories(filePath).Length = 0 Then
							Try
								deletedirectory(filePath)
							Catch ex As Exception
								Application.Log.AddException(ex)
								TestDelete(filePath)
							End Try
						Else
							For Each data As String In Directory.GetDirectories(filePath)
								Application.Log.AddWarningMessage("Remaining folders found " + " : " + data)
							Next

						End If
					Catch ex As Exception
					End Try
				Catch ex As Exception
				End Try
			End If

			filePath = filepaths + "\AppData\Roaming\NVIDIA"

			Try
				For Each child As String In My.Computer.FileSystem.GetDirectories(filePath)
					If IsNullOrWhitespace(child) = False Then
						If child.ToLower.Contains("computecache") Or
						 child.ToLower.Contains("glcache") Then
							Try
								deletedirectory(child)
							Catch ex As Exception
								Application.Log.AddException(ex)
								TestDelete(child)
							End Try
						End If
					End If
				Next
				Try
					If Directory.GetDirectories(filePath).Length = 0 Then
						Try
							deletedirectory(filePath)
						Catch ex As Exception
							Application.Log.AddException(ex)
							TestDelete(filePath)
						End Try
					Else
						For Each data As String In Directory.GetDirectories(filePath)
							Application.Log.AddWarningMessage("Remaining folders found " + " : " + data)
						Next

					End If
				Catch ex As Exception
				End Try
			Catch ex As Exception
			End Try


			filePath = filepaths + "\AppData\Local\NVIDIA Corporation"
			If config.RemoveGFE Then
				Try
					For Each child As String In My.Computer.FileSystem.GetDirectories(filePath)
						If IsNullOrWhitespace(child) = False Then
							If (child.ToLower.Contains("ledvisualizer") AndAlso config.RemoveGFE) Or
							 (child.ToLower.Contains("shadowplay") AndAlso config.RemoveGFE) Or
							 (child.ToLower.Contains("gfexperience") AndAlso config.RemoveGFE) Or
							 (child.ToLower.Contains("nvstreamsrv") AndAlso config.RemoveGFE) Or
							 (child.ToLower.EndsWith("\osc") AndAlso config.RemoveGFE) Or
							 (child.ToLower.Contains("nvvad") AndAlso config.RemoveGFE) Or
							 (child.ToLower.Contains("shield apps") AndAlso config.RemoveGFE) Then

								Try
									deletedirectory(child)
								Catch ex As Exception
									Application.Log.AddException(ex)
								End Try
							End If
						End If
					Next
					Try
						If Directory.GetDirectories(filePath).Length = 0 Then
							Try
								deletedirectory(filePath)
							Catch ex As Exception
								Application.Log.AddException(ex)
							End Try
						Else
							For Each data As String In Directory.GetDirectories(filePath)
								Application.Log.AddWarningMessage("Remaining folders found " + " : " + data)
							Next

						End If
					Catch ex As Exception
					End Try
				Catch ex As Exception
				End Try
			End If

		Next

		filePath = Environment.GetFolderPath _
	  (Environment.SpecialFolder.CommonApplicationData) + "\NVIDIA"

		Try
			For Each child As String In My.Computer.FileSystem.GetDirectories(filePath)
				If IsNullOrWhitespace(child) = False Then
					If child.ToLower.Contains("updatus") Or _
					 (child.ToLower.Contains("grid") AndAlso config.RemoveGFE) Then
						Try
							deletedirectory(child)
						Catch ex As Exception
							Application.Log.AddException(ex)
							TestDelete(child)
						End Try
					End If
				End If
			Next
			Try
				If Directory.GetDirectories(filePath).Length = 0 Then
					Try
						deletedirectory(filePath)
					Catch ex As Exception
						Application.Log.AddException(ex)
						TestDelete(filePath)
					End Try
				Else
					For Each data As String In Directory.GetDirectories(filePath)
						Application.Log.AddWarningMessage("Remaining folders found " + " : " + data)
					Next
				End If
			Catch ex As Exception
			End Try
		Catch ex As Exception
		End Try

		filePath = Environment.GetFolderPath _
	  (Environment.SpecialFolder.CommonApplicationData) + "\NVIDIA Corporation"
		Try
			For Each child As String In My.Computer.FileSystem.GetDirectories(filePath)
				If IsNullOrWhitespace(child) = False Then
					If child.ToLower.Contains("drs") Or
					 (child.ToLower.Contains("geforce experience") AndAlso config.RemoveGFE) Or
					 (child.ToLower.Contains("gfexperience") AndAlso config.RemoveGFE) Or
					 (child.ToLower.Contains("netservice") AndAlso config.RemoveGFE) Or
					 (child.ToLower.Contains("crashdumps") AndAlso config.RemoveGFE) Or
					 (child.ToLower.Contains("nvstream") AndAlso config.RemoveGFE) Or
					 (child.ToLower.Contains("shadowplay") AndAlso config.RemoveGFE) Or
					 (child.ToLower.Contains("ledvisualizer") AndAlso config.RemoveGFE) Or
					 (child.ToLower.Contains("nview") AndAlso config.RemoveGFE) Or
					 (child.ToLower.Contains("nvstreamsvc") AndAlso config.RemoveGFE) Then
						Try
							deletedirectory(child)
						Catch ex As Exception
							Application.Log.AddException(ex)
							TestDelete(child)
						End Try
					End If
				End If
			Next
			If Directory.GetDirectories(filePath).Length = 0 Then
				Try
					deletedirectory(filePath)
				Catch ex As Exception
					Application.Log.AddException(ex)
					TestDelete(filePath)
				End Try
			Else
				For Each data As String In Directory.GetDirectories(filePath)
					Application.Log.AddWarningMessage("Remaining folders found " + " : " + data)
				Next

			End If
		Catch ex As Exception
		End Try

		filePath = Environment.GetFolderPath _
	  (Environment.SpecialFolder.CommonApplicationData) + "\Microsoft\Windows\Start Menu\Programs\NVIDIA Corporation"
		Try
			For Each child As String In My.Computer.FileSystem.GetDirectories(filePath)
				If IsNullOrWhitespace(child) = False Then
					If child.ToLower.Contains("3d vision") Then
						Try
							deletedirectory(child)
						Catch ex As Exception
							Application.Log.AddException(ex)
							TestDelete(child)
						End Try
					End If
				End If
			Next
			Try
				If Directory.GetDirectories(filePath).Length = 0 Then
					Try
						deletedirectory(filePath)
					Catch ex As Exception
						Application.Log.AddException(ex)
						TestDelete(filePath)
					End Try
				Else
					For Each data As String In Directory.GetDirectories(filePath)
						Application.Log.AddWarningMessage("Remaining folders found " + " : " + data)
					Next
				End If
			Catch ex As Exception
			End Try
		Catch ex As Exception
		End Try


		filePath = Environment.GetFolderPath _
		(Environment.SpecialFolder.ProgramFiles) + "\NVIDIA Corporation"
		If Directory.Exists(filePath) Then
			For Each child As String In Directory.GetDirectories(filePath)
				If IsNullOrWhitespace(child) = False Then
					If child.ToLower.Contains("control panel client") Or
					   child.ToLower.Contains("display") Or
					   child.ToLower.Contains("coprocmanager") Or
					   child.ToLower.Contains("drs") Or
					   child.ToLower.Contains("nvsmi") Or
					   child.ToLower.Contains("opencl") Or
					   child.ToLower.Contains("3d vision") Or
					   child.ToLower.Contains("led visualizer") AndAlso config.RemoveGFE Or
					   child.ToLower.Contains("netservice") AndAlso config.RemoveGFE Or
					   child.ToLower.Contains("geforce experience") AndAlso config.RemoveGFE Or
					   child.ToLower.Contains("nvstreamc") AndAlso config.RemoveGFE Or
					   child.ToLower.Contains("nvstreamsrv") AndAlso config.RemoveGFE Or
					   child.ToLower.EndsWith("\physx") AndAlso config.RemovePhysX Or
					   child.ToLower.Contains("nvstreamsrv") AndAlso config.RemoveGFE Or
					   child.ToLower.Contains("shadowplay") AndAlso config.RemoveGFE Or
					   child.ToLower.Contains("update common") AndAlso config.RemoveGFE Or
					   child.ToLower.Contains("shield") AndAlso config.RemoveGFE Or
					   child.ToLower.Contains("nview") Or
					   child.ToLower.Contains("nvidia wmi provider") Or
					   child.ToLower.Contains("gamemonitor") AndAlso config.RemoveGFE Or
					   child.ToLower.Contains("nvgsync") Or
					   child.ToLower.Contains("update core") AndAlso config.RemoveGFE Then


						Try
							deletedirectory(child)
						Catch ex As Exception
							Application.Log.AddException(ex)
							TestDelete(child)
						End Try
					End If
					If child.ToLower.Contains("installer2") Then
						For Each child2 As String In Directory.GetDirectories(child)
							If IsNullOrWhitespace(child2) = False Then
								If child2.ToLower.Contains("display.3dvision") Or
								   child2.ToLower.Contains("display.controlpanel") Or
								   child2.ToLower.Contains("display.driver") Or
								   child2.ToLower.Contains("display.optimus") Or
								   child2.ToLower.Contains("msvcruntime") Or
								   child2.ToLower.Contains("display.gfexperience") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("osc.") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("osclib.") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("display.nvirusb") Or
								   child2.ToLower.Contains("display.physx") AndAlso config.RemovePhysX Or
								   child2.ToLower.Contains("display.update") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("display.gamemonitor") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("gfexperience") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("nvidia.update") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("installer2\installer") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("network.service") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("miracast.virtualaudio") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("shadowplay") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("update.core") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("virtualaudio.driver") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("coretemp") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("shield") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("hdaudio.driver") Then

									Try
										deletedirectory(child2)
									Catch ex As Exception
										Application.Log.AddException(ex)
										TestDelete(child2)
									End Try
								End If
							End If
						Next

						If Directory.GetDirectories(child).Length = 0 Then
							Try
								deletedirectory(child)
							Catch ex As Exception
								Application.Log.AddException(ex)
								TestDelete(child)
							End Try
						Else
							For Each data As String In Directory.GetDirectories(child)
								Application.Log.AddWarningMessage("Remaining folders found " + " : " + data)
							Next

						End If
					End If
				End If
			Next
			If Directory.GetDirectories(filePath).Length = 0 Then
				Try
					deletedirectory(filePath)
				Catch ex As Exception
					Application.Log.AddException(ex)
					TestDelete(filePath)
				End Try
			Else
				For Each data As String In Directory.GetDirectories(filePath)
					Application.Log.AddWarningMessage("Remaining folders found " + " : " + data)
				Next
			End If
		End If


		If config.RemoveVulkan Then
			filePath = Environment.GetFolderPath _
			 (Environment.SpecialFolder.ProgramFiles) + "\AGEIA Technologies"
			If Directory.Exists(filePath) Then
				Try
					deletedirectory(filePath)
				Catch ex As Exception
				End Try
			End If
		End If

		If config.RemoveVulkan Then
			filePath = config.Paths.ProgramFiles + "VulkanRT"
			If Directory.Exists(filePath) Then
				Try
					deletedirectory(filePath)
				Catch ex As Exception
				End Try
			End If
		End If

		If IntPtr.Size = 8 Then
			filePath = Environment.GetFolderPath _
					(Environment.SpecialFolder.ProgramFiles) + " (x86)" + "\NVIDIA Corporation"
			If Directory.Exists(filePath) Then
				For Each child As String In Directory.GetDirectories(filePath)
					If IsNullOrWhitespace(child) = False Then
						If child.ToLower.Contains("3d vision") Or
							child.ToLower.Contains("coprocmanager") Or
							child.ToLower.Contains("led visualizer") AndAlso config.RemoveGFE Or
							child.ToLower.Contains("osc") AndAlso config.RemoveGFE Or
							child.ToLower.Contains("netservice") AndAlso config.RemoveGFE Or
							child.ToLower.Contains("nvidia geforce experience") AndAlso config.RemoveGFE Or
							child.ToLower.Contains("nvstreamc") AndAlso config.RemoveGFE Or
							child.ToLower.Contains("nvstreamsrv") AndAlso config.RemoveGFE Or
							child.ToLower.Contains("update common") AndAlso config.RemoveGFE Or
							child.ToLower.Contains("nvgsync") Or
							child.ToLower.EndsWith("\physx") AndAlso config.RemovePhysX Or
							child.ToLower.Contains("update core") AndAlso config.RemoveGFE Then
							If removephysx Then
								Try
									deletedirectory(child)
								Catch ex As Exception
									Application.Log.AddException(ex)
									TestDelete(child)
								End Try
							Else
								If child.ToLower.Contains("physx") Then
									'do nothing
								Else
									Try
										deletedirectory(child)
									Catch ex As Exception
										Application.Log.AddException(ex)
										TestDelete(child)
									End Try
								End If
							End If
						End If
					End If
				Next

				If Directory.GetDirectories(filePath).Length = 0 Then
					Try
						deletedirectory(filePath)
					Catch ex As Exception
						Application.Log.AddException(ex)
						TestDelete(filePath)
					End Try
				Else
					For Each data As String In Directory.GetDirectories(filePath)
						Application.Log.AddMessage("Remaining folders found " + " : " + data)
					Next

				End If
			End If
		End If


		If config.RemovePhysX Then
			If IntPtr.Size = 8 Then
				filePath = Environment.GetFolderPath _
				 (Environment.SpecialFolder.ProgramFiles) + " (x86)" + "\AGEIA Technologies"
				If Directory.Exists(filePath) Then
					Try
						deletedirectory(filePath)
					Catch ex As Exception
					End Try
				End If
			End If
		End If

		If config.RemoveVulkan Then
			If IntPtr.Size = 8 Then
				filePath = Application.Paths.ProgramFilesx86 + "VulkanRT"
				If Directory.Exists(filePath) Then
					Try
						deletedirectory(filePath)
					Catch ex As Exception
					End Try
				End If
			End If
		End If

		CleanupEngine.folderscleanup(IO.File.ReadAllLines(baseDir & "\settings\NVIDIA\driverfiles.cfg")) '// add each line as String Array.
		If config.RemoveGFE Then
			CleanupEngine.folderscleanup(IO.File.ReadAllLines(baseDir & "\settings\NVIDIA\gfedriverfiles.cfg"))	'// add each line as String Array.
		End If

		filePath = System.Environment.SystemDirectory
		Dim files() As String = IO.Directory.GetFiles(filePath + "\", "nvdisp*.*")
		For i As Integer = 0 To files.Length - 1
			If Not IsNullOrWhitespace(files(i)) Then
				Try
					deletefile(files(i))
				Catch ex As Exception
				End Try
			End If
		Next

		filePath = System.Environment.SystemDirectory
		files = IO.Directory.GetFiles(filePath + "\", "nvhdagenco*.*")
		For i As Integer = 0 To files.Length - 1
			If Not IsNullOrWhitespace(files(i)) Then
				Try
					deletefile(files(i))
				Catch ex As Exception
				End Try
			End If
		Next

		filePath = Environment.GetEnvironmentVariable("windir")
		Try
			deletedirectory(filePath + "\Help\nvcpl")
		Catch ex As Exception
		End Try

		Try
			filePath = Environment.GetEnvironmentVariable("windir") + "\Temp\NVIDIA Corporation"
			For Each child As String In Directory.GetDirectories(filePath)
				If IsNullOrWhitespace(child) = False Then
					If child.ToLower.Contains("nv_cache") Then
						Try
							deletedirectory(child)
						Catch ex As Exception
							Application.Log.AddException(ex)
							TestDelete(child)
						End Try
					End If
				End If
			Next
			Try
				If Directory.GetDirectories(filePath).Length = 0 Then
					Try
						deletedirectory(filePath)
					Catch ex As Exception
						Application.Log.AddException(ex)
						TestDelete(filePath)
					End Try
				Else
					For Each data As String In Directory.GetDirectories(filePath)
						Application.Log.AddWarningMessage("Remaining folders found " + " : " + data)
					Next

				End If
			Catch ex As Exception
			End Try
		Catch ex As Exception
		End Try



		For Each filepaths As String In Directory.GetDirectories(IO.Path.GetDirectoryName(userpth))

			filePath = filepaths + "\AppData\Local\Temp\NVIDIA Corporation"

			Try
				For Each child As String In My.Computer.FileSystem.GetDirectories(filePath)
					If IsNullOrWhitespace(child) = False Then
						If child.ToLower.Contains("nv_cache") Or
						 child.ToLower.Contains("displaydriver") Then
							Try
								deletedirectory(child)
							Catch ex As Exception
								Application.Log.AddException(ex)
								TestDelete(child)
							End Try
						End If
					End If
				Next
				Try
					If Directory.GetDirectories(filePath).Length = 0 Then
						Try
							deletedirectory(filePath)
						Catch ex As Exception
							Application.Log.AddException(ex)
							TestDelete(filePath)
						End Try
					Else
						For Each data As String In Directory.GetDirectories(filePath)
							Application.Log.AddWarningMessage("Remaining folders found " + " : " + data)
						Next

					End If
				Catch ex As Exception
				End Try
			Catch ex As Exception
			End Try

			filePath = filepaths + "\AppData\Local\Temp\NVIDIA"

			Try
				For Each child As String In My.Computer.FileSystem.GetDirectories(filePath)
					If IsNullOrWhitespace(child) = False Then
						If child.ToLower.Contains("geforceexperienceselfupdate") AndAlso config.RemoveGFE Or _
						   child.ToLower.Contains("displaydriver") Then
							Try
								deletedirectory(child)
							Catch ex As Exception
								Application.Log.AddException(ex)
								TestDelete(child)
							End Try
						End If
					End If
				Next
				Try
					If Directory.GetDirectories(filePath).Length = 0 Then
						Try
							deletedirectory(filePath)
						Catch ex As Exception
							Application.Log.AddException(ex)
							TestDelete(filePath)
						End Try
					Else
						For Each data As String In Directory.GetDirectories(filePath)
							Application.Log.AddWarningMessage("Remaining folders found " + " : " + data)
						Next

					End If
				Catch ex As Exception
				End Try
			Catch ex As Exception
			End Try

			filePath = filepaths + "\AppData\Local\Temp\Low\NVIDIA Corporation"

			Try
				For Each child As String In My.Computer.FileSystem.GetDirectories(filePath)
					If IsNullOrWhitespace(child) = False Then
						If child.ToLower.Contains("nv_cache") Then
							Try
								deletedirectory(child)
							Catch ex As Exception
								Application.Log.AddException(ex)
								TestDelete(child)
							End Try
						End If
					End If
				Next
				Try
					If Directory.GetDirectories(filePath).Length = 0 Then
						Try
							deletedirectory(filePath)
						Catch ex As Exception
							Application.Log.AddException(ex)
							TestDelete(filePath)
						End Try
					Else
						For Each data As String In Directory.GetDirectories(filePath)
							Application.Log.AddWarningMessage("Remaining folders found " + " : " + data)
						Next

					End If
				Catch ex As Exception
				End Try
			Catch ex As Exception
			End Try

			'windows 8+ only (store apps nv_cache cleanup)
			Try
				If win8higher Then
					Dim prefilePath As String = filepaths + "\AppData\Local\Packages"
					For Each childs As String In My.Computer.FileSystem.GetDirectories(prefilePath)
						If Not IsNullOrWhitespace(childs) Then
							filePath = childs + "\AC\Temp\NVIDIA Corporation"

							If Directory.Exists(filePath) Then
								For Each child As String In My.Computer.FileSystem.GetDirectories(filePath)
									If IsNullOrWhitespace(child) = False Then
										If child.ToLower.Contains("nv_cache") Then
											Try
												deletedirectory(child)
											Catch ex As Exception
												Application.Log.AddException(ex)
												TestDelete(child)
											End Try
										End If
									End If
								Next

								If Directory.GetDirectories(filePath).Length = 0 Then
									Try
										deletedirectory(filePath)
									Catch ex As Exception
										Application.Log.AddException(ex)
										TestDelete(filePath)
									End Try
								Else
									For Each data As String In Directory.GetDirectories(filePath)
										Application.Log.AddWarningMessage("Remaining folders found " + " : " + data)
									Next

								End If
							End If
						End If
					Next
				End If
			Catch ex As Exception
			End Try

		Next

		'Cleaning the GFE 2.0.1 and earlier assemblies.
		If config.RemoveGFE Then
			filePath = Environment.GetEnvironmentVariable("windir") + "\assembly\NativeImages_v4.0.30319_32"
			If Directory.Exists(filePath) Then
				For Each child As String In Directory.GetDirectories(filePath)
					If IsNullOrWhitespace(child) = False Then
						If child.ToLower.Contains("gfexperience") Or
						 child.ToLower.Contains("nvidia.sett") Or
						 child.ToLower.Contains("nvidia.updateservice") Or
						 child.ToLower.Contains("nvidia.win32api") Or
						 child.ToLower.Contains("installeruiextension") Or
						 child.ToLower.Contains("installerservice") Or
						 child.ToLower.Contains("gridservice") Or
						 child.ToLower.Contains("shadowplay") Or
						   child.ToLower.Contains("nvidia.gfe") Then
							Try
								deletedirectory(child)
							Catch ex As Exception
								Application.Log.AddException(ex)
								TestDelete(child)
							End Try
						End If
					End If
				Next
			End If
		End If

		'-----------------
		'MUI cache cleanUP
		'-----------------
		'Note: this MUST be done after cleaning the folders.
		Application.Log.AddMessage("MuiCache CleanUP")
		Try
			For Each regusers As String In My.Computer.Registry.Users.GetSubKeyNames
				If Not IsNullOrWhitespace(regusers) Then
					regkey = My.Computer.Registry.Users.OpenSubKey(regusers & "\software\classes\local settings\muicache", False)
					If regkey IsNot Nothing Then
						For Each child As String In regkey.GetSubKeyNames()
							If IsNullOrWhitespace(child) = False Then
								subregkey = regkey.OpenSubKey(child, False)
								If subregkey IsNot Nothing Then
									For Each childs As String In subregkey.GetSubKeyNames()
										If IsNullOrWhitespace(childs) = False Then
											For Each Keyname As String In subregkey.OpenSubKey(childs).GetValueNames
												If Not IsNullOrWhitespace(Keyname) Then

													If Keyname.ToLower.Contains("nvstlink.exe") Or
													 Keyname.ToLower.Contains("nvstview.exe") Or
													   Keyname.ToLower.Contains("gfexperience.exe") AndAlso config.RemoveGFE Or
													   Keyname.ToLower.Contains("nvcpluir.dll") Then
														Try
															deletevalue(subregkey.OpenSubKey(childs, True), Keyname)
														Catch ex As Exception
															Application.Log.AddException(ex)
														End Try
													End If
												End If
											Next
										End If
									Next
								End If
							End If
						Next
					End If
				End If
			Next
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		Try
			For Each regusers As String In My.Computer.Registry.Users.GetSubKeyNames
				If Not IsNullOrWhitespace(regusers) Then
					regkey = My.Computer.Registry.Users.OpenSubKey(regusers & "\software\classes\local settings\software\microsoft\windows\shell\muicache", True)
					If regkey IsNot Nothing Then

						For Each Keyname As String In regkey.GetValueNames
							If Not IsNullOrWhitespace(Keyname) Then

								If Keyname.ToLower.Contains("nvstlink.exe") Or
								 Keyname.ToLower.Contains("nvstview.exe") Or
								   Keyname.ToLower.Contains("gfexperience.exe") AndAlso config.RemoveGFE Or
								   Keyname.ToLower.Contains("nvcpluir.dll") Then
									Try
										deletevalue(regkey, Keyname)
									Catch ex As Exception
										Application.Log.AddException(ex)
									End Try
								End If
							End If
						Next
					End If
				End If
			Next
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

	End Sub

	Private Sub cleannvidia(ByVal config As ThreadSettings)
		Dim regkey As RegistryKey = Nothing
		Dim subregkey As RegistryKey = Nothing
		Dim subregkey2 As RegistryKey = Nothing
		Dim wantedvalue As String = Nothing
		Dim wantedvalue2 As String = Nothing
		Dim removegfe As Boolean = config.RemoveGFE
		Dim removephysx As Boolean = config.RemovePhysX

		'-----------------
		'Registry Cleaning
		'-----------------
		UpdateTextMethod(UpdateTextMethodmessagefn(5))
		Application.Log.AddMessage("Starting reg cleanUP... May take a minute or two.")


		'Deleting DCOM object /classroot
		Application.Log.AddMessage("Starting dcom/clsid/appid/typelib cleanup")

		CleanupEngine.classroot(IO.File.ReadAllLines(baseDir & "\settings\NVIDIA\classroot.cfg")) '// add each line as String Array.

		CleanupEngine.clsidleftover(IO.File.ReadAllLines(baseDir & "\settings\NVIDIA\clsidleftover.cfg")) '// add each line as String Array.

		'for GFE removal only
		If removegfe Then
			CleanupEngine.clsidleftover(IO.File.ReadAllLines(baseDir & "\settings\NVIDIA\clsidleftoverGFE.cfg")) '// add each line as String Array.
		End If
		'------------------------------
		'Clean the rebootneeded message
		'------------------------------
		Try

			regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames()
					If Not IsNullOrWhitespace(child) Then
						If child.ToLower.Contains("nvidia_rebootneeded") Then
							Try
								deletesubregkey(regkey, child)
							Catch ex As Exception
								Application.Log.AddException(ex)
							End Try
						End If
					End If
				Next
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		'-----------------
		'interface cleanup
		'-----------------

		CleanupEngine.interfaces(IO.File.ReadAllLines(baseDir & "\settings\NVIDIA\interface.cfg")) '// add each line as String Array.

		'When removing GFE only
		If removegfe Then
			CleanupEngine.interfaces(IO.File.ReadAllLines(baseDir & "\settings\NVIDIA\interfaceGFE.cfg")) '// add each line as String Array.
		End If

		Application.Log.AddMessage("Finished dcom/clsid/appid/typelib/interface cleanup")

		'end of deleting dcom stuff
		Application.Log.AddMessage("Pnplockdownfiles region cleanUP")

		CleanupEngine.Pnplockdownfiles(IO.File.ReadAllLines(baseDir & "\settings\NVIDIA\driverfiles.cfg")) '// add each line as String Array.

		'Cleaning PNPRessources.
		If My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpResources\Registry\HKLM\SOFTWARE\Khronos", False) IsNot Nothing Then
			Try
				deletesubregkey(My.Computer.Registry.LocalMachine, "SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpResources\Registry\HKLM\SOFTWARE\Khronos")
			Catch ex As Exception
				Application.Log.AddException(ex)
			End Try
		End If

		If My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpResources\Registry\HKLM\SOFTWARE\Wow6432Node\NVIDIA Corporation\Global", False) IsNot Nothing Then
			Try
				deletesubregkey(My.Computer.Registry.LocalMachine, "SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpResources\Registry\HKLM\SOFTWARE\Wow6432Node\NVIDIA Corporation\global")
			Catch ex As Exception
				Application.Log.AddException(ex)
			End Try
		End If

		If My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpResources\Registry\HKLM\SOFTWARE\Wow6432Node\NVIDIA Corporation", False) IsNot Nothing Then
			If My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpResources\Registry\HKLM\SOFTWARE\Wow6432Node\NVIDIA Corporation", False).SubKeyCount = 0 Then
				Try
					deletesubregkey(My.Computer.Registry.LocalMachine, "SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpResources\Registry\HKLM\SOFTWARE\Wow6432Node\NVIDIA Corporation")
				Catch ex As Exception
					Application.Log.AddException(ex)
				End Try
			End If
		End If

		If My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpResources\Registry\HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Controls Folder\Display\shellex\PropertySheetHandlers\NVIDIA CPL Extension", False) IsNot Nothing Then
			Try
				deletesubregkey(My.Computer.Registry.LocalMachine, "SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpResources\Registry\HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Controls Folder\Display\shellex\PropertySheetHandlers\NVIDIA CPL Extension")
			Catch ex As Exception
				Application.Log.AddException(ex)
			End Try
		End If

		If My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpResources\Registry\HKLM\SOFTWARE\NVIDIA Corporation", False) IsNot Nothing Then
			Try
				deletesubregkey(My.Computer.Registry.LocalMachine, "SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpResources\Registry\HKLM\SOFTWARE\NVIDIA Corporation")
			Catch ex As Exception
				Application.Log.AddException(ex)
			End Try
		End If

		If IntPtr.Size = 8 Then
			If My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpResources\Registry\HKLM\SOFTWARE\Wow6432Node\Khronos", False) IsNot Nothing Then
				Try
					deletesubregkey(My.Computer.Registry.LocalMachine, "SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpResources\Registry\HKLM\SOFTWARE\Wow6432Node\Khronos")
				Catch ex As Exception
					Application.Log.AddException(ex)
				End Try
			End If
		End If



		If removegfe Then
			'----------------------
			'Firewall entry cleanup
			'----------------------
			Application.Log.AddMessage("Firewall entry cleanUP")
			Try
				If winxp = False Then
					subregkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM", False)
					If subregkey IsNot Nothing Then
						For Each child2 As String In subregkey.GetSubKeyNames()
							If child2.ToLower.Contains("controlset") Then
								regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\" & child2 & "\Services\SharedAccess\Parameters\FirewallPolicy\FirewallRules", True)
								If regkey IsNot Nothing Then
									For Each child As String In regkey.GetValueNames()
										If IsNullOrWhitespace(child) = False Then
											If IsNullOrWhitespace(CStr(regkey.GetValue(child))) = False Then
												wantedvalue = regkey.GetValue(child).ToString()
											End If
											If wantedvalue.ToLower.ToString.Contains("nvstreamsrv") Or
											   wantedvalue.ToLower.ToString.Contains("nvidia network service") Or
											   wantedvalue.ToLower.ToString.Contains("nvidia update core") Then
												Try
													deletevalue(regkey, child)
												Catch ex As Exception
												End Try
											End If
										End If
									Next
								End If
							End If
						Next
					End If
				End If
			Catch ex As Exception
				Application.Log.AddException(ex)
			End Try
		End If
		'--------------------------
		'End Firewall entry cleanup
		'--------------------------
		Application.Log.AddMessage("End Firewall CleanUP")
		'--------------------------
		'Power Settings CleanUP
		'--------------------------
		Application.Log.AddMessage("Power Settings Cleanup")
		Try
			If winxp = False Then
				subregkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM", False)
				If subregkey IsNot Nothing Then
					For Each child2 As String In subregkey.GetSubKeyNames()
						If child2.ToLower.Contains("controlset") Then
							regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\" & child2 & "\Control\Power\PowerSettings", True)
							If regkey IsNot Nothing Then
								For Each childs As String In regkey.GetSubKeyNames()
									If IsNullOrWhitespace(childs) = False Then
										For Each child As String In regkey.OpenSubKey(childs).GetValueNames()
											If IsNullOrWhitespace(child) = False And child.ToString.ToLower.Contains("description") Then
												If IsNullOrWhitespace(CStr(regkey.OpenSubKey(childs).GetValue(child))) = False Then
													wantedvalue = regkey.OpenSubKey(childs).GetValue(child).ToString()
												End If
												If wantedvalue.ToString.ToLower.Contains("nvsvc") Then
													deletesubregkey(regkey, childs)
												End If
												If wantedvalue.ToString.ToLower.Contains("video and display power management") Then
													subregkey2 = regkey.OpenSubKey(childs, True)
													If subregkey2 IsNot Nothing Then
														For Each childinsubregkey2 As String In subregkey2.GetSubKeyNames()
															If IsNullOrWhitespace(childinsubregkey2) = False Then
																For Each childinsubregkey2value As String In subregkey2.OpenSubKey(childinsubregkey2).GetValueNames()
																	If IsNullOrWhitespace(childinsubregkey2value) = False And childinsubregkey2value.ToString.ToLower.Contains("description") Then
																		If IsNullOrWhitespace(CStr(subregkey2.OpenSubKey(childinsubregkey2).GetValue(childinsubregkey2value))) = False Then
																			wantedvalue2 = subregkey2.OpenSubKey(childinsubregkey2).GetValue(childinsubregkey2value).ToString
																		End If
																		If wantedvalue2.ToString.ToLower.Contains("nvsvc") Then
																			Try
																				deletesubregkey(subregkey2, childinsubregkey2)
																			Catch ex As Exception
																			End Try
																		End If
																	End If
																Next
															End If
														Next
													End If
												End If
											End If
										Next
									End If
								Next
							End If
						End If
					Next
				End If
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		'--------------------------
		'End Power Settings CleanUP
		'--------------------------
		Application.Log.AddMessage("End Power Settings Cleanup")

		'--------------------------------
		'System environement path cleanup
		'--------------------------------


		If removephysx Then
			Application.Log.AddMessage("System environement CleanUP")
			Try
				subregkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM", False)
				If subregkey IsNot Nothing Then
					For Each child2 As String In subregkey.GetSubKeyNames()
						If child2.ToLower.Contains("controlset") Then
							regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\" & child2 & "\Control\Session Manager\Environment", True)
							If regkey IsNot Nothing Then
								For Each child As String In regkey.GetValueNames()
									If IsNullOrWhitespace(child) = False Then
										If child.Contains("Path") Then
											If Not IsNullOrWhitespace(regkey.GetValue(child).ToString()) Then
												wantedvalue = regkey.GetValue(child).ToString.ToLower
												Try
													Select Case True
														Case wantedvalue.Contains(sysdrv & "\program files (x86)\nvidia corporation\physx\common;")
															wantedvalue = wantedvalue.Replace(sysdrv & "\program files (x86)\nvidia corporation\physx\common;", "")
															Try
																regkey.SetValue(child, wantedvalue)
															Catch ex As Exception
															End Try
														Case wantedvalue.Contains(";" + sysdrv & "\program files (x86)\nvidia corporation\physx\common")
															wantedvalue = wantedvalue.Replace(";" + sysdrv & "\program files (x86)\nvidia corporation\physx\common", "")
															Try
																regkey.SetValue(child, wantedvalue)
															Catch ex As Exception
															End Try
													End Select
												Catch ex As Exception
												End Try
											End If
										End If
									End If
								Next
							End If
						End If
					Next
				End If
			Catch ex As Exception
				Application.Log.AddException(ex)
			End Try
		End If
		'-------------------------------------
		'end system environement patch cleanup
		'-------------------------------------
		Application.Log.AddMessage("End System environement path cleanup")

		Try
			sysdrv = sysdrv.ToUpper
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey _
			  ("SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows", True)
			If regkey IsNot Nothing Then
				If IsNullOrWhitespace(CStr(regkey.GetValue("AppInit_DLLs"))) = False Then
					wantedvalue = CStr(regkey.GetValue("AppInit_DLLs"))	  'Will need to consider the comma in the future for multiple value
					If IsNullOrWhitespace(wantedvalue) = False Then
						Select Case True
							Case wantedvalue.Contains(sysdrv & "\PROGRA~2\NVIDIA~1\3DVISI~1\NVSTIN~1.DLL, " & sysdrv & "\PROGRA~1\NVIDIA~1\NVSTRE~1\rxinput.dll")
								wantedvalue = wantedvalue.Replace(sysdrv & "\PROGRA~2\NVIDIA~1\3DVISI~1\NVSTIN~1.DLL, " & sysdrv & "\PROGRA~1\NVIDIA~1\NVSTRE~1\rxinput.dll", "")
								regkey.SetValue("AppInit_DLLs", wantedvalue)

							Case wantedvalue.Contains(sysdrv & "\PROGRA~2\NVIDIA~1\3DVISI~1\NVSTIN~1.DLL")
								wantedvalue = wantedvalue.Replace(sysdrv & "\PROGRA~2\NVIDIA~1\3DVISI~1\NVSTIN~1.DLL", "")
								regkey.SetValue("AppInit_DLLs", wantedvalue)

							Case wantedvalue.Contains(sysdrv & "\PROGRA~1\NVIDIA~1\NVSTRE~1\rxinput.dll")
								wantedvalue = wantedvalue.Replace(sysdrv & "\PROGRA~1\NVIDIA~1\NVSTRE~1\rxinput.dll", "")
								regkey.SetValue("AppInit_DLLs", wantedvalue)
						End Select
					End If
				End If
				If CStr(regkey.GetValue("AppInit_DLLs")) = "" Then
					Try
						regkey.SetValue("LoadAppInit_DLLs", "0", RegistryValueKind.DWord)
					Catch ex As Exception
					End Try
				End If
			End If
			sysdrv = sysdrv.ToLower
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		Try
			If IntPtr.Size = 8 Then
				regkey = My.Computer.Registry.LocalMachine.OpenSubKey _
				   ("SOFTWARE\Wow6432Node\Microsoft\Windows NT\CurrentVersion\Windows", True)

				If regkey IsNot Nothing Then
					If IsNullOrWhitespace(CStr(regkey.GetValue("AppInit_DLLs"))) = False Then
						wantedvalue = CStr(regkey.GetValue("AppInit_DLLs"))
						If IsNullOrWhitespace(wantedvalue) = False Then
							Select Case True
								Case wantedvalue.Contains(sysdrv & "\PROGRA~2\NVIDIA~1\3DVISI~1\NVSTIN~1.DLL, " & sysdrv & "\PROGRA~2\NVIDIA~1\NVSTRE~1\rxinput.dll")
									wantedvalue = wantedvalue.Replace(sysdrv & "\PROGRA~2\NVIDIA~1\3DVISI~1\NVSTIN~1.DLL, " & sysdrv & "\PROGRA~2\NVIDIA~1\NVSTRE~1\rxinput.dll", "")
									regkey.SetValue("AppInit_DLLs", wantedvalue)

								Case wantedvalue.Contains(sysdrv & "\PROGRA~2\NVIDIA~1\3DVISI~1\NVSTIN~1.DLL")
									wantedvalue = wantedvalue.Replace(sysdrv & "\PROGRA~2\NVIDIA~1\3DVISI~1\NVSTIN~1.DLL", "")
									regkey.SetValue("AppInit_DLLs", wantedvalue)

								Case wantedvalue.Contains(sysdrv & "\PROGRA~2\NVIDIA~1\NVSTRE~1\rxinput.dll")
									wantedvalue = wantedvalue.Replace(sysdrv & "\PROGRA~2\NVIDIA~1\NVSTRE~1\rxinput.dll", "")
									regkey.SetValue("AppInit_DLLs", wantedvalue)
							End Select
						End If
					End If
					If CStr(regkey.GetValue("AppInit_DLLs")) = "" Then
						Try
							regkey.SetValue("LoadAppInit_DLLs", "0", RegistryValueKind.DWord)
						Catch ex As Exception
						End Try
					End If
				End If
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		'remove opencl registry Khronos
		Try
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey("Software\Khronos\OpenCL\Vendors", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetValueNames()
					If IsNullOrWhitespace(child) = False Then
						If child.ToLower.Contains("nvopencl") Then
							Try
								deletevalue(regkey, child)
							Catch ex As Exception
							End Try
						End If
					End If
				Next
				If regkey.GetValueNames().Length = 0 Then
					Try
						deletesubregkey(My.Computer.Registry.LocalMachine, "Software\Khronos\OpenCL")
					Catch ex As Exception
					End Try
				End If
				CleanVulkan(config)
				regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Khronos", True)
				If regkey IsNot Nothing Then
					If regkey.GetSubKeyNames().Length = 0 Then
						Try
							deletesubregkey(My.Computer.Registry.LocalMachine, "Software\Khronos")
						Catch ex As Exception
						End Try
					End If
				End If
			End If
		Catch ex As Exception
		End Try

		If IntPtr.Size = 8 Then
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey("Software\Wow6432Node\Khronos\OpenCL\Vendors", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetValueNames()
					If IsNullOrWhitespace(child) = False Then
						If child.ToLower.Contains("nvopencl") Then
							Try
								deletevalue(regkey, child)
							Catch ex As Exception
							End Try
						End If
					End If
				Next
				If regkey.GetValueNames().Length = 0 Then
					Try
						deletesubregkey(My.Computer.Registry.LocalMachine, "Software\Wow6432Node\Khronos")
					Catch ex As Exception
					End Try
				End If
			End If

		End If


		Try
			For Each users As String In My.Computer.Registry.Users.GetSubKeyNames()
				If Not IsNullOrWhitespace(users) Then
					regkey = My.Computer.Registry.Users.OpenSubKey(users & "\Software", True)
					If regkey IsNot Nothing Then
						For Each child As String In regkey.GetSubKeyNames()
							If IsNullOrWhitespace(child) = False Then
								If child.ToLower.Contains("nvidia corporation") Then
									For Each child2 As String In regkey.OpenSubKey(child).GetSubKeyNames()
										If IsNullOrWhitespace(child2) = False Then
											If child2.ToLower.Contains("global") Then
												If removegfe Then
													Try
														deletesubregkey(regkey.OpenSubKey(child, True), child2)
													Catch ex As Exception
													End Try
												Else
													For Each child3 As String In regkey.OpenSubKey(child + "\" + child2).GetSubKeyNames()
														If IsNullOrWhitespace(child3) = False Then
															If child3.ToLower.Contains("gfeclient") Or _
															 child3.ToLower.Contains("gfexperience") Or _
															 child3.ToLower.Contains("shadowplay") Or _
															 child3.ToLower.Contains("ledvisualizer") Then
																'do nothing
															Else
																Try
																	deletesubregkey(regkey.OpenSubKey(child + "\" + child2, True), child3)
																Catch ex As Exception
																End Try
															End If
														End If
													Next
												End If
											End If
											If child2.ToLower.Contains("logging") Or
											 child2.ToLower.Contains("nvbackend") AndAlso removegfe Or
											 child2.ToLower.Contains("nvidia update core") AndAlso removegfe Or
											 child2.ToLower.Contains("nvcontrolpanel2") Or
											 child2.ToLower.Contains("nvcontrolpanel") Or
											 child2.ToLower.Contains("nvtray") AndAlso removegfe Or
											 child2.ToLower.Contains("nvstream") AndAlso removegfe Or
											 child2.ToLower.Contains("nvidia control panel") Then
												Try
													deletesubregkey(regkey.OpenSubKey(child, True), child2)
												Catch ex As Exception
												End Try
											End If
										End If
									Next
									If regkey.OpenSubKey(child).SubKeyCount = 0 Then
										Try
											deletesubregkey(regkey, child)
										Catch ex As Exception
										End Try
									End If
								End If
							End If
						Next
					End If

					regkey = My.Computer.Registry.Users.OpenSubKey(users & "\SOFTWARE\Microsoft\Windows\CurrentVersion\UFH\SHC", True)
					If regkey IsNot Nothing Then
						For Each child As String In regkey.GetValueNames()
							If IsNullOrWhitespace(child) = False Then
								Dim tArray() As String = CType(regkey.GetValue(child), String())
								For i As Integer = 0 To tArray.Length - 1
									If IsNullOrWhitespace(tArray(i)) = False AndAlso Not tArray(i) = "" Then
										If tArray(i).ToLower.ToString.Contains("nvstview.exe") Or _
										   tArray(i).ToLower.ToString.Contains("vulkaninfo") Or _
										   tArray(i).ToLower.ToString.Contains("nvstlink.exe") Then
											Try
												deletevalue(regkey, child)
											Catch ex As Exception
											End Try
										End If
									End If
								Next
							End If
						Next
					End If
				End If
			Next
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\UFH\ARP", True)
		If regkey IsNot Nothing Then
			For Each child As String In regkey.GetValueNames()
				If IsNullOrWhitespace(child) = False Then
					Dim tArray() As String = CType(regkey.GetValue(child), String())
					For i As Integer = 0 To tArray.Length - 1
						If IsNullOrWhitespace(tArray(i)) = False AndAlso Not tArray(i) = "" Then
							If tArray(i).ToLower.ToString.Contains("nvi2.dll") Or _
							   tArray(i).ToLower.ToString.Contains("vulkaninfo") Or _
							   tArray(i).ToLower.ToString.Contains("nvstlink.exe") Then
								Try
									deletevalue(regkey, child)
								Catch ex As Exception
								End Try
							End If
						End If
					Next
				End If
			Next
		End If

		regkey = My.Computer.Registry.Users.OpenSubKey(".DEFAULT\Software", True)
		If regkey IsNot Nothing Then
			For Each child As String In regkey.GetSubKeyNames()
				If IsNullOrWhitespace(child) = False Then
					If child.ToLower.Contains("nvidia corporation") Then
						For Each child2 As String In regkey.OpenSubKey(child).GetSubKeyNames()
							If IsNullOrWhitespace(child2) = False Then
								If child2.ToLower.Contains("global") Or
								   child2.ToLower.Contains("nvbackend") Or
								   child2.ToLower.Contains("nvidia update core") AndAlso removegfe Or
								 child2.ToLower.Contains("nvcontrolpanel2") Or
								 child2.ToLower.Contains("nvidia control panel") Then
									Try
										deletesubregkey(regkey.OpenSubKey(child, True), child2)
									Catch ex As Exception
									End Try
								End If
							End If
						Next
						If regkey.OpenSubKey(child).SubKeyCount = 0 Then
							Try
								deletesubregkey(regkey, child)
							Catch ex As Exception
							End Try
						End If
					End If
				End If
			Next
		End If


		regkey = My.Computer.Registry.LocalMachine.OpenSubKey("Software", True)
		If regkey IsNot Nothing Then
			For Each child As String In regkey.GetSubKeyNames()
				If IsNullOrWhitespace(child) = False Then
					If child.ToLower.Contains("ageia technologies") Then
						If removephysx Then
							Try
								deletesubregkey(regkey, child)
							Catch ex As Exception
							End Try
						End If
					End If
					If child.ToLower.Contains("nvidia corporation") Then
						For Each child2 As String In regkey.OpenSubKey(child).GetSubKeyNames()
							If IsNullOrWhitespace(child2) = False Then
								If child2.ToLower.Contains("global") Then
									If removegfe Then
										Try
											deletesubregkey(regkey.OpenSubKey(child, True), child2)
										Catch ex As Exception
										End Try
									Else
										For Each child3 As String In regkey.OpenSubKey(child + "\" + child2).GetSubKeyNames()
											If IsNullOrWhitespace(child3) = False Then
												If child3.ToLower.Contains("gfeclient") Or _
												 child3.ToLower.Contains("gfexperience") Or _
												 child3.ToLower.Contains("nvbackend") Or _
												 child3.ToLower.Contains("nvscaps") Or _
												 child3.ToLower.Contains("shadowplay") Or _
												 child3.ToLower.Contains("ledvisualizer") Then
													'do nothing
												Else
													Try
														deletesubregkey(regkey.OpenSubKey(child + "\" + child2, True), child3)
													Catch ex As Exception
													End Try
												End If
											End If
										Next
									End If
								End If
								If child2.ToLower.Contains("installer") Or
								   child2.ToLower.Contains("logging") Or
								 child2.ToLower.Contains("installer2") AndAlso removegfe Or
								 child2.ToLower.Contains("nvidia update core") Or
								 child2.ToLower.Contains("nvcontrolpanel") Or
								 child2.ToLower.Contains("nvcontrolpanel2") Or
								 child2.ToLower.Contains("nvstream") AndAlso removegfe Or
								 child2.ToLower.Contains("nvstreamc") AndAlso removegfe Or
								 child2.ToLower.Contains("nvstreamsrv") AndAlso removegfe Or
								 child2.ToLower.Contains("physx_systemsoftware") Or
								 child2.ToLower.Contains("physxupdateloader") Or
								 child2.ToLower.Contains("uxd") Or
								 child2.ToLower.Contains("nvtray") AndAlso removegfe Then
									If removephysx Then
										Try
											deletesubregkey(regkey.OpenSubKey(child, True), child2)
										Catch ex As Exception
										End Try
									Else
										If child2.ToLower.Contains("physx") Then
											'do nothing
										Else
											Try
												deletesubregkey(regkey.OpenSubKey(child, True), child2)
											Catch ex As Exception
											End Try
										End If
									End If
								End If
							End If
						Next
						If regkey.OpenSubKey(child).SubKeyCount = 0 Then
							Try
								deletesubregkey(regkey, child)
							Catch ex As Exception
							End Try
						End If
					End If
				End If
			Next
		End If



		If IntPtr.Size = 8 Then
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey("Software\Wow6432Node", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames()
					If IsNullOrWhitespace(child) = False Then
						If child.ToLower.Contains("ageia technologies") Then
							If removephysx Then
								deletesubregkey(regkey, child)
							End If
						End If
						If child.ToLower.Contains("nvidia corporation") Then
							For Each child2 As String In regkey.OpenSubKey(child).GetSubKeyNames()
								If IsNullOrWhitespace(child2) = False Then
									If child2.ToLower.Contains("global") Then
										If removegfe Then
											Try
												deletesubregkey(regkey.OpenSubKey(child, True), child2)
											Catch ex As Exception
											End Try
										Else
											For Each child3 As String In regkey.OpenSubKey(child + "\" + child2).GetSubKeyNames()
												If IsNullOrWhitespace(child3) = False Then
													If child3.ToLower.Contains("gfeclient") Or _
													 child3.ToLower.Contains("gfexperience") Or _
													 child3.ToLower.Contains("nvbackend") Or _
													 child3.ToLower.Contains("nvscaps") Or _
													 child3.ToLower.Contains("shadowplay") Or _
													 child3.ToLower.Contains("ledvisualizer") Then
														'do nothing
													Else
														Try
															deletesubregkey(regkey.OpenSubKey(child + "\" + child2, True), child3)
														Catch ex As Exception
														End Try
													End If
												End If
											Next
										End If
									End If
									If child2.ToLower.Contains("logging") Or
									 child2.ToLower.Contains("physx_systemsoftware") Or
									 child2.ToLower.Contains("physxupdateloader") Or
									   child2.ToLower.Contains("installer2") Or
									   child2.ToLower.Contains("physx") Then
										If removephysx Then
											Try
												deletesubregkey(regkey.OpenSubKey(child, True), child2)
											Catch ex As Exception
											End Try
										Else
											If child2.ToLower.Contains("physx") Then
												'do nothing
											Else
												Try
													deletesubregkey(regkey.OpenSubKey(child, True), child2)
												Catch ex As Exception
												End Try
											End If
										End If
									End If
								End If
							Next
							If regkey.OpenSubKey(child).SubKeyCount = 0 Then
								Try
									deletesubregkey(regkey, child)
								Catch ex As Exception
								End Try
							End If
						End If
					End If
				Next
			End If
		End If



		If IntPtr.Size = 8 Then
			Try
				regkey = My.Computer.Registry.LocalMachine.OpenSubKey _
				 ("Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall", True)
				If regkey IsNot Nothing Then
					For Each child As String In regkey.GetSubKeyNames()
						If IsNullOrWhitespace(child) = False Then
							Try
								If removephysx Then
									If IsNullOrWhitespace(CStr(regkey.OpenSubKey(child).GetValue("DisplayName"))) = False Then
										If regkey.OpenSubKey(child).GetValue("DisplayName").ToString.ToLower.Contains("physx") Then
											deletesubregkey(regkey, child)
											Continue For
										End If
									End If
								End If
							Catch ex As Exception
								Application.Log.AddException(ex)
							End Try
							If child.ToLower.Contains("display.3dvision") Or
							 child.ToLower.Contains("3dtv") Or
							 child.ToLower.Contains("_display.controlpanel") Or
							 child.ToLower.Contains("_display.driver") Or
							 child.ToLower.Contains("_display.gfexperience") AndAlso removegfe Or
							 child.ToLower.Contains("_display.nvirusb") Or
							 child.ToLower.Contains("_display.physx") AndAlso removephysx Or
							 child.ToLower.Contains("_display.update") AndAlso removegfe Or
							 child.ToLower.Contains("_display.gamemonitor") AndAlso removegfe Or
							 child.ToLower.Contains("_gfexperience") AndAlso removegfe Or
							 child.ToLower.Contains("_hdaudio.driver") Or
							 child.ToLower.Contains("_installer") AndAlso removegfe Or
							 child.ToLower.Contains("_network.service") AndAlso removegfe Or
							 child.ToLower.Contains("_shadowplay") AndAlso removegfe Or
							 child.ToLower.Contains("_update.core") AndAlso removegfe Or
							 child.ToLower.Contains("nvidiastereo") Or
							 child.ToLower.Contains("_shieldwireless") AndAlso removegfe Or
							 child.ToLower.Contains("miracast.virtualaudio") AndAlso removegfe Or
							 child.ToLower.Contains("_virtualaudio.driver") AndAlso removegfe Then
								If removephysx = False And child.ToLower.Contains("physx") Then
									Continue For
								End If
								If config.Remove3DTVPlay = False And child.ToLower.Contains("3dtv") Then
									Continue For
								End If
								Try
									deletesubregkey(regkey, child)
								Catch ex As Exception
								End Try
							End If
						End If
					Next
				End If
			Catch ex As Exception
				Application.Log.AddException(ex)
			End Try
		End If


		Try

			regkey = My.Computer.Registry.LocalMachine.OpenSubKey _
			 ("Software\Microsoft\Windows\CurrentVersion\Uninstall", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames()
					If IsNullOrWhitespace(child) = False Then
						Try
							If removephysx Then
								If IsNullOrWhitespace(CStr(regkey.OpenSubKey(child).GetValue("DisplayName"))) = False Then
									If regkey.OpenSubKey(child).GetValue("DisplayName").ToString.ToLower.Contains("physx") Then
										deletesubregkey(regkey, child)
										Continue For
									End If
								End If
							End If
						Catch ex As Exception
							Application.Log.AddException(ex)
						End Try
						If child.ToLower.Contains("display.3dvision") Or
						 child.ToLower.Contains("3dtv") Or
						 child.ToLower.Contains("_display.controlpanel") Or
						 child.ToLower.Contains("_display.driver") Or
						 child.ToLower.Contains("_display.optimus") Or
						 child.ToLower.Contains("_display.gfexperience") AndAlso removegfe Or
						 child.ToLower.Contains("_display.nvirusb") Or
						 child.ToLower.Contains("_display.physx") Or
						 child.ToLower.Contains("_display.update") AndAlso removegfe Or
						 child.ToLower.Contains("_osc") AndAlso removegfe Or
						 child.ToLower.Contains("_display.nview") Or
						 child.ToLower.Contains("_display.nvwmi") Or
						 child.ToLower.Contains("_display.gamemonitor") AndAlso removegfe Or
						 child.ToLower.Contains("_nvidia.update") AndAlso removegfe Or
						 child.ToLower.Contains("_gfexperience") AndAlso removegfe Or
						 child.ToLower.Contains("_hdaudio.driver") Or
						 child.ToLower.Contains("_installer") AndAlso removegfe Or
						 child.ToLower.Contains("_network.service") AndAlso removegfe Or
						 child.ToLower.Contains("_shadowplay") AndAlso removegfe Or
						 child.ToLower.Contains("_update.core") AndAlso removegfe Or
						 child.ToLower.Contains("nvidiastereo") Or
						 child.ToLower.Contains("_shieldwireless") AndAlso removegfe Or
						 child.ToLower.Contains("miracast.virtualaudio") AndAlso removegfe Or
						 child.ToLower.Contains("_virtualaudio.driver") AndAlso removegfe Or
						 child.ToLower.Contains("vulkanrt1.") AndAlso config.RemoveVulkan Then
							If removephysx = False And child.ToLower.Contains("physx") Then
								Continue For
							End If

							If config.Remove3DTVPlay = False And child.ToLower.Contains("3dtv") Then
								Continue For
							End If
							Try
								deletesubregkey(regkey, child)
							Catch ex As Exception
							End Try
						End If
					End If
				Next
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		regkey = My.Computer.Registry.CurrentUser.OpenSubKey _
		 ("Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Compatibility Assistant\Store", True)
		If regkey IsNot Nothing Then
			For Each child As String In regkey.GetValueNames()
				If Not IsNullOrWhitespace(child) Then
					If child.ToLower.Contains("gfexperience.exe") AndAlso removegfe Then
						deletevalue(regkey, child)
					End If
				End If
			Next
		End If

		regkey = My.Computer.Registry.CurrentUser.OpenSubKey _
	  ("Software\Microsoft\.NETFramework\SQM\Apps", True)
		If regkey IsNot Nothing Then
			For Each child As String In regkey.GetSubKeyNames()
				If Not IsNullOrWhitespace(child) Then
					If child.ToLower.Contains("gfexperience.exe") AndAlso removegfe Then
						deletesubregkey(regkey, child)
					End If
				End If
			Next
		End If

		Try
			For Each users As String In My.Computer.Registry.Users.GetSubKeyNames()
				If Not IsNullOrWhitespace(users) Then
					regkey = My.Computer.Registry.Users.OpenSubKey _
				  (users + "\Software\Microsoft\.NETFramework\SQM\Apps", True)
					If regkey IsNot Nothing Then
						For Each child As String In regkey.GetSubKeyNames()
							If Not IsNullOrWhitespace(child) Then
								If child.ToLower.Contains("gfexperience.exe") AndAlso removegfe Then
									deletesubregkey(regkey, child)
								End If
							End If
						Next
					End If
				End If
			Next
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try


		Try

			For Each users As String In My.Computer.Registry.Users.GetSubKeyNames()
				If Not IsNullOrWhitespace(users) Then
					regkey = My.Computer.Registry.Users.OpenSubKey _
				  (users + "\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Compatibility Assistant\Store", True)
					If regkey IsNot Nothing Then
						For Each child As String In regkey.GetValueNames()
							If Not IsNullOrWhitespace(child) Then
								If child.ToLower.Contains("gfexperience.exe") AndAlso removegfe Then
									deletevalue(regkey, child)
								End If
							End If
						Next
					End If
				End If
			Next

		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try


		regkey = My.Computer.Registry.LocalMachine.OpenSubKey _
		 ("Software\Microsoft\Windows NT\CurrentVersion\ProfileList", True)
		If regkey IsNot Nothing Then
			For Each child As String In regkey.GetSubKeyNames()
				If IsNullOrWhitespace(child) = False Then
					subregkey = My.Computer.Registry.LocalMachine.OpenSubKey _
					("Software\Microsoft\Windows NT\CurrentVersion\ProfileList\" & child, False)
					If subregkey IsNot Nothing Then
						If IsNullOrWhitespace(CStr(subregkey.GetValue("ProfileImagePath"))) = False Then
							wantedvalue = subregkey.GetValue("ProfileImagePath").ToString
							If IsNullOrWhitespace(wantedvalue) = False Then
								If wantedvalue.Contains("UpdatusUser") Then
									Try
										deletesubregkey(regkey, child)
									Catch ex As Exception
									End Try
								End If
							End If
						End If
					End If
				End If
			Next
		End If


		regkey = My.Computer.Registry.LocalMachine.OpenSubKey _
		 ("Software\Microsoft\Windows\CurrentVersion\Explorer\ControlPanel\NameSpace", True)
		If regkey IsNot Nothing Then
			For Each child As String In regkey.GetSubKeyNames()
				If IsNullOrWhitespace(child) = False Then
					subregkey = My.Computer.Registry.LocalMachine.OpenSubKey _
			   ("Software\Microsoft\Windows\CurrentVersion\Explorer\ControlPanel\NameSpace\" & child, False)
					If subregkey IsNot Nothing Then
						If IsNullOrWhitespace(CStr(subregkey.GetValue(""))) = False Then
							wantedvalue = subregkey.GetValue("").ToString
							If IsNullOrWhitespace(wantedvalue) = False Then
								If wantedvalue.ToLower.Contains("nvidia control panel") Or
								   wantedvalue.ToLower.Contains("nvidia nview desktop manager") Then
									Try
										deletesubregkey(regkey, child)
									Catch ex As Exception
									End Try
									'special case only to nvidia afaik. there i a clsid for a control pannel that link from namespace.
									Try
										deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("CLSID", True), child)
									Catch ex As Exception
									End Try
								End If
							End If
						End If
					End If
				End If
			Next
		End If


		'----------------------
		'.net ngenservice clean
		'----------------------
		Application.Log.AddMessage("ngenservice Clean")

		regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\.NETFramework\v2.0.50727\NGenService\Roots", True)
		If regkey IsNot Nothing Then
			For Each child As String In regkey.GetSubKeyNames()
				If IsNullOrWhitespace(child) = False Then
					If child.ToLower.Contains("gfexperience.exe") AndAlso removegfe Then
						Try
							deletesubregkey(regkey, child)
						Catch ex As Exception
						End Try
					End If
				End If
			Next
		End If

		If IntPtr.Size = 8 Then

			regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Wow6432Node\Microsoft\.NETFramework\v2.0.50727\NGenService\Roots", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames()
					If IsNullOrWhitespace(child) = False Then
						If child.ToLower.Contains("gfexperience.exe") AndAlso removegfe Then
							Try
								deletesubregkey(regkey, child)
							Catch ex As Exception
							End Try
						End If
					End If
				Next
			End If
		End If
		Application.Log.AddMessage("End ngenservice Clean")
		'-----------------------------
		'End of .net ngenservice clean
		'-----------------------------

		'-----------------------------
		'Mozilla plugins
		'-----------------------------
		regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\MozillaPlugins", True)
		If regkey IsNot Nothing Then
			For Each child As String In regkey.GetSubKeyNames()
				If IsNullOrWhitespace(child) = False Then
					If child.ToLower.Contains("nvidia.com/3dvision") Then
						Try
							deletesubregkey(regkey, child)
						Catch ex As Exception
						End Try
					End If
				End If
			Next
		End If


		If IntPtr.Size = 8 Then
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Wow6432Node\MozillaPlugins", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames()
					If IsNullOrWhitespace(child) = False Then
						If child.ToLower.Contains("nvidia.com/3dvision") Then
							Try
								deletesubregkey(regkey, child)
							Catch ex As Exception
							End Try
						End If
					End If
				Next
			End If
		End If


		'-----------------------
		'remove event view stuff
		'-----------------------
		Application.Log.AddMessage("Remove eventviewer stuff")

		subregkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM", False)
		If subregkey IsNot Nothing Then
			For Each child2 As String In subregkey.GetSubKeyNames()
				If IsNullOrWhitespace(child2) = False Then
					If child2.ToLower.Contains("controlset") Then
						regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\" & child2 & "\Services\eventlog\Application", True)
						If regkey IsNot Nothing Then
							For Each child As String In regkey.GetSubKeyNames()
								If IsNullOrWhitespace(child) = False Then
									If child.ToLower.StartsWith("nvidia update") Or
									 (child.ToLower.StartsWith("nvstreamsvc") AndAlso removegfe) Or
									 child.ToLower.StartsWith("nvidia opengl driver") Or
									 child.ToLower.StartsWith("nvwmi") Or
									 child.ToLower.StartsWith("nview") Then
										Try
											deletesubregkey(regkey, child)
										Catch ex As Exception
											Application.Log.AddException(ex)
										End Try
									End If
								End If
							Next
						End If
					End If
				End If
			Next
		End If

		subregkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM", False)
		If subregkey IsNot Nothing Then
			For Each child2 As String In subregkey.GetSubKeyNames()
				If IsNullOrWhitespace(child2) = False Then
					If child2.ToLower.Contains("controlset") Then
						regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\" & child2 & "\Services\eventlog\System", True)
						If regkey IsNot Nothing Then
							For Each child As String In regkey.GetSubKeyNames()
								If IsNullOrWhitespace(child) = False Then
									If child.ToLower.StartsWith("nvidia update") Or
									 child.ToLower.StartsWith("nvidia opengl driver") Or
									 child.ToLower.StartsWith("nvwmi") Or
									 child.ToLower.StartsWith("nview") Then
										deletesubregkey(regkey, child)
									End If
								End If
							Next
						End If
					End If
				End If
			Next
		End If

		Application.Log.AddMessage("End Remove eventviewer stuff")
		'---------------------------
		'end remove event view stuff
		'---------------------------

		'---------------------------
		'virtual store
		'---------------------------

		regkey = My.Computer.Registry.ClassesRoot.OpenSubKey("VirtualStore\MACHINE\SOFTWARE\NVIDIA Corporation", True)
		If regkey IsNot Nothing Then
			Try
				deletesubregkey(regkey, "Global")
			Catch ex As Exception
			End Try
			If regkey.SubKeyCount = 0 Then
				Try
					deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("VirtualStore\MACHINE\SOFTWARE", True), "NVIDIA Corporation")
				Catch ex As Exception
				End Try
			End If
		End If

		Try
			For Each users As String In My.Computer.Registry.Users.GetSubKeyNames()
				If Not IsNullOrWhitespace(users) Then
					regkey = My.Computer.Registry.Users.OpenSubKey(users & "\Software\Classes\VirtualStore\MACHINE\SOFTWARE\NVIDIA Corporation", True)
					If regkey IsNot Nothing Then
						Try
							deletesubregkey(regkey, "Global")
						Catch ex As Exception
						End Try
						If regkey.SubKeyCount = 0 Then
							Try
								deletesubregkey(My.Computer.Registry.Users.OpenSubKey(users & "\Software\Classes\VirtualStore\MACHINE\SOFTWARE", True), "NVIDIA Corporation")
							Catch ex As Exception
							End Try
						End If
					End If
				End If
			Next
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		Try
			For Each child As String In My.Computer.Registry.Users.GetSubKeyNames()
				If Not IsNullOrWhitespace(child) Then
					If child.ToLower.Contains("s-1-5") Then
						Try
							deletesubregkey(My.Computer.Registry.Users.OpenSubKey(child & "Software\Classes\VirtualStore\MACHINE\SOFTWARE\NVIDIA Corporation", True), "Global")
							If My.Computer.Registry.Users.OpenSubKey(child & "Software\Classes\VirtualStore\MACHINE\SOFTWARE\NVIDIA Corporation", False).SubKeyCount = 0 Then
								deletesubregkey(My.Computer.Registry.Users.OpenSubKey(child & "Software\Classes\VirtualStore\MACHINE\SOFTWARE", True), "NVIDIA Corporation")
							End If
						Catch ex As Exception
						End Try
					End If
				End If
			Next
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try


		Try
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey _
			("Software\Microsoft\Windows\CurrentVersion\Run", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetValueNames
					If Not IsNullOrWhitespace(child) Then
						If StrContainsAny(child, True, "nvtmru", "NvCplDaemon", "NvMediaCenter", "NvBackend", "nwiz", "ShadowPlay", "StereoLinksInstall", "NvGameMonitor") Then
							deletevalue(regkey, child)
						End If
					End If
				Next
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		Try
			If IntPtr.Size = 8 Then
				regkey = My.Computer.Registry.LocalMachine.OpenSubKey _
				 ("Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run", True)
				If regkey IsNot Nothing Then
					For Each child As String In regkey.GetValueNames
						If Not IsNullOrWhitespace(child) Then
							If StrContainsAny(child, True, "StereoLinksInstall") Then
								deletevalue(regkey, child)
							End If
						End If
					Next
				End If
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		CleanupEngine.installer(IO.File.ReadAllLines(baseDir & "\settings\NVIDIA\packages.cfg"), config)


		If config.Remove3DTVPlay Then
			Try
				deletesubregkey(My.Computer.Registry.ClassesRoot, "mpegfile\shellex\ContextMenuHandlers\NvPlayOnMyTV")
			Catch ex As Exception
			End Try
			Try
				deletesubregkey(My.Computer.Registry.ClassesRoot, "WMVFile\shellex\ContextMenuHandlers\NvPlayOnMyTV")
			Catch ex As Exception
			End Try
			Try
				deletesubregkey(My.Computer.Registry.ClassesRoot, "AVIFile\shellex\ContextMenuHandlers\NvPlayOnMyTV")
			Catch ex As Exception
			End Try
		End If

		'-----------------------------
		'Shell extensions\aproved
		'-----------------------------
		Try
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetValueNames()
					If IsNullOrWhitespace(child) = False Then
						If regkey.GetValue(child).ToString.ToLower.Contains("nvcpl desktopcontext class") Or
						   regkey.GetValue(child).ToString.ToLower.Contains("nview desktop context menu") Or
						   regkey.GetValue(child).ToString.ToLower.Contains("nvappshext extension") Or
						   regkey.GetValue(child).ToString.ToLower.Contains("openglshext extension") Or
						   regkey.GetValue(child).ToString.ToLower.Contains("nvidia play on my tv context menu extension") Then
							Try
								deletevalue(regkey, child)
							Catch ex As Exception
							End Try
						End If
					End If
				Next
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		Try
			deletesubregkey(My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Controls Folder\" &
						"Display\shellex\PropertySheetHandlers", True), "NVIDIA CPL Extension")
		Catch ex As Exception
		End Try

		regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Control Panel\Extended Properties", False)
		If regkey IsNot Nothing Then
			For Each child As String In regkey.GetSubKeyNames()
				If IsNullOrWhitespace(child) = False Then
					For Each childs As String In regkey.OpenSubKey(child).GetValueNames()
						If Not IsNullOrWhitespace(childs) Then
							If childs.ToLower.Contains("nvcpl.cpl") Then
								Try
									deletevalue(regkey.OpenSubKey(child, True), childs)
								Catch ex As Exception
								End Try
							End If
						End If
					Next
				End If
			Next
		End If


		If IntPtr.Size = 8 Then

			regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetValueNames()
					If IsNullOrWhitespace(child) = False Then
						If regkey.GetValue(child).ToString.ToLower.Contains("nvcpl desktopcontext class") Then
							Try
								deletevalue(regkey, child)
							Catch ex As Exception
							End Try
						End If
					End If
				Next
			End If

		End If
		'-----------------------------
		'End Shell extensions\aprouved
		'-----------------------------

		'Shell ext
		Try
			deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("Directory\background\shellex\ContextMenuHandlers", True), "NvCplDesktopContext")
		Catch ex As Exception
		End Try

		Try
			deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("Directory\background\shellex\ContextMenuHandlers", True), "00nView")
		Catch ex As Exception
		End Try

		Try
			deletesubregkey(My.Computer.Registry.LocalMachine.OpenSubKey("Software\Classes\Directory\background\shellex\ContextMenuHandlers", True), "NvCplDesktopContext")
		Catch ex As Exception
		End Try

		Try
			deletesubregkey(My.Computer.Registry.LocalMachine.OpenSubKey("Software\Classes\Directory\background\shellex\ContextMenuHandlers", True), "00nView")
		Catch ex As Exception
		End Try

		'Cleaning of some "open with application" related to 3d vision
		regkey = My.Computer.Registry.ClassesRoot.OpenSubKey("jpsfile\shell\open\command", True)
		If regkey IsNot Nothing Then
			If (Not IsNullOrWhitespace(CType(regkey.GetValue(""), String))) AndAlso regkey.GetValue("").ToString.ToLower.Contains _
			 ("nvstview") Then
				Try
					deletesubregkey(My.Computer.Registry.ClassesRoot, "jpsfile")
				Catch ex As Exception
				End Try
			End If
		End If
		regkey = My.Computer.Registry.ClassesRoot.OpenSubKey("mpofile\shell\open\command", True)
		If regkey IsNot Nothing Then
			If (Not IsNullOrWhitespace(CStr(regkey.GetValue("")))) AndAlso regkey.GetValue("").ToString.ToLower.Contains _
			 ("nvstview") Then
				Try
					deletesubregkey(My.Computer.Registry.ClassesRoot, "mpofile")
				Catch ex As Exception
				End Try
			End If
		End If

		regkey = My.Computer.Registry.ClassesRoot.OpenSubKey("pnsfile\shell\open\command", True)
		If regkey IsNot Nothing Then
			If (Not IsNullOrWhitespace(CStr(regkey.GetValue("")))) AndAlso regkey.GetValue("").ToString.ToLower.Contains _
			 ("nvstview") Then
				Try
					deletesubregkey(My.Computer.Registry.ClassesRoot, "pnsfile")
				Catch ex As Exception
				End Try
			End If
		End If

		Try
			deletesubregkey(My.Computer.Registry.ClassesRoot, ".tvp")  'CrazY_Milojko
		Catch ex As Exception
		End Try

		UpdateTextMethod("-End of Registry Cleaning")

		Application.Log.AddMessage("End of Registry Cleaning")

	End Sub

	Private Sub cleanintelfolders()

		Dim filePath As String = Nothing

		UpdateTextMethod(UpdateTextMethodmessagefn(4))

		Application.Log.AddMessage("Cleaning Directory")

		CleanupEngine.folderscleanup(IO.File.ReadAllLines(baseDir & "\settings\INTEL\driverfiles.cfg"))	'// add each line as String Array.

		filePath = System.Environment.SystemDirectory
		Dim files() As String = IO.Directory.GetFiles(filePath + "\", "igfxcoin*.*")
		For i As Integer = 0 To files.Length - 1
			If Not IsNullOrWhitespace(files(i)) Then
				Try
					deletefile(files(i))
				Catch ex As Exception
				End Try
			End If
		Next

	End Sub

	Private Sub cleanintelserviceprocess()

		CleanupEngine.cleanserviceprocess(IO.File.ReadAllLines(baseDir & "\settings\INTEL\services.cfg")) '// add each line as String Array.

		Dim appproc = process.GetProcessesByName("IGFXEM")
		For i As Integer = 0 To appproc.Length - 1
			appproc(i).Kill()
		Next i

	End Sub

	Private Sub cleanintel(ByVal config As ThreadSettings)

		Dim regkey As RegistryKey = Nothing
		Dim subregkey As RegistryKey = Nothing
		Dim wantedvalue As String = Nothing
		Dim packages As String()

		UpdateTextMethod(UpdateTextMethodmessagefn(5))

		Application.Log.AddMessage("Cleaning registry")

		CleanupEngine.Pnplockdownfiles(IO.File.ReadAllLines(baseDir & "\settings\INTEL\driverfiles.cfg")) '// add each line as String Array.

		CleanupEngine.classroot(IO.File.ReadAllLines(baseDir & "\settings\INTEL\classroot.cfg")) '// add each line as String Array.

		CleanupEngine.interfaces(IO.File.ReadAllLines(baseDir & "\settings\INTEL\interface.cfg")) '// add each line as String Array.

		CleanupEngine.clsidleftover(IO.File.ReadAllLines(baseDir & "\settings\INTEL\clsidleftover.cfg")) '// add each line as String Array.

		Try
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey("Software\Intel", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames()
					If IsNullOrWhitespace(child) = False Then
						If child.ToLower.Contains("igfx") Or
						   child.ToLower.Contains("mediasdk") Or
						   child.ToLower.Contains("opencl") Or
						   child.ToLower.Contains("intel wireless display") Then
							Try
								deletesubregkey(regkey, child)
							Catch ex As Exception
							End Try
						End If
					End If
				Next
				If regkey.SubKeyCount = 0 Then
					Try
						deletesubregkey(My.Computer.Registry.LocalMachine.OpenSubKey("Software", True), "Intel")
					Catch ex As Exception
					End Try
				End If
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		Try
			For Each users As String In My.Computer.Registry.Users.GetSubKeyNames()
				If Not IsNullOrWhitespace(users) Then
					regkey = My.Computer.Registry.Users.OpenSubKey(users & "\Software\Intel", True)
					If regkey IsNot Nothing Then
						For Each child As String In regkey.GetSubKeyNames()
							If IsNullOrWhitespace(child) = False Then
								If child.ToLower.Contains("display") Then
									Try
										deletesubregkey(regkey, child)
									Catch ex As Exception
									End Try
								End If
							End If
						Next
						If regkey.SubKeyCount = 0 Then
							Try
								deletesubregkey(My.Computer.Registry.Users.OpenSubKey(users & "\Software", True), "Intel")
							Catch ex As Exception
							End Try
						End If
					End If
				End If
			Next
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		If IntPtr.Size = 8 Then
			Try
				regkey = My.Computer.Registry.LocalMachine.OpenSubKey("Software\Wow6432Node\Intel", True)
				If regkey IsNot Nothing Then
					For Each child As String In regkey.GetSubKeyNames()
						If IsNullOrWhitespace(child) = False Then
							If child.ToLower.Contains("igfx") Or
							   child.ToLower.Contains("mediasdk") Or
							   child.ToLower.Contains("opencl") Or
							   child.ToLower.Contains("intel wireless display") Then
								Try
									deletesubregkey(regkey, child)
								Catch ex As Exception
								End Try
							End If
						End If
					Next
					If regkey.SubKeyCount = 0 Then
						Try
							deletesubregkey(My.Computer.Registry.LocalMachine.OpenSubKey("Software\Wow6432Node", True), "Intel")
						Catch ex As Exception
						End Try
					End If
				End If
			Catch ex As Exception
				Application.Log.AddException(ex)
			End Try
		End If


		Try
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey _
			("Software\Microsoft\Windows\CurrentVersion\Run", True)
			If regkey IsNot Nothing Then
				Try
					deletevalue(regkey, "IgfxTray")
				Catch ex As Exception
					Application.Log.AddException(ex)
				End Try

				Try
					deletevalue(regkey, "Persistence")
				Catch ex As Exception
					Application.Log.AddException(ex)
				End Try

				Try
					deletevalue(regkey, "HotKeysCmds")
				Catch ex As Exception
					Application.Log.AddException(ex)
				End Try
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		Try
			regkey = My.Computer.Registry.ClassesRoot.OpenSubKey _
			 ("Directory\background\shellex\ContextMenuHandlers", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames()
					If IsNullOrWhitespace(child) = False Then
						If child.ToLower.Contains("igfxcui") Or
						   child.ToLower.Contains("igfxosp") Or
						 child.ToLower.Contains("igfxdtcm") Then

							deletesubregkey(regkey, child)

						End If
					End If

				Next
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		CleanupEngine.installer(IO.File.ReadAllLines(baseDir & "\settings\INTEL\packages.cfg"), config)

		If IntPtr.Size = 8 Then
			packages = IO.File.ReadAllLines(baseDir & "\settings\INTEL\packages.cfg") '// add each line as String Array.
			Try
				regkey = My.Computer.Registry.LocalMachine.OpenSubKey _
				 ("Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall", True)
				If regkey IsNot Nothing Then
					For Each child As String In regkey.GetSubKeyNames()
						If IsNullOrWhitespace(child) = False Then
							Try
								subregkey = My.Computer.Registry.LocalMachine.OpenSubKey _
								("Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\" & child, True)
							Catch ex As Exception
								Continue For
							End Try
							If subregkey IsNot Nothing Then
								If IsNullOrWhitespace(CStr(subregkey.GetValue("DisplayName"))) = False Then
									wantedvalue = subregkey.GetValue("DisplayName").ToString
									If IsNullOrWhitespace(wantedvalue) = False Then
										For i As Integer = 0 To packages.Length - 1
											If Not IsNullOrWhitespace(packages(i)) Then
												If wantedvalue.ToLower.Contains(packages(i).ToLower) Then
													Try
														If Not (config.RemoveVulkan = False AndAlso StrContainsAny(wantedvalue, True, "vulkan")) Then
															deletesubregkey(regkey, child)
														End If
													Catch ex As Exception
													End Try
												End If
											End If
										Next
									End If
								End If
							End If
						End If
					Next
				End If
			Catch ex As Exception
				Application.Log.AddException(ex)
			End Try
		End If
		Try
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Control Panel\Cpls", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames()
					If IsNullOrWhitespace(child) = False Then
						If child.ToLower.Contains("igfxcpl") Then
							Try
								deletesubregkey(regkey, child)
							Catch ex As Exception
							End Try
						End If
					End If
				Next
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		'Special Cleanup For Intel PnpResources
		Try
			If win8higher Then
				regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpResources\Registry\HKCR", True)
				If regkey IsNot Nothing Then
					Dim classroot As String() = IO.File.ReadAllLines(baseDir & "\settings\INTEL\classroot.cfg")
					For Each child As String In regkey.GetSubKeyNames()
						If Not IsNullOrWhitespace(child) Then
							For i As Integer = 0 To classroot.Length - 1
								If Not IsNullOrWhitespace(classroot(i)) Then
									If child.ToLower.Contains(classroot(i).ToLower) Then
										Try
											deletesubregkey(regkey, child)
										Catch ex As Exception
										End Try
									End If
								End If
							Next
						End If
					Next
				End If
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		Try
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon\Notify", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames()
					If Not IsNullOrWhitespace(child) Then
						If child.ToLower.Contains("igfx") Then
							Try
								deletesubregkey(regkey, child)
							Catch ex As Exception
							End Try
						End If
					End If
				Next
				If regkey.SubKeyCount = 0 Then
					Try
						deletesubregkey(My.Computer.Registry.LocalMachine, "SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon\Notify")
					Catch ex As Exception
					End Try
				End If
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		UpdateTextMethod(UpdateTextMethodmessagefn(6))
	End Sub

	Private Sub checkpcieroot()	 'This is for Nvidia Optimus to prevent the yellow mark on the PCI-E controler. We must remove the UpperFilters.

		Dim regkey As RegistryKey = Nothing
		Dim subregkey As RegistryKey = Nothing
		Dim array() As String

		UpdateTextMethod(UpdateTextMethodmessagefn(7))

		Application.Log.AddMessage("Starting the removal of nVidia Optimus UpperFilter if present.")

		Try
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey _
			   ("SYSTEM\CurrentControlSet\Enum\PCI")
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames()
					If Not IsNullOrWhitespace(child) Then
						If child.ToLower.Contains("ven_8086") Then
							subregkey = regkey.OpenSubKey(child)
							If subregkey IsNot Nothing Then
								For Each childs As String In subregkey.GetSubKeyNames()
									If IsNullOrWhitespace(childs) = False Then
										array = CType(subregkey.OpenSubKey(childs).GetValue("UpperFilters"), String())
										If (array IsNot Nothing) AndAlso (Not array.Length < 1) Then
											For i As Integer = 0 To array.Length - 1
												If Not IsNullOrWhitespace(array(i)) Then
													Application.Log.AddMessage("UpperFilter found : " + array(i))
													If (array(i).ToLower.Contains("nvpciflt")) Then
														Dim AList As ArrayList = New ArrayList(array)

														AList.Remove("nvpciflt")
														AList.Remove("nvkflt")

														Application.Log.AddMessage("nVidia Optimus UpperFilter Found.")
														Dim upfiler As String() = CType(AList.ToArray(GetType(String)), String())

														Try

															deletevalue(subregkey.OpenSubKey(childs, True), "UpperFilters")
															If (upfiler IsNot Nothing) AndAlso (Not upfiler.Length < 1) Then
																subregkey.OpenSubKey(childs, True).SetValue("UpperFilters", upfiler, RegistryValueKind.MultiString)
															End If
														Catch ex As Exception
															Application.log.AddException(ex)
															Application.Log.AddMessage("Failed to fix Optimus. You will have to manually remove the device with yellow mark in device manager to fix the missing videocard")
														End Try
													End If
												End If
											Next
										End If
									End If
								Next
							End If
						End If
					End If
				Next
			End If
		Catch ex As Exception
			MessageBox.Show(Languages.GetTranslation(Me.Name, "Messages", "Text6"), Application.Current.MainWindow.GetType().Assembly.GetName().Name, MessageBoxButtons.OK, MessageBoxIcon.Error)
			Application.log.AddException(ex)
		End Try
	End Sub

	Private Sub restartcomputer()

		Application.Log.AddMessage("Restarting Computer ")
		processinfo.FileName = "shutdown"
		processinfo.Arguments = "/r /t 0"
		processinfo.WindowStyle = ProcessWindowStyle.Hidden
		processinfo.UseShellExecute = True
		processinfo.CreateNoWindow = True
		processinfo.RedirectStandardOutput = False

		process.StartInfo = processinfo
		process.Start()
		process.WaitForExit()
		process.Close()
		closeddu()

	End Sub

	Private Sub shutdowncomputer()
		preventclose = False
		processinfo.FileName = "shutdown"
		processinfo.Arguments = "/s /t 0"
		processinfo.WindowStyle = ProcessWindowStyle.Hidden
		processinfo.UseShellExecute = True
		processinfo.CreateNoWindow = True
		processinfo.RedirectStandardOutput = False

		process.StartInfo = processinfo
		process.Start()
		process.WaitForExit()
		process.Close()
		closeddu()

	End Sub

	Private Sub rescan()

		'Scan for new devices...
		Dim scan As New ProcessStartInfo
		scan.FileName = baseDir & "\" & ddudrfolder & "\ddudr.exe"
		scan.Arguments = "rescan"
		scan.UseShellExecute = False
		scan.CreateNoWindow = True
		scan.RedirectStandardOutput = False


		UpdateTextMethod(UpdateTextMethodmessagefn(8))
		Application.Log.AddMessage("Scanning for new device...")
		Dim proc4 As New Process
		proc4.StartInfo = scan
		proc4.Start()
		proc4.WaitForExit()
		proc4.Close()
		System.Threading.Thread.Sleep(2000)
		If Not safemode Then
			Dim appproc = process.GetProcessesByName("explorer")
			For i As Integer = 0 To appproc.Length - 1
				appproc(i).Kill()
			Next i
		End If


	End Sub

	Private Function winupdatepending() As Boolean
		Dim regkey As RegistryKey = Nothing
		regkey = Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired")
		If regkey IsNot Nothing Then
			Return True
		Else
			Return False
		End If
	End Function

	Private Sub gpuidentify(ByVal gpu As String)

		Dim regkey As RegistryKey = Nothing
		Dim subregkey As RegistryKey = Nothing
		Dim array() As String

		Try
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Enum\PCI")

			For Each child As String In regkey.GetSubKeyNames
				If Not IsNullOrWhitespace(child) Then
					If child.ToLower.Contains(gpu) Then

						subregkey = regkey.OpenSubKey(child)
						For Each child2 As String In subregkey.GetSubKeyNames
							array = CType(subregkey.OpenSubKey(child2).GetValue("CompatibleIDs"), String())
							If (array IsNot Nothing) AndAlso (Not (array.Length < 1)) Then
								For i As Integer = 0 To array.Length - 1
									If array(i).ToLower.Contains("pci\cc_03") Then
										For j As Integer = 0 To array.Length - 1
											If array(j).ToLower.Contains("ven_8086") Then
												Application.Settings.SelectedGPU = GPUVendor.Intel
												Return
												'ComboBox1.SelectedIndex = 2
												'PictureBox2.Location = New Point(picturebox2originalx, picturebox2originaly)
												'PictureBox2.Size = New Size(158, 126)
											End If
											If array(j).ToLower.Contains("ven_1002") Then
												Application.Settings.SelectedGPU = GPUVendor.AMD
												Return
												'ComboBox1.SelectedIndex = 1
												'PictureBox2.Location = New Point(picturebox2originalx, picturebox2originaly)
												'PictureBox2.Size = New Size(158, 126)
											End If
											If array(j).ToLower.Contains("ven_10de") Then
												Application.Settings.SelectedGPU = GPUVendor.Nvidia
												Return
												'ComboBox1.SelectedIndex = 0
												'PictureBox2.Location = New Point(CInt(286 * (picturebox2originalx / 333)), CInt(92 * (picturebox2originaly / 92)))
												'PictureBox2.Size = New Size(252, 123)
											End If
										Next
									End If
								Next
							End If
						Next
					End If
				End If
			Next
		Catch ex As Exception
			MessageBox.Show(Languages.GetTranslation(Me.Name, "Messages", "Text6"), Application.Current.MainWindow.GetType().Assembly.GetName().Name, MessageBoxButtons.OK, MessageBoxIcon.Error)
			Application.log.AddException(ex)
		End Try
	End Sub

	Private Sub restartinsafemode(Optional ByVal withNetwork As Boolean = False)

		Dim regkey As RegistryKey = Nothing

		systemrestore()	'we try to do a system restore if allowed before going into safemode.
		Application.Log.AddMessage("restarting in safemode")


		Me.Topmost = False

		Dim setbootconf As New ProcessStartInfo("bcdedit")

		If withNetwork Then
			setbootconf.Arguments = "/set safeboot network"
		Else
			setbootconf.Arguments = "/set safeboot minimal"
		End If

		setbootconf.UseShellExecute = False
		setbootconf.CreateNoWindow = True
		setbootconf.RedirectStandardOutput = False

		Dim processstopservice As New Process
		processstopservice.StartInfo = setbootconf
		processstopservice.Start()
		processstopservice.WaitForExit()
		processstopservice.Close()

		Try
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", True)

			If regkey IsNot Nothing Then
				'Dim sw As StreamWriter = System.IO.File.CreateText(basedir + "\DDU.bat")
				'sw.WriteLine(Chr(34) + basedir + "\" + System.Diagnostics.Process.GetCurrentProcess().ProcessName + ".exe" + Chr(34) + " " + arg)
				'sw.Flush()
				'sw.Close()
				'settings.setconfig("arguments", arg)   'May need to fix in the future to support command argument.
				regkey.SetValue("*" + Application.Current.MainWindow.GetType().Assembly.GetName().Name, System.Reflection.Assembly.GetExecutingAssembly().Location)
				regkey.SetValue("*UndoSM", "BCDEDIT /deletevalue safeboot")
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try


		processinfo.FileName = "shutdown"
		processinfo.Arguments = "/r /t 0"
		processinfo.WindowStyle = ProcessWindowStyle.Hidden
		processinfo.UseShellExecute = True
		processinfo.CreateNoWindow = True
		processinfo.RedirectStandardOutput = False

		process.StartInfo = processinfo
		process.Start()
		process.WaitForExit()
		process.Close()

		closeddu()
	End Sub

	Private Sub closeddu()

		If Not Dispatcher.CheckAccess() Then
			Dispatcher.Invoke(New MethodInvoker(AddressOf closeddu))
		Else
			Try
				preventclose = False

				' Me.Close()
				Application.Current.MainWindow.Close()

			Catch ex As Exception
				Application.log.AddException(ex)
			End Try
		End If
	End Sub

#Region "frmMain Controls"

	Private Sub btnCleanRestart_Click(sender As Object, e As RoutedEventArgs) Handles btnCleanRestart.Click

		If Not CBool(Application.Settings.GoodSite) Then
			MessageBox.Show("A simple 1 time message.... For helping DDU developpement, please always download DDU from its homepage http://www.wagnardmobile.com it really help and will encourage me to continue developping DDU. In the event there is a problem with the main page, feel free to use the Guru3d mirror.")
			Application.Settings.GoodSite = True
		End If

		btnCleanRestart.IsEnabled = False
		btnClean.IsEnabled = False
		btnCleanShutdown.IsEnabled = False
		MenuStrip1.IsEnabled = False

		disabledriversearch()
		'kill processes that read GPU stats, like RTSS, MSI Afterburner, EVGA Prec X to prevent invalid readings
		KillGPUStatsProcesses()
		'this shouldn't be slow, so it isn't on a thread/background worker

		reboot = True
		systemrestore()
		BackgroundWorker1.RunWorkerAsync(
		 New ThreadSettings() With {
		   .DoShutdown = False,
		   .DoReboot = True})
	End Sub

	Private Sub btnClean_Click(sender As Object, e As RoutedEventArgs) Handles btnClean.Click

		If Not CBool(Application.Settings.GoodSite) Then
			MessageBox.Show("A simple 1 time message.... For helping DDU developpement, please always download DDU from its homepage http://www.wagnardmobile.com it really help and will encourage me to continue developping DDU. In the event there is a problem with the main page, feel free to use the Guru3d mirror.")
			Application.Settings.GoodSite = True
		End If

		btnCleanRestart.IsEnabled = False
		btnClean.IsEnabled = False
		btnCleanShutdown.IsEnabled = False
		MenuStrip1.IsEnabled = False

		disabledriversearch()
		'kill processes that read GPU stats, like RTSS, MSI Afterburner, EVGA Prec X to prevent invalid readings
		KillGPUStatsProcesses()
		'this shouldn't be slow, so it isn't on a thread/background worker

		reboot = False
		shutdown = False
		systemrestore()
		BackgroundWorker1.RunWorkerAsync(
		 New ThreadSettings() With {
		   .DoShutdown = False,
		   .DoReboot = False})

	End Sub

	Private Sub btnCleanShutdown_Click(sender As Object, e As RoutedEventArgs) Handles btnCleanShutdown.Click
		If Not CBool(Application.Settings.GoodSite) Then
			MessageBox.Show("A simple 1 time message.... For helping DDU developpement, please always download DDU from its homepage http://www.wagnardmobile.com it really help and will encourage me to continue developping DDU. In the event there is a problem with the main page, feel free to use the Guru3d mirror.")
			Application.Settings.GoodSite = True
		End If

		btnCleanRestart.IsEnabled = False
		btnClean.IsEnabled = False
		btnCleanShutdown.IsEnabled = False
		MenuStrip1.IsEnabled = False

		disabledriversearch()
		'kill processes that read GPU stats, like RTSS, MSI Afterburner, EVGA Prec X to prevent invalid readings
		KillGPUStatsProcesses()
		'this shouldn't be slow, so it isn't on a thread/background worker

		reboot = False
		shutdown = True
		systemrestore()
		BackgroundWorker1.RunWorkerAsync(
		 New ThreadSettings() With {
		   .DoShutdown = True,
		   .DoReboot = False})
	End Sub

	Private Sub btnWuRestore_Click(sender As Object, e As EventArgs) Handles btnWuRestore.Click

		Dim regkey As RegistryKey = Nothing

		If version >= "6.1" Then
			Try
				regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching", True)
				regkey.SetValue("SearchOrderConfig", 1)
				MsgBox(Languages.GetTranslation("frmMain", "Messages", "Text11"))
			Catch ex As Exception
				Application.Log.AddException(ex)
			End Try
		End If
		If version >= "6.0" And version < "6.1" Then
			Try
				regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Policies\Microsoft\Windows\DriverSearching", True)
				regkey.SetValue("DontSearchWindowsUpdate", 0)
				MsgBox(Languages.GetTranslation("frmMain", "Messages", "Text11"))
			Catch ex As Exception
				Application.Log.AddException(ex)
			End Try
		End If

	End Sub

	Private Sub btnExtendedLog_Click(sender As Object, e As EventArgs) Handles btnExtendedLog.Click
		Dim frmLog As New frmLog

		With frmLog
			.Owner = Me
			.DataContext = Me.DataContext
			.ResizeMode = Windows.ResizeMode.CanResizeWithGrip
			.WindowStyle = Windows.WindowStyle.SingleBorderWindow
			.WindowStartupLocation = Windows.WindowStartupLocation.CenterOwner
		End With

		frmLog.ShowDialog()
		Me.Activate()
	End Sub



	Private Sub cbSelectedGPU_SelectedIndexChanged(sender As Object, e As SelectionChangedEventArgs) Handles cbSelectedGPU.SelectionChanged
		Dim bi3 As New BitmapImage

		If Application.Settings.SelectedGPU = GPUVendor.Nvidia Then

			'PictureBox2.Location = New Point(CInt(286 * (picturebox2originalx / 333)), CInt(92 * (picturebox2originaly / 92)))
			'PictureBox2.Size = New Size(252, 123)
			'         PictureBox2.Source = My.Resources.NV_GF_GTX_preferred_badge_FOR_WEB_ONLY
			bi3.BeginInit()
			bi3.UriSource = New Uri("/Resources/NV_GF_GTX_preferred_badge_FOR_WEB_ONLY.jpg", UriKind.Relative)
			bi3.EndInit()
			imgLogo.Source = bi3
		End If

		If Application.Settings.SelectedGPU = GPUVendor.AMD Then


			bi3.BeginInit()
			bi3.UriSource = New Uri("/Resources/RadeonLogo1.png", UriKind.Relative)
			bi3.EndInit()
			imgLogo.Source = bi3
			'PictureBox2.Location = New Point(picturebox2originalx, picturebox2originaly)
			'PictureBox2.Size = New Size(158, 126)
			'           PictureBox2.Source = My.Resources.RadeonLogo1
		End If

		If Application.Settings.SelectedGPU = GPUVendor.Intel Then
			bi3.BeginInit()
			bi3.UriSource = New Uri("/Resources/intel_logo.png", UriKind.Relative)
			bi3.EndInit()
			imgLogo.Source = bi3
			'PictureBox2.Location = New Point(picturebox2originalx, picturebox2originaly)
			'PictureBox2.Size = New Size(158, 126)
			'         PictureBox2.Source = My.Resources.intel_logo
		End If

	End Sub

	Private Sub cbLanguage_SelectedIndexChanged(sender As Object, e As SelectionChangedEventArgs) Handles cbLanguage.SelectionChanged
		If cbLanguage.SelectedItem IsNot Nothing Then
			InitLanguage(False, CType(cbLanguage.SelectedItem, Languages.LanguageOption))
		End If

		Checkupdates2()
	End Sub

	Private Sub PictureBox1_Click(sender As Object, e As EventArgs)
		'settings.setconfig("donate", "true")   'Will fix later.

		'Create the ddu.bat file
		Dim sw As StreamWriter = System.IO.File.CreateText(baseDir + "\DDU.bat")
		sw.WriteLine(Chr(34) + Application.Paths.AppExeFile + Chr(34))
		sw.Flush()
		sw.Close()

		Dim UserTokenHandle As IntPtr = IntPtr.Zero
		WindowsApi.WTSQueryUserToken(WindowsApi.WTSGetActiveConsoleSessionId, UserTokenHandle)
		Dim ProcInfo As New WindowsApi.PROCESS_INFORMATION
		Dim StartInfo As New WindowsApi.STARTUPINFOW
		StartInfo.cb = CUInt(Runtime.InteropServices.Marshal.SizeOf(StartInfo))

		If WindowsApi.CreateProcessAsUser(UserTokenHandle, Application.Paths.AppBase + "DDU.bat", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, False, 0, IntPtr.Zero, Nothing, StartInfo, ProcInfo) Then
		Else
			MsgBox("Error ---" & System.Runtime.InteropServices.Marshal.GetLastWin32Error())
		End If

		If Not UserTokenHandle = IntPtr.Zero Then
			WindowsApi.CloseHandle(UserTokenHandle)
		End If

	End Sub



	'Private Sub ToSToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles ToSToolStripMenuItem.Click
	'    MessageBox.Show(Languages.GetTranslation("Misc", "Tos", "Text"))
	'End Sub

	'Private Sub AboutToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles AboutToolStripMenuItem.Click
	'    Using frm As New frmAbout
	'        frm.ShowDialog(Me)
	'    End Using
	'End Sub

	'Private Sub VisitGuru3dNVIDIAThreadToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles VisitGuru3dNVIDIAThreadToolStripMenuItem.Click

	'    settings.setconfig("guru3dnvidia", "true")

	'    'Create the ddu.bat file
	'    Dim sw As StreamWriter = System.IO.File.CreateText(basedir + "\DDU.bat")
	'    sw.WriteLine(Chr(34) + basedir + "\" + System.Diagnostics.Process.GetCurrentProcess().ProcessName + ".exe" + Chr(34))
	'    sw.Flush()
	'    sw.Close()

	'    Dim UserTokenHandle As IntPtr = IntPtr.Zero
	'    WindowsApi.WTSQueryUserToken(WindowsApi.WTSGetActiveConsoleSessionId, UserTokenHandle)
	'    Dim ProcInfo As New WindowsApi.PROCESS_INFORMATION
	'    Dim StartInfo As New WindowsApi.STARTUPINFOW
	'    StartInfo.cb = CUInt(Runtime.InteropServices.Marshal.SizeOf(StartInfo))

	'    If WindowsApi.CreateProcessAsUser(UserTokenHandle, basedir + "\DDU.bat", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, False, 0, IntPtr.Zero, Nothing, StartInfo, ProcInfo) Then
	'    Else
	'        MsgBox("Error ---" & System.Runtime.InteropServices.Marshal.GetLastWin32Error())
	'    End If

	'    If Not UserTokenHandle = IntPtr.Zero Then
	'        WindowsApi.CloseHandle(UserTokenHandle)
	'    End If
	'End Sub

	'Private Sub VisitGuru3dAMDThreadToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles VisitGuru3dAMDThreadToolStripMenuItem.Click

	'    settings.setconfig("guru3damd", "true")

	'    'Create the ddu.bat file
	'    Dim sw As StreamWriter = System.IO.File.CreateText(basedir + "\DDU.bat")
	'    sw.WriteLine(Chr(34) + basedir + "\" + System.Diagnostics.Process.GetCurrentProcess().ProcessName + ".exe" + Chr(34))
	'    sw.Flush()
	'    sw.Close()

	'    Dim UserTokenHandle As IntPtr = IntPtr.Zero
	'    WindowsApi.WTSQueryUserToken(WindowsApi.WTSGetActiveConsoleSessionId, UserTokenHandle)
	'    Dim ProcInfo As New WindowsApi.PROCESS_INFORMATION
	'    Dim StartInfo As New WindowsApi.STARTUPINFOW
	'    StartInfo.cb = CUInt(Runtime.InteropServices.Marshal.SizeOf(StartInfo))

	'    If WindowsApi.CreateProcessAsUser(UserTokenHandle, basedir + "\DDU.bat", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, False, 0, IntPtr.Zero, Nothing, StartInfo, ProcInfo) Then
	'    Else
	'        MsgBox("Error ---" & System.Runtime.InteropServices.Marshal.GetLastWin32Error())
	'    End If

	'    If Not UserTokenHandle = IntPtr.Zero Then
	'        WindowsApi.CloseHandle(UserTokenHandle)
	'    End If
	'End Sub

	'Private Sub VisitGeforceThreadToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles VisitGeforceThreadToolStripMenuItem.Click

	'    settings.setconfig("geforce", "true")

	'    'Create the ddu.bat file
	'    Dim sw As StreamWriter = System.IO.File.CreateText(basedir + "\DDU.bat")
	'    sw.WriteLine(Chr(34) + basedir + "\" + System.Diagnostics.Process.GetCurrentProcess().ProcessName + ".exe" + Chr(34))
	'    sw.Flush()
	'    sw.Close()

	'    Dim UserTokenHandle As IntPtr = IntPtr.Zero
	'    WindowsApi.WTSQueryUserToken(WindowsApi.WTSGetActiveConsoleSessionId, UserTokenHandle)
	'    Dim ProcInfo As New WindowsApi.PROCESS_INFORMATION
	'    Dim StartInfo As New WindowsApi.STARTUPINFOW
	'    StartInfo.cb = CUInt(Runtime.InteropServices.Marshal.SizeOf(StartInfo))

	'    If WindowsApi.CreateProcessAsUser(UserTokenHandle, basedir + "\DDU.bat", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, False, 0, IntPtr.Zero, Nothing, StartInfo, ProcInfo) Then
	'    Else
	'        MsgBox("Error ---" & System.Runtime.InteropServices.Marshal.GetLastWin32Error())
	'    End If

	'    If Not UserTokenHandle = IntPtr.Zero Then
	'        WindowsApi.CloseHandle(UserTokenHandle)
	'    End If
	'End Sub

	'Private Sub SVNToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles SVNToolStripMenuItem.Click

	'    settings.setconfig("svn", "true")

	'    'Create the ddu.bat file
	'    Dim sw As StreamWriter = System.IO.File.CreateText(basedir + "\DDU.bat")
	'    sw.WriteLine(Chr(34) + basedir + "\" + System.Diagnostics.Process.GetCurrentProcess().ProcessName + ".exe" + Chr(34))
	'    sw.Flush()
	'    sw.Close()

	'    Dim UserTokenHandle As IntPtr = IntPtr.Zero
	'    WindowsApi.WTSQueryUserToken(WindowsApi.WTSGetActiveConsoleSessionId, UserTokenHandle)
	'    Dim ProcInfo As New WindowsApi.PROCESS_INFORMATION
	'    Dim StartInfo As New WindowsApi.STARTUPINFOW
	'    StartInfo.cb = CUInt(Runtime.InteropServices.Marshal.SizeOf(StartInfo))

	'    If WindowsApi.CreateProcessAsUser(UserTokenHandle, basedir + "\DDU.bat", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, False, 0, IntPtr.Zero, Nothing, StartInfo, ProcInfo) Then
	'    Else
	'        MsgBox("Error ---" & System.Runtime.InteropServices.Marshal.GetLastWin32Error())
	'    End If

	'    If Not UserTokenHandle = IntPtr.Zero Then
	'        WindowsApi.CloseHandle(UserTokenHandle)
	'    End If
	'End Sub

	Private Sub VisitDDUHomepageToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles VisitDDUHomeMenuItem.Click

		'Create the ddu.bat file
		Dim sw As StreamWriter = System.IO.File.CreateText(baseDir + "\DDU.bat")
		sw.WriteLine(Chr(34) + Application.Paths.AppExeFile + Chr(34) + " -visitdduhome")
		sw.Flush()
		sw.Close()

		Dim UserTokenHandle As IntPtr = IntPtr.Zero
		WindowsApi.WTSQueryUserToken(WindowsApi.WTSGetActiveConsoleSessionId, UserTokenHandle)
		Dim ProcInfo As New WindowsApi.PROCESS_INFORMATION
		Dim StartInfo As New WindowsApi.STARTUPINFOW
		StartInfo.cb = CUInt(Runtime.InteropServices.Marshal.SizeOf(StartInfo))

		If WindowsApi.CreateProcessAsUser(UserTokenHandle, Application.Paths.AppBase + "DDU.bat", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, False, 0, IntPtr.Zero, Nothing, StartInfo, ProcInfo) Then
		Else
			MsgBox("Error ---" & System.Runtime.InteropServices.Marshal.GetLastWin32Error())
		End If

		If Not UserTokenHandle = IntPtr.Zero Then
			WindowsApi.CloseHandle(UserTokenHandle)
		End If
	End Sub

	Private Sub OptionsMenuItem_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles OptionsMenuItem.Click
		Dim frmOptions As New frmOptions

		With frmOptions
			.Owner = Me
			.DataContext = Me.DataContext
			.ResizeMode = Windows.ResizeMode.NoResize
			.WindowStyle = Windows.WindowStyle.ToolWindow
		End With

		frmOptions.ShowDialog()
	End Sub

	Private Sub LinksMenuItem_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles LinksMenuItem.Click

	End Sub

	Private Sub InfoMenuItem_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles InfoMenuItem.Click

	End Sub

	Private Sub ToSMenuItem_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles ToSMenuItem.Click

	End Sub

	Private Sub AboutMenuItem_Click_1(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles AboutMenuItem.Click

	End Sub



	Private Sub frmMain_Loaded(sender As Object, e As RoutedEventArgs)
		If Me.DataContext Is Nothing Then
			Me.DataContext = Application.Data
		End If

		Try
			Dim defaultLang As New Languages.LanguageOption("en", "English", Application.Paths.Language & "English.xml")
			Dim foundLangs As List(Of Languages.LanguageOption) = Languages.ScanFolderForLang(Application.Paths.Language)

			foundLangs.Add(defaultLang)
			foundLangs.Sort(Function(x, y) x.DisplayText.CompareTo(y.DisplayText))

			For Each lang As Languages.LanguageOption In foundLangs
				Application.Settings.LanguageOptions.Add(lang)
			Next

			Application.Settings.Load()
			InitLanguage(True)

			If Not Application.Settings.DontCheckUpdates Then
				Me.Topmost = True
				Checkupdates2()

				Me.Topmost = False

				If closeapp Then
					Exit Sub
				End If
			End If

			Dim regkey As RegistryKey = Nothing
			Dim subregkey As RegistryKey = Nothing
			Dim webAddress As String = ""

			' used arguments: -dduhome -svn -donate -guru3dnvidia -guru3damd -geforce
			' NOTE: Application.Settings & Paths creates needed folders (-> moved to beginning of Sub)

#If 1 + 1 = 3 Then ' TODO: REMOVE (msgboxes gets annoying...)
			MessageBox.Show(
			 "Arguments: " & Application.Settings.Arguments & vbCrLf & vbCrLf & vbCrLf &
			 "VisitDDUHome: " & Application.Settings.VisitDDUHome.ToString() & vbCrLf &
			 "VisitDonate: " & Application.Settings.VisitDonate.ToString() & vbCrLf &
			 "VisitGeforce: " & Application.Settings.VisitGeforce.ToString() & vbCrLf &
			 "VisitGuru3DAMD: " & Application.Settings.VisitGuru3DAMD.ToString() & vbCrLf &
			 "VisitGuru3DNvidia: " & Application.Settings.VisitGuru3DNvidia.ToString() & vbCrLf &
			 "VisitSVN: " & Application.Settings.VisitSVN.ToString()
			 )
#End If
			'we check if the donate/guru3dnvidia/gugu3damd/geforce/dduhome is trigger here directly.
			'If CBool(settings.getconfig("donate")) = True Then
			'	webAddress = "https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business=KAQAJ6TNR9GQE&lc=CA&item_name=Display%20Driver%20Uninstaller%20%28DDU%29&currency_code=USD&bn=PP%2dDonationsBF%3abtn_donate_LG%2egif%3aNonHosted"
			'End If

			'If CBool(settings.getconfig("guru3dnvidia")) = True Then
			'	webAddress = "http://forums.guru3d.com/showthread.php?t=379506"
			'End If

			'If CBool(settings.getconfig("guru3damd")) = True Then
			'	webAddress = "http://forums.guru3d.com/showthread.php?t=379505"
			'End If

			'If CBool(settings.getconfig("geforce")) = True Then
			'	webAddress = "https://forums.geforce.com/default/topic/550192/geforce-drivers/wagnard-tools-ddu-gmp-tdr-manupulator-updated-01-22-2015-/"
			'End If

			If CBool(Application.Settings.VisitDDUHome) Then
				webAddress = "http://www.wagnardmobile.com"
			End If

			'If CBool(settings.getconfig("svn")) = True Then
			'	webAddress = "https://github.com/Wagnard/display-drivers-uninstaller"
			'End If

			If CBool(Application.Settings.VisitDonate) = True Or
			   CBool(Application.Settings.VisitGuru3DNvidia) = True Or
			   CBool(Application.Settings.VisitGuru3DAMD) = True Or
			   CBool(Application.Settings.VisitGeforce) = True Or
			   CBool(Application.Settings.VisitSVN) = True Or
			   CBool(Application.Settings.VisitDDUHome) = True Then

				processinfo.FileName = webAddress
				processinfo.Arguments = Nothing
				processinfo.UseShellExecute = True
				processinfo.CreateNoWindow = True
				processinfo.RedirectStandardOutput = False

				process.StartInfo = processinfo
				process.Start()
				'Do not put WaitForExit here. It will cause error and prevent DDU to exit.
				process.Close()

				'         settings.setconfig("donate", "false")
				'         settings.setconfig("guru3dnvidia", "false")
				'         settings.setconfig("guru3damd", "false")
				'         settings.setconfig("geforce", "false")
				'         settings.setconfig("dduhome", "false")
				'         settings.setconfig("svn", "false")

				closeddu()
				deletefile(Application.Paths.AppBase + "DDU.bat")
				Exit Sub
			End If


			If Not isElevated Then
				MessageBox.Show(Languages.GetTranslation(Me.Name, "Messages", "Text3"), Application.Current.MainWindow.GetType().Assembly.GetName().Name, MessageBoxButtons.OK, MessageBoxIcon.Error)
				closeddu()
				Exit Sub
			End If

			'second, we check on what we are running and set variables accordingly (os, architecture)
			If Not IsNullOrWhitespace(CStr(My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows NT\CurrentVersion", False).GetValue("CurrentVersion"))) Then
				version = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows NT\CurrentVersion", False).GetValue("CurrentVersion").ToString
			Else
				version = "5.0"
			End If


#If 1 + 1 = 3 Then ' TODO: REMOVE (msgboxes gets annoying...)
			MessageBox.Show("version (regkey): " & version & vbCrLf & vbCrLf &
			"My.Computer.Info.OSFullName: " & My.Computer.Info.OSFullName &
			"My.Computer.Info.OSVersion: " & My.Computer.Info.OSVersion)
#End If
			' https://msdn.microsoft.com/en-us/library/windows/desktop/ms724832%28v=vs.85%29.aspx

			btnCleanRestart.IsEnabled = True
			btnClean.IsEnabled = True
			btnCleanShutdown.IsEnabled = True
			btnWuRestore.IsEnabled = True

			Select Case version
				Case "5.1"
					lblWinVersionValue.Content = "Windows XP"
					winxp = True

				Case "5.2"
					lblWinVersionValue.Content = "Windows XP (x64) or Server 2003"
					winxp = True

				Case "6.0"
					lblWinVersionValue.Content = "Windows Vista or Server 2008"

				Case "6.1"
					lblWinVersionValue.Content = "Windows 7 or Server 2008R2"

				Case "6.2"
					lblWinVersionValue.Content = "Windows 8 or Server 2012"
					win8higher = True

				Case "6.3"
					lblWinVersionValue.Content = "Windows 8.1"
					If Not IsNullOrWhitespace(CStr(My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows NT\CurrentVersion", False).GetValue("CurrentMajorVersionNumber"))) Then
						If CStr(My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows NT\CurrentVersion", False).GetValue("CurrentMajorVersionNumber")) = "10" Then
							lblWinVersionValue.Content = "Windows 10"
							win10 = True
						End If
					End If
					win8higher = True

				Case "6.4", "10.0"
					lblWinVersionValue.Content = "Windows 10"
					win8higher = True
					win10 = True

				Case Else
					lblWinVersionValue.Content = "Unsupported OS"
					Application.Log.AddMessage("Unsupported OS.")
					btnCleanRestart.IsEnabled = False
					btnClean.IsEnabled = False
					btnCleanShutdown.IsEnabled = False
					btnWuRestore.IsEnabled = False
			End Select


			Try
				'allow Paexec to run in safemode

				'  If BootMode.FailSafe Or BootMode.FailSafeWithNetwork Then ' we do this in safemode because of some Antivirus....(Kaspersky)
				Try
					My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Control\SafeBoot\Minimal", True).CreateSubKey("PAexec")
					My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Control\SafeBoot\Minimal\PAexec", True).SetValue("", "Service")
				Catch ex As Exception
				End Try

				Try
					My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Control\SafeBoot\Network", True).CreateSubKey("PAexec")
					My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Control\SafeBoot\Network\PAexec", True).SetValue("", "Service")
				Catch ex As Exception
				End Try
				'End If

				'read config file

				If closeapp Then
					Exit Sub
				End If


				'----------------------
				'check computer/os info
				'----------------------

				Dim archIs64 As Boolean


				Application.Settings.SelectedGPU = GPUVendor.Nvidia

				If IntPtr.Size = 8 Then
					archIs64 = True
					lblArch.Content = "x64"

					Application.Paths.CreateDirectories(Application.Paths.AppBase & "\x64")

				ElseIf IntPtr.Size = 4 Then
					archIs64 = False
					lblArch.Content = "x86"

					Application.Paths.CreateDirectories(Application.Paths.AppBase & "\x86")
				End If

				ddudrfolder = CStr(lblArch.Content)

				If Not identity.IsSystem Then
					If archIs64 Then
						Try
							If winxp Then  'XP64
								File.WriteAllBytes(Application.Paths.AppBase & "x64\ddudr.exe", My.Resources.ddudrxp64)
							Else
								File.WriteAllBytes(Application.Paths.AppBase & "x64\ddudr.exe", My.Resources.ddudr64)
							End If

							File.WriteAllBytes(Application.Paths.AppBase & "x64\paexec.exe", My.Resources.paexec)
						Catch ex As Exception
							Application.Log.AddException(ex)
						End Try
					Else
						Try
							If winxp Then  'XP32
								System.IO.File.WriteAllBytes(Application.Paths.AppBase & "x86\ddudr.exe", My.Resources.ddudrxp32)
							Else 'all other 32 bits
								System.IO.File.WriteAllBytes(Application.Paths.AppBase & "x86\ddudr.exe", My.Resources.ddudr32)
							End If

							System.IO.File.WriteAllBytes(Application.Paths.AppBase & "x86\paexec.exe", My.Resources.paexec)
						Catch ex As Exception
							Application.Log.AddException(ex)
						End Try
					End If

					If archIs64 = True Then
						If Not File.Exists(Application.Paths.AppBase & "x64\ddudr.exe") Then
							MessageBox.Show(Languages.GetTranslation(Me.Name, "Messages", "Text4"), Application.Current.MainWindow.GetType().Assembly.GetName().Name, MessageBoxButtons.OK, MessageBoxIcon.Error)

							btnCleanRestart.IsEnabled = False
							btnClean.IsEnabled = False
							btnCleanShutdown.IsEnabled = False
							Exit Sub
						End If
					ElseIf archIs64 = False Then
						If Not File.Exists(Application.Paths.AppBase & "x86\ddudr.exe") Then
							MessageBox.Show(Languages.GetTranslation(Me.Name, "Messages", "Text4"), Application.Current.MainWindow.GetType().Assembly.GetName().Name, MessageBoxButtons.OK, MessageBoxIcon.Error)

							btnCleanRestart.IsEnabled = False
							btnClean.IsEnabled = False
							btnCleanShutdown.IsEnabled = False
							Exit Sub
						End If
					End If
				End If
				'processing arguments

				'arg = String.Join(" ", arguments, 1, arguments.Length - 1)
				'arg = arg.ToLower.Replace("  ", " ")

				'If Not IsNullOrWhitespace(settings.getconfig("arguments")) Then
				'    arg = settings.getconfig("arguments")
				'End If

				'settings.setconfig("arguments", "")

				'If Not IsNullOrWhitespace(arg) Then
				'    If Not arg = " " Then
				'        settings.setconfig("logbox", "false")
				'        settings.setconfig("systemrestore", "false")
				'        settings.setconfig("removemonitor", "false")
				'        settings.setconfig("showsafemodebox", "true")
				'        settings.setconfig("removeamdaudiobus", "false")
				'        settings.setconfig("removeamdkmpfd", "false")
				'        settings.setconfig("removegfe", "false")

				'        If arg.Contains("-silent") Then
				'            silent = True
				'            Me.WindowState = Windows.WindowState.Minimized
				'        Else
				'            Checkupdates2()
				'            If closeapp Then
				'                Exit Sub
				'            End If
				'        End If


				'        If arg.Contains("-logging") Then
				'            settings.setconfig("logbox", "true")
				'        End If
				'        If arg.Contains("-createsystemrestorepoint") Then
				'            settings.setconfig("systemrestore", "true")
				'        End If
				'        If arg.Contains("-removemonitors") Then
				'            settings.setconfig("removemonitor", "true")
				'        End If
				'        If arg.Contains("-nosafemode") Then
				'            settings.setconfig("showsafemodebox", "false")
				'        End If
				'        If arg.Contains("-restart") Then
				'            restart = True
				'        End If
				'        If arg.Contains("-removeamdaudiobus") Then
				'            settings.setconfig("removeamdaudiobus", "true")
				'        End If
				'        If arg.Contains("-removeamdkmpfd") Then
				'            settings.setconfig("removeamdkmpfd", "true")
				'        End If
				'        If arg.Contains("-removegfe") Then
				'            settings.setconfig("removegfe", "true")
				'        End If
				'        If arg.Contains("-cleanamd") Then
				'            argcleanamd = True
				'            nbclean = nbclean + 1
				'        End If
				'        If arg.Contains("-cleanintel") Then
				'            argcleanintel = True
				'            nbclean = nbclean + 1
				'        End If
				'        If arg.Contains("-cleannvidia") Then
				'            argcleannvidia = True
				'            nbclean = nbclean + 1
				'        End If
				'    End If
				'End If


				'We check if there are any reboot from windows update pending. and if so we quit.
				If winupdatepending() Then
					MessageBox.Show(Languages.GetTranslation(Me.Name, "Messages", "Text14"), Application.Current.MainWindow.GetType().Assembly.GetName().Name, MessageBoxButtons.OK, MessageBoxIcon.Warning)
					closeddu()
					Exit Sub
				End If

				Me.Topmost = True





				'here I check if the process is running on system user account. if not, make it so.
				If Not MyIdentity.IsSystem Then
					'This code checks to see which mode Windows has booted up in.
					Dim processstopservice As New Process
					Select Case System.Windows.Forms.SystemInformation.BootMode
						Case BootMode.FailSafeWithNetwork, BootMode.FailSafe
							'The computer was booted using only the basic files and drivers.
							'This is the same as Safe Mode
							safemode = True
							Me.WindowState = Windows.WindowState.Normal
							If winxp = False Then
								Dim setbcdedit As New ProcessStartInfo
								setbcdedit.FileName = "cmd.exe"
								setbcdedit.Arguments = " /CBCDEDIT /deletevalue safeboot"
								setbcdedit.UseShellExecute = False
								setbcdedit.CreateNoWindow = True
								setbcdedit.RedirectStandardOutput = False

								processstopservice.StartInfo = setbcdedit
								processstopservice.Start()
								processstopservice.WaitForExit()
								processstopservice.Close()
							End If
						Case BootMode.Normal
							safemode = False

							If winxp = False AndAlso isElevated Then 'added iselevated so this will not try to boot into safe mode/boot menu without admin rights, as even with the admin check on startup it was for some reason still trying to gain registry access and throwing an exception --probably because there's no return
								If restart Then	 'restart command line argument
									restartinsafemode()
									Exit Sub
								Else
									If Application.Settings.ShowSafeModeMsg = True Then
										If Not silent Then
											Dim bootOption As Integer = -1 '-1 = close, 0 = normal, 1 = SafeMode, 2 = SafeMode with network
											Dim frmSafeBoot As New frmLaunch

											With frmSafeBoot
												.Topmost = True
												.ShowInTaskbar = False
												.ResizeMode = Windows.ResizeMode.NoResize
												.Owner = Application.Current.MainWindow
											End With



											' frmMain could be Invisible from start and shown AFTER all "processing"
											' (WPF renders UI too fast which cause 'flash' before frmLaunch on start)

											Dim launch As Boolean? = frmSafeBoot.ShowDialog()

											If launch IsNot Nothing AndAlso launch Then
												bootOption = frmSafeBoot.selection
											End If

											Select Case bootOption
												Case 0 'normal

													Exit Select
												Case 1 'SafeMode
													restartinsafemode(False)
													Exit Sub
												Case 2 'SafeMode with network
													restartinsafemode(True)
													Exit Sub
												Case Else '-1 = Close
													Me.Topmost = False
													closeddu()
													Exit Sub
											End Select
										End If
									End If
								End If
							End If
					End Select

					Topmost = False


					GoTo skipboot 'TODO: REMOVE THIS LINE!!!! Blocked restart for faster debugging
					Dim stopservice As New ProcessStartInfo
					stopservice.FileName = "cmd.exe"
					stopservice.Arguments = " /Csc stop PAExec"
					stopservice.UseShellExecute = False
					stopservice.CreateNoWindow = True
					stopservice.RedirectStandardOutput = False

					processstopservice.StartInfo = stopservice
					processstopservice.Start()
					processstopservice.WaitForExit()
					processstopservice.Close()
					System.Threading.Thread.Sleep(10)

					stopservice.Arguments = " /Csc delete PAExec"

					processstopservice.StartInfo = stopservice
					processstopservice.Start()
					processstopservice.WaitForExit()
					processstopservice.Close()

					stopservice.Arguments = " /Csc interrogate PAExec"
					processstopservice.StartInfo = stopservice
					processstopservice.Start()
					processstopservice.WaitForExit()
					processstopservice.Close()

					processinfo.FileName = baseDir & "\" & ddudrfolder & "\paexec.exe"
					processinfo.Arguments = "-noname -i -s " & Chr(34) & baseDir & "\" & System.Diagnostics.Process.GetCurrentProcess().ProcessName + ".exe" & Chr(34) + arg
					processinfo.UseShellExecute = False
					processinfo.CreateNoWindow = True
					processinfo.RedirectStandardOutput = False

					process.StartInfo = processinfo
					process.Start()
					'Do not add waitforexit here or DDU(current user)will not close
					process.Close()

					closeddu()
					Exit Sub
				Else
					Me.WindowState = Windows.WindowState.Normal
				End If
skipboot:
				Me.WindowState = Windows.WindowState.Normal	  'temporary so we can easilly debug.
				Dim info As LogEntry = LogEntry.Create()
				UpdateTextMethod(UpdateTextMethodmessagefn(10) + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString())

				info.Message = "System Information"
				info.Add("DDU Version", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString())
				info.Add("OS", CStr(lblWinVersionValue.Content))
				info.Add("Architecture", CStr(lblArch.Content))

				Try
					regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}")
					If regkey IsNot Nothing Then
						For Each child As String In regkey.GetSubKeyNames
							If Not IsNullOrWhitespace(child) Then

								If Not child.ToLower.Contains("properties") Then

									subregkey = regkey.OpenSubKey(child)
									If subregkey IsNot Nothing Then

										If Not IsNullOrWhitespace(CStr(subregkey.GetValue("Device Description"))) Then
											currentdriverversion = subregkey.GetValue("Device Description").ToString
											UpdateTextMethod(UpdateTextMethodmessagefn(11) + " " + child + " " + UpdateTextMethodmessagefn(12) + " " + currentdriverversion)
											info.Add("GPU #" + child, currentdriverversion)
										Else
											If (subregkey.GetValue("DriverDesc") IsNot Nothing) AndAlso (subregkey.GetValueKind("DriverDesc") = RegistryValueKind.Binary) Then
												UpdateTextMethod(UpdateTextMethodmessagefn(11) + " " + child + " " + UpdateTextMethodmessagefn(12) + " " + HexToString(GetREG_BINARY(subregkey.ToString, "DriverDesc").Replace("00", "")))
												info.Add("GPU #" + child, HexToString(GetREG_BINARY(subregkey.ToString, "DriverDesc").Replace("00", "")))
											Else
												If Not IsNullOrWhitespace(CStr(subregkey.GetValue("DriverDesc"))) Then
													currentdriverversion = subregkey.GetValue("DriverDesc").ToString
													UpdateTextMethod(UpdateTextMethodmessagefn(11) + " " + child + " " + UpdateTextMethodmessagefn(12) + " " + currentdriverversion)
													info.Add("GPU #" + child, currentdriverversion)
												End If
											End If

										End If
										If Not IsNullOrWhitespace(CStr(subregkey.GetValue("MatchingDeviceId"))) Then
											currentdriverversion = subregkey.GetValue("MatchingDeviceId").ToString
											UpdateTextMethod(UpdateTextMethodmessagefn(13) + " " + currentdriverversion)
											info.Add("GPU DeviceId", currentdriverversion)
										End If

										Try
											If (Not IsNullOrWhitespace(CStr(subregkey.GetValue("HardwareInformation.BiosString")))) AndAlso (subregkey.GetValueKind("HardwareInformation.BiosString") = RegistryValueKind.Binary) Then
												UpdateTextMethod("Vbios :" + " " + HexToString(GetREG_BINARY(subregkey.ToString, "HardwareInformation.BiosString").Replace("00", "")))
												info.Add("Vbios", HexToString(GetREG_BINARY(subregkey.ToString, "HardwareInformation.BiosString").Replace("00", "")))
											Else
												If Not IsNullOrWhitespace(CStr(subregkey.GetValue("HardwareInformation.BiosString"))) Then
													currentdriverversion = subregkey.GetValue("HardwareInformation.BiosString").ToString
													For i As Integer = 0 To 9
														'this is a little fix to correctly show the vbios version info
														currentdriverversion = currentdriverversion.Replace("." + i.ToString + ".", ".0" + i.ToString + ".")
													Next
													UpdateTextMethod("Vbios :" + " " + currentdriverversion)
													info.Add("Vbios", currentdriverversion)
												End If
											End If
										Catch ex As Exception
										End Try


										If Not IsNullOrWhitespace(CStr(subregkey.GetValue("DriverVersion"))) Then
											currentdriverversion = subregkey.GetValue("DriverVersion").ToString
											UpdateTextMethod(UpdateTextMethodmessagefn(14) + " " + currentdriverversion)
											info.Add("Detected Driver(s) Version(s)", currentdriverversion)
										End If
										If Not IsNullOrWhitespace(CStr(subregkey.GetValue("InfPath"))) Then
											currentdriverversion = subregkey.GetValue("InfPath").ToString
											UpdateTextMethod(UpdateTextMethodmessagefn(15) + " " + currentdriverversion)
											info.Add("INF", currentdriverversion)
										End If
										If Not IsNullOrWhitespace(CStr(subregkey.GetValue("InfSection"))) Then
											currentdriverversion = subregkey.GetValue("InfSection").ToString
											UpdateTextMethod(UpdateTextMethodmessagefn(16) + " " + currentdriverversion)
											info.Add("INF", currentdriverversion)
										End If
									End If
									UpdateTextMethod("--------------")

								End If
							End If
						Next
					End If
				Catch ex As Exception
					Application.Log.AddException(ex)
				End Try

				' ----------------------------------------------------------------------------
				' Trying to get the installed GPU info 
				' (These list the one that are at least installed with minimal driver support)
				' ----------------------------------------------------------------------------

				gpuidentify("ven_8086")
				gpuidentify("ven_1002")
				gpuidentify("ven_10de")


				' -------------------------------------
				' Check if this is an AMD Enduro system
				' -------------------------------------
				Try
					regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Enum\PCI")

					If regkey IsNot Nothing Then
						For Each child As String In regkey.GetSubKeyNames()
							If IsNullOrWhitespace(child) = False Then
								If child.ToLower.Contains("ven_8086") Then
									Try
										subregkey = regkey.OpenSubKey(child)
									Catch ex As Exception
										Continue For
									End Try
									For Each childs As String In subregkey.GetSubKeyNames()
										If IsNullOrWhitespace(childs) = False Then
											If Not IsNullOrWhitespace(CStr(subregkey.OpenSubKey(childs).GetValue("Service"))) Then
												If subregkey.OpenSubKey(childs).GetValue("Service").ToString.ToLower.Contains("amdkmdap") Then
													enduro = True
													UpdateTextMethod("System seems to be an AMD Enduro (Intel)")
												End If
											End If
										End If
									Next
								End If
							End If
						Next
					End If
				Catch ex As Exception
					Application.Log.AddException(ex)
				End Try

				If MyIdentity.IsSystem Then
					Select Case System.Windows.Forms.SystemInformation.BootMode
						Case BootMode.FailSafe
							Application.Log.AddMessage("We are in Safe Mode")
						Case BootMode.FailSafeWithNetwork
							Application.Log.AddMessage("We are in Safe Mode with Networking")
						Case BootMode.Normal
							Application.Log.AddMessage("We are not in Safe Mode")
					End Select
				End If

				Application.Log.Add(info)

				GetOemInfo()

			Catch ex As Exception
				MsgBox(ex)
				Application.Log.AddException(ex)
				closeddu()
				Exit Sub
			End Try


			Topmost = False

			If argcleanamd Or argcleannvidia Or argcleanintel Or restart Or silent Then
				Dim trd As Thread = New Thread(AddressOf ThreadTask)
				trd.CurrentCulture = New Globalization.CultureInfo("en-US")
				trd.CurrentUICulture = New Globalization.CultureInfo("en-US")

				trd.IsBackground = True
				trd.Start()
			End If
		Catch ex As Exception
			MsgBox(ex)
		End Try
	End Sub

	Private Sub frmMain_ContentRendered(sender As System.Object, e As System.EventArgs) Handles MyBase.ContentRendered
		If silent Then
			Me.Hide()
		End If
	End Sub

	Private Sub frmMain_Closing(sender As System.Object, e As System.ComponentModel.CancelEventArgs) Handles MyBase.Closing
		If preventclose Then
			e.Cancel = True
			Exit Sub
		End If

		If MyIdentity.IsSystem AndAlso safemode Then
			Try
				My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Control\SafeBoot\Minimal", True).DeleteSubKeyTree("PAexec")
			Catch ex As Exception
			End Try
			Try
				My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Control\SafeBoot\Network", True).DeleteSubKeyTree("PAexec")
			Catch ex As Exception
			End Try
		End If

		Application.Settings.Save()
		Application.Log.SaveToFile()
	End Sub



	Private Sub BackgroundWorker1_DoWork(ByVal sender As System.Object, ByVal e As System.ComponentModel.DoWorkEventArgs) Handles BackgroundWorker1.DoWork
		Dim config As ThreadSettings = CType(e.Argument, ThreadSettings)
		Dim card1 As Integer = Nothing
		Dim vendid As String = ""
		Dim vendidexpected As String = ""
		Dim removegfe As Boolean = config.RemoveGFE
		Dim regkey As RegistryKey = Nothing
		Dim subregkey As RegistryKey = Nothing
		Dim array() As String


		UpdateTextMethod(UpdateTextMethodmessagefn(19))

		preventclose = True

		' Application.Settings is created on MainThread = crossthread
		' Instead: use config.SelectedGPU  <-- Thread safe (actually, combobox1value not needed anymore)
		' If you need any properties,  ThreadSettings.vb <-- just put new Propery line there and assign at btnClean / btnCleanShutdown / btnCleanRestart

		'combobox1value = config.SelectedGPU.ToString()


		Try


			Select Case config.SelectedGPU
				Case GPUVendor.Nvidia
					vendidexpected = "VEN_10DE"
				Case GPUVendor.AMD
					vendidexpected = "VEN_1002"
				Case GPUVendor.Intel
					vendidexpected = "VEN_8086"
			End Select


			UpdateTextMethod(UpdateTextMethodmessagefn(20) + " " & config.SelectedGPU.ToString() & " " + UpdateTextMethodmessagefn(21))
			Application.Log.AddMessage("Uninstalling " + config.SelectedGPU.ToString() + " driver ...")
			UpdateTextMethod(UpdateTextMethodmessagefn(22))


			'SpeedUP the removal of the NVIDIA adapter due to how the NVIDIA installer work.
			If config.SelectedGPU = GPUVendor.Nvidia Then
				temporarynvidiaspeedup(config)
			End If



			'----------------------------------------------
			'Here I remove AMD HD Audio bus (System device)
			'----------------------------------------------

			' First , get the ParentIdPrefix

			If config.RemoveAMDAudioBus AndAlso config.SelectedGPU = GPUVendor.AMD Then
				If config.UseSetupAPI Then
					Try
						'Need to be able to find the "Sibling" with SetupAPI before doing this section

					Catch ex As Exception
						'MessageBox.Show(Languages.GetTranslation(Me.Name, "Messages", "Text6"), Application.Current.MainWindow.GetType().Assembly.GetName().Name, MessageBoxButtons.OK, MessageBoxIcon.Error)
						Application.Log.AddException(ex)
					End Try
				Else
					Try
						If config.SelectedGPU = GPUVendor.AMD Then
							Dim removed As Boolean = False
							Application.Log.AddMessage("Trying to remove the AMD HD Audio BUS")
							regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Enum\HDAUDIO")
							If regkey IsNot Nothing Then
								For Each child As String In regkey.GetSubKeyNames()
									If IsNullOrWhitespace(child) = False Then
										If child.ToLower.Contains("ven_1002") Then
											For Each ParentIdPrefix As String In regkey.OpenSubKey(child).GetSubKeyNames
												subregkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Enum\PCI")
												If subregkey IsNot Nothing Then
													For Each child2 As String In subregkey.GetSubKeyNames()
														removed = False
														If IsNullOrWhitespace(child2) = False Then
															If child2.ToLower.Contains("ven_1002") Then
																For Each child3 As String In subregkey.OpenSubKey(child2).GetSubKeyNames()
																	If IsNullOrWhitespace(child3) = False Then
																		array = CType(subregkey.OpenSubKey(child2 & "\" & child3).GetValue("LowerFilters"), String())
																		If (array IsNot Nothing) AndAlso Not (array.Length < 1) Then
																			For i As Integer = 0 To array.Length - 1
																				If Not IsNullOrWhitespace(array(i)) Then
																					If array(i).ToLower.Contains("amdkmafd") AndAlso ParentIdPrefix.ToLower.Contains(subregkey.OpenSubKey(child2 & "\" & child3).GetValue("ParentIdPrefix").ToString.ToLower) Then
																						Application.Log.AddMessage("Found an AMD audio controller bus !")
																						Try
																							Application.Log.AddMessage("array result: " + array(i))
																						Catch ex As Exception
																						End Try
																						processinfo.FileName = baseDir & "\" & ddudrfolder & "\ddudr.exe"
																						processinfo.Arguments = "remove =system " & Chr(34) & "*" & child2 & Chr(34)
																						processinfo.UseShellExecute = False
																						processinfo.CreateNoWindow = True
																						processinfo.RedirectStandardOutput = True
																						process.StartInfo = processinfo
																						process.Start()
																						reply2 = process.StandardOutput.ReadToEnd
																						process.StandardOutput.Close()
																						process.Close()
																						Application.Log.AddMessage(reply2)
																						Application.Log.AddMessage("AMD HD Audio Bus Removed !")
																						removed = True
																					End If
																				End If
																			Next
																		End If
																		If removed Then
																			Exit For
																		End If
																	End If
																Next
															End If
														End If
													Next
												End If
											Next
										End If
									End If
								Next
							End If
						End If
					Catch ex As Exception
						Application.Log.AddException(ex)
					End Try
				End If

				'Verification is there is still an AMD HD Audio Bus device and set donotremoveamdhdaudiobusfiles to true if thats the case
				Try
					donotremoveamdhdaudiobusfiles = False
					subregkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Enum\PCI")
					If subregkey IsNot Nothing Then
						For Each child2 As String In subregkey.GetSubKeyNames()
							If Not IsNullOrWhitespace(child2) AndAlso child2.ToLower.Contains("ven_1002") Then
								For Each child3 As String In subregkey.OpenSubKey(child2).GetSubKeyNames()
									If IsNullOrWhitespace(child3) = False Then
										array = CType(subregkey.OpenSubKey(child2 & "\" & child3).GetValue("LowerFilters"), String())
										If (array IsNot Nothing) AndAlso Not (array.Length < 1) Then
											For i As Integer = 0 To array.Length - 1
												If Not IsNullOrWhitespace(array(i)) Then
													If array(i).ToLower.Contains("amdkmafd") Then
														Application.Log.AddWarningMessage("Found a remaining AMD audio controller bus ! Preventing the removal of its driverfiles.")
														donotremoveamdhdaudiobusfiles = True
													End If
												End If
											Next
										End If
									End If
								Next
							End If
						Next
					End If
				Catch ex As Exception
					Application.Log.AddException(ex)
				End Try

			End If



				' ----------------------
				' Removing the videocard
				' ----------------------

				If config.UseSetupAPI Then
					Try
						Dim found As List(Of SetupAPI.Device) = SetupAPI.TEST_GetDevices("Device_ClassName", "display")
						If found.Count > 0 Then
							For Each d As SetupAPI.Device In found
								If StrContainsAny(d.HardwareIDs(0), True, vendidexpected) Then
									'SetupAPI.TEST_RemoveDevice(d.HardwareIDs(0))
									SetupAPI.UninstallDevice(d)
								End If
							Next
						End If

					Catch ex As Exception
						'MessageBox.Show(Languages.GetTranslation(Me.Name, "Messages", "Text6"), Application.Current.MainWindow.GetType().Assembly.GetName().Name, MessageBoxButtons.OK, MessageBoxIcon.Error)
						Application.Log.AddException(ex)
					End Try

				Else
					'OLD DDUDR (DEVCON Section)
					For a = 1 To 2	 'loop 2 time here for nVidia SLI pupose in normal mode.(4 may be necessary for quad SLI... need to check.)
						Try
							regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Enum\PCI")
							If regkey IsNot Nothing Then
								For Each child As String In regkey.GetSubKeyNames
									If Not IsNullOrWhitespace(child) AndAlso
									 (child.ToLower.Contains("ven_10de") Or
									 child.ToLower.Contains("ven_8086") Or
									 child.ToLower.Contains("ven_1002")) Then

										subregkey = regkey.OpenSubKey(child)
										If subregkey IsNot Nothing Then

											For Each child2 As String In subregkey.GetSubKeyNames

												If subregkey.OpenSubKey(child2) Is Nothing Then
													Continue For
												End If

												array = CType(subregkey.OpenSubKey(child2).GetValue("CompatibleIDs"), String())

												If (array IsNot Nothing) AndAlso Not (array.Length < 1) Then
													For i As Integer = 0 To array.Length - 1

														If Not IsNullOrWhitespace(array(i)) AndAlso array(i).ToLower.Contains("pci\cc_03") Then

															vendid = child & "\" & child2

															If vendid.ToLower.Contains(vendidexpected.ToLower) Then
																processinfo.FileName = baseDir & "\" & ddudrfolder & "\ddudr.exe"
																processinfo.Arguments = "remove " & Chr(34) & "@pci\" & vendid & Chr(34)
																processinfo.UseShellExecute = False
																processinfo.CreateNoWindow = True
																processinfo.RedirectStandardOutput = True
																process.StartInfo = processinfo

																process.Start()
																reply2 = process.StandardOutput.ReadToEnd
																process.StandardOutput.Close()
																process.Close()
																'process.WaitForExit()
																Application.Log.AddMessage(reply2)
															End If
															Exit For   'the card is removed so we exit the loop from here.
														End If
													Next
												End If
											Next
										End If
									End If
								Next
							End If
						Catch ex As Exception
							MessageBox.Show(Languages.GetTranslation(Me.Name, "Messages", "Text6"), Application.Current.MainWindow.GetType().Assembly.GetName().Name, MessageBoxButtons.OK, MessageBoxIcon.Error)
							Application.Log.AddException(ex)
						End Try
					Next
				End If

				UpdateTextMethod(UpdateTextMethodmessagefn(23))
				Application.Log.AddMessage("SetupAPI Display Driver removal: Complete.")


				cleandriverstore(config)

				UpdateTextMethod(UpdateTextMethodmessagefn(24))
				Application.Log.AddMessage("Executing DDUDR Remove Audio controler.")

				If config.UseSetupAPI Then
					Try
						Dim found As List(Of SetupAPI.Device) = SetupAPI.TEST_GetDevices("Device_ClassName", "media")
						If found.Count > 0 Then
							For Each d As SetupAPI.Device In found
								If StrContainsAny(d.HardwareIDs(0), True, vendidexpected) Then
									'SetupAPI.TEST_RemoveDevice(d.HardwareIDs(0))
									SetupAPI.UninstallDevice(d)
								End If
							Next
						End If

					Catch ex As Exception
						'MessageBox.Show(Languages.GetTranslation(Me.Name, "Messages", "Text6"), Application.Current.MainWindow.GetType().Assembly.GetName().Name, MessageBoxButtons.OK, MessageBoxIcon.Error)
						Application.Log.AddException(ex)
					End Try
				Else
					Try
						regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Enum\HDAUDIO")
						If regkey IsNot Nothing Then
							For Each child As String In regkey.GetSubKeyNames
								If Not IsNullOrWhitespace(child) AndAlso
								   (child.ToLower.Contains("ven_10de") Or
								   child.ToLower.Contains("ven_8086") Or
								   child.ToLower.Contains("ven_1002")) Then

									subregkey = regkey.OpenSubKey(child)
									If subregkey IsNot Nothing Then

										For Each child2 As String In subregkey.GetSubKeyNames

											If subregkey.OpenSubKey(child2) Is Nothing Then
												Continue For
											End If

											vendid = child & "\" & child2

											If vendid.ToLower.Contains(vendidexpected.ToLower) Then
												processinfo.FileName = baseDir & "\" & ddudrfolder & "\ddudr.exe"
												processinfo.Arguments = "remove " & Chr(34) & "@HDAUDIO\" & vendid & Chr(34)
												processinfo.UseShellExecute = False
												processinfo.CreateNoWindow = True
												processinfo.RedirectStandardOutput = True
												process.StartInfo = processinfo

												process.Start()
												reply2 = process.StandardOutput.ReadToEnd
												process.StandardOutput.Close()
												process.Close()
												'process.WaitForExit()
												Application.Log.AddMessage(reply2)


											End If
										Next
									End If
								End If
							Next
						End If
					Catch ex As Exception
						MessageBox.Show(Languages.GetTranslation(Me.Name, "Messages", "Text6"), Application.Current.MainWindow.GetType().Assembly.GetName().Name, MessageBoxButtons.OK, MessageBoxIcon.Error)
						Application.Log.AddException(ex)
					End Try
				End If
				UpdateTextMethod(UpdateTextMethodmessagefn(25))


				Application.Log.AddMessage("DDUDR Remove Audio controler Complete.")


				If config.SelectedGPU <> GPUVendor.Intel Then
					cleandriverstore(config)
				End If

			'Here I remove 3dVision USB Adapter.
			If config.SelectedGPU = GPUVendor.Nvidia Then

				If config.UseSetupAPI Then
					Try
						Dim HWID3dvision As String() =
							{"USB\VID_0955&PID_0007",
							 "USB\VID_0955&PID_7001",
							 "USB\VID_0955&PID_7002",
							 "USB\VID_0955&PID_7003",
							 "USB\VID_0955&PID_7004",
							 "USB\VID_0955&PID_7008",
							 "USB\VID_0955&PID_7009",
							 "USB\VID_0955&PID_700A",
							 "USB\VID_0955&PID_700C",
							 "USB\VID_0955&PID_700D&MI_00",
							 "USB\VID_0955&PID_700E&MI_00"}

						'3dVision Removal
						Dim found As List(Of SetupAPI.Device) = SetupAPI.TEST_GetDevices("Device_ClassName", "media")
						If found.Count > 0 Then
							For Each d As SetupAPI.Device In found
								If StrContainsAny(d.HardwareIDs(0), True, HWID3dvision) Then
									'SetupAPI.TEST_RemoveDevice(d.HardwareIDs(0))
									SetupAPI.UninstallDevice(d)
								End If
							Next
							found.Clear()
						End If



						'NVIDIA SHIELD Wireless Controller Trackpad
						found = SetupAPI.TEST_GetDevices("Device_ClassName", "mouse")
						If found.Count > 0 Then
							For Each d As SetupAPI.Device In found
								If StrContainsAny(d.HardwareIDs(0), True, HWID3dvision) Then
									SetupAPI.UninstallDevice(d)
								End If
							Next
							found.Clear()
						End If

						If config.RemoveGFE Then
							' NVIDIA Virtual Audio Device (Wave Extensible) (WDM) Removal

							found = SetupAPI.TEST_GetDevices("Device_ClassName", "media")
							If found.Count > 0 Then
								For Each d As SetupAPI.Device In found
									If StrContainsAny(d.HardwareIDs(0), True, "USB\VID_0955&PID_9000") Then
										SetupAPI.UninstallDevice(d)
									End If
								Next
								found.Clear()
							End If
						End If

						'nVidia AudioEndpoints Removal
						found = SetupAPI.TEST_GetDevices("Device_ClassName", "audioendpoint")
						If found.Count > 0 Then
							For Each d As SetupAPI.Device In found
								If StrContainsAny(d.FriendlyName, True, "nvidia virtual audio device", "nvidia high definition audio") Then
									SetupAPI.UninstallDevice(d)
								End If
							Next
							found.Clear()
						End If

					Catch ex As Exception
						'MessageBox.Show(Languages.GetTranslation(Me.Name, "Messages", "Text6"), Application.Current.MainWindow.GetType().Assembly.GetName().Name, MessageBoxButtons.OK, MessageBoxIcon.Error)
						Application.Log.AddException(ex)
					End Try
				Else
					Try
						'removing 3DVision USB driver
						processinfo.FileName = baseDir & "\" & ddudrfolder & "\ddudr.exe"
						processinfo.Arguments = "findall =USB"
						processinfo.UseShellExecute = False
						processinfo.CreateNoWindow = True
						processinfo.RedirectStandardOutput = True

						'creation dun process fantome pour le wait on exit.

						process.StartInfo = processinfo
						process.Start()
						reply = process.StandardOutput.ReadToEnd
						process.StandardOutput.Close()
						process.Close()
						'process.WaitForExit()

						Try
							card1 = reply.IndexOf("USB\")
						Catch ex As Exception
						End Try

						While card1 > -1

							position2 = reply.IndexOf(":", card1)
							vendid = reply.Substring(card1, position2 - card1).Trim
							If vendid.Contains("USB\VID_0955&PID_0007") Or
							 vendid.Contains("USB\VID_0955&PID_7001") Or
							 vendid.Contains("USB\VID_0955&PID_7002") Or
							 vendid.Contains("USB\VID_0955&PID_7003") Or
							 vendid.Contains("USB\VID_0955&PID_7004") Or
							 vendid.Contains("USB\VID_0955&PID_7008") Or
							 vendid.Contains("USB\VID_0955&PID_7009") Or
							 vendid.Contains("USB\VID_0955&PID_700A") Or
							 vendid.Contains("USB\VID_0955&PID_700C") Or
							 vendid.Contains("USB\VID_0955&PID_700D&MI_00") Or
							 vendid.Contains("USB\VID_0955&PID_700E&MI_00") Then
								Application.Log.AddMessage("-" & vendid & "- 3D vision usb controler found")


								processinfo.FileName = baseDir & "\" & ddudrfolder & "\ddudr.exe"
								processinfo.Arguments = "remove =USB " & Chr(34) & vendid & Chr(34)
								processinfo.UseShellExecute = False
								processinfo.CreateNoWindow = True
								processinfo.RedirectStandardOutput = True
								process.StartInfo = processinfo

								process.Start()
								reply2 = process.StandardOutput.ReadToEnd
								process.StandardOutput.Close()
								process.Close()
								'process.WaitForExit()
								Application.Log.AddMessage(reply2)



							End If
							card1 = reply.IndexOf("USB\", card1 + 1)

						End While

					Catch ex As Exception
						MessageBox.Show(Languages.GetTranslation(Me.Name, "Messages", "Text6"), Application.Current.MainWindow.GetType().Assembly.GetName().Name, MessageBoxButtons.OK, MessageBoxIcon.Error)
						Application.Log.AddException(ex)
					End Try

					UpdateTextMethod(UpdateTextMethodmessagefn(26))

					Try
						'removing NVIDIA SHIELD Wireless Controller Trackpad
						processinfo.FileName = baseDir & "\" & ddudrfolder & "\ddudr.exe"
						processinfo.Arguments = "findall =MOUSE"
						processinfo.UseShellExecute = False
						processinfo.CreateNoWindow = True
						processinfo.RedirectStandardOutput = True

						'creation dun process fantome pour le wait on exit.

						process.StartInfo = processinfo
						process.Start()
						reply = process.StandardOutput.ReadToEnd
						process.StandardOutput.Close()
						process.Close()
						'process.WaitForExit()

						Try
							card1 = reply.IndexOf("HID\")
						Catch ex As Exception
						End Try

						While card1 > -1

							position2 = reply.IndexOf(":", card1)
							vendid = reply.Substring(card1, position2 - card1).Trim
							If vendid.ToLower.Contains("hid\vid_0955&pid_7210") Then
								Application.Log.AddMessage("-" & vendid & "- NVIDIA SHIELD Wireless Controller Trackpad found")


								processinfo.FileName = baseDir & "\" & ddudrfolder & "\ddudr.exe"
								processinfo.Arguments = "remove =MOUSE " & Chr(34) & vendid & Chr(34)
								processinfo.UseShellExecute = False
								processinfo.CreateNoWindow = True
								processinfo.RedirectStandardOutput = True
								process.StartInfo = processinfo

								process.Start()
								reply2 = process.StandardOutput.ReadToEnd
								process.StandardOutput.Close()
								process.Close()
								'process.WaitForExit()

								Application.Log.AddMessage(reply2)


							End If
							card1 = reply.IndexOf("HID\", card1 + 1)

						End While

					Catch ex As Exception
						MessageBox.Show(Languages.GetTranslation(Me.Name, "Messages", "Text6"), Application.Current.MainWindow.GetType().Assembly.GetName().Name, MessageBoxButtons.OK, MessageBoxIcon.Error)
						Application.Log.AddException(ex)
					End Try

					'Removing NVIDIA Virtual Audio Device (Wave Extensible) (WDM)
					If removegfe Then

						Try
							regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Enum\ROOT")
							If regkey IsNot Nothing Then
								For Each child As String In regkey.GetSubKeyNames
									If Not IsNullOrWhitespace(child) Then

										subregkey = regkey.OpenSubKey(child)
										If subregkey IsNot Nothing Then

											For Each child2 As String In subregkey.GetSubKeyNames
												If Not IsNullOrWhitespace(child2) Then
													If subregkey.OpenSubKey(child2) Is Nothing Then
														Continue For
													End If

													If Not IsNullOrWhitespace(CStr(subregkey.OpenSubKey(child2).GetValue("DeviceDesc"))) AndAlso
													   subregkey.OpenSubKey(child2).GetValue("DeviceDesc").ToString.ToLower.Contains("nvidia virtual audio device") Then

														vendid = child & "\" & child2

														processinfo.FileName = baseDir & "\" & ddudrfolder & "\ddudr.exe"
														processinfo.Arguments = "remove " & Chr(34) & "@ROOT\" & vendid & Chr(34)
														processinfo.UseShellExecute = False
														processinfo.CreateNoWindow = True
														processinfo.RedirectStandardOutput = True
														process.StartInfo = processinfo

														process.Start()
														reply2 = process.StandardOutput.ReadToEnd
														process.StandardOutput.Close()
														process.Close()
														'process.WaitForExit()
														Application.Log.AddMessage(reply2)


													End If
												End If
											Next
										End If
									End If
								Next
							End If
						Catch ex As Exception
							MessageBox.Show(Languages.GetTranslation(Me.Name, "Messages", "Text6"), Application.Current.MainWindow.GetType().Assembly.GetName().Name, MessageBoxButtons.OK, MessageBoxIcon.Error)
							Application.Log.AddException(ex)
						End Try

					End If
					' ------------------------------
					' Removing nVidia AudioEndpoints
					' ------------------------------

					Application.Log.AddMessage("Removing nVidia Audio Endpoints")


					Try
						regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Enum\SWD\MMDEVAPI")
						If regkey IsNot Nothing Then
							For Each child As String In regkey.GetSubKeyNames
								If Not IsNullOrWhitespace(child) Then

									If Not IsNullOrWhitespace(CStr(regkey.OpenSubKey(child).GetValue("FriendlyName"))) AndAlso
									   (regkey.OpenSubKey(child).GetValue("FriendlyName").ToString.ToLower.Contains("nvidia virtual audio device") AndAlso removegfe) Or
									   regkey.OpenSubKey(child).GetValue("FriendlyName").ToString.ToLower.Contains("nvidia high definition audio") Then

										vendid = child

										processinfo.FileName = baseDir & "\" & ddudrfolder & "\ddudr.exe"
										processinfo.Arguments = "remove " & Chr(34) & "@SWD\MMDEVAPI\" & vendid & Chr(34)
										processinfo.UseShellExecute = False
										processinfo.CreateNoWindow = True
										processinfo.RedirectStandardOutput = True
										process.StartInfo = processinfo

										process.Start()
										reply2 = process.StandardOutput.ReadToEnd
										process.StandardOutput.Close()
										process.Close()
										'process.WaitForExit()
										Application.Log.AddMessage(reply2)


									End If
								End If
							Next
						End If
					Catch ex As Exception
						MessageBox.Show(Languages.GetTranslation(Me.Name, "Messages", "Text6"), Application.Current.MainWindow.GetType().Assembly.GetName().Name, MessageBoxButtons.OK, MessageBoxIcon.Error)
						Application.Log.AddException(ex)
					End Try
				End If
			End If

			If config.SelectedGPU = GPUVendor.AMD Then
				' ------------------------------
				' Removing some of AMD AudioEndpoints
				' ------------------------------
				Application.Log.AddMessage("Removing AMD Audio Endpoints")
				Try
					If config.UseSetupAPI Then
						'nVidia AudioEndpoints Removal
						Dim found As List(Of SetupAPI.Device) = SetupAPI.TEST_GetDevices("Device_ClassName", "audioendpoint")
						If found.Count > 0 Then
							For Each d As SetupAPI.Device In found
								If StrContainsAny(d.FriendlyName, True, "amd high definition audio device", "digital audio (hdmi) (high definition audio device)") Then
									SetupAPI.UninstallDevice(d)
								End If
							Next
							found.Clear()
						End If
					Else
						regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Enum\SWD\MMDEVAPI")
						If regkey IsNot Nothing Then
							For Each child As String In regkey.GetSubKeyNames
								If Not IsNullOrWhitespace(child) Then

									If Not IsNullOrWhitespace(CStr(regkey.OpenSubKey(child).GetValue("FriendlyName"))) AndAlso
									   regkey.OpenSubKey(child).GetValue("FriendlyName").ToString.ToLower.Contains("amd high definition audio device") Or
									   regkey.OpenSubKey(child).GetValue("FriendlyName").ToString.ToLower.Contains("digital audio (hdmi) (high definition audio device)") Then

										vendid = child

										processinfo.FileName = baseDir & "\" & ddudrfolder & "\ddudr.exe"
										processinfo.Arguments = "remove " & Chr(34) & "@SWD\MMDEVAPI\" & vendid & Chr(34)
										processinfo.UseShellExecute = False
										processinfo.CreateNoWindow = True
										processinfo.RedirectStandardOutput = True
										process.StartInfo = processinfo

										process.Start()
										reply2 = process.StandardOutput.ReadToEnd
										process.StandardOutput.Close()
										process.Close()
										'process.WaitForExit()
										Application.Log.AddMessage(reply2)


									End If
								End If
							Next
						End If
					End If
			Catch ex As Exception
					MessageBox.Show(Languages.GetTranslation(Me.Name, "Messages", "Text6"), Application.Current.MainWindow.GetType().Assembly.GetName().Name, MessageBoxButtons.OK, MessageBoxIcon.Error)
					Application.Log.AddException(ex)
				End Try

			End If

			If config.SelectedGPU = GPUVendor.Intel Then
				'Removing Intel WIdI bus Enumerator
				Application.Log.AddMessage("Removing IWD Bus Enumerator")

				processinfo.FileName = baseDir & "\" & ddudrfolder & "\ddudr.exe"
				processinfo.Arguments = "remove =system " & Chr(34) & "root\iwdbus" & Chr(34)
				processinfo.UseShellExecute = False
				processinfo.CreateNoWindow = True
				processinfo.RedirectStandardOutput = True
				process.StartInfo = processinfo

				process.Start()
				reply2 = process.StandardOutput.ReadToEnd
				process.StandardOutput.Close()
				process.Close()
				'process.WaitForExit()
				Application.Log.AddMessage(reply2)



				' ------------------------------
				' Removing Intel AudioEndpoints
				' ------------------------------
				Application.Log.AddMessage("Removing Intel Audio Endpoints")

				Try
					regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Enum\SWD\MMDEVAPI")
					If regkey IsNot Nothing Then
						For Each child As String In regkey.GetSubKeyNames
							If Not IsNullOrWhitespace(child) Then

								If Not IsNullOrWhitespace(CStr(regkey.OpenSubKey(child).GetValue("FriendlyName"))) AndAlso
								   regkey.OpenSubKey(child).GetValue("FriendlyName").ToString.ToLower.Contains("intel widi") Or
								   regkey.OpenSubKey(child).GetValue("FriendlyName").ToString.ToLower.Contains("intel(r)") Then

									vendid = child

									processinfo.FileName = baseDir & "\" & ddudrfolder & "\ddudr.exe"
									processinfo.Arguments = "remove " & Chr(34) & "@SWD\MMDEVAPI\" & vendid & Chr(34)
									processinfo.UseShellExecute = False
									processinfo.CreateNoWindow = True
									processinfo.RedirectStandardOutput = True
									process.StartInfo = processinfo

									process.Start()
									reply2 = process.StandardOutput.ReadToEnd
									process.StandardOutput.Close()
									process.Close()
									'process.WaitForExit()
									Application.Log.AddMessage(reply2)


								End If
							End If
						Next
					End If
				Catch ex As Exception
					MessageBox.Show(Languages.GetTranslation(Me.Name, "Messages", "Text6"), Application.Current.MainWindow.GetType().Assembly.GetName().Name, MessageBoxButtons.OK, MessageBoxIcon.Error)
					Application.Log.AddException(ex)
				End Try
			End If


			Application.Log.AddMessage("ddudr Remove Audio/HDMI Complete")

			'removing monitor and hidden monitor



			If config.RemoveMonitors Then
				Application.Log.AddMessage("ddudr Remove Monitor started")
				If config.UseSetupAPI Then
					Dim found As List(Of SetupAPI.Device) = SetupAPI.TEST_GetDevices("Device_ClassName", "monitor")
					If found.Count > 0 Then
						For Each d As SetupAPI.Device In found
							'SetupAPI.TEST_RemoveDevice(d.HardwareIDs(0))
							SetupAPI.UninstallDevice(d)
						Next
					End If
				Else
					Try
						regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Enum\DISPLAY")
						If regkey IsNot Nothing Then
							For Each child As String In regkey.GetSubKeyNames
								If Not IsNullOrWhitespace(child) Then

									subregkey = regkey.OpenSubKey(child)
									If subregkey IsNot Nothing Then

										For Each child2 As String In subregkey.GetSubKeyNames
											If Not IsNullOrWhitespace(child2) Then

												If subregkey.OpenSubKey(child2) Is Nothing Then
													Continue For
												End If

												vendid = child & "\" & child2


												processinfo.FileName = baseDir & "\" & ddudrfolder & "\ddudr.exe"
												processinfo.Arguments = "remove " & Chr(34) & "@DISPLAY\" & vendid & Chr(34)
												processinfo.UseShellExecute = False
												processinfo.CreateNoWindow = True
												processinfo.RedirectStandardOutput = True
												process.StartInfo = processinfo

												process.Start()
												reply2 = process.StandardOutput.ReadToEnd
												process.StandardOutput.Close()
												process.Close()
												'process.WaitForExit()
											End If
										Next
									End If
								End If
							Next
						End If
					Catch ex As Exception
						MessageBox.Show(Languages.GetTranslation(Me.Name, "Messages", "Text6"), Application.Current.MainWindow.GetType().Assembly.GetName().Name, MessageBoxButtons.OK, MessageBoxIcon.Error)
						Application.Log.AddException(ex)
					End Try
				End If
				UpdateTextMethod(UpdateTextMethodmessagefn(27))
			End If
			UpdateTextMethod(UpdateTextMethodmessagefn(28))

			'here we set back to default the changes made by the AMDKMPFD even if we are cleaning amd or intel. We dont what that
			'espcially if we are not using an AMD GPU

			If config.RemoveAMDKMPFD Then
				If config.UseSetupAPI Then
					Try
						Dim found As List(Of SetupAPI.Device) = SetupAPI.TEST_GetDevices("Device_ClassName", "system")
						If found.Count > 0 Then
							For Each d As SetupAPI.Device In found
								If StrContainsAny(d.HardwareIDs(0), True, "DEV_0A08", "DEV_0A03") Then
									If d.LowerFilters IsNot Nothing AndAlso StrContainsAny(d.LowerFilters(0), True, "amdkmpfd") Then
										If win10 Then
											SetupAPI.TEST_UpdateDevice(d.HardwareIDs(0), config.Paths.WinDir + "inf\PCI.inf", True)
										Else
											SetupAPI.TEST_UpdateDevice(d.HardwareIDs(0), config.Paths.WinDir + "inf\machine.inf", True)
										End If
									End If
								End If
							Next
						End If

					Catch ex As Exception
						'MessageBox.Show(Languages.GetTranslation(Me.Name, "Messages", "Text6"), Application.Current.MainWindow.GetType().Assembly.GetName().Name, MessageBoxButtons.OK, MessageBoxIcon.Error)
						Application.Log.AddException(ex)
					End Try
				Else

					Try
						Application.Log.AddMessage("Checking and Removing AMDKMPFD Filter if present")

						regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Enum\ACPI")
						If regkey IsNot Nothing Then
							For Each child As String In regkey.GetSubKeyNames()
								If IsNullOrWhitespace(child) = False Then
									If child.ToLower.Contains("pnp0a08") Or
									   child.ToLower.Contains("pnp0a03") Then
										subregkey = regkey.OpenSubKey(child)
										If subregkey IsNot Nothing Then
											For Each child2 As String In subregkey.GetSubKeyNames()
												If Not IsNullOrWhitespace(child2) Then
													array = CType(subregkey.OpenSubKey(child2).GetValue("LowerFilters"), String())
													If (array IsNot Nothing) AndAlso Not (array.Length < 1) Then
														For i As Integer = 0 To array.Length - 1
															If Not IsNullOrWhitespace(array(i)) Then
																If array(i).ToLower.Contains("amdkmpfd") Then
																	Application.Log.AddMessage("Found an AMDKMPFD! in " + child)

																	Try
																		Application.Log.AddMessage("array result: " + array(i))
																	Catch ex As Exception
																	End Try
																	processinfo.FileName = baseDir & "\" & ddudrfolder & "\ddudr.exe"
																	If win10 Then
																		processinfo.Arguments = "update " & config.Paths.WinDir & "inf\pci.inf " & Chr(34) & "*" & child & Chr(34)
																	Else
																		processinfo.Arguments = "update " & config.Paths.WinDir & "inf\machine.inf " & Chr(34) & "*" & child & Chr(34)
																	End If
																	processinfo.UseShellExecute = False
																	processinfo.CreateNoWindow = True
																	processinfo.RedirectStandardOutput = True
																	process.StartInfo = processinfo

																	process.Start()
																	reply2 = process.StandardOutput.ReadToEnd
																	'process.WaitForExit()
																	process.StandardOutput.Close()
																	process.Close()

																	Application.Log.AddMessage(reply2)
																	Application.Log.AddMessage(child + " Restored.")

																End If
															End If
														Next
													End If
												End If
											Next
										End If
									End If
								End If
							Next
						End If
					Catch ex As Exception
						Application.Log.AddException(ex)
					End Try
				End If
				'We now try to remove the service AMDPMPFD if its lowerfilter is not found
				If reboot Or shutdown Then
					If Not checkamdkmapfd() Then
						CleanupEngine.cleanserviceprocess({"amdkmpfd"})
					End If
				End If
			End If

			If config.SelectedGPU = GPUVendor.AMD Then
				cleanamdserviceprocess()
				cleanamd(config)

				If System.Windows.Forms.SystemInformation.BootMode = BootMode.Normal Then
					Application.Log.AddMessage("Killing Explorer.exe")

					Dim appproc = process.GetProcessesByName("explorer")
					For i As Integer = 0 To appproc.Length - 1
						appproc(i).Kill()
					Next i
				End If

				cleanamdfolders(config)
			End If

			If config.SelectedGPU = GPUVendor.Nvidia Then
				cleannvidiaserviceprocess(config)
				cleannvidia(config)

				If System.Windows.Forms.SystemInformation.BootMode = BootMode.Normal Then
					Application.Log.AddMessage("Killing Explorer.exe")

					Dim appproc = process.GetProcessesByName("explorer")
					For i As Integer = 0 To appproc.Length - 1
						appproc(i).Kill()
					Next i
				End If


				cleannvidiafolders(config)
				checkpcieroot()
			End If

			If config.SelectedGPU = GPUVendor.Intel Then
				cleanintelserviceprocess()
				cleanintel(config)

				If System.Windows.Forms.SystemInformation.BootMode = BootMode.Normal Then
					Application.Log.AddMessage("Killing Explorer.exe")

					Dim appproc = process.GetProcessesByName("explorer")
					For i As Integer = 0 To appproc.Length - 1
						appproc(i).Kill()
					Next i
				End If

				cleanintelfolders()
			End If

			cleandriverstore(config)
			fixregistrydriverstore()
			'rebuildcountercache()
		Catch ex As Exception
			Application.Log.AddException(ex)
			MessageBox.Show(Languages.GetTranslation("frmMain", "Messages", "Text6"), config.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error)
			stopme = True
		End Try

	End Sub

	' TODO: BackgroundWorker1_DoWork1 (see text)
	Private Sub BackgroundWorker1_DoWork1(sender As Object, e As System.ComponentModel.DoWorkEventArgs) Handles BackgroundWorker1.DoWork
		Dim config As ThreadSettings = CType(e.Argument, ThreadSettings)

		'	Application.Settings  and  Application.Paths
		'	causes CrossThread exception  (DONT USE !!! )
		' 
		'	Instead:
		'
		'		BackgroundWorker1.RunWorkerAsync()
		' --->	BackgroundWorker1.RunWorkerAsync(New ThreadSettings())    'Creates Copy of AppSettings & AppPaths = Safe for Threading (no reference)
		'		"ThreadSettings" is passed to thread via "e.Argument"
		'
		'
		'		@ BackgroundWorker1_DoWork
		'		Dim config As ThreadSettings = CType(e.Argument, ThreadSettings)  
		'
		'		e.Result = "Obj2"  <-- "Obj2" is passed to RunWorkerCompleted after DoWork
		'
		'
		'		@ BackgroundWorker1_RunWorkerCompleted
		'		e.Result  <-- "Obj2"
	End Sub

	Private Sub BackgroundWorker1_RunWorkerCompleted(ByVal sender As System.Object, ByVal e As System.ComponentModel.RunWorkerCompletedEventArgs) Handles BackgroundWorker1.RunWorkerCompleted
		Try

			If stopme = True Then
				'Scan for new hardware to not let users into a non working state.

				Dim scan As New ProcessStartInfo
				scan.FileName = baseDir & "\" & ddudrfolder & "\ddudr.exe"
				scan.Arguments = "rescan"
				scan.UseShellExecute = False
				scan.CreateNoWindow = True
				scan.RedirectStandardOutput = False
				Dim proc4 As New Process
				proc4.StartInfo = scan
				proc4.Start()
				proc4.WaitForExit()
				proc4.Close()
				'then quit
				closeddu()
				Exit Sub
			End If


			'For command line arguement to know if there is more cleans to be done.

			preventclose = False
			backgroundworkcomplete = True

			UpdateTextMethod(UpdateTextMethodmessagefn(9))

			Application.Log.AddMessage("Clean uninstall completed!")


			If Not shutdown Then
				rescan()
			End If

			btnCleanRestart.IsEnabled = True
			btnClean.IsEnabled = True
			btnCleanShutdown.IsEnabled = True
			MenuStrip1.IsEnabled = True

			If nbclean < 2 And Not silent And Not reboot And Not shutdown Then
				If MessageBox.Show(Languages.GetTranslation(Me.Name, "Messages", "Text10"), Application.Current.MainWindow.GetType().Assembly.GetName().Name, MessageBoxButtons.YesNo, MessageBoxIcon.Information) = Windows.Forms.DialogResult.Yes Then
					closeddu()
					Exit Sub
				End If
			End If

			If reboot Then
				restartcomputer()
			End If

			If shutdown Then
				shutdowncomputer()
			End If

		Catch ex As Exception
			preventclose = False
			Application.Log.AddException(ex)
		End Try
	End Sub

#End Region

	Private Sub ThreadTask()

		Try
			If argcleanamd Then
				backgroundworkcomplete = False
				cleananddonothing("AMD")
			End If

			Do Until backgroundworkcomplete
				System.Threading.Thread.Sleep(10)
			Loop

			If argcleannvidia Then
				backgroundworkcomplete = False
				cleananddonothing("NVIDIA")
			End If

			Do Until backgroundworkcomplete
				System.Threading.Thread.Sleep(10)
			Loop

			If argcleanintel Then
				backgroundworkcomplete = False
				cleananddonothing("INTEL")
			End If

			Do Until backgroundworkcomplete
				System.Threading.Thread.Sleep(10)
			Loop

			If restart Then
				Application.Log.AddMessage("Restarting Computer ")
				processinfo.FileName = "shutdown"
				processinfo.Arguments = "/r /t 0"
				processinfo.WindowStyle = ProcessWindowStyle.Hidden
				processinfo.UseShellExecute = True
				processinfo.CreateNoWindow = True
				processinfo.RedirectStandardOutput = False

				process.StartInfo = processinfo
				process.Start()
				process.WaitForExit()
				process.Close()

				closeddu()
				Exit Sub
			End If

			If silent And (Not restart) Then
				closeddu()
			End If
		Catch ex As Exception
			Application.log.AddException(ex)
		End Try
	End Sub

	Private Sub systemrestore()
		'THIS NEEDS TO BE FIXED!!! DOES NOT WORK WITH OPTION STRICT ON. I WAS UNABLE TO FIGURE OUT MY SELF. BE SURE TO FIX BEFORE RELEASE.

		If Application.Settings.CreateRestorePoint Then
			Try
				UpdateTextMethod("Creating System Restore point (If allowed by the system)")
				Application.Log.AddMessage("Trying to Create a System Restored Point")
				Dim oScope As New ManagementScope("\\localhost\root\default")
				Dim oPath As New ManagementPath("SystemRestore")
				Dim oGetOp As New ObjectGetOptions()
				Dim oProcess As New ManagementClass(oScope, oPath, oGetOp)

				Dim oInParams As ManagementBaseObject = oProcess.GetMethodParameters("CreateRestorePoint")
				oInParams("Description") = "DDU System Restored Point"
				oInParams("RestorePointType") = 12 ' MODIFY_SETTINGS
				oInParams("EventType") = 100

				Dim oOutParams As ManagementBaseObject = oProcess.InvokeMethod("CreateRestorePoint", oInParams, Nothing)

				Application.Log.AddMessage("System Restored Point Created. code: " + CStr(oOutParams("ReturnValue")))
			Catch ex As Exception
				Application.Log.AddMessage("System Restored Point Could not be Created! Err Code: 0x" & Hex(Err.Number))
			End Try

		End If

	End Sub

	Private Sub GetOemInfo()
		Dim info As LogEntry = LogEntry.Create()
		info.Type = LogType.Event
		info.Separator = " = "
		info.Message = "The following third-party driver packages are installed on this computer"

		Try
			For Each oem As OemINF In GetOemInfList(Application.Paths.WinDir & "inf\")
				info.Add(oem.FileName)
				info.Add("Provider", oem.Provider)
				info.Add("Class", oem.Class)

				If Not oem.IsValid Then
					info.Add("This inf entry is corrupted or invalid.")
					'	deletefile(oem.FileName)  ' DOUBLE CHECK THIS before uncommentting
				End If

				info.Add("")
			Next

			Application.Log.Add(info)
		Catch ex As Exception
			Application.log.AddException(ex)
		End Try
	End Sub

	Public Sub TestDelete(ByVal folder As String)
		' UpdateTextMethod(UpdateTextMethodmessagefn("18"))
		'Application.Log.AddMessage("Deleting some specials folders, it could take some times...")
		'ensure that this folder can be accessed with current user ac.
		If Not Directory.Exists(folder) Then
			Exit Sub
		End If

		'Get an object repesenting the directory path below
		Dim di As New DirectoryInfo(folder)

		'Traverse all of the child directors in the root; get to the lowest child
		'and delete all files, working our way back up to the top.  All files
		'must be deleted in the directory, before the directory itself can be deleted.
		'also if there is hidden / readonly / system attribute..  change those attribute.
		Try


			For Each diChild As DirectoryInfo In di.GetDirectories()
				diChild.Attributes = diChild.Attributes And Not IO.FileAttributes.ReadOnly
				diChild.Attributes = diChild.Attributes And Not IO.FileAttributes.Hidden
				diChild.Attributes = diChild.Attributes And Not IO.FileAttributes.System
				If Not (((Not Application.Settings.RemovePhysX) AndAlso diChild.ToString.ToLower.Contains("physx"))) AndAlso Not diChild.ToString.ToLower.Contains("nvidia demos") Then

					Try
						TraverseDirectory(diChild)
					Catch ex As Exception
						Application.Log.AddException(ex)
					End Try
				End If
			Next
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try
		'Finally, clean all of the files directly in the root directory
		CleanAllFilesInDirectory(di)

		'The containing directory can only be deleted if the directory
		'is now completely empty and all files previously within
		'were deleted.
		Try
			If di.GetFiles().Length = 0 And Directory.GetDirectories(folder).Length = 0 Then
				di.Delete()
				Application.Log.AddMessage(di.ToString + " - " + "Folder removed via testdelete sub")
			End If
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try
	End Sub

	Private Sub TraverseDirectory(ByVal di As DirectoryInfo)

		'If the current directory has more child directories, then continure
		'to traverse down until we are at the lowest level and remove
		'there hidden / readonly / system attribute..  At that point all of the
		'files will be deleted.
		For Each diChild As DirectoryInfo In di.GetDirectories()
			diChild.Attributes = diChild.Attributes And Not IO.FileAttributes.ReadOnly
			diChild.Attributes = diChild.Attributes And Not IO.FileAttributes.Hidden
			diChild.Attributes = diChild.Attributes And Not IO.FileAttributes.System
			If Not (((Not Application.Settings.RemovePhysX) AndAlso diChild.ToString.ToLower.Contains("physx"))) AndAlso Not diChild.ToString.ToLower.Contains("nvidia demos") Then

				Try
					TraverseDirectory(diChild)
				Catch ex As Exception
					Application.Log.AddException(ex)
				End Try
			End If
		Next

		'Now that we have no more child directories to traverse, delete all of the files
		'in the current directory, and then delete the directory itself.
		CleanAllFilesInDirectory(di)


		'The containing directory can only be deleted if the directory
		'is now completely empty and all files previously within
		'were deleted.
		If di.GetFiles().Length = 0 Then
			Try
				di.Delete()
			Catch ex As Exception
				Application.log.AddException(ex)
			End Try
		End If

	End Sub

	''' Iterates through all files in the directory passed into
	''' method and deletes them.
	''' It may be necessary to wrap this call in impersonation or ensure parent directory
	''' permissions prior, because delete permissions are not guaranteed.

	Private Sub CleanAllFilesInDirectory(ByVal DirectoryToClean As DirectoryInfo)

		Try
			For Each fi As FileInfo In DirectoryToClean.GetFiles()
				'The following code is NOT required, but shows how some logic can be wrapped
				'around the deletion of files.  For example, only delete files with
				'a creation date older than 1 hour from the current time.  If you
				'always want to delete all of the files regardless, just remove
				'the next 'If' statement.

				'Read only files can not be deleted, so mark the attribute as 'IsReadOnly = False'

				Try
					fi.IsReadOnly = False
				Catch ex As Exception
				End Try

				Try
					fi.Delete()
				Catch ex As Exception
				End Try
				'On a rare occasion, files being deleted might be slower than program execution, and upon returning
				'from this call, attempting to delete the directory will throw an exception stating it is not yet
				'empty, even though a fraction of a second later it actually is.  Therefore the 'Optional' code below
				'can stall the process just long enough to ensure the file is deleted before proceeding. The value
				'can be adjusted as needed from testing and running the process repeatedly.
				'System.Threading.Thread.sleep(10)  '50 millisecond stall (0.025 Seconds)

			Next
		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try
	End Sub

	Private Sub KillProcess(ByVal ParamArray processnames As String())
		For Each processName As String In processnames
			If String.IsNullOrEmpty(processName) Then
				Continue For
			End If

			For Each process As Process In process.GetProcessesByName(processName)
				Try
					process.Kill()
				Catch ex As Exception
					Application.Log.AddException(ex, "@KillProcesses()", String.Concat("ProcessName: ", processName))
				End Try
			Next
		Next
	End Sub

	Private Sub KillGPUStatsProcesses()
		' Not sure for the x86 one...
		' Shady: probably the same but without _x64, and a few sites seem to confirm this, doesn't hurt to just add it anyway

		KillProcess(
		 "MSIAfterburner",
		  "PrecisionX_x64",
		  "PrecisionXServer_x64",
		  "PrecisionX",
		  "PrecisionXServer",
		  "RTSS",
		  "RTSSHooksLoader64",
		  "EncoderServer64",
		  "RTSSHooksLoader",
		  "EncoderServer",
		  "nvidiaInspector")
	End Sub

	Private Sub cleananddonothing(ByVal gpu As String)
		reboot = False
		shutdown = False
		BackgroundWorker1.RunWorkerAsync()
	End Sub

	Private Sub cleanandandreboot(ByVal gpu As String)
		reboot = True
		shutdown = False
		BackgroundWorker1.RunWorkerAsync()

	End Sub

	Private Sub disabledriversearch()
		Dim regkey As RegistryKey = Nothing
		Application.Log.AddMessage("Trying to disable search for Windows Updates :")
		Application.Log.AddMessage("Version " + version + " detected")

		If version >= "6.1" Then
			Try
				regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching", True)
				If CInt(regkey.GetValue("SearchOrderConfig").ToString) <> 0 Then
					regkey.SetValue("SearchOrderConfig", 0)
					MessageBox.Show(Languages.GetTranslation(Me.Name, "Messages", "Text9"), Application.Current.MainWindow.GetType().Assembly.GetName().Name, MessageBoxButtons.OK, MessageBoxIcon.Information)
				End If
			Catch ex As Exception
				Application.Log.AddException(ex)
			End Try
		End If

		If version >= "6.0" And version < "6.1" Then
			Try
				regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Policies\Microsoft\Windows\DriverSearching", True)
				If CInt(regkey.GetValue("DontSearchWindowsUpdate").ToString) <> 1 Then
					regkey.SetValue("DontSearchWindowsUpdate", 1)
					MessageBox.Show(Languages.GetTranslation(Me.Name, "Messages", "Text9"), Application.Current.MainWindow.GetType().Assembly.GetName().Name, MessageBoxButtons.OK, MessageBoxIcon.Information)
				End If
			Catch ex As Exception
				Application.Log.AddException(ex)
			End Try
		End If
	End Sub

	Private Function checkamdkmapfd() As Boolean

		Dim regkey As RegistryKey = Nothing
		Dim subregkey As RegistryKey = Nothing
		Dim array As String() = Nothing
		Dim iskmpfdpresent As Boolean = False

		Try
			Application.Log.AddMessage("Checking if AMDKMPFD is present before Service removal")
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Enum\ACPI")
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames()
					If IsNullOrWhitespace(child) = False Then
						If child.ToLower.Contains("pnp0a08") Or
						   child.ToLower.Contains("pnp0a03") Then
							subregkey = regkey.OpenSubKey(child)
							If subregkey IsNot Nothing Then
								For Each child2 As String In subregkey.GetSubKeyNames()
									If Not IsNullOrWhitespace(child2) Then
										array = CType(subregkey.OpenSubKey(child2).GetValue("LowerFilters"), String())
										If (array IsNot Nothing) AndAlso Not (array.Length < 1) Then
											For i As Integer = 0 To array.Length - 1
												If Not IsNullOrWhitespace(array(i)) Then
													If array(i).ToLower.Contains("amdkmpfd") Then
														Application.Log.AddMessage("Found an AMDKMPFD! in " + child)
														Application.Log.AddMessage("We do not remove the AMDKMPFP service yet")
														iskmpfdpresent = True

													End If
												End If
											Next
										End If
									End If
								Next
							End If
						End If
					End If
				Next
			End If
		Catch ex As Exception
			Application.log.AddException(ex)
		End Try
		If iskmpfdpresent Then
			Return True
		Else
			Return False
		End If

	End Function

	Private Sub InitLanguage(ByVal firstLaunch As Boolean, Optional ByVal changeTo As Languages.LanguageOption = Nothing)
		'TODO: InitLanguage (just comment for quick find)

		If firstLaunch Then
			cbSelectedGPU.ItemsSource = [Enum].GetValues(GetType(GPUVendor))

			Dim defaultLang As New Languages.LanguageOption("en", "English", Application.Paths.Language & "English.xml")

			ExtractEnglishLangFile(defaultLang)
			Languages.Load() 'default = english

			'Dim systemLang As String = Globalization.CultureInfo.InstalledUICulture.TwoLetterISOLanguageName	'en, fr, sv etc.
			Dim systemlang = PreferredUILanguages()
			Dim lastUsedLang As Languages.LanguageOption = Nothing
			Dim nativeLang As Languages.LanguageOption = Nothing

			For Each item As Languages.LanguageOption In Application.Settings.LanguageOptions
				If lastUsedLang Is Nothing AndAlso item.Equals(Application.Settings.SelectedLanguage) Then
					lastUsedLang = item
				End If

				If nativeLang Is Nothing AndAlso systemLang.Equals(item.ISOLanguage, StringComparison.OrdinalIgnoreCase) Then
					nativeLang = item 'take native on hold incase last used language not found (avoid multiple loops)
				End If
			Next

			If lastUsedLang IsNot Nothing Then
				Application.Settings.SelectedLanguage = lastUsedLang
			Else
				If nativeLang IsNot Nothing Then
					Application.Settings.SelectedLanguage = nativeLang 'couldn't find last used, using native lang
				Else
					Application.Settings.SelectedLanguage = defaultLang	'couldn't find last used nor native lang, using default (English)
				End If
			End If

			Languages.TranslateForm(Me)

		Else
			If changeTo IsNot Nothing AndAlso Not changeTo.Equals(Languages.Current) Then
				Languages.Load(changeTo)
				Languages.TranslateForm(Me)

			End If
		End If
	End Sub

	Private Sub ExtractEnglishLangFile(ByVal langEng As Languages.LanguageOption)
		Using stream As Stream = Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(String.Format("{0}.{1}", GetType(Languages).Namespace, "English.xml"))
			If File.Exists(langEng.Filename) Then
				Using fsEnglish As FileStream = File.Open(langEng.Filename, FileMode.Open, FileAccess.Read, FileShare.None)
					If CompareStreams(stream, fsEnglish) Then
						Return
					End If
				End Using
			End If

			stream.Position = 0L
			Using sr As New StreamReader(stream, Encoding.UTF8, True)
				Using sw As New StreamWriter(langEng.Filename, False, Encoding.UTF8)
					While (sr.Peek() <> -1)
						sw.WriteLine(sr.ReadLine())
					End While

					sw.Flush()
					sw.Close()
				End Using

				sr.Close()
			End Using
		End Using
	End Sub

	Public Function UpdateTextMethodmessagefn(ByRef number As Integer) As String
		Return Languages.GetTranslation("frmMain", "UpdateLog", String.Format("Text{0}", number + 1))
	End Function

	Private Sub temporarynvidiaspeedup(ByVal config As ThreadSettings)	 'we do this to speedup the removal of the nividia display driver because of the huge time the nvidia installer files take to do unknown stuff.
		Dim filePath As String = Nothing

		Try
			filePath = Environment.GetFolderPath _
			(Environment.SpecialFolder.ProgramFiles) + "\NVIDIA Corporation"

			For Each child As String In Directory.GetDirectories(filePath)
				If IsNullOrWhitespace(child) = False Then
					If child.ToLower.Contains("installer2") Then
						For Each child2 As String In Directory.GetDirectories(child)
							If IsNullOrWhitespace(child2) = False Then
								If child2.ToLower.Contains("display.3dvision") Or
								   child2.ToLower.Contains("display.controlpanel") Or
								   child2.ToLower.Contains("display.driver") Or
								   child2.ToLower.Contains("display.gfexperience") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("display.nvirusb") Or
								   child2.ToLower.Contains("display.optimus") Or
								   child2.ToLower.Contains("display.physx") AndAlso config.RemovePhysX Or
								   child2.ToLower.Contains("display.update") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("display.nview") Or
								   child2.ToLower.Contains("display.nvwmi") Or
								   child2.ToLower.Contains("gfexperience") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("nvidia.update") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("installer2\installer") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("network.service") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("miracast.virtualaudio") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("shadowplay") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("update.core") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("virtualaudio.driver") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("coretemp") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("shield") AndAlso config.RemoveGFE Or
								   child2.ToLower.Contains("hdaudio.driver") Then
									Try
										deletedirectory(child2)
									Catch ex As Exception
									End Try
								End If
							End If
						Next

						If Directory.GetDirectories(child).Length = 0 Then
							Try
								deletedirectory(child)
							Catch ex As Exception
							End Try
						End If
					End If
				End If
			Next
		Catch ex As Exception
		End Try
	End Sub

	Public Sub UpdateTextMethod(ByVal strMessage As String)
		If Not tbLog.Dispatcher.CheckAccess() Then
			Dispatcher.Invoke(Sub() tbLog.Text = tbLog.Text & strMessage & vbNewLine)
			Dispatcher.Invoke(Sub() tbLog.Select(tbLog.Text.Length, 0))
			Dispatcher.Invoke(Sub() tbLog.ScrollToEnd())
		Else
			tbLog.Text = tbLog.Text & strMessage & vbNewLine
			tbLog.Select(tbLog.Text.Length, 0)
			tbLog.ScrollToEnd()
		End If

	End Sub

	Public Sub UpdateTextMethod2(ByVal strMessage As String)
		'Dim frmLog As New frmLog
		'
		'If Not frmLog.tbLog.Dispatcher.CheckAccess() Then
		'Dispatcher.Invoke(Sub() frmLog.tbLog.Text = frmLog.tbLog.Text + strMessage + vbNewLine)
		'Dispatcher.Invoke(Sub() frmLog.tbLog.Select(frmLog.tbLog.Text.Length, 0))
		'Dispatcher.Invoke(Sub() frmLog.tbLog.ScrollToEnd())
		'Else
		'frmLog.tbLog.Text = frmLog.tbLog.Text + strMessage + vbNewLine
		'frmLog.tbLog.Select(frmLog.tbLog.Text.Length, 0)
		'frmLog.tbLog.ScrollToEnd()
		'End If

	End Sub

	Public Function GetREG_BINARY(ByVal Path As String, ByVal Value As String) As String
		Dim Data() As Byte = CType(Microsoft.Win32.Registry.GetValue(Path, Value, Nothing), Byte())
		If Data Is Nothing Then Return "N/A"
		Dim Result As String = String.Empty
		For j As Integer = 0 To Data.Length - 1
			Result &= Hex(Data(j)).PadLeft(2, "0"c) & ""
		Next
		Return Result
	End Function

	Public Function HexToString(ByVal Data As String) As String
		Dim com As String = ""
		For x = 0 To Data.Length - 1 Step 2
			com &= ChrW(CInt("&H" & Data.Substring(x, 2)))
		Next
		Return com
	End Function

	Public Sub deletesubregkey(ByVal value1 As RegistryKey, ByVal value2 As String)

		CleanupEngine.deletesubregkey(value1, value2)

	End Sub

	Private Sub deletedirectory(ByVal directory As String)
		CleanupEngine.deletedirectory(directory)
	End Sub

	Private Sub deletefile(ByVal file As String)
		CleanupEngine.deletefile(file)
	End Sub

	Public Sub deletevalue(ByVal value1 As RegistryKey, ByVal value2 As String)

		CleanupEngine.deletevalue(value1, value2)

	End Sub

	Private Sub amdenvironementpath(ByVal filepath As String)

		Dim regkey As RegistryKey
		Dim subregkey As RegistryKey
		Dim wantedvalue As String = Nothing

		'--------------------------------
		'System environement path cleanup
		'--------------------------------

		Application.Log.AddMessage("System environement cleanUP")
		filepath = filepath.ToLower
		Try
			subregkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM", False)
			If subregkey IsNot Nothing Then
				For Each child2 As String In subregkey.GetSubKeyNames()
					If child2.ToLower.Contains("controlset") Then
						regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\" & child2 & "\Control\Session Manager\Environment", True)
						If regkey IsNot Nothing Then
							For Each child As String In regkey.GetValueNames()
								If IsNullOrWhitespace(child) = False Then
									If child.Contains("Path") Then
										If IsNullOrWhitespace(CStr(regkey.GetValue(child))) = False Then
											wantedvalue = regkey.GetValue(child).ToString.ToLower
											Try
												Select Case True
													Case wantedvalue.Contains(";" + filepath & "\amd app\bin\x86_64")
														wantedvalue = wantedvalue.Replace(";" + filepath & "\amd app\bin\x86_64", "")
														regkey.SetValue(child, wantedvalue)

													Case wantedvalue.Contains(filepath & "\amd app\bin\x86_64;")
														wantedvalue = wantedvalue.Replace(filepath & "\amd app\bin\x86_64;", "")
														regkey.SetValue(child, wantedvalue)

													Case wantedvalue.Contains(";" + filepath & "\amd app\bin\x86")
														wantedvalue = wantedvalue.Replace(";" + filepath & "\amd app\bin\x86", "")
														regkey.SetValue(child, wantedvalue)

													Case wantedvalue.Contains(filepath & "\amd app\bin\x86;")
														wantedvalue = wantedvalue.Replace(filepath & "\amd app\bin\x86;", "")
														regkey.SetValue(child, wantedvalue)

													Case wantedvalue.Contains(";" + filepath & "\ati.ace\core-static")
														wantedvalue = wantedvalue.Replace(";" + filepath & "\ati.ace\core-static", "")
														regkey.SetValue(child, wantedvalue)

													Case wantedvalue.Contains(filepath & "\ati.ace\core-static;")
														wantedvalue = wantedvalue.Replace(filepath & "\ati.ace\core-static;", "")
														regkey.SetValue(child, wantedvalue)

													Case wantedvalue.Contains(";" + filepath & "\ati.ace\core-static")
														wantedvalue = wantedvalue.Replace(";" + filepath & "\ati.ace\core-static", "")
														regkey.SetValue(child, wantedvalue)

													Case wantedvalue.Contains(filepath & "\ati.ace\core-static;")
														wantedvalue = wantedvalue.Replace(filepath & "\ati.ace\core-static;", "")
														regkey.SetValue(child, wantedvalue)

												End Select
											Catch ex As Exception
											End Try
										End If
									End If
								End If
							Next
						End If
					End If
				Next
			End If

		Catch ex As Exception
			Application.Log.AddException(ex)
		End Try

		'end system environement patch cleanup
	End Sub

    Private Sub frmMain_Sourceinitialized(sender As Object, e As EventArgs) Handles MyBase.SourceInitialized
        Me.WindowState = Windows.WindowState.Minimized
    End Sub

    Private Sub Button1_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles Button1.Click
        ' SetupAPI.GetDevices("Display")
        Dim setupAPIWindow As New SetupAPITestWindow

        setupAPIWindow.ShowDialog()
    End Sub
End Class

Public Class CleanupEngine

	Private Function UpdateTextMethodmessagefn(ByRef number As Integer) As String
		Return Languages.GetTranslation("frmMain", "UpdateLog", String.Format("Text{0}", number + 1))
	End Function

    Private Sub updatetextmethod(strmessage As String)
        'updatetextmethod(strmessage)
    End Sub


    Public Sub TestDelete(ByVal folder As String)
        'TestDelete(folder)
    End Sub

    Public Sub deletesubregkey(ByVal regkeypath As RegistryKey, ByVal child As String)

        If (regkeypath IsNot Nothing) AndAlso (Not IsNullOrWhitespace(child)) Then

            regkeypath.DeleteSubKeyTree(child)
            application.log.addmessage(regkeypath.ToString + "\" + child + " - " + UpdateTextMethodmessagefn(39))

        End If
    End Sub

	Public Sub deletedirectory(ByVal directorypath As String)
		If Not IsNullOrWhitespace(directorypath) Then
			My.Computer.FileSystem.DeleteDirectory _
			  (directorypath, FileIO.DeleteDirectoryOption.DeleteAllContents)
			Application.Log.AddMessage(directorypath + " - " + UpdateTextMethodmessagefn(39))
		End If

		If Not Directory.Exists(directorypath) Then
			If My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\Folders", False) IsNot Nothing Then
				For Each child As String In My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\Folders", False).GetValueNames
					If Not IsNullOrWhitespace(child) Then
						If child.ToLower.Contains(directorypath.ToLower + "\") Then
							Try
								deletevalue(My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\Folders", True), child)
							Catch ex As Exception
							End Try
						End If
					End If
				Next
			End If
			If My.Computer.Registry.LocalMachine.OpenSubKey("Software\Microsoft\Windows\CurrentVersion\SharedDLLs", False) IsNot Nothing Then
				For Each child As String In My.Computer.Registry.LocalMachine.OpenSubKey("Software\Microsoft\Windows\CurrentVersion\SharedDLLs", False).GetValueNames
					If Not IsNullOrWhitespace(child) Then
						If child.ToLower.Contains(directorypath.ToLower + "\") Then
							Try
								deletevalue(My.Computer.Registry.LocalMachine.OpenSubKey("Software\Microsoft\Windows\CurrentVersion\SharedDLLs", True), child)
							Catch ex As Exception
							End Try
						End If
					End If
				Next
			End If
			If IntPtr.Size = 8 Then
				If My.Computer.Registry.LocalMachine.OpenSubKey("Software\Wow6432Node\Microsoft\Windows\CurrentVersion\SharedDLLs", False) IsNot Nothing Then
					For Each child As String In My.Computer.Registry.LocalMachine.OpenSubKey("Software\Wow6432Node\Microsoft\Windows\CurrentVersion\SharedDLLs", False).GetValueNames
						If Not IsNullOrWhitespace(child) Then
							If child.ToLower.Contains(directorypath.ToLower + "\") Then
								Try
									deletevalue(My.Computer.Registry.LocalMachine.OpenSubKey("Software\Wow6432Node\Microsoft\Windows\CurrentVersion\SharedDLLs", True), child)
								Catch ex As Exception
								End Try
							End If
						End If
					Next
				End If
			End If
		End If
	End Sub

    Public Sub deletefile(ByVal filepath As String)


        If Not IsNullOrWhitespace(filepath) Then

            My.Computer.FileSystem.DeleteFile(filepath) 'filepath here include the file too.

            application.log.addmessage(filepath + " - " + UpdateTextMethodmessagefn(41))
        End If

    End Sub

    Public Sub deletevalue(ByVal regkeypath As RegistryKey, ByVal child As String)


        If (regkeypath IsNot Nothing) AndAlso (Not IsNullOrWhitespace(child)) Then

            regkeypath.DeleteValue(child)
            application.log.addmessage(regkeypath.ToString + "\" + child + " - " + UpdateTextMethodmessagefn(40))


        End If

    End Sub

    Public Sub classroot(ByVal classroot As String())

        Dim regkey As RegistryKey
        Dim subregkey As RegistryKey
        Dim wantedvalue As String = Nothing
        Dim appid As String = Nothing
        Dim typelib As String = Nothing

        application.log.addmessage("Begin classroot CleanUP")

        Try
            regkey = My.Computer.Registry.ClassesRoot
            If regkey IsNot Nothing Then
                For Each child As String In regkey.GetSubKeyNames()
                    If IsNullOrWhitespace(child) = False Then
                        For i As Integer = 0 To classroot.Length - 1
                            If Not IsNullOrWhitespace(classroot(i)) Then
                                If child.ToLower.StartsWith(classroot(i).ToLower) Then
                                    subregkey = My.Computer.Registry.ClassesRoot.OpenSubKey(child & "\CLSID")
                                    If subregkey IsNot Nothing Then
                                        If IsNullOrWhitespace(CStr(subregkey.GetValue(""))) = False Then
                                            wantedvalue = subregkey.GetValue("").ToString
                                            If IsNullOrWhitespace(wantedvalue) = False Then
                                                Try
                                                    Try
                                                        If Not IsNullOrWhitespace(CStr(My.Computer.Registry.ClassesRoot.OpenSubKey("CLSID\" & wantedvalue).GetValue("AppID"))) Then
                                                            appid = My.Computer.Registry.ClassesRoot.OpenSubKey("CLSID\" & wantedvalue).GetValue("AppID").ToString
                                                            Try

                                                                deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("AppID", True), appid)
                                                            Catch ex As Exception
                                                            End Try
                                                        End If
                                                    Catch ex As Exception
                                                    End Try

                                                    Try
                                                        If Not IsNullOrWhitespace(CStr(My.Computer.Registry.ClassesRoot.OpenSubKey("CLSID\" & wantedvalue & "\TypeLib").GetValue(""))) Then
                                                            typelib = My.Computer.Registry.ClassesRoot.OpenSubKey("CLSID\" & wantedvalue & "\TypeLib").GetValue("").ToString
                                                            Try
                                                                deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("TypeLib", True), typelib)
                                                            Catch ex As Exception
                                                            End Try
                                                        End If
                                                    Catch ex As Exception
                                                    End Try

                                                    deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("CLSID", True), wantedvalue)

                                                Catch ex As Exception
                                                End Try
                                            End If
                                        End If
                                    End If
                                    'here I remove the mediafoundationkeys if present
                                    'f79eac7d-e545-4387-bdee-d647d7bde42a is the Ecnoder section. Same on all windows version.
                                    Try
                                        deletesubregkey(regkey.OpenSubKey("MediaFoundation\Transforms", True), (child.Replace("{", "")).Replace("}", ""))
                                    Catch ex As Exception
                                    End Try
                                    Try
                                        deletesubregkey(regkey.OpenSubKey("MediaFoundation\Transforms\Categories\f79eac7d-e545-4387-bdee-d647d7bde42a", True), (child.Replace("{", "")).Replace("}", ""))
                                    Catch ex As Exception
                                    End Try
                                    deletesubregkey(regkey, child)
                                End If
                            End If
                        Next
                    End If
                Next
            End If
        Catch ex As Exception
            Application.Log.AddException(ex)
        End Try

        If IntPtr.Size = 8 Then
            Try
                regkey = My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node", True)
                If regkey IsNot Nothing Then
                    For Each child As String In regkey.GetSubKeyNames()
                        If IsNullOrWhitespace(child) = False Then
                            For i As Integer = 0 To classroot.Length - 1
                                If Not IsNullOrWhitespace(classroot(i)) Then
                                    If child.ToLower.StartsWith(classroot(i).ToLower) Then
                                        subregkey = My.Computer.Registry.ClassesRoot.OpenSubKey(child & "\CLSID")
                                        If subregkey IsNot Nothing Then
                                            If IsNullOrWhitespace(CStr(subregkey.GetValue(""))) = False Then
                                                wantedvalue = subregkey.GetValue("").ToString
                                                If IsNullOrWhitespace(wantedvalue) = False Then
                                                    Try
                                                        Try
                                                            If Not IsNullOrWhitespace(CStr(regkey.OpenSubKey("CLSID\" & wantedvalue).GetValue("AppID"))) Then
                                                                appid = regkey.OpenSubKey("CLSID\" & wantedvalue).GetValue("AppID").ToString
                                                                Try
                                                                    deletesubregkey(regkey.OpenSubKey("AppID", True), appid)
                                                                Catch ex As Exception
                                                                End Try
                                                            End If
                                                        Catch ex As Exception
                                                        End Try

                                                        Try
                                                            If Not IsNullOrWhitespace(CStr(regkey.OpenSubKey("CLSID\" & wantedvalue & "\TypeLib").GetValue(""))) Then
                                                                typelib = regkey.OpenSubKey("CLSID\" & wantedvalue & "\TypeLib").GetValue("").ToString
                                                                Try
                                                                    deletesubregkey(regkey.OpenSubKey("TypeLib", True), typelib)
                                                                Catch ex As Exception
                                                                End Try
                                                            End If
                                                        Catch ex As Exception
                                                        End Try

                                                        deletesubregkey(regkey.OpenSubKey("CLSID", True), wantedvalue)

                                                    Catch ex As Exception
                                                    End Try
                                                End If
                                            End If
                                        End If
                                        'here I remove the mediafoundationkeys if present
                                        'f79eac7d-e545-4387-bdee-d647d7bde42a is the Ecnoder section. Same on all windows version.
                                        Try
                                            deletesubregkey(regkey.OpenSubKey("MediaFoundation\Transforms", True), (child.Replace("{", "")).Replace("}", ""))
                                        Catch ex As Exception
                                        End Try
                                        Try
                                            deletesubregkey(regkey.OpenSubKey("MediaFoundation\Transforms\Categories\f79eac7d-e545-4387-bdee-d647d7bde42a", True), (child.Replace("{", "")).Replace("}", ""))
                                        Catch ex As Exception
                                        End Try
                                        deletesubregkey(regkey, child)
                                    End If
                                End If
                            Next
                        End If
                    Next
                End If
            Catch ex As Exception
                application.log.AddException(ex)
            End Try
        End If

        application.log.addmessage("End classroot CleanUP")
    End Sub

	Public Sub installer(ByVal packages As String(), config As ThreadSettings)
		Dim regkey As RegistryKey
		Dim basekey As RegistryKey
		Dim superregkey As RegistryKey
		Dim subregkey As RegistryKey
		Dim subsuperregkey As RegistryKey
		Dim wantedvalue As String = Nothing
		Dim removephysx As Boolean = config.RemovePhysX

		updatetextmethod(UpdateTextMethodmessagefn(29))

		Try
			Application.Log.AddMessage("-Starting S-1-5-xx region cleanUP")
			basekey = My.Computer.Registry.LocalMachine.OpenSubKey _
				  ("Software\Microsoft\Windows\CurrentVersion\Installer\UserData", False)
			If basekey IsNot Nothing Then
				For Each super As String In basekey.GetSubKeyNames()
					If IsNullOrWhitespace(super) = False Then
						If super.ToLower.Contains("s-1-5") Then

							regkey = My.Computer.Registry.LocalMachine.OpenSubKey _
								("Software\Microsoft\Windows\CurrentVersion\Installer\UserData\" & super & "\Products", True)

							If regkey IsNot Nothing Then
								For Each child As String In regkey.GetSubKeyNames()
									If IsNullOrWhitespace(child) = False Then

										subregkey = My.Computer.Registry.LocalMachine.OpenSubKey _
							("Software\Microsoft\Windows\CurrentVersion\Installer\UserData\" & super & "\Products\" & child & _
							"\InstallProperties", False)

										If subregkey IsNot Nothing Then
											If IsNullOrWhitespace(CStr(subregkey.GetValue("DisplayName"))) = False Then
												wantedvalue = subregkey.GetValue("DisplayName").ToString
												If IsNullOrWhitespace(wantedvalue) = False Then
													For i As Integer = 0 To packages.Length - 1
														If Not IsNullOrWhitespace(packages(i)) Then
															If wantedvalue.ToLower.Contains(packages(i).ToLower) AndAlso
															  Not ((removephysx = False) AndAlso wantedvalue.ToLower.Contains("physx")) Then



																'Deleting here the c:\windows\installer entries.
																Try
																	If (Not IsNullOrWhitespace(CStr(subregkey.GetValue("LocalPackage")))) AndAlso
																	  subregkey.GetValue("LocalPackage").ToString.ToLower.Contains(".msi") Then
																		deletefile(subregkey.GetValue("LocalPackage").ToString)
																	End If
																Catch ex As Exception
																End Try


																Try
																	If (Not IsNullOrWhitespace(CStr(subregkey.GetValue("UninstallString")))) AndAlso
																	  subregkey.GetValue("UninstallString").ToString.ToLower.Contains("{") Then
																		Dim folder As String = subregkey.GetValue("UninstallString").ToString
																		folder = folder.Substring(folder.IndexOf("{"), (folder.IndexOf("}") - folder.IndexOf("{")) + 1)
																		TestDelete(Environment.GetEnvironmentVariable("windir") + "\installer\" + folder)
																		If My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\Folders", False) IsNot Nothing Then
																			For Each subkeyname As String In My.Computer.Registry.LocalMachine.OpenSubKey _
					  ("Software\Microsoft\Windows\CurrentVersion\Installer\Folders").GetValueNames
																				If Not IsNullOrWhitespace(subkeyname) Then
																					If subkeyname.ToLower.Contains(folder.ToLower) Then
																						deletevalue(My.Computer.Registry.LocalMachine.OpenSubKey _
					  ("Software\Microsoft\Windows\CurrentVersion\Installer\Folders", True), subkeyname)
																					End If
																				End If
																			Next
																		End If
																	End If
																Catch ex As Exception
																	Application.Log.AddException(ex)
																End Try

																Try
																	deletesubregkey(regkey, child)
																Catch ex As Exception
																End Try

																superregkey = My.Computer.Registry.LocalMachine.OpenSubKey _
	("SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UpgradeCodes", True)
																If superregkey IsNot Nothing Then
																	For Each child2 As String In superregkey.GetSubKeyNames()
																		If IsNullOrWhitespace(child2) = False Then

																			subsuperregkey = My.Computer.Registry.LocalMachine.OpenSubKey _
("SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UpgradeCodes\" & child2, False)

																			If subsuperregkey IsNot Nothing Then
																				For Each wantedstring As String In subsuperregkey.GetValueNames()
																					If IsNullOrWhitespace(wantedstring) = False Then
																						If wantedstring.Contains(child) Then
																							Try
																								deletesubregkey(superregkey, child2)
																							Catch ex As Exception
																							End Try
																						End If
																					End If
																				Next
																			End If
																		End If
																	Next
																End If
																superregkey = My.Computer.Registry.LocalMachine.OpenSubKey _
	("SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\" & super & "\Components", True)
																If superregkey IsNot Nothing Then
																	For Each child2 As String In superregkey.GetSubKeyNames()
																		If IsNullOrWhitespace(child2) = False Then

																			subsuperregkey = My.Computer.Registry.LocalMachine.OpenSubKey _
("SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\" & super & "\Components\" & child2, False)


																			If subsuperregkey IsNot Nothing Then
																				For Each wantedstring In subsuperregkey.GetValueNames()
																					If IsNullOrWhitespace(wantedstring) = False Then
																						If wantedstring.Contains(child) Then
																							Try
																								deletesubregkey(superregkey, child2)
																							Catch ex As Exception
																							End Try
																						End If
																					End If
																				Next
																			End If
																		End If
																	Next
																End If
															End If
														End If
													Next
												End If
											End If
										End If
									End If
								Next
							End If
						End If
					End If
				Next
			End If
			updatetextmethod(UpdateTextMethodmessagefn(30))
			Application.Log.AddMessage("-End of S-1-5-xx region cleanUP")
		Catch ex As Exception
			MsgBox(Languages.GetTranslation("frmMain", "Messages", "Text6"))
			Application.Log.AddException(ex)
		End Try

		updatetextmethod(UpdateTextMethodmessagefn(31))
		Try
			regkey = My.Computer.Registry.ClassesRoot.OpenSubKey _
	  ("Installer\Products", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames()
					If IsNullOrWhitespace(child) = False Then

						subregkey = My.Computer.Registry.ClassesRoot.OpenSubKey _
("Installer\Products\" & child, False)

						If subregkey IsNot Nothing Then
							If IsNullOrWhitespace(CStr(subregkey.GetValue("ProductName"))) = False Then
								wantedvalue = subregkey.GetValue("ProductName").ToString
								If IsNullOrWhitespace(wantedvalue) = False Then
									For i As Integer = 0 To packages.Length - 1
										If Not IsNullOrWhitespace(packages(i)) Then
											If wantedvalue.ToLower.Contains(packages(i).ToLower) AndAlso
											   Not ((removephysx = False) AndAlso wantedvalue.ToLower.Contains("physx")) Then

												Try
													If (Not IsNullOrWhitespace(CStr(subregkey.GetValue("ProductIcon")))) AndAlso
													  subregkey.GetValue("ProductIcon").ToString.ToLower.Contains("{") Then
														Dim folder As String = subregkey.GetValue("ProductIcon").ToString
														folder = folder.Substring(folder.IndexOf("{"), (folder.IndexOf("}") - folder.IndexOf("{")) + 1)
														TestDelete(Environment.GetEnvironmentVariable("windir") + "\installer\" + folder)
														If My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\Folders", False) IsNot Nothing Then
															For Each subkeyname As String In My.Computer.Registry.LocalMachine.OpenSubKey _
	("Software\Microsoft\Windows\CurrentVersion\Installer\Folders").GetValueNames
																If Not IsNullOrWhitespace(subkeyname) Then
																	If subkeyname.ToLower.Contains(folder.ToLower) Then
																		deletevalue(My.Computer.Registry.LocalMachine.OpenSubKey _
	  ("Software\Microsoft\Windows\CurrentVersion\Installer\Folders", True), subkeyname)
																	End If
																End If
															Next
														End If
													End If
												Catch ex As Exception
													Application.Log.AddException(ex)
												End Try

												Try
													deletesubregkey(regkey, child)
												Catch ex As Exception
												End Try
												Try
													deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("Installer\Features", True), child)
												Catch ex As Exception
												End Try
												superregkey = My.Computer.Registry.ClassesRoot.OpenSubKey _
					 ("Installer\UpgradeCodes", True)
												If superregkey IsNot Nothing Then
													For Each child2 As String In superregkey.GetSubKeyNames()
														If IsNullOrWhitespace(child2) = False Then

															subsuperregkey = My.Computer.Registry.ClassesRoot.OpenSubKey _
														("Installer\UpgradeCodes\" & child2, False)

															If subsuperregkey IsNot Nothing Then
																For Each wantedstring As String In subsuperregkey.GetValueNames()
																	If IsNullOrWhitespace(wantedstring) = False Then
																		If wantedstring.Contains(child) Then
																			Try
																				deletesubregkey(superregkey, child2)
																			Catch ex As Exception
																			End Try
																		End If
																	End If
																Next
															End If
														End If
													Next
												End If
											End If
										End If
									Next
								End If
							End If
						End If
					End If
				Next
			End If
			updatetextmethod(UpdateTextMethodmessagefn(32))
		Catch ex As Exception
			MsgBox(Languages.GetTranslation("frmMain", "Messages", "Text6"))
			Application.Log.AddException(ex)
		End Try


		updatetextmethod(UpdateTextMethodmessagefn(33))

		Try
			regkey = My.Computer.Registry.LocalMachine.OpenSubKey _
	  ("Software\Classes\Installer\Products", True)
			If regkey IsNot Nothing Then
				For Each child As String In regkey.GetSubKeyNames()
					If IsNullOrWhitespace(child) = False Then

						subregkey = My.Computer.Registry.LocalMachine.OpenSubKey _
("Software\Classes\Installer\Products\" & child, False)

						If subregkey IsNot Nothing Then
							If IsNullOrWhitespace(CStr(subregkey.GetValue("ProductName"))) = False Then
								wantedvalue = subregkey.GetValue("ProductName").ToString
								If IsNullOrWhitespace(wantedvalue) = False Then
									For i As Integer = 0 To packages.Length - 1
										If Not IsNullOrWhitespace(packages(i)) Then
											If wantedvalue.ToLower.Contains(packages(i).ToLower) AndAlso
											  Not ((removephysx = False) AndAlso wantedvalue.ToLower.Contains("physx")) Then
												Try
													deletesubregkey(regkey, child)
												Catch ex As Exception
												End Try
												Try
													deletesubregkey(My.Computer.Registry.LocalMachine.OpenSubKey("Software\Classes\Installer\Features", True), child)
												Catch ex As Exception
												End Try

												superregkey = My.Computer.Registry.LocalMachine.OpenSubKey _
					 ("Software\Classes\Installer\UpgradeCodes", True)
												If superregkey IsNot Nothing Then
													For Each child2 As String In superregkey.GetSubKeyNames()
														If IsNullOrWhitespace(child2) = False Then

															subsuperregkey = My.Computer.Registry.LocalMachine.OpenSubKey _
														("Software\Classes\Installer\UpgradeCodes\" & child2, False)

															If subsuperregkey IsNot Nothing Then
																For Each wantedstring As String In subsuperregkey.GetValueNames()
																	If IsNullOrWhitespace(wantedstring) = False Then
																		If wantedstring.Contains(child) Then
																			Try
																				deletesubregkey(superregkey, child2)
																			Catch ex As Exception
																			End Try
																		End If
																	End If
																Next
															End If
														End If
													Next
												End If
											End If
										End If
									Next
								End If
							End If
						End If
					End If
				Next
			End If
			updatetextmethod(UpdateTextMethodmessagefn(34))
		Catch ex As Exception
			MsgBox(Languages.GetTranslation("frmMain", "Messages", "Text6"))
			Application.Log.AddException(ex)
		End Try

		updatetextmethod(UpdateTextMethodmessagefn(35))
		Try
			For Each users As String In My.Computer.Registry.Users.GetSubKeyNames()
				If Not IsNullOrWhitespace(users) Then

					regkey = My.Computer.Registry.Users.OpenSubKey _
			  (users & "\Software\Microsoft\Installer\Products", True)

					If regkey IsNot Nothing Then
						For Each child As String In regkey.GetSubKeyNames()
							If IsNullOrWhitespace(child) = False Then

								subregkey = My.Computer.Registry.Users.OpenSubKey _
	(users & "\Software\Microsoft\Installer\Products\" & child, False)

								If subregkey IsNot Nothing Then
									If IsNullOrWhitespace(CStr(subregkey.GetValue("ProductName"))) = False Then
										wantedvalue = subregkey.GetValue("ProductName").ToString
										If IsNullOrWhitespace(wantedvalue) = False Then
											For i As Integer = 0 To packages.Length - 1
												If Not IsNullOrWhitespace(packages(i)) Then
													If wantedvalue.ToLower.Contains(packages(i).ToLower) AndAlso
													   Not ((removephysx = False) AndAlso wantedvalue.ToLower.Contains("physx")) Then
														Try
															deletesubregkey(regkey, child)
														Catch ex As Exception
														End Try
														Try
															deletesubregkey(My.Computer.Registry.Users.OpenSubKey(users & "\Software\Microsoft\Installer\Features", True), child)
														Catch ex As Exception
														End Try

														superregkey = My.Computer.Registry.Users.OpenSubKey _
							 (users & "\Software\Microsoft\Installer\UpgradeCodes", True)
														If superregkey IsNot Nothing Then
															For Each child2 As String In superregkey.GetSubKeyNames()
																If IsNullOrWhitespace(child2) = False Then

																	subsuperregkey = My.Computer.Registry.Users.OpenSubKey _
																(users & "\Software\Microsoft\Installer\UpgradeCodes" & child2, False)

																	If subsuperregkey IsNot Nothing Then
																		For Each wantedstring As String In subsuperregkey.GetValueNames()
																			If IsNullOrWhitespace(wantedstring) = False Then
																				If wantedstring.Contains(child) Then
																					Try
																						deletesubregkey(superregkey, child2)
																					Catch ex As Exception
																					End Try
																				End If
																			End If
																		Next
																	End If
																End If
															Next
														End If
													End If
												End If
											Next
										End If
									End If
								End If
							End If
						Next
					End If
				End If
			Next
			updatetextmethod(UpdateTextMethodmessagefn(36))
		Catch ex As Exception
			MsgBox(Languages.GetTranslation("frmMain", "Messages", "Text6"))
			Application.Log.AddException(ex)
		End Try

	End Sub

    Public Sub cleanserviceprocess(ByVal services As String())

        Dim donotremoveamdhdaudiobusfiles = frmMain.donotremoveamdhdaudiobusfiles
        Dim regkey As RegistryKey
        Dim subregkey As RegistryKey

        updatetextmethod(UpdateTextMethodmessagefn(37))
        application.log.addmessage("Cleaning Process/Services...")


        regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Services", False)
        If regkey IsNot Nothing Then
            For i As Integer = 0 To services.Length - 1
                If Not IsNullOrWhitespace(services(i)) Then
                    If regkey.OpenSubKey(services(i), False) IsNot Nothing Then

                        If Not (donotremoveamdhdaudiobusfiles AndAlso services(i).ToLower.Contains("amdkmafd")) Then

                            Dim stopservice As New ProcessStartInfo
                            stopservice.FileName = "cmd.exe"
                            stopservice.Arguments = " /Cnet stop " & Chr(34) & services(i) & Chr(34)
                            stopservice.UseShellExecute = False
                            stopservice.CreateNoWindow = True
                            stopservice.RedirectStandardOutput = False


                            Dim processstopservice As New Process
                            processstopservice.StartInfo = stopservice
                            updatetextmethod("Stopping service : " & services(i))
                            application.log.addmessage("Stopping service : " & services(i))
                            processstopservice.Start()
                            processstopservice.WaitForExit()
                            processstopservice.Close()

                            stopservice.Arguments = " /Csc delete " & Chr(34) & services(i) & Chr(34)

                            processstopservice.StartInfo = stopservice
                            updatetextmethod("Trying to Deleting service : " & services(i))
                            application.log.addmessage("Trying to Deleting service : " & services(i))
                            processstopservice.Start()
                            processstopservice.WaitForExit()
                            processstopservice.Close()

                            stopservice.Arguments = " /Csc interrogate " & Chr(34) & services(i) & Chr(34)
                            processstopservice.StartInfo = stopservice
                            processstopservice.Start()
                            processstopservice.WaitForExit()
                            processstopservice.Close()

                            'Verify that the service was indeed removed.
                            If regkey.OpenSubKey(services(i), False) IsNot Nothing Then
                                updatetextmethod("Failed to remove the service.")
                                application.log.addmessage("Failed to remove the service.")
                            Else
                                updatetextmethod("Service removed.")
                                application.log.addmessage("Service removed.")
                            End If

                        End If
                    End If
                End If

                System.Threading.Thread.Sleep(10)
            Next
        End If
        updatetextmethod(UpdateTextMethodmessagefn(38))
        application.log.addmessage("Process/Services CleanUP Complete")

        '-------------
        'control/video
        '-------------
        'Reason I put this in service is that the removal of this is based from its service.
        application.log.addmessage("Control/Video CleanUP")
        Try
            regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SYSTEM\CurrentControlSet\Control\Video", True)
            If regkey IsNot Nothing Then
                For Each child As String In regkey.GetSubKeyNames
                    If IsNullOrWhitespace(child) = False Then
                        subregkey = regkey.OpenSubKey(child & "\Video", False)
                        If subregkey IsNot Nothing Then
                            For i As Integer = 0 To services.Length - 1
                                If IsNullOrWhitespace(CStr(subregkey.GetValue("Service"))) = False Then
                                    If subregkey.GetValue("Service").ToString.ToLower = services(i).ToLower Then
                                        Try
                                            deletesubregkey(regkey, child)
                                            deletesubregkey(My.Computer.Registry.LocalMachine, "SYSTEM\CurrentControlSet\Hardware Profiles\UnitedVideo\CONTROL\VIDEO\" & child)
                                            Exit For
                                        Catch ex As Exception
                                        End Try
                                    End If
                                End If
                            Next
                        Else
                            'Here, if subregkey is nothing, it mean \video doesnt exist and is no \0000, we can delete it.
                            'this is a general cleanUP we could say.
                            If regkey.OpenSubKey(child + "\0000") Is Nothing Then
                                Try
                                    deletesubregkey(regkey, child)
                                    deletesubregkey(My.Computer.Registry.LocalMachine, "SYSTEM\CurrentControlSet\Hardware Profiles\UnitedVideo\CONTROL\VIDEO\" & child)
                                Catch ex As Exception
                                End Try
                            End If
                        End If
                    End If
                Next
            End If
        Catch ex As Exception
            application.log.AddException(ex)
        End Try
    End Sub

    Public Sub prePnplockdownfiles(ByVal oeminf As String)

        Dim regkey As RegistryKey
        Dim win8higher = frmMain.win8higher
        Dim processinfo As New ProcessStartInfo
        Dim process As New Process
        Dim donotremoveamdhdaudiobusfiles = frmMain.donotremoveamdhdaudiobusfiles

        Try
            If CBool(win8higher) Then
                regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpLockdownFiles", True)
                If regkey IsNot Nothing Then
                    If Not IsNullOrWhitespace(oeminf) Then
                        If Not (donotremoveamdhdaudiobusfiles AndAlso oeminf.ToLower.Contains("amdkmafd.sys")) Then
                            For Each child As String In regkey.GetSubKeyNames()
                                If IsNullOrWhitespace(child) = False Then
                                    If (Not IsNullOrWhitespace(CStr(regkey.OpenSubKey(child).GetValue("Source")))) AndAlso regkey.OpenSubKey(child).GetValue("Source").ToString.ToLower.Contains(oeminf.ToLower) Then
                                        Try
                                            deletesubregkey(regkey, child)
                                        Catch ex As Exception
                                            Application.Log.AddException(ex)
                                        End Try
                                    End If
                                End If
                            Next
                        End If
                    End If
                End If
            End If
        Catch ex As Exception
            application.log.AddException(ex)
        End Try

    End Sub

    Public Sub Pnplockdownfiles(ByVal driverfiles As String())
        Dim regkey As RegistryKey
        Dim winxp = frmMain.winxp
        Dim win8higher = frmMain.win8higher
        Dim processinfo As New ProcessStartInfo
        Dim process As New Process
        Dim donotremoveamdhdaudiobusfiles = frmMain.donotremoveamdhdaudiobusfiles

        Try
            If Not winxp Then  'this does not exist on winxp so we skip if winxp detected
                If win8higher Then
                    regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpLockdownFiles", True)
                    If regkey IsNot Nothing Then
                        For i As Integer = 0 To driverfiles.Length - 1
                            If Not IsNullOrWhitespace(driverfiles(i)) Then
                                If Not (donotremoveamdhdaudiobusfiles AndAlso driverfiles(i).ToLower.Contains("amdkmafd.sys")) Then
                                    For Each child As String In regkey.GetSubKeyNames()
                                        If IsNullOrWhitespace(child) = False Then
                                            If child.ToLower.Replace("/", "\").Contains("\" + driverfiles(i).ToLower) Then
                                                Try
                                                    deletesubregkey(regkey, child)
                                                Catch ex As Exception
                                                    Application.Log.AddException(ex)
                                                End Try
                                            End If
                                        End If
                                    Next
                                End If
                            End If
                        Next
                    End If

                Else   'Older windows  (windows vista and 7 run here)

                    regkey = My.Computer.Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows\CurrentVersion\Setup\PnpLockdownFiles", True)
                    If regkey IsNot Nothing Then
                        For i As Integer = 0 To driverfiles.Length - 1
                            If Not IsNullOrWhitespace(driverfiles(i)) Then
                                If Not (donotremoveamdhdaudiobusfiles AndAlso driverfiles(i).ToLower.Contains("amdkmafd")) Then
                                    For Each child As String In regkey.GetValueNames()
                                        If IsNullOrWhitespace(child) = False Then
                                            If child.ToLower.Contains(driverfiles(i).ToLower) Then
                                                Try
                                                    deletevalue(regkey, child)
                                                Catch ex As Exception
                                                    Application.Log.AddException(ex)
                                                End Try
                                            End If
                                        End If
                                    Next
                                End If
                            End If
                        Next
                    End If
                End If
            End If

        Catch ex As Exception
            Application.Log.AddException(ex)
        End Try

    End Sub

    Public Sub clsidleftover(ByVal clsidleftover As String())

        Dim regkey As RegistryKey
        Dim subregkey As RegistryKey
        Dim wantedvalue As String
        Dim appid As String = Nothing
        Dim typelib As String = Nothing

        application.log.addmessage("Begin clsidleftover CleanUP")

        Try
            regkey = My.Computer.Registry.ClassesRoot.OpenSubKey("CLSID", True)
            If regkey IsNot Nothing Then
                For Each child As String In regkey.GetSubKeyNames()
                    If IsNullOrWhitespace(child) = False Then
                        subregkey = My.Computer.Registry.ClassesRoot.OpenSubKey("CLSID\" & child & "\InProcServer32", False)
                        If subregkey IsNot Nothing Then
                            If IsNullOrWhitespace(CStr(subregkey.GetValue(""))) = False Then
                                wantedvalue = subregkey.GetValue("").ToString
                                If IsNullOrWhitespace(wantedvalue) = False Then
                                    For i As Integer = 0 To clsidleftover.Length - 1
                                        If Not IsNullOrWhitespace(clsidleftover(i)) Then
                                            If wantedvalue.ToLower.Contains(clsidleftover(i).ToLower) Then

                                                Try
                                                    If Not IsNullOrWhitespace(CStr(regkey.OpenSubKey(child).GetValue("AppID"))) Then
                                                        appid = regkey.OpenSubKey(child).GetValue("AppID").ToString
                                                        Try
                                                            deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("AppID", True), appid)
                                                        Catch ex As Exception
                                                        End Try
                                                    End If
                                                Catch ex As Exception
                                                End Try

                                                Try
                                                    If Not IsNullOrWhitespace(CStr(regkey.OpenSubKey(child & "\TypeLib").GetValue(""))) Then
                                                        typelib = regkey.OpenSubKey(child & "\TypeLib").GetValue("").ToString
                                                        Try
                                                            deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("TypeLib", True), typelib)
                                                        Catch ex As Exception
                                                        End Try
                                                    End If
                                                Catch ex As Exception
                                                End Try

                                                Try
                                                    'here I remove the mediafoundationkeys if present
                                                    'f79eac7d-e545-4387-bdee-d647d7bde42a is the Ecnoder section. Same on all windows version.
                                                    Try
                                                        deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("MediaFoundation\Transforms", True), (child.Replace("{", "")).Replace("}", ""))
                                                    Catch ex As Exception
                                                    End Try
                                                    Try
                                                        deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("MediaFoundation\Transforms\Categories\f79eac7d-e545-4387-bdee-d647d7bde42a", True), (child.Replace("{", "")).Replace("}", ""))
                                                    Catch ex As Exception
                                                    End Try
                                                    deletesubregkey(regkey, child)
                                                    Exit For
                                                Catch ex As Exception
                                                    application.log.AddException(ex)
                                                End Try
                                            End If
                                        End If
                                    Next
                                End If
                            End If
                        End If
                    End If
                Next
            End If
        Catch ex As Exception
            application.log.AddException(ex)
        End Try

        Try
            regkey = My.Computer.Registry.ClassesRoot.OpenSubKey("CLSID", True)
            If regkey IsNot Nothing Then
                For Each child As String In regkey.GetSubKeyNames()
                    If IsNullOrWhitespace(child) = False Then
                        subregkey = My.Computer.Registry.ClassesRoot.OpenSubKey("CLSID\" & child, False)
                        If subregkey IsNot Nothing Then
                            If IsNullOrWhitespace(CStr(subregkey.GetValue(""))) = False Then
                                wantedvalue = subregkey.GetValue("").ToString
                                If IsNullOrWhitespace(wantedvalue) = False Then
                                    For i As Integer = 0 To clsidleftover.Length - 1
                                        If Not IsNullOrWhitespace(clsidleftover(i)) Then
                                            If wantedvalue.ToLower.Contains(clsidleftover(i).ToLower) Then

                                                Try
                                                    If Not IsNullOrWhitespace(CStr(regkey.OpenSubKey(child).GetValue("AppID"))) Then
                                                        appid = regkey.OpenSubKey(child).GetValue("AppID").ToString
                                                        Try
                                                            deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("AppID", True), appid)
                                                        Catch ex As Exception
                                                        End Try
                                                    End If
                                                Catch ex As Exception
                                                End Try

                                                Try
                                                    If Not IsNullOrWhitespace(CStr(regkey.OpenSubKey(child & "\TypeLib").GetValue(""))) Then
                                                        typelib = regkey.OpenSubKey(child & "\TypeLib").GetValue("").ToString
                                                        Try
                                                            deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("TypeLib", True), typelib)
                                                        Catch ex As Exception
                                                        End Try
                                                    End If
                                                Catch ex As Exception
                                                End Try
                                                Try
                                                    'here I remove the mediafoundationkeys if present
                                                    'f79eac7d-e545-4387-bdee-d647d7bde42a is the Ecnoder section. Same on all windows version.
                                                    Try
                                                        deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("MediaFoundation\Transforms", True), (child.Replace("{", "")).Replace("}", ""))
                                                    Catch ex As Exception
                                                    End Try
                                                    Try
                                                        deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("MediaFoundation\Transforms\Categories\f79eac7d-e545-4387-bdee-d647d7bde42a", True), (child.Replace("{", "")).Replace("}", ""))
                                                    Catch ex As Exception
                                                    End Try
                                                    deletesubregkey(regkey, child)
                                                    Exit For
                                                Catch ex As Exception
                                                    application.log.AddException(ex)
                                                End Try
                                            End If
                                        End If
                                    Next
                                End If
                            End If
                        End If
                    End If
                Next
            End If
        Catch ex As Exception
            application.log.AddException(ex)
        End Try


        If IntPtr.Size = 8 Then
            Try
                regkey = My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\CLSID", True)
                If regkey IsNot Nothing Then
                    For Each child As String In regkey.GetSubKeyNames()
                        If IsNullOrWhitespace(child) = False Then
                            subregkey = My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\CLSID\" & child & "\InProcServer32", False)

                            If subregkey IsNot Nothing Then
                                If IsNullOrWhitespace(CStr(subregkey.GetValue(""))) = False Then
                                    wantedvalue = subregkey.GetValue("").ToString
                                    If IsNullOrWhitespace(wantedvalue) = False Then
                                        For i As Integer = 0 To clsidleftover.Length - 1
                                            If Not IsNullOrWhitespace(clsidleftover(i)) Then
                                                If wantedvalue.ToLower.Contains(clsidleftover(i).ToLower) Then

                                                    Try
                                                        If Not IsNullOrWhitespace(CStr(regkey.OpenSubKey(child).GetValue("AppID"))) Then
                                                            appid = regkey.OpenSubKey(child).GetValue("AppID").ToString
                                                            Try
                                                                deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\AppID", True), appid)
                                                            Catch ex As Exception
                                                            End Try
                                                        End If
                                                    Catch ex As Exception
                                                    End Try

                                                    Try
                                                        If Not IsNullOrWhitespace(CStr(regkey.OpenSubKey(child & "\TypeLib").GetValue(""))) Then
                                                            typelib = regkey.OpenSubKey(child & "\TypeLib").GetValue("").ToString
                                                            Try
                                                                deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\TypeLib", True), typelib)
                                                            Catch ex As Exception
                                                            End Try
                                                        End If
                                                    Catch ex As Exception
                                                    End Try

                                                    Try
                                                        'here I remove the mediafoundationkeys if present
                                                        'f79eac7d-e545-4387-bdee-d647d7bde42a is the Ecnoder section. Same on all windows version.
                                                        Try
                                                            deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\MediaFoundation\Transforms", True), (child.Replace("{", "")).Replace("}", ""))

                                                        Catch ex As Exception
                                                        End Try
                                                        Try
                                                            deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\MediaFoundation\Transforms\Categories\f79eac7d-e545-4387-bdee-d647d7bde42a", True), (child.Replace("{", "")).Replace("}", ""))
                                                        Catch ex As Exception
                                                        End Try
                                                        deletesubregkey(regkey, child)
                                                        Exit For
                                                    Catch ex As Exception
                                                        application.log.AddException(ex)
                                                    End Try
                                                End If
                                            End If
                                        Next
                                    End If
                                End If
                            End If
                        End If
                    Next
                End If
            Catch ex As Exception
                application.log.AddException(ex)
            End Try

            Try
                regkey = My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\CLSID", True)
                If regkey IsNot Nothing Then
                    For Each child As String In regkey.GetSubKeyNames()
                        If IsNullOrWhitespace(child) = False Then
                            subregkey = My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\CLSID\" & child, False)
                            If subregkey IsNot Nothing Then
                                If IsNullOrWhitespace(CStr(subregkey.GetValue(""))) = False Then
                                    wantedvalue = subregkey.GetValue("").ToString
                                    If IsNullOrWhitespace(wantedvalue) = False Then
                                        For i As Integer = 0 To clsidleftover.Length - 1
                                            If Not IsNullOrWhitespace(clsidleftover(i)) Then
                                                If wantedvalue.ToLower.Contains(clsidleftover(i).ToLower) Then

                                                    Try
                                                        If Not IsNullOrWhitespace(CStr(regkey.OpenSubKey(child).GetValue("AppID"))) Then
                                                            appid = regkey.OpenSubKey(child).GetValue("AppID").ToString
                                                            Try
                                                                deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\AppID", True), appid)
                                                            Catch ex As Exception
                                                            End Try
                                                        End If
                                                    Catch ex As Exception
                                                    End Try

                                                    Try
                                                        If Not IsNullOrWhitespace(CStr(regkey.OpenSubKey(child & "\TypeLib").GetValue(""))) Then
                                                            typelib = regkey.OpenSubKey(child & "\TypeLib").GetValue("").ToString
                                                            Try
                                                                deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\TypeLib", True), typelib)
                                                            Catch ex As Exception
                                                            End Try
                                                        End If
                                                    Catch ex As Exception
                                                    End Try
                                                    Try
                                                        'here I remove the mediafoundationkeys if present
                                                        'f79eac7d-e545-4387-bdee-d647d7bde42a is the Ecnoder section. Same on all windows version.
                                                        Try
                                                            deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\MediaFoundation\Transforms", True), (child.Replace("{", "")).Replace("}", ""))
                                                        Catch ex As Exception
                                                        End Try
                                                        Try
                                                            deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\MediaFoundation\Transforms\Categories\f79eac7d-e545-4387-bdee-d647d7bde42a", True), (child.Replace("{", "")).Replace("}", ""))
                                                        Catch ex As Exception
                                                        End Try
                                                        deletesubregkey(regkey, child)
                                                        Exit For
                                                    Catch ex As Exception
                                                        application.log.AddException(ex)
                                                    End Try
                                                End If
                                            End If
                                        Next
                                    End If
                                End If
                            End If
                        End If
                    Next
                End If
            Catch ex As Exception
                application.log.AddException(ex)
            End Try
        End If

        Try
            regkey = My.Computer.Registry.ClassesRoot.OpenSubKey("CLSID", True)
            If regkey IsNot Nothing Then
                For Each child As String In regkey.GetSubKeyNames()
                    If IsNullOrWhitespace(child) = False Then
                        subregkey = My.Computer.Registry.ClassesRoot.OpenSubKey("CLSID\" & child & "\LocalServer32", False)
                        If subregkey IsNot Nothing Then
                            If IsNullOrWhitespace(CStr(subregkey.GetValue(""))) = False Then
                                wantedvalue = subregkey.GetValue("").ToString
                                If IsNullOrWhitespace(wantedvalue) = False Then
                                    For i As Integer = 0 To clsidleftover.Length - 1
                                        If Not IsNullOrWhitespace(clsidleftover(i)) Then
                                            If wantedvalue.ToLower.Contains(clsidleftover(i).ToLower) Then

                                                Try
                                                    If Not IsNullOrWhitespace(CStr(regkey.OpenSubKey(child).GetValue("AppID"))) Then
                                                        appid = regkey.OpenSubKey(child).GetValue("AppID").ToString
                                                        Try
                                                            deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("AppID", True), appid)
                                                        Catch ex As Exception
                                                        End Try
                                                    End If
                                                Catch ex As Exception
                                                End Try

                                                Try
                                                    If Not IsNullOrWhitespace(CStr(regkey.OpenSubKey(child & "\TypeLib").GetValue(""))) Then
                                                        typelib = regkey.OpenSubKey(child & "\TypeLib").GetValue("").ToString
                                                        Try
                                                            deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("TypeLib", True), typelib)
                                                        Catch ex As Exception
                                                        End Try
                                                    End If
                                                Catch ex As Exception
                                                End Try
                                                Try
                                                    'here I remove the mediafoundationkeys if present
                                                    'f79eac7d-e545-4387-bdee-d647d7bde42a is the Ecnoder section. Same on all windows version.
                                                    Try
                                                        deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("MediaFoundation\Transforms", True), (child.Replace("{", "")).Replace("}", ""))
                                                    Catch ex As Exception
                                                    End Try
                                                    Try
                                                        deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("MediaFoundation\Transforms\Categories\f79eac7d-e545-4387-bdee-d647d7bde42a", True), (child.Replace("{", "")).Replace("}", ""))
                                                    Catch ex As Exception
                                                    End Try
                                                    deletesubregkey(regkey, child)
                                                    Exit For
                                                Catch ex As Exception
                                                    application.log.AddException(ex)
                                                End Try
                                            End If
                                        End If
                                    Next
                                End If
                            End If
                        End If
                    End If
                Next
            End If
        Catch ex As Exception
            application.log.AddException(ex)
        End Try

        If IntPtr.Size = 8 Then
            Try
                regkey = My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\CLSID", True)
                If regkey IsNot Nothing Then
                    For Each child As String In regkey.GetSubKeyNames()
                        If IsNullOrWhitespace(child) = False Then
                            subregkey = My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\CLSID\" & child & "\LocalServer32", False)
                            If subregkey IsNot Nothing Then
                                If IsNullOrWhitespace(CStr(subregkey.GetValue(""))) = False Then
                                    wantedvalue = subregkey.GetValue("").ToString
                                    If IsNullOrWhitespace(wantedvalue) = False Then
                                        For i As Integer = 0 To clsidleftover.Length - 1
                                            If Not IsNullOrWhitespace(clsidleftover(i)) Then
                                                If wantedvalue.ToLower.Contains(clsidleftover(i).ToLower) Then


                                                    Try
                                                        If Not IsNullOrWhitespace(CStr(regkey.OpenSubKey(child).GetValue("AppID"))) Then
                                                            appid = regkey.OpenSubKey(child).GetValue("AppID").ToString
                                                            Try
                                                                deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\AppID", True), appid)
                                                            Catch ex As Exception
                                                            End Try
                                                        End If
                                                    Catch ex As Exception
                                                    End Try

                                                    Try
                                                        If Not IsNullOrWhitespace(CStr(regkey.OpenSubKey(child & "\TypeLib").GetValue(""))) Then
                                                            typelib = regkey.OpenSubKey(child & "\TypeLib").GetValue("").ToString
                                                            Try
                                                                deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\TypeLib", True), typelib)
                                                            Catch ex As Exception
                                                            End Try
                                                        End If
                                                    Catch ex As Exception
                                                    End Try
                                                    Try
                                                        'here I remove the mediafoundationkeys if present
                                                        'f79eac7d-e545-4387-bdee-d647d7bde42a is the Ecnoder section. Same on all windows version.
                                                        Try
                                                            deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\MediaFoundation\Transforms", True), (child.Replace("{", "")).Replace("}", ""))
                                                        Catch ex As Exception
                                                        End Try
                                                        Try
                                                            deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\MediaFoundation\Transforms\Categories\f79eac7d-e545-4387-bdee-d647d7bde42a", True), (child.Replace("{", "")).Replace("}", ""))
                                                        Catch ex As Exception
                                                        End Try
                                                        deletesubregkey(regkey, child)
                                                        Exit For
                                                    Catch ex As Exception
                                                        application.log.AddException(ex)
                                                    End Try
                                                End If
                                            End If
                                        Next
                                    End If
                                End If
                            End If
                        End If
                    Next
                End If
            Catch ex As Exception
                application.log.AddException(ex)
            End Try
        End If

        Try
            regkey = My.Computer.Registry.ClassesRoot.OpenSubKey("AppID", True)
            If regkey IsNot Nothing Then
                For Each child As String In regkey.GetSubKeyNames()
                    If IsNullOrWhitespace(child) = False Then
                        For i As Integer = 0 To clsidleftover.Length - 1
                            If Not IsNullOrWhitespace(clsidleftover(i)) Then
                                If child.ToLower.Contains(clsidleftover(i).ToLower) Then
                                    subregkey = regkey.OpenSubKey(child)
                                    If subregkey IsNot Nothing Then
                                        If IsNullOrWhitespace(CStr(subregkey.GetValue("AppID"))) = False Then
                                            wantedvalue = subregkey.GetValue("AppID").ToString
                                            If IsNullOrWhitespace(wantedvalue) = False Then

                                                Try
                                                    deletesubregkey(regkey, wantedvalue)
                                                Catch ex As Exception
                                                End Try

                                                Try
                                                    deletesubregkey(regkey, child)
                                                    Exit For
                                                Catch ex As Exception
                                                End Try
                                            End If
                                        End If
                                    End If
                                End If
                            End If
                        Next
                    End If
                Next
            End If
        Catch ex As Exception
            application.log.AddException(ex)
        End Try

        If IntPtr.Size = 8 Then
            Try
                regkey = My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\AppID", True)
                If regkey IsNot Nothing Then
                    For Each child As String In regkey.GetSubKeyNames()
                        If IsNullOrWhitespace(child) = False Then
                            For i As Integer = 0 To clsidleftover.Length - 1
                                If Not IsNullOrWhitespace(clsidleftover(i)) Then
                                    If child.ToLower.Contains(clsidleftover(i).ToLower) Then
                                        subregkey = regkey.OpenSubKey(child)
                                        If subregkey IsNot Nothing Then
                                            If IsNullOrWhitespace(CStr(subregkey.GetValue("AppID"))) = False Then
                                                wantedvalue = subregkey.GetValue("AppID").ToString
                                                If IsNullOrWhitespace(wantedvalue) = False Then

                                                    Try
                                                        deletesubregkey(regkey, wantedvalue)
                                                    Catch ex As Exception
                                                    End Try

                                                    Try
                                                        deletesubregkey(regkey, child)
                                                        Exit For
                                                    Catch ex As Exception
                                                    End Try
                                                End If
                                            End If
                                        End If
                                    End If
                                End If
                            Next
                        End If
                    Next
                End If
            Catch ex As Exception
                application.log.AddException(ex)
            End Try
        End If


        'clean orphan typelib.....
        application.log.addmessage("Orphan cleanUp")
        Try
            regkey = My.Computer.Registry.ClassesRoot.OpenSubKey("TypeLib", True)
            If regkey IsNot Nothing Then
                For Each child As String In regkey.GetSubKeyNames()
                    If (Not IsNullOrWhitespace(child)) AndAlso (regkey.OpenSubKey(child) IsNot Nothing) Then
                        For Each child2 As String In regkey.OpenSubKey(child).GetSubKeyNames()
                            If (regkey.OpenSubKey(child, False) IsNot Nothing) AndAlso (Not IsNullOrWhitespace(child2)) Then
                                For Each child3 As String In regkey.OpenSubKey(child).OpenSubKey(child2).GetSubKeyNames()
                                    If (regkey.OpenSubKey(child, False) IsNot Nothing) AndAlso (Not IsNullOrWhitespace(child3)) Then
                                        For Each child4 As String In regkey.OpenSubKey(child).OpenSubKey(child2).OpenSubKey(child3).GetSubKeyNames()
                                            If (Not IsNullOrWhitespace(child4)) AndAlso regkey.OpenSubKey(child, False) IsNot Nothing Then
                                                For i As Integer = 0 To clsidleftover.Length - 1
                                                    If Not IsNullOrWhitespace(clsidleftover(i)) Then
                                                        If (regkey.OpenSubKey(child, False) IsNot Nothing) AndAlso (Not IsNullOrWhitespace(CStr(regkey.OpenSubKey(child).OpenSubKey(child2).OpenSubKey(child3).OpenSubKey(child4).GetValue("")))) Then
                                                            If regkey.OpenSubKey(child).OpenSubKey(child2).OpenSubKey(child3).OpenSubKey(child4).GetValue("").ToString.ToLower.Contains(clsidleftover(i).ToLower) Then
                                                                Try
                                                                    deletesubregkey(regkey, child)
                                                                    application.log.addmessage(child + " for " + clsidleftover(i))
                                                                    Exit For
                                                                Catch ex As Exception
                                                                End Try
                                                            End If
                                                        End If
                                                    End If
                                                Next
                                            End If
                                        Next
                                    End If
                                Next
                            End If
                        Next
                    End If
                Next
            End If
        Catch ex As Exception
            application.log.AddException(ex)
        End Try

        application.log.addmessage("End clsidleftover CleanUP")
    End Sub

    Public Sub interfaces(ByVal interfaces As String())

        Dim regkey As RegistryKey
        Dim subregkey As RegistryKey
        Dim wantedvalue As String
        Dim typelib As String = Nothing

        application.log.addmessage("Start Interface CleanUP")

        Try
            regkey = My.Computer.Registry.ClassesRoot.OpenSubKey("Interface", True)
            If regkey IsNot Nothing Then
                For Each child As String In regkey.GetSubKeyNames()
                    If IsNullOrWhitespace(child) = False Then

                        subregkey = My.Computer.Registry.ClassesRoot.OpenSubKey("Interface\" & child, False)

                        If subregkey IsNot Nothing Then
                            If IsNullOrWhitespace(CStr(subregkey.GetValue(""))) = False Then
                                wantedvalue = subregkey.GetValue("").ToString
                                If IsNullOrWhitespace(wantedvalue) = False Then
                                    For i As Integer = 0 To interfaces.Length - 1
                                        If Not IsNullOrWhitespace(interfaces(i)) Then
                                            If wantedvalue.ToLower.StartsWith(interfaces(i).ToLower) Then
                                                If subregkey.OpenSubKey("Typelib", False) IsNot Nothing Then
                                                    If IsNullOrWhitespace(CStr(subregkey.OpenSubKey("TypeLib", False).GetValue(""))) = False Then
                                                        typelib = CStr(subregkey.OpenSubKey("TypeLib", False).GetValue(""))
                                                        If IsNullOrWhitespace(typelib) = False Then
                                                            Try
                                                                deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("TypeLib", True), typelib)
                                                            Catch ex As Exception
                                                            End Try
                                                        End If
                                                    End If
                                                End If
                                                Try
                                                    deletesubregkey(regkey, child)
                                                Catch ex As Exception
                                                End Try
                                            End If
                                        End If
                                    Next
                                End If
                            End If
                        End If
                    End If
                Next
            End If
        Catch ex As Exception
            application.log.AddException(ex)
        End Try

        If IntPtr.Size = 8 Then

            Try
                regkey = My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\Interface", True)
                If regkey IsNot Nothing Then
                    For Each child As String In regkey.GetSubKeyNames()
                        If IsNullOrWhitespace(child) = False Then

                            subregkey = My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\Interface\" & child, False)

                            If subregkey IsNot Nothing Then
                                'Hack for some weird registry state  "For user: Watcher"
                                Try
                                    If IsNullOrWhitespace(CStr((subregkey.GetValue("")))) = False Then
                                        'do nothing
                                    End If
                                Catch ex As Exception
									Application.Log.AddException(ex, "non standard keytype found : " + child)
                                    Continue For
                                End Try
                                If IsNullOrWhitespace(CStr((subregkey.GetValue("")))) = False Then
                                    wantedvalue = subregkey.GetValue("").ToString
                                    If IsNullOrWhitespace(wantedvalue) = False Then
                                        For i As Integer = 0 To interfaces.Length - 1
                                            If Not IsNullOrWhitespace(interfaces(i)) Then
                                                If wantedvalue.ToLower.StartsWith(interfaces(i).ToLower) Then
                                                    If subregkey.OpenSubKey("Typelib", False) IsNot Nothing Then
                                                        If IsNullOrWhitespace(CStr(subregkey.OpenSubKey("TypeLib", False).GetValue(""))) = False Then
                                                            typelib = CStr(subregkey.OpenSubKey("TypeLib", False).GetValue(""))
                                                            If IsNullOrWhitespace(typelib) = False Then
                                                                Try
                                                                    deletesubregkey(My.Computer.Registry.ClassesRoot.OpenSubKey("Wow6432Node\TypeLib", True), typelib)
                                                                Catch ex As Exception
                                                                End Try
                                                            End If
                                                        End If
                                                    End If
                                                    Try
                                                        deletesubregkey(regkey, child)
                                                    Catch ex As Exception
                                                    End Try
                                                End If
                                            End If
                                        Next
                                    End If
                                End If
                            End If
                        End If
                    Next
                End If
            Catch ex As Exception
                application.log.AddException(ex)
            End Try

        End If

        application.log.addmessage("END Interface CleanUP")
    End Sub

    Public Sub folderscleanup(ByVal driverfiles As String())

        Dim winxp = frmMain.winxp
        Dim filePath As String
        Dim donotremoveamdhdaudiobusfiles = frmMain.donotremoveamdhdaudiobusfiles

        For i As Integer = 0 To driverfiles.Length - 1
            If Not IsNullOrWhitespace(driverfiles(i)) Then
                If Not (donotremoveamdhdaudiobusfiles AndAlso driverfiles(i).ToLower.Contains("amdkmafd")) Then

                    filePath = System.Environment.SystemDirectory

                    Try
                        deletefile(filePath & "\" & driverfiles(i))
                    Catch ex As Exception
                    End Try

                    Try
                        deletefile(filePath + "\Drivers\" + driverfiles(i))
                    Catch ex As Exception
                    End Try

                    If winxp Then
                        Try
                            deletefile(filePath + "\Drivers\dllcache\" + driverfiles(i))
                        Catch ex As Exception
                        End Try
                    End If
                End If
            End If
        Next

        Try
            For i As Integer = 0 To driverfiles.Length - 1
                If Not IsNullOrWhitespace(driverfiles(i)) Then
                    filePath = Environment.GetEnvironmentVariable("windir")
                    For Each child As String In My.Computer.FileSystem.GetFiles(filePath & "\Prefetch")
                        If IsNullOrWhitespace(child) = False Then
                            If child.ToLower.Contains(driverfiles(i).ToLower) Then
                                Try
                                    deletefile(child)
                                Catch ex As Exception
                                End Try
                            End If
                        End If
                    Next
                End If
            Next
        Catch ex As Exception
			Application.Log.AddException(ex)
        End Try

        Const CSIDL_WINDOWS As Integer = &H29
        Dim winPath As New StringBuilder(300)
        If WindowsApi.SHGetFolderPath(Nothing, CSIDL_WINDOWS, Nothing, 0, winPath) <> 0 Then
            Throw New ApplicationException("Can't get window's sysWOW64 directory")
            Application.Log.AddMessage("Can't get window's sysWOW64 directory")
        End If


        If IntPtr.Size = 8 Then
            For i As Integer = 0 To driverfiles.Length - 1
                If Not IsNullOrWhitespace(driverfiles(i)) Then
                    If Not (donotremoveamdhdaudiobusfiles AndAlso driverfiles(i).ToLower.Contains("amdkmafd")) Then

                        For Each child As String In My.Computer.FileSystem.GetFiles(winPath.ToString, FileIO.SearchOption.SearchTopLevelOnly, "*.log")
                            If IsNullOrWhitespace(child) = False Then
                                If child.ToLower.Contains(driverfiles(i).ToLower) Then
                                    Try
                                        deletefile(child)
                                    Catch ex As Exception
                                    End Try
                                End If
                            End If
                        Next

                        Try
                            deletefile(winPath.ToString + "\Drivers\" + driverfiles(i))
                        Catch ex As Exception
                        End Try

                        Try
                            deletefile(winPath.ToString + "\" + driverfiles(i))
                        Catch ex As Exception
                        End Try
                    End If
                End If
            Next
        End If
    End Sub

End Class

Public Class WindowsApi

    <DllImport("shell32.dll")> _
    Public Shared Function SHGetFolderPath(ByVal hwndOwner As IntPtr, ByVal nFolder As Int32, ByVal hToken As IntPtr, ByVal dwFlags As Int32, ByVal pszPath As StringBuilder) As Int32
    End Function

    <DllImport("kernel32.dll", EntryPoint:="WTSGetActiveConsoleSessionId", SetLastError:=True)> _
    Public Shared Function WTSGetActiveConsoleSessionId() As UInteger
    End Function

    <DllImport("Wtsapi32.dll", EntryPoint:="WTSQueryUserToken", SetLastError:=True)> _
    Public Shared Function WTSQueryUserToken(ByVal SessionId As UInteger, ByRef phToken As IntPtr) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <DllImport("kernel32.dll", EntryPoint:="CloseHandle", SetLastError:=True)> _
    Public Shared Function CloseHandle(<InAttribute()> ByVal hObject As IntPtr) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <DllImport("advapi32.dll", EntryPoint:="CreateProcessAsUserW", SetLastError:=True)> _
    Public Shared Function CreateProcessAsUser(<InAttribute()> ByVal hToken As IntPtr, _
                                                    <InAttribute(), MarshalAs(UnmanagedType.LPWStr)> ByVal lpApplicationName As String, _
                                                    ByVal lpCommandLine As IntPtr, _
                                                    <InAttribute()> ByVal lpProcessAttributes As IntPtr, _
                                                    <InAttribute()> ByVal lpThreadAttributes As IntPtr, _
                                                    <MarshalAs(UnmanagedType.Bool)> ByVal bInheritHandles As Boolean, _
                                                    ByVal dwCreationFlags As UInteger, _
                                                    <InAttribute()> ByVal lpEnvironment As IntPtr, _
                                                    <InAttribute(), MarshalAsAttribute(UnmanagedType.LPWStr)> ByVal lpCurrentDirectory As String, _
                                                    <InAttribute()> ByRef lpStartupInfo As STARTUPINFOW, _
                                                    <OutAttribute()> ByRef lpProcessInformation As PROCESS_INFORMATION) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <StructLayout(LayoutKind.Sequential)> _
    Public Structure SECURITY_ATTRIBUTES
        Public nLength As UInteger
        Public lpSecurityDescriptor As IntPtr
        <MarshalAs(UnmanagedType.Bool)> _
        Public bInheritHandle As Boolean
    End Structure

    <StructLayout(LayoutKind.Sequential)> _
    Public Structure STARTUPINFOW
        Public cb As UInteger
        <MarshalAs(UnmanagedType.LPWStr)> _
        Public lpReserved As String
        <MarshalAs(UnmanagedType.LPWStr)> _
        Public lpDesktop As String
        <MarshalAs(UnmanagedType.LPWStr)> _
        Public lpTitle As String
        Public dwX As UInteger
        Public dwY As UInteger
        Public dwXSize As UInteger
        Public dwYSize As UInteger
        Public dwXCountChars As UInteger
        Public dwYCountChars As UInteger
        Public dwFillAttribute As UInteger
        Public dwFlags As UInteger
        Public wShowWindow As UShort
        Public cbReserved2 As UShort
        Public lpReserved2 As IntPtr
        Public hStdInput As IntPtr
        Public hStdOutput As IntPtr
        Public hStdError As IntPtr
    End Structure

    <StructLayout(LayoutKind.Sequential)> _
    Public Structure PROCESS_INFORMATION
        Public hProcess As IntPtr
        Public hThread As IntPtr
        Public dwProcessId As UInteger
        Public dwThreadId As UInteger
    End Structure

End Class
