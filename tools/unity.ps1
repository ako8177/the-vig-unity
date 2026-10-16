# Run a Vig editor tool in Unity's batch mode and show what it logged.
#   powershell -File tools/unity.ps1 shots     # renders shots/*.png
#   powershell -File tools/unity.ps1 setup     # TextMeshPro resources, the scene, the build list
#   powershell -File tools/unity.ps1 build     # Builds/Windows/TheVig.exe
param([string]$what = "shots")
$unity = "C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe"
$root = Split-Path -Parent $PSScriptRoot
$method = @{ shots = "Vig.EditorTools.VigTools.BatchShots"; setup = "Vig.EditorTools.VigTools.BatchSetup"; build = "Vig.EditorTools.VigTools.BuildWindows" }[$what]
$log = Join-Path $env:TEMP "vig-unity-$what.log"
$p = Start-Process -FilePath $unity -ArgumentList @('-batchmode', '-projectPath', $root, '-executeMethod', $method, '-quit', '-logFile', $log) -PassThru -Wait
Select-String -Path $log -Pattern "error CS|Vig:|Exception" | Select-Object -First 40 | ForEach-Object { $_.Line }
"exit: $($p.ExitCode)"
exit $p.ExitCode
