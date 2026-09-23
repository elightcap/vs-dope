# Cross-platform (Windows primary, works under pwsh on Linux/macOS) deploy for vs-dope.
# Auto-finds the Vintage Story install dir, sets VINTAGE_STORY, builds, and copies
# mod files into the game's Mods data folder. No user interaction required.
$ErrorActionPreference = "Stop"

$MODID  = "vs-dope"
$CONFIG = if ($env:VS_BUILD_CONFIG) { $env:VS_BUILD_CONFIG } else { "Debug" }

Set-Location -Path $PSScriptRoot

function Test-InstallDir([string]$dir) {
    return ($dir) -and (Test-Path -LiteralPath (Join-Path $dir "VintagestoryAPI.dll"))
}

# --- Collect candidate install dirs ------------------------------------------
$cands = New-Object System.Collections.Generic.List[string]

if ($env:VINTAGE_STORY -and (Test-InstallDir $env:VINTAGE_STORY)) {
    $cands.Add($env:VINTAGE_STORY)
}

$pf86  = ${env:ProgramFiles(x86)}
$pf    = $env:ProgramFiles
if ($IsWindows -or (-not (Test-Path variable:IsWindows))) {
    foreach ($base in @($pf86, $pf, "C:\")) {
        if (-not $base) { continue }
        $cands.Add((Join-Path $base "Steam\steamapps\common\Vintage Story"))
        $cands.Add((Join-Path $base "GOG Games\Vintage Story"))
    }
    $cands.Add("C:\GOG Games\Vintage Story")
} else {
    # *nix fallbacks when run under pwsh
    $home_ = $env:HOME
    $cands.Add("/opt/vintagestory")
    $cands.Add((Join-Path $home_ ".local/share/Steam/steamapps/common/Vintage Story"))
    $cands.Add((Join-Path $home_ "GOG Games/Vintage Story"))
}

# --- Enumerate Steam libraries from libraryfolders.vdf ------------------------
$vdfs = @()
if ($pf86) {
    $vdfs += (Join-Path $pf86 "Steam\steamapps\libraryfolders.vdf")
    $vdfs += (Join-Path $pf86 "Steam\config\libraryfolders.vdf")
}
$vdfs += (Join-Path $env:USERPROFILE ".steam\root\libraryfolders.vdf")

foreach ($vdf in $vdfs) {
    if (-not (Test-Path -LiteralPath $vdf)) { continue }
    foreach ($line in Get-Content -LiteralPath $vdf) {
        if ($line -match '"path"\s*"[^"]+"') {
            $lib = ($Matches[0] -replace '"path"\s*"','' ) -replace '"',''
            if (-not $lib) { continue }
            $cands.Add((Join-Path $lib "steamapps\common\Vintage Story"))
        }
    }
}

# --- Pick the first valid install dir ----------------------------------------
$installDir = $null
foreach ($c in $cands) {
    if (Test-InstallDir $c) { $installDir = $c; break }
}

if (-not $installDir) {
    # Bounded fallback search under Steam program dirs.
    foreach ($base in @($pf86, $pf)) {
        if (-not $base -or -not (Test-Path -LiteralPath $base)) { continue }
        $hit = Get-ChildItem -LiteralPath $base -Filter "VintagestoryAPI.dll" `
                -Recurse -Depth 7 -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($hit) { $installDir = $hit.DirectoryName; break }
    }
}

if (-not $installDir) {
    Write-Error "Could not locate Vintage Story install dir (VintagestoryAPI.dll). Set VINTAGE_STORY and re-run."
    exit 1
}

$env:VINTAGE_STORY = $installDir
Write-Host "Install dir : $env:VINTAGE_STORY"

# --- Data Mods destination ----------------------------------------------------
if ($IsWindows -or (-not (Test-Path variable:IsWindows))) {
    $dest = Join-Path $env:APPDATA "VintagestoryData\Mods\$MODID"
} else {
    $cfgRoot = if ($env:XDG_CONFIG_HOME) { $env:XDG_CONFIG_HOME } else { Join-Path $env:HOME ".config" }
    $dest = Join-Path $cfgRoot "VintagestoryData/Mods/$MODID"
}
Write-Host "Deploy dest : $dest"

# --- Build --------------------------------------------------------------------
dotnet build -c $CONFIG
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit $LASTEXITCODE)" }

$dll = Join-Path $PSScriptRoot "bin\$CONFIG\$MODID.dll"
if (-not (Test-Path -LiteralPath $dll)) { throw "Build did not produce $dll" }

# --- Copy ---------------------------------------------------------------------
if (Test-Path -LiteralPath $dest) { Remove-Item -LiteralPath $dest -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $dest "assets") | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "modinfo.json") -Destination $dest -Force
Copy-Item -LiteralPath $dll -Destination $dest -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "assets\$MODID") -Destination (Join-Path $dest "assets") -Recurse -Force

Write-Host "Deployed to $dest (restart game for DLL changes; assets hot-reload with F3+R)"
