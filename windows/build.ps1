<#
.SYNOPSIS
  Build / test / run the Windows Blitztext app. Requires the .NET 8 SDK.

.EXAMPLES
  ./build.ps1                 # restore + build (Release) + run tests
  ./build.ps1 -Run            # build + launch the app
  ./build.ps1 -Publish win-x64   # self-contained single-file build into ./publish
#>
param(
    [switch]$Run,
    [string]$Publish,
    [switch]$Debug
)

$ErrorActionPreference = "Stop"
$configuration = if ($Debug) { "Debug" } else { "Release" }
$root = $PSScriptRoot
$solution = Join-Path $root "Blitztext.sln"
$appProject = Join-Path $root "src/Blitztext.App/Blitztext.App.csproj"
$testProject = Join-Path $root "src/Blitztext.Core.Tests/Blitztext.Core.Tests.csproj"

Write-Host "Restoring..." -ForegroundColor Cyan
dotnet restore $solution

Write-Host "Building ($configuration)..." -ForegroundColor Cyan
dotnet build $solution --configuration $configuration --no-restore

Write-Host "Testing core..." -ForegroundColor Cyan
dotnet test $testProject --configuration $configuration --no-build

if ($Publish) {
    $out = Join-Path $root "publish/$Publish"
    Write-Host "Publishing self-contained $Publish -> $out" -ForegroundColor Cyan
    dotnet publish $appProject --configuration $configuration --runtime $Publish `
        --self-contained true -p:PublishSingleFile=true --output $out
    Write-Host "Published to $out" -ForegroundColor Green
}

if ($Run) {
    Write-Host "Starting Blitztext..." -ForegroundColor Cyan
    dotnet run --project $appProject --configuration $configuration
}
