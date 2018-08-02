namespace Fpub

module Epub =
  open Internal

  type T =
    { Id: string
      Title: string
      Container: Container.T
      Metadata: Package.Metadata.T
      Manifest: Package.Manifest.T
      LinearSpinePaths: string [] // Non empty
      Toc: TocItem[] }

  let getManifestItem (path:string) epub =
    if path.StartsWith "/" then
      Package.Manifest.getItemByAbsoluteResourcePath path epub.Manifest
    else
      Package.Manifest.getItemByResourcePath path epub.Manifest

  let getResource (path:string) epub =
    let resPath = if path.StartsWith "/" then path.Substring 1 else path
    Container.getResource resPath epub.Container

  let private make id title container metadata manifest linearSpinePaths toc =
    { Id = id
      Title = title
      Container = container
      Metadata = metadata
      Manifest = manifest
      LinearSpinePaths = linearSpinePaths
      Toc = toc }

  let private getSpinePaths pkg manifest =
    let itemrefs =
      pkg
      |> Result.bind Package.getSpine
      |> Result.bind Package.Spine.getItemRefs
    match itemrefs, manifest with
    | Ok items, Ok mf ->
      let linears =
        items
        |> Seq.choose (fun i ->
          if Package.Spine.ItemRef.getLinear i then
            let path =
              Package.Spine.ItemRef.getManifestItem i mf
              |> Result.bind Package.Manifest.Item.getResourcePath
            match path  with
            | Ok p -> Some p
            | Error _ -> None
          else None)
        |> Array.ofSeq
      if Array.length linears > 0 then Ok linears
      else Result.attempt (fun () -> failwith "No linear spines")
    | _ -> Result.attempt (fun () -> failwith "No spines or manifest")

  let private getToc container =
    let tocInNav =
      container
      |> Result.bind Container.getNav
      |> Result.bind Nav.getToc
      |> Result.map Nav.Toc.getItems
    let tocInNcx () =
      container
      |> Result.bind Container.getNcx
      |> Result.bind Ncx.getToc
      |> Result.map Ncx.Toc.getItems
    match tocInNav with
    | Ok t -> Ok t
    | Error _ -> tocInNcx ()

  let private withContainer container =
    let pkg = container |> Result.bind Container.getDefaultPackage
    let metadata = pkg |> Result.bind Package.getMetadata
    let manifest = pkg |> Result.bind Package.getManifest
    let id = metadata |> Result.bind Package.Metadata.getUniqueIdentifier
    let getTitle () = metadata |> Result.bind Package.Metadata.getTitle
    id
    <!> make
    <*> getTitle ()
    <*> container
    <*> metadata
    <*> manifest
    <*> getSpinePaths pkg manifest
    <*> getToc container

  let withStream stream =
    withContainer <| Container.withStream stream

  let withFile file =
    withContainer <| Container.withFile file
