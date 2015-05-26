﻿module WebSharper.VisualStudio.Main

open System.IO
module VSI = WebSharper.VisualStudio.VSIntegration

let root =
    Path.Combine(__SOURCE_DIRECTORY__, "..")
    |> Path.GetFullPath

let configureVSI wsNupkgPath extraNupkgPaths wsTemplatesNupkgPath : VSI.Config =
    let vsixPath =
        match System.Environment.GetEnvironmentVariable "NuGetPackageOutputPath" with
        | null -> Path.ChangeExtension(wsNupkgPath, ".vsix")
        | dir -> Path.Combine(dir, Path.GetFileNameWithoutExtension(wsNupkgPath) + ".vsix")
    let wsTemplatesPath =
        Path.Combine(
            Path.GetDirectoryName(wsTemplatesNupkgPath),
            Path.GetFileNameWithoutExtension(wsTemplatesNupkgPath))
    if Directory.Exists wsTemplatesPath then Directory.Delete(wsTemplatesPath, true)
    Directory.CreateDirectory(wsTemplatesPath) |> ignore
    Compression.ZipFile.ExtractToDirectory(wsTemplatesNupkgPath, wsTemplatesPath)
    {
        NuPkgPath = wsNupkgPath
        ExtraNuPkgPaths = extraNupkgPaths
        RootPath = root
        TemplatesPath = Path.Combine(wsTemplatesPath, "templates")
        VsixPath = vsixPath
    }

let downloadPackage (source, id) =
    printf "Downloading %s nupkg..." id
    let pkg = FsNuGet.Package.GetLatest(id, ?source = source)
    let path = Path.Combine("build", sprintf "%s.%s.nupkg" pkg.Id pkg.Version)
    let fullPath = Path.Combine(Directory.GetCurrentDirectory(), path)
    pkg.SaveToFile(fullPath)
    printfn " Got %s." path
    pkg.Id, fullPath

[<EntryPoint>]
let main argv =
    let vsixConfig =
        let online = None
        let local =
            match System.Environment.GetEnvironmentVariable("LocalNuget") with
            | null ->
                eprintfn "Warning: LocalNuget variable not set, using online repository."
                online
            | localPath -> Some (FsNuGet.FileSystem localPath)
        let _, wsTemplatesDir = downloadPackage(local, "WebSharper.Templates")
        let extra =
            [
                local, "WebSharper"
                local, "WebSharper.Owin"
                local, "WebSharper.UI.Next"
                online, "Owin"
                online, "Microsoft.Owin"
                online, "Microsoft.Owin.Diagnostics"
                online, "Microsoft.Owin.FileSystems"
                online, "Microsoft.Owin.Host.HttpListener"
                online, "Microsoft.Owin.Hosting"
                online, "Microsoft.Owin.StaticFiles"
                online, "Mono.Cecil"
            ]
            |> List.map downloadPackage
            |> Map.ofList
        let ws = extra.["WebSharper"]
        configureVSI ws extra wsTemplatesDir
    printf "Generating vsix installer..."
    VSI.BuildVsixFile vsixConfig
    printfn " Created %s." vsixConfig.VsixPath
    0
