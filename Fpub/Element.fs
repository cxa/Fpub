namespace Fpub

module Element =
  open Internal
  open System
  open System.Xml
  open System.Xml.XPath
  open System.Text.RegularExpressions

  [<Struct>]
  type private XPather =
    val navigator: XPathNavigator
    val namespaces: (string * string) list
    val namespaceManager: XmlNamespaceManager
    val defaultNamespacePrefix: string

    new (navigator: XPathNavigator, namespaces: (string * string) list) =
      let xnm = XmlNamespaceManager (NameTable ())
      namespaces |> List.iter (fun (prefix, uri) ->
        xnm.AddNamespace (prefix, uri))
      { navigator = navigator
        namespaces = namespaces
        namespaceManager = xnm
        defaultNamespacePrefix = namespaces |> List.head |> fst }

    member this.NormalizeXPath (xpath:string) =
      let defaultNsPrefix = this.defaultNamespacePrefix
      xpath.Split '|'
      |> Seq.map (fun x ->
        x.Trim().Split '/'
        |> Seq.map (
          function
          | "" -> ""
          | s ->
            if s.Contains "::" then
              let regex =
                Regex (@"(?<!attribute)(\:\:)([a-z*][\w\d-_\.]*)([=\s\[\]]|$)",
                       RegexOptions.IgnoreCase)
              regex.Replace(s, sprintf "$1%s:$2$3" defaultNsPrefix)
            else
              let regex1 =
                Regex (@"(^|\()([a-z*][\w\d-_\.]*)([\)\[]|$)",
                       RegexOptions.IgnoreCase)
              let s1 = regex1.Replace(s, sprintf "$1%s:$2$3" defaultNsPrefix)
              let regex2 =
                Regex (@"(\[)([a-z*][\w\d-_\.]*)([\]=])",
                       RegexOptions.IgnoreCase)
              regex2.Replace(s1, sprintf "$1%s:$2$3" defaultNsPrefix))
        |> String.concat "/"
      )
      |> String.concat "|"

  type T = private T of XPather

  let internal create namespaces navigator =
    T (XPather (navigator, namespaces))

  let getValue (T xpather) =
    xpather.navigator.Value.Trim ()

  let getAttribute (name:string) (T xpather) =
    Result.attempt <| fun () ->
      let attrName, nsUri =
        match Seq.toList (name.Split ':') with
        | [n] -> n, String.Empty
        | [prefix; n] ->
          match List.tryFind (fun (p, _) -> p = prefix) xpather.namespaces with
          | Some (_, uri) -> n, uri
          | _ -> failwith <| sprintf "Namespace %A is not registered" prefix
        | _ -> failwith <| sprintf "Invalid attribute name: %A" name
      match (xpather.navigator.GetAttribute (attrName, nsUri)).Trim() with
      | "" -> failwith <| sprintf "Attribute %A not found or empty" attrName
      | str -> str

  let eval xpath (T xpather) =
    Result.attempt <| fun () ->
      xpather.navigator.Evaluate
        (xpather.NormalizeXPath xpath, xpather.namespaceManager)

  let evalToString xpath element =
    eval xpath element
    |> Result.attemptMap (fun obj -> obj :?> string)

  let evalToDouble xpath element =
    eval xpath element
    |> Result.attemptMap (fun obj -> obj :?> double)

  let evalToBoolean xpath element =
    eval xpath element
    |> Result.attemptMap (fun obj -> obj :?> bool)

  let evalToElements xpath element =
    let (T xpather) = element
    eval xpath element
    |> Result.attemptMap (fun obj ->
      obj :?> XPathNodeIterator
      |> Seq.cast<XPathNavigator>
      |> Seq.map (create xpather.namespaces)
    )

  let getFirstElement xpath (T xpather) =
    Result.attempt
    <| fun () ->
      let node =
        xpather.navigator.SelectSingleNode
          (xpather.NormalizeXPath xpath, xpather.namespaceManager)
        |> Option.ofObj
      match node with
      | Some n -> create xpather.namespaces n
      | None -> failwith <| sprintf "No node found for %A" xpath

  let getAllElements xpath (T xpather) =
    Result.attempt (fun () ->
      xpather.navigator.Select
        (xpather.NormalizeXPath xpath, xpather.namespaceManager))
    |> Result.map (
      Seq.cast<XPathNavigator>
      >> Seq.map (create xpather.namespaces))
