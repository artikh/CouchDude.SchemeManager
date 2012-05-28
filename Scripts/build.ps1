Include .\utils.ps1

properties {
    Import-Module .\teamcity.psm1
    
    if($config -eq $null) {
        $config = 'Debug'
    }
    
    $scriptDir = (resolve-path .).Path
    $rootDir = (resolve-path ..).Path;
    $buildDir = "$rootDir\Build";   
    $srcDir = "$rootDir\Src";
    $assemblyInfoFileName = "$rootDir\GlobalAssemblyInfo.cs"
        
    $version = detectVersion $version $assemblyInfoFileName $buildNumber
    TeamCity-SetBuildNumber $version
    
    echo "Building version $nuGetVersion"
    
    if ($nugetSources -eq $null) {
        $nugetSources = "https://go.microsoft.com/fwlink/?LinkID=206669"
    }
}

task default -depends setVersion, test, package

task clean {
    if(test-path $buildDir) {
        dir $buildDir | del -Recurse -Force
    }
    mkdir -Force $buildDir > $null
}

task setVersion {  
    $assembyInfo = [System.IO.File]::ReadAllText($assemblyInfoFileName)
    $assembyInfo = $assembyInfo -replace "Version\((.*)\)]", "Version(`"$version`")]"
    $assembyInfo.Trim() > $assemblyInfoFileName
}


task installPackages {    
    dir -Path $rootDir -Recurse -Filter packages.config | %{    
        exec { ..\tools\nuget\NuGet.exe install $_.FullName -Source $nugetSources -OutputDirectory "$rootDir\Packages"  }
    }
}

task build -depends clean, installPackages {
    exec { msbuild "$rootDir\SchemeManager.sln" /nologo /p:Configuration=$config /p:Platform='Any Cpu' /maxcpucount }    
}

function startTest([string]$testAssembly, [string]$outputFileName, [bool]$publish) {
    exec { & "$rootDir\Tools\xunit\xunit.console.clr4.x86.exe" $testAssembly /silent /-trait level=integration /html "$outputFileName" }
    if($publish) {
        TeamCity-PublishArtifact $outputFileName        
    }
}

task test -depends build {
    startTest "$rootDir\Tests\bin\$config\CouchDude.SchemeManager.Tests.dll" "$buildDir\test-results-$version.html" -publish $true
}

task package -depends build {
    prepareAndPackage -templateNuSpec "$srcDir\Core\CouchDude.SchemeManager.nuspec" -fileTemplates ("$srcDir\Core\bin\$config\CouchDude.SchemeManager.*") -version $version -packagesFileName "$srcDir\Core\packages.config"
    TeamCity-PublishArtifact "$buildDir\CouchDude.$version.nupkg"
}

#experimental
task testWithNewVersion -depends clean {
    $testDir = "$buildDir\NewVerTest"
    mkdir -Force $testDir
    mkdir -Force "$testDir\Src"
    
    copy "$rootDir\Tests" -Recurse  -Destination "$testDir\Tests" -Force
    copy "$rootDir\Src\Core" -Recurse -Destination "$testDir\Src\Core" -Force
}