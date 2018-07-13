namespace Fpub

module Nav =
  open Internal
  open System
  open System.Xml.XPath
  open System.Text.RegularExpressions

  let private getHeadingTitle element =
    Element.evalToString "local-name(*)" element
    |> Result.bind (fun tagName ->
      let regex = Regex (@"^h[1-6]$", RegexOptions.IgnoreCase)
      if regex.IsMatch tagName then
        Element.evalToString "string(*[1])" element
      else
        Result.attempt <| fun () -> failwith "No heading elements h[1-6] found"
    )

  module Toc =
    type T = internal T of DirContext<Element.T>

    let getHeadingTitle (T dc) =
      getHeadingTitle dc.context

    let rec private processLis dir lisResult =
      let map lis =
        Seq.foldBack (fun li acc ->
          match processLi dir li with
          | Some item -> item :: acc
          | _ -> acc
        ) lis []
        |> Seq.toArray
      match Result.map map lisResult with
      | Ok items -> items
      | _ -> [||]
    and private processLi dir li =
      Element.getFirstElement "a|span" li
      |> Result.mapOr (fun el ->
        Some
          { title = Element.getValue el
          ; resourcePath = Element.getAttribute "href" el |> Result.mapOr (fun h -> IO.Path.Combine (dir, h) |> Some) None
          ; subitems = Element.getAllElements "ol/li" li |> processLis dir
          }
      ) None

    let getItems (T dc) =
      processLis dc.dir <| Element.getAllElements "ol/li" dc.context

  module Landmarks =
    type Item =
      { title: string
      ; ``type``: string
      ; resourcePath: string
      ; subitems: Item array
      }

    type T = internal T of DirContext<Element.T>

    let getHeadingTitle (T dc) =
      getHeadingTitle dc.context

    let rec private processLis dir lisResult =
      let map lis =
        Seq.foldBack (fun li acc ->
          match processLi dir li with
          | Some item -> item :: acc
          | _ -> acc
        ) lis []
        |> Seq.toArray
      Result.map map lisResult
      |> Result.getOr [||]
    and private processLi dir li =
      Element.getFirstElement "a" li
      |> Result.mapOr (fun a ->
        Result.mapOr2 (fun t h ->
          Some
            { title = Element.getValue a
            ; ``type`` = t
            ; resourcePath = IO.Path.Combine (dir, h)
            ; subitems = Element.getAllElements "ol/li" li |> processLis dir
            }
        ) None (Element.getAttribute "epub:type" a) (Element.getAttribute "href" a)
      ) None
    let getItems (T dc) =
      processLis dc.dir <| Element.getAllElements "ol/li" dc.context

  module PageList =
    type T = internal T of DirContext<Element.T>

    let getHeadingTitle (T dc) =
      getHeadingTitle dc.context

    let getPages (T dc) =
      dc.context
      |> Element.getAllElements "ol/li/a"
      |> Result.map (Seq.fold (fun acc a ->
          match Element.getAttribute "href" a with
          | Ok h ->
            let resPath = IO.Path.Combine (dc.dir, h)
            Map.add (Element.getValue a) resPath acc
          | _ -> acc
        ) Map.empty
      )
      |> Result.getOr Map.empty

  type T = internal T of DirContext<Element.T>

  let private withDoc (doc:XPathDocument) docDir =
    let namespaces =
      [ "xhtml", "http://www.w3.org/1999/xhtml"
      ; "epub", "http://www.idpf.org/2007/ops"
      ]
    Element.create namespaces (doc.CreateNavigator ())
    |> Element.getFirstElement "html"
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
    |> Element.getFirstElement """body/nav[@epub:type="toc"]"""
    |> Result.map (DirContext<_>.Create' (getDirectory t) >> Toc.T)

  let getLandmarks t =
    t
    |> getElement
    |> Element.getFirstElement """body/nav[@epub:type="landmarks"]"""
    |> Result.map (DirContext<_>.Create' (getDirectory t) >> Landmarks.T)

  let getPageList t =
    t
    |> getElement
    |> Element.getFirstElement """body/nav[@epub:type="page-list"]"""
    |> Result.map (DirContext<_>.Create' (getDirectory t) >> PageList.T)
