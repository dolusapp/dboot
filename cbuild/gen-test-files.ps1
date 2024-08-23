# Set the directory where files will be created
$testDir = "C:\TestFiles"

# Create the directory if it doesn't exist
New-Item -ItemType Directory -Force -Path $testDir

# Function to generate random content
function Get-RandomContent {
    param (
        [int]$minSize,
        [int]$maxSize
    )
    $size = Get-Random -Minimum $minSize -Maximum $maxSize
    $content = -join ((65..90) + (97..122) | Get-Random -Count $size | % {[char]$_})
    return $content
}

# Generate text files
1..5 | ForEach-Object {
    $content = Get-RandomContent -minSize 100 -maxSize 1000
    Set-Content -Path "$testDir\textfile$_.txt" -Value $content
}

# Generate JSON files
1..3 | ForEach-Object {
    $jsonContent = @{
        "id" = Get-Random
        "name" = "Test Object $_"
        "value" = Get-Random -Minimum 1 -Maximum 100
    } | ConvertTo-Json
    Set-Content -Path "$testDir\jsonfile$_.json" -Value $jsonContent
}

# Generate a larger file
$largeContent = Get-RandomContent -minSize 10000 -maxSize 50000
Set-Content -Path "$testDir\largefile.dat" -Value $largeContent

# Create a subdirectory with more files
$subDir = "$testDir\subdir"
New-Item -ItemType Directory -Force -Path $subDir

1..3 | ForEach-Object {
    $content = Get-RandomContent -minSize 50 -maxSize 500
    Set-Content -Path "$subDir\subfile$_.txt" -Value $content
}

Write-Host "Test files generated in $testDir"