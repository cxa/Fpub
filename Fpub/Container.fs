namespace Fpub

module Container =
  open Internal
  open System
  open System.IO
  open System.IO.Compression
  open System.Xml
  open System.Xml.XPath

  type T = private T of ZipArchive

  let withStream stream =
    Result.attempt <| fun () ->
      let archive = new ZipArchive (stream, ZipArchiveMode.Read)
      let mimeEntry = archive.GetEntry "mimetype"
      use sreader = new StreamReader (mimeEntry.Open (), Text.Encoding.ASCII)
      let mimeStr = sreader.ReadToEnd ()
      match mimeStr with
      | "application/epub+zip" -> T archive
      | _ -> raise <| InvalidDataException ()

  let withFile file =
    Result.attempt
    <| fun () -> new FileStream (file, FileMode.Open)
    |> Result.bind withStream

  let getResource path (T zip) =
    Result.attempt <| fun () ->
      let entry = zip.GetEntry path
      entry.Open ()

  let getResource' t path =
    getResource path t

  let getPackagePaths container =
    container
    |> getResource "META-INF/container.xml"
    |> Result.attemptMap (fun stream ->
      let doc = XPathDocument (stream=stream)
      let nav = doc.CreateNavigator ()
      let xnm = XmlNamespaceManager (NameTable ())
      xnm.AddNamespace ("ocf", "urn:oasis:names:tc:opendocument:xmlns:container")
      nav.Select ("//ocf:rootfile", xnm)
      |> Seq.cast<XPathNavigator>
      |> Seq.map (fun nav ->
        nav.GetAttribute("full-path", String.Empty).Trim ())
    )

  let getDefaultPackage container =
    let withStream (streamResult, pkgDir) =
      streamResult
      |> Result.bind (flip Package.withStream pkgDir)
    getPackagePaths container
    |> Result.attemptMap Seq.head
    |> Result.map (fun path ->
      getResource path container, IO.Path.GetDirectoryName path)
    |> Result.bind withStream

  let getNav container =
    let withStream (streamResult, navDir) =
      streamResult |> Result.bind (flip Nav.withStream navDir)
    getDefaultPackage container
    |> Result.bind Package.getNavDocPath
    |> Result.attemptMap (fun path ->
      getResource path container, IO.Path.GetDirectoryName path)
    |> Result.bind withStream

  let getNcx container =
    let withStream (streamResult, ncxDir) =
      streamResult
      |> Result.bind (flip Ncx.withStream ncxDir)
    getDefaultPackage container
    |> Result.bind Package.getNcxPath
    |> Result.attemptMap (fun path ->
      getResource path container, IO.Path.GetDirectoryName path)
    |> Result.bind withStream
