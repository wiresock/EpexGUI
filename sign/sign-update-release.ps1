param (
    [string]$versionTag
)

# Import PowerShellForGitHub module
Import-Module PowerShellForGitHub

# Base URL for downloading files
$owner = "wiresock"
$repository = "WireSockUI"
$baseURL = "https://github.com/$owner/$repository/releases/download/$versionTag/"
$files = @("$repository-$versionTag-AnyCPU-no-uwp.zip", "$repository-$versionTag-AnyCPU.zip", "$repository-$versionTag-ARM64.zip")

foreach ($file in $files) {
    $downloadURL = $baseURL + $file
    $downloadPath = "./" + $file
    Invoke-WebRequest -Uri $downloadURL -OutFile $downloadPath

    # Correcting the folder name extraction
    $folderName = $file -replace ".zip", ""
    $extractPath = "./" + $folderName
    Expand-Archive -Path $downloadPath -DestinationPath $extractPath -Force
    Remove-Item -Path $downloadPath -Force

    $exePath = $extractPath + "/$repository.exe"
    & signtool sign /fd sha1 /t http://timestamp.digicert.com /n "IP SMIRNOV VADIM VALERIEVICH" $exePath
    & signtool sign /as /td sha256 /fd sha256 /tr http://timestamp.digicert.com /n "IP SMIRNOV VADIM VALERIEVICH" $exePath

    # Change to the directory of the folder to be zipped
    Push-Location $extractPath

    # Get all items in the current directory (the extracted folder)
    $items = Get-ChildItem

    $zipPath = "../" + $folderName + ".zip"
    Compress-Archive -Path $items -DestinationPath $zipPath -Force

    # Return to the original directory
    Pop-Location

    # Delete the folder after zipping
    Remove-Item -Path $extractPath -Recurse -Force
}

# Getting the GitHub release
$release = Get-GitHubRelease -OwnerName $owner -RepositoryName $repository -Tag $versionTag

# Removing existing ZIP files from the release
$assets = Get-GitHubReleaseAsset -OwnerName $owner -RepositoryName $repository -ReleaseId $release.id
foreach ($asset in $assets) {
    if ($asset.name -like "*.zip") {
        Remove-GitHubReleaseAsset -OwnerName $owner -RepositoryName $repository -AssetId $asset.id -Force
    }
}

# Uploading new ZIP files and deleting them after upload
foreach ($file in Get-ChildItem "./" -Filter "*.zip") {
    $release | New-GitHubReleaseAsset -Path $file.Name
    Remove-Item -Path $file.Name -Force
}