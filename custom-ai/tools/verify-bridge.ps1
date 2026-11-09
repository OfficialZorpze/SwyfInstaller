param(
    [string]$GameDir = $env:SWYF_GAME_DIR,
    [string]$BridgePath = "$PSScriptRoot/../src/Bridge/bin/Release/netstandard2.1/SWYF.CustomAI.Bridge.dll"
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($GameDir)) { throw 'Pass -GameDir or set SWYF_GAME_DIR to your game installation folder.' }
Add-Type -Path "$PSScriptRoot/../dist/installer/Mono.Cecil.dll"
$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory((Join-Path $GameDir 'Scam With Your Friends_Data/Managed'))
$parameters = [Mono.Cecil.ReaderParameters]::new()
$parameters.AssemblyResolver = $resolver
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly([IO.Path]::GetFullPath($BridgePath), $parameters)
try {
    $missing = @($assembly.MainModule.GetMemberReferences() | Where-Object { $null -eq $_.Resolve() })
    if ($missing.Count) { throw "Members missing from the game libraries: $($missing.FullName -join ', ')" }
    Write-Output 'All bridge member references were verified against the game libraries.'
} finally { $assembly.Dispose(); $resolver.Dispose() }
