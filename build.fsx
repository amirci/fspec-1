#I @"./packages/FAKE/tools"
#r @"./packages/FAKE/tools/FakeLib.dll"
open System
open Fake
open Fake.Git
open Fake.AssemblyInfoFile

type Version = { Major:int; Minor:int; Build:int }

[<AutoOpen>]
module Helpers =
    let parseVersion (v:string) =
        let regex = new System.Text.RegularExpressions.Regex("^(\d+).(\d+).(\d+)$");
        let m = regex.Match(v.Trim())
        if not (m.Success) then failwithf "Invalid version file: %A" v
        let getValue (x:int) = m.Groups.Item(x).Value |> System.Int32.Parse
        { Major = getValue 1;
          Minor = getValue 2;
          Build = getValue 3 }

    let versionToString version = 
        sprintf "%d.%d.%d" version.Major version.Minor version.Build

    let versionToCommitMsg version =
        version
        |> versionToString
        |> sprintf "v-%s"
          
let getVersion () = 
    ReadFileAsString "version.txt"
    |> parseVersion

let getCommitMsg () =
    getVersion ()
    |> versionToCommitMsg

let writeVersion version =
    version
    |> versionToString
    |> WriteStringToFile false "version.txt"

Target "Build" <| fun _ ->
    let version = getVersion () |> versionToString
    CreateFSharpAssemblyInfo "./core/AssemblyInfo.fs" [
        Attribute.Title "FSpec"
        Attribute.Version version
        Attribute.FileVersion version
    ]

    CreateFSharpAssemblyInfo "./FSpec.AutoFoq/AssemblyInfo.fs" [
        Attribute.Title "FSpec.AutoFoq"
        Attribute.Version version
        Attribute.FileVersion version
    ]

    CreateFSharpAssemblyInfo "./FSpec.MbUnitWrapper/AssemblyInfo.fs" [
        Attribute.Title "FSpec.MbUnitWrapper"
        Attribute.Version version
        Attribute.FileVersion version
    ]

    let setParams defaults =
        { defaults with
            Targets = ["Build"]
            Properties =
                [
                    "Optimize", "True"
                    "Platform", "Any CPU"
                    "Configuration", "Release"
                ]
        }

    let rebuild config = {(setParams config) with Targets = ["Rebuild"]}

    build rebuild "./FSpec.sln"

Target "CreatePackage" <| fun _ ->
    let version = getVersion() |> versionToString
    ensureDirectory "NuGet"
    CleanDir "Nuget"

    let result =
        let dependencies = 
            ["FSharp.core"; "FSpec"; "CommandLine"] 
            |> Seq.map (fun dep -> "output" @@ (dep + ".dll"))
            |> String.concat " "

        ExecProcess (fun info ->
            info.FileName <- ("packages" @@ "ilmerge" @@ "tools" @@ "ilmerge.exe")
            info.WorkingDirectory <- "."
            info.Arguments <- "/out:output/fspec-runner-merged.exe output/fspec-runner.exe " + dependencies
        ) (System.TimeSpan.FromMinutes 5.)

    if result <> 0 then failwithf "Calling ILMerge failed with non zero code"

    NuGet (fun p -> 
        {p with
            Version = version
            WorkingDir = "."
        })
        "fspec.nuspec"
    NuGet (fun p -> 
        {p with
            Version = version
            WorkingDir = "."
        })
        "FSpec.AutoFoq.nuspec"
    NuGet (fun p -> 
        {p with
            Version = version
            WorkingDir = "."
        })
        "FSpec.MbUnitWrapper.nuspec"
    
Target "IncBuildNo" <| fun _ ->
    let version = getVersion()
    { version with Build = version.Build + 1 }
    |> writeVersion

Target "IncMinorVersion" <| fun _ ->
    let version = getVersion()
    { version with Build = 0; Minor = version.Minor + 1 }
    |> writeVersion

Target "Commit" <| fun _ ->
    StageAll "."
    let commitMsg = getCommitMsg ()
    Commit "." commitMsg
    sprintf "tag %s" commitMsg
    |> runSimpleGitCommand "." 
    |> trace

// Default target
Target "Default" <| fun _ -> ()

Target "TestCreateBuild" <| fun _ ->
    run "IncBuildNo"
    run "Build"
    run "CreatePackage"

Target "TestCreateMinor" <| fun _ -> 
    run "IncMinorVersion"
    run "Build"
    run "CreatePackage"

Target "CreateBuild" <| fun _ -> 
    run "TestCreateBuild"
    run "Commit"

Target "CreateMinor" <| fun _ -> 
    run "TestCreateMinor"
    run "Commit"

"Build" ==> "Default"

// start build
RunTargetOrDefault "Default"
