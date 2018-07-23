namespace Fpub

module Package =
  open Internal
  open System
  open System.Xml.XPath

  let private getResourcePath pkgDir href =
    IO.Path.Combine (pkgDir, href)

  module Metadata =
    type T = internal T of Element.T

    let getElement (T element) =
      element

    let getUniqueIdentifier (T element) =
      Element.evalToString "string(../@unique-identifier)" element
      |> Result.bind (fun pkguid ->
        Element.evalToString
          (sprintf """string(dc:identifier[@id="%s"])""" pkguid) element)

    let getReleaseIdentifier metadata =
      let (T element) = metadata
      let expr = """string(meta[@property="dcterms:modified"])"""
      let uid = getUniqueIdentifier metadata
      let moddate = Element.evalToString expr element
      match uid, moddate with
      | Ok uid, Ok moddate -> Ok <| uid + "@" + moddate
      | Error e, _
      | _, Error e -> Error e

    let getAllIdentifiers (T element) =
      Element.getAllElements "dc:identifier" element
      |> Result.map (Seq.map Element.getValue)

    let getSchemeIdentifiers (T element) =
      Element.getAllElements "dc:identifier" element
      |> Result.map (Seq.fold (fun map n ->
          let scheme = Element.getAttribute "opf:scheme" n
          match scheme with
          | Ok s -> Map.add s (Element.getValue n) map
          | _ -> map) Map.empty)

    let getTitle (T element) =
      Element.evalToString "string(dc:title)" element

    let getLanguage (T element) =
      Element.evalToString "string(dc:language)" element

    let getCreators (T element) =
      Element.getAllElements "dc:creator" element
      |> Result.map (Seq.map Element.getValue)

    let getContributors (T element) =
      Element.getAllElements "dc:contributor" element
      |> Result.map (Seq.map Element.getValue)

    let getDate (T element) =
      Element.evalToString "dc:date" element
      |> Result.attemptMap DateTime.Parse

  module Manifest =
    type T = internal T of DirContext<Element.T>

    let getElement (T dc) =
      dc.Context

    let private getDirectory (T dc) =
      dc.Dir

    module Item =
      type T = internal T of DirContext<Element.T>

      let getElement (T dc) =
        dc.Context

      let private getDirectory (T dc) =
        dc.Dir

      let getAttribute attribute t =
        t
        |> getElement
        |> Element.getAttribute attribute

      let getId t =
        getAttribute "id" t

      let getHref t =
        getAttribute "href" t

      let getMediaType t =
        getAttribute "media-type" t

      let getResourcePath t =
        getHref t
        |> Result.map (t |> getDirectory |> getResourcePath)

    let getItems t =
      let dir = getDirectory t
      t
      |> getElement
      |> Element.getAllElements "item"
      |> Result.map (Seq.map (DirContext<_>.Create' dir >> Item.T))

    let getItemById id t =
      let dir = getDirectory t
      t
      |> getElement
      |> Element.getFirstElement (sprintf """item[@id="%s"]""" id)
      |> Result.map (DirContext<_>.Create' dir >> Item.T)

    let getItemByHref href t =
      let dir = getDirectory t
      t
      |> getElement
      |> Element.getFirstElement (sprintf """item[@href="%s"]""" href)
      |> Result.map (DirContext<_>.Create' dir >> Item.T)

    let getItemByResourcePath (path:string) t =
      let dir = getDirectory t
      let href =
        if String.IsNullOrEmpty dir
        then path
        else path.Substring <| dir.Length + 1
      getItemByHref href t

    let getItemByAbsoluteResourcePath (path:string) t =
      let dir = getDirectory t
      let href =
        let extraPos = if String.IsNullOrEmpty dir then 1 else 2
        path.Substring <| dir.Length + extraPos
      getItemByHref href t

    let getNavItem t =
      let dir = getDirectory t
      t
      |> getElement
      |> Element.getFirstElement """item[@properties="nav"]"""
      |> Result.map (DirContext<_>.Create' dir >> Item.T)

    let getCoverImageItem t =
      let element, dir = getElement t, getDirectory t
      let getEpub3Cover () =
        element
        |> Element.getFirstElement """item[@properties="cover-image"]"""
        |> Result.map (DirContext<_>.Create' dir >> Item.T)
      let getEpub2Cover () =
        element
        |> Element.evalToString
          """string(../metadata/meta[@name="cover"]/@content)"""
        |> Result.bind (flip getItemById t)
      getEpub3Cover ()
      |> Result.mapOr Ok (getEpub2Cover ())

  module Spine =
    type T = internal T of Element.T

    let getElement (T element) =
      element

    module ItemRef =
      type T = internal T of Element.T

      let getElement (T element) =
        element

      let getId (T element) =
        Element.getAttribute "id" element

      let getLinear (T element) =
        Element.getAttribute "linear" element
        |> Result.map (fun s -> match s with "yes" -> true | _ -> false)
        |> Result.getOr true

      let getProperties (T element) =
        Element.getAttribute "properties" element

      let getIdref (T element) =
        Element.getAttribute "idref" element

      let getManifestItem itemRef element =
        getIdref itemRef
        |> Result.bind (fun id -> Manifest.getItemById id element)

    module PageProgressionDirection =
      type T =
        | Ltr
        | Rtl
        | Default

    let getPageProgressionDirection (T element) =
      let v = Element.evalToString "string(@page-progression-direction)" element
      match v with
      | Ok "ltr" -> PageProgressionDirection.Ltr
      | Ok "rtl" -> PageProgressionDirection.Rtl
      | _ -> PageProgressionDirection.Default

    let getItemRefs (T element) =
      Element.getAllElements "itemref" element
      |> Result.map (Seq.map ItemRef.T)

  type T = private T of DirContext<Element.T>

  let private withDoc (doc:XPathDocument) pkgDir =
    let namespaces =
      [ "opf", "http://www.idpf.org/2007/opf"
      ; "dc", "http://purl.org/dc/elements/1.1/"
      ]
    Element.create namespaces (doc.CreateNavigator ())
    |> Element.getFirstElement "package"
    |> Result.map (fun el -> T <| DirContext<_>.Create el pkgDir)

  let withFile uri pkgDir =
    withDoc <| XPathDocument (uri=uri) <| pkgDir

  let withStream stream pkgDir =
    withDoc <| XPathDocument (stream=stream) <| pkgDir

  let getElement (T dc) =
    dc.Context

  let getDirectory (T dc) =
    dc.Dir

  let getVersion t =
    t
    |> getElement
    |> Element.evalToDouble "number(@version)"

  let getUniqueIdentifier t =
    t
    |> getElement
    |> Element.evalToString "string(@unique-identifier)"

  let getMetadata t =
    t
    |> getElement
    |> Element.getFirstElement "metadata"
    |> Result.map Metadata.T

  let getManifest t =
    t
    |> getElement
    |> Element.getFirstElement "manifest"
    |> Result.map (DirContext<_>.Create' (getDirectory t) >> Manifest.T)

  let getSpine t =
    t
    |> getElement
    |> Element.getFirstElement "spine"
    |> Result.map Spine.T

  let getNavDocPath t =
    t
    |> getElement
    |> Element.evalToString """string(manifest/item[@properties="nav"]/@href)"""
    |> Result.map (t |> getDirectory |> getResourcePath)

  let getNcxPath t =
    getSpine t
    |> Result.map Spine.getElement
    |> Result.bind (Element.getAttribute "toc")
    |> Result.map (fun id ->
       sprintf """string(manifest/item[@id="%s"]/@href)""" id)
    |> Result.bind (fun xpath -> t |> getElement |> Element.evalToString xpath)
    |> Result.map (t |> getDirectory |> getResourcePath)
