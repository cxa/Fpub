namespace Fpub

module Ncx =
  open Internal
  open System
  open System.Xml.XPath

  module Toc =
    type T = internal T of DirContext<Element.T>

    let rec private processNavPoints dir nps =
      Result.mapOr (fun points ->
        Seq.foldBack (fun point acc ->
          match processNavPoint dir point with
          | Some item -> item :: acc
          | _ -> acc
        ) points []
        |> List.toArray
      ) [||] nps
    and private processNavPoint dir np =
      Result.mapOr2
        (fun title href ->
          Some
            { title = title
            ; resourcePath = IO.Path.Combine (dir, href) |> Some
            ; subitems = Element.getAllElements "navPoint" np |> processNavPoints dir
            }
        )
        None
        (Element.evalToString "string(navLabel/text)" np)
        (Element.evalToString "string(content/@src)" np)

    let getItems (T dc) =
      dc.context |> Element.getAllElements "navPoint" |> processNavPoints dc.dir

  module PageList =
    type T = internal T of DirContext<Element.T>

    let getPages (T dc) =
      dc.context
      |> Element.getAllElements "pageTarget"
      |> Result.map (fun els ->
        Seq.foldBack (fun el acc ->
          let page = Element.evalToString "string(navLabel/text)" el
          let href = Element.evalToString "string(content/@src)" el
          match page, href with
          | Ok k, Ok v -> Map.add k (IO.Path.Combine (dc.dir, v)) acc
          | _ -> acc
        ) els Map.empty
      )
      |> Result.getOr Map.empty

  type T = internal T of DirContext<Element.T>

  let private withDoc (doc:XPathDocument) docDir =
    Element.create
      [ "ncx", "http://www.daisy.org/z3986/2005/ncx/" ]
      (doc.CreateNavigator ())
    |> Element.getFirstElement "ncx"
    |> Result.map (DirContext<_>.Create' docDir >> T)

  let withFile uri docDir =
    withDoc <| XPathDocument (uri=uri) <| docDir

  let withStream stream docDir =
    withDoc <| XPathDocument (stream=stream) <| docDir

  let getElement (T dc) =
    dc.context

  let getDirectory (T dc) =
    dc.dir

  let getToc t =
    t
    |> getElement
    |> Element.getFirstElement "navMap"
    |> Result.map (DirContext<_>.Create' (getDirectory t) >> Toc.T)

  let getPageList t =
    t
    |> getElement
    |> Element.getFirstElement "pageList"
    |> Result.map (DirContext<_>.Create' (getDirectory t) >> PageList.T)