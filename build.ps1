$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    throw 'The .NET Framework 4.x C# compiler for Windows is required.'
}

$outputDirectory = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$executable = Join-Path $outputDirectory 'Recomp-Hub.exe'
Push-Location -LiteralPath $PSScriptRoot
try {
    $arguments = @(
        '/nologo', '/langversion:5', '/target:winexe', '/platform:anycpu', '/optimize+'
        "/out:$executable"
        '/win32icon:resources\hub-mark.ico'
        '/resource:resources\hub-mark.ico,app-icon'
        '/resource:resources\hub-mark.png,brand'
        '/resource:resources\faq.txt,faq'
        '/resource:resources\repositories.tsv,repositories'
        '/resource:resources\gpu.tsv,gpu'
        '/resource:resources\cpu.tsv,cpu'
        '/resource:resources\ram-type.tsv,ram-type'
        '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll'
        '/r:System.Web.Extensions.dll', '/r:System.Net.Http.dll', '/r:System.Management.dll'
        'src\NativeHub.cs', 'src\PcHardware.cs'
    )
    & $compiler @arguments
    if ($LASTEXITCODE -ne 0) { throw "Build failed (exit code $LASTEXITCODE)." }
    Write-Output $executable
}
finally { Pop-Location }
