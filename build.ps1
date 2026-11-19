<#
    Builds Whittle.exe with the C# compiler that ships with Windows (.NET Framework 4.8).
    No SDK or extra downloads needed.

    .\build.ps1          Release build  -> dist\Whittle.exe (asks for admin when started)
    .\build.ps1 -Dev     Test build     -> obj\dev\Whittle-dev.exe (no admin prompt, includes
                                           --shot <folder> and --selftest <file>)
#>
param([switch]$Dev)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path (Join-Path $fw 'csc.exe'))) { $fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319' }
$csc = Join-Path $fw 'csc.exe'
if (-not (Test-Path $csc)) { throw "The .NET Framework 4.8 C# compiler wasn't found at $csc" }

$refs = @(
    "$fw\WPF\PresentationFramework.dll", "$fw\WPF\PresentationCore.dll", "$fw\WPF\WindowsBase.dll",
    "$fw\System.Xaml.dll", "System.dll", "System.Core.dll", "System.Xml.dll", "System.Drawing.dll",
    "System.Management.dll", "System.ServiceProcess.dll", "System.Web.Extensions.dll", "Microsoft.CSharp.dll"
) | ForEach-Object { "/r:$_" }

# Icon: regenerate from the vector mark if it's missing.
$ico = Join-Path $root 'assets\whittle.ico'
if (-not (Test-Path $ico)) {
    $gen = Join-Path $env:TEMP 'WhittleIconGen.exe'
    & $csc /nologo /target:exe /out:$gen /r:"$fw\WPF\PresentationFramework.dll" /r:"$fw\WPF\PresentationCore.dll" /r:"$fw\WPF\WindowsBase.dll" /r:"$fw\System.Xaml.dll" (Join-Path $root 'tools\IconGen.cs') (Join-Path $root 'src\Brand.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Icon generator failed to compile' }
    & $gen (Join-Path $root 'assets')
}

$sources = Get-ChildItem (Join-Path $root 'src') -Recurse -Filter *.cs | ForEach-Object { $_.FullName }
$theme = Join-Path $root 'src\Theme.xaml'

if ($Dev) {
    $outDir = Join-Path $root 'obj\dev'
    $out = Join-Path $outDir 'Whittle-dev.exe'
    $manifest = Join-Path $env:TEMP 'whittle-dev.manifest'
    (Get-Content (Join-Path $root 'app.manifest') -Raw) -replace 'requireAdministrator', 'asInvoker' | Set-Content $manifest -Encoding UTF8
    $defines = '/define:DEV'
} else {
    $outDir = Join-Path $root 'dist'
    $out = Join-Path $outDir 'Whittle.exe'
    $manifest = Join-Path $root 'app.manifest'
    $defines = '/define:RELEASE'
}
New-Item -ItemType Directory -Force $outDir | Out-Null

$cscArgs = @('/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/codepage:65001', '/warn:4', '/nowarn:1591',
          $defines, "/out:$out", "/win32icon:$ico", "/win32manifest:$manifest", "/resource:$theme,Whittle.Theme.xaml") + $refs + $sources
& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "Build failed (csc exit code $LASTEXITCODE)" }

$size = [math]::Round((Get-Item $out).Length / 1KB)
Write-Host "Built $out ($size KB)"
