function Get-ProjectEditorVersion {
  param([string]$ProjectRoot)

  $projectVersionPath = Join-Path $ProjectRoot "ProjectSettings\ProjectVersion.txt"
  if (-not (Test-Path $projectVersionPath)) {
    return $null
  }

  $match = Select-String -Path $projectVersionPath -Pattern '^m_EditorVersion:\s*(.+)$' | Select-Object -First 1
  if (-not $match) {
    return $null
  }

  return $match.Matches[0].Groups[1].Value.Trim()
}

function Get-UnityEditorVersionFromPath {
  param([string]$UnityPath)

  if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    return $null
  }

  $editorDirectory = Split-Path -Parent $UnityPath
  $versionDirectory = Split-Path -Parent $editorDirectory
  return Split-Path -Leaf $versionDirectory
}

function Assert-UnityEditorMatchesProject {
  param(
    [string]$ProjectRoot,
    [string]$UnityPath,
    [bool]$AllowMismatch = $false
  )

  if ($AllowMismatch) {
    return
  }

  $projectEditorVersion = Get-ProjectEditorVersion -ProjectRoot $ProjectRoot
  $unityEditorVersion = Get-UnityEditorVersionFromPath -UnityPath $UnityPath
  if ([string]::IsNullOrWhiteSpace($projectEditorVersion) -or
      [string]::IsNullOrWhiteSpace($unityEditorVersion) -or
      $projectEditorVersion -eq $unityEditorVersion) {
    return
  }

  throw "Unity editor version mismatch. Project is pinned to $projectEditorVersion, but resolved editor is $unityEditorVersion. Pass -UnityExePath for the pinned editor, or pass -AllowEditorVersionMismatch only when intentionally validating the Unity upgrade lane."
}
