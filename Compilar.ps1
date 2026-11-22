$ErrorActionPreference='Stop'
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$version=Get-Date -Format 'yyyyMMdd-HHmmssfff'
$versionDir=Join-Path $PSScriptRoot ('Versions\'+$version)
[IO.Directory]::CreateDirectory($versionDir) | Out-Null
$references=@('WPF\PresentationFramework.dll','WPF\PresentationCore.dll','WPF\WindowsBase.dll','System.Xaml.dll','System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll') | ForEach-Object {'/reference:'+(Join-Path $framework $_)}
$sources=@('Glass.cs','Integrations.cs','Dashboard.cs','MascotEyes.cs','Presence.cs','Presentation.cs','LifecycleChecks.cs','Experience.cs','Productivity.cs','Style.cs','Settings.cs','Autostart.cs','PainelIA.cs','IA.cs','Fun.cs','Themes.cs','Drop.cs','Audio.cs','Motor.cs','Monitors.cs','Comando.cs','Robo.cs','Laboratorio.cs','AssemblyInfo.cs') | ForEach-Object {Join-Path $PSScriptRoot $_}
$fontBundle=Join-Path $versionDir 'TitoGlass.g.resources'
$fontWriter=New-Object System.Resources.ResourceWriter($fontBundle)
foreach($weight in @('Regular','Light','Medium','SemiBold')){$fontWriter.AddResource(('fonts/inter-'+$weight.ToLowerInvariant()+'.ttf'),[IO.File]::OpenRead((Join-Path $PSScriptRoot ('Fonts\Inter-'+$weight+'.ttf'))),$true)}
$fontWriter.Generate();$fontWriter.Close()
$fontResources=@('/resource:'+$fontBundle+',TitoGlass.g.resources')
& (Join-Path $framework 'csc.exe') /nologo /target:winexe /platform:anycpu ('/win32icon:'+(Join-Path $PSScriptRoot 'Tito.ico')) ('/win32manifest:'+(Join-Path $PSScriptRoot 'app.manifest')) ('/out:'+(Join-Path $versionDir 'TitoGlass.exe')) @references @fontResources @sources
if($LASTEXITCODE -ne 0){throw 'Falha ao compilar o Tito.'}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'TitoGlass.exe.config') -Destination (Join-Path $versionDir 'TitoGlass.exe.config')
& (Join-Path $framework 'csc.exe') /nologo /target:winexe ('/win32icon:'+(Join-Path $PSScriptRoot 'Tito.ico')) ('/win32manifest:'+(Join-Path $PSScriptRoot 'app.manifest')) ('/reference:'+(Join-Path $framework 'System.Windows.Forms.dll')) ('/out:'+(Join-Path $PSScriptRoot 'Tito.exe')) (Join-Path $PSScriptRoot 'Launcher.cs') (Join-Path $PSScriptRoot 'AssemblyInfo.cs')
if($LASTEXITCODE -ne 0){throw 'Falha ao compilar o inicializador.'}
$manifest=Join-Path $PSScriptRoot 'current-version.txt'
$temp=$manifest+'.tmp'
[IO.File]::WriteAllText($temp,('Versions\'+$version+'\TitoGlass.exe'),[Text.UTF8Encoding]::new($false))
if([IO.File]::Exists($manifest)){[IO.File]::Replace($temp,$manifest,($manifest+'.bak'))}else{[IO.File]::Move($temp,$manifest)}
Write-Output ('Versao compilada: '+$version)
