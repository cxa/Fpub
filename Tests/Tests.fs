module Tests

open System.IO
open Expecto
open FSharp.Core
open Fpub
open Package

[<Tests>]
let tests =
  let makeValidContainer () =
    Container.withFile "assets/epub31-v31-20170105.epub"

  let makePackage () =
    makeValidContainer () |> Result.bind Container.getDefaultPackage

  let makeMetadata () =
    makeValidContainer ()
    |> Result.bind Container.getDefaultPackage
    |> Result.bind Package.getMetadata

  let getManifestItems () =
    makePackage ()
    |> Result.bind Package.getManifest
    |> Result.bind Package.Manifest.getItems

  let getCoverHref pkg =
    pkg
    |> Result.bind Package.getManifest
    |> Result.bind Package.Manifest.getCoverImageItem
    |> Result.bind Package.Manifest.Item.getHref

  testList "Fpub" [
    testList "Container" [
      testCase "read valid epub" <| fun _ ->
        Expect.isTrue
          (match (makeValidContainer ()) with Ok _ -> true | _ -> false )
          "Should read epub31-v31-20170105.epub successfully"

      testCase "read invald epub" <| fun _ ->
        let container = Container.withFile "assets/invalid.epub"
        let isInvalid =
          match container with
          | Ok _ -> false
          | Error e ->
            match e with
            | :? InvalidDataException -> true
            | _ -> false
        Expect.isTrue isInvalid "Should fail with InvalidDataException"

      testCase "get package path" <| fun _ ->
        let pathsR =
          makeValidContainer ()
          |> Result.bind Container.getPackagePaths
          |> Result.map Seq.toList
        match pathsR with
        | Ok paths -> 
          Expect.equal 
            (List.head paths) "EPUB/package.opf" "Should get package path"
        | Error e -> failtest e.Message

      testCase "get package" <| fun _ ->
        Expect.isOk (makePackage ()) "Should get package"

      testCase "get nav" <| fun _ ->
        let nav = makeValidContainer () |> Result.bind Container.getNav
        Expect.isOk nav "Should get nav"
    ]

    testList "Package" [
      testCase "get version" <| fun _ ->
        let vr = makePackage () |> Result.bind Package.getVersion
        match vr with
        | Ok v -> Expect.equal v 3.1 "should get version"
        | Error e -> failtest e.Message

      testCase "get uid" <| fun _ ->
        let uidr = makePackage () |> Result.bind Package.getUniqueIdentifier
        match uidr with
        | Ok uid -> Expect.equal uid "uid" "should get uid"
        | Error e -> failtest e.Message

      testCase "get nav doc path" <| fun _ ->
        let path =
          makePackage ()
          |> Result.bind Package.getNavDocPath
        match path with
        | Ok p -> Expect.equal p "EPUB/nav.xhtml" "should get nav doc path"
        | Error e -> failtest e.Message

      testCase "get ncx path" <| fun _ ->
        let path = makePackage () |> Result.bind Package.getNcxPath
        Expect.isError path "should be error for epub 3 that has no ncx"
        let pkg = Package.withFile "assets/epub2.opf" ""
        let path = pkg |> Result.bind Package.getNcxPath
        match path with
        | Ok p -> Expect.equal p "toc.ncx" "should get ncx path"
        | Error e -> failtest e.Message
    ]

    testList "Metadata" [
      testCase "get all identifiers" <| fun _ ->
        let pkg = Package.withFile "assets/test.opf" ""
        let idsR = 
          pkg 
          |> Result.bind Package.getMetadata 
          |> Result.bind Package.Metadata.getAllIdentifiers
        match idsR with
        | Ok ids -> 
          Expect.sequenceEqual 
            ids 
            (Seq.ofList ["org.idpf.epub31"; "9780123456789"])
            "should get all identifiers"
        | Error e -> failtest e.Message

      testCase "get scheme identifiers" <| fun _ ->
        let pkg = Package.withFile "assets/test.opf" ""
        let idsR = 
          pkg 
          |> Result.bind Package.getMetadata 
          |> Result.bind Package.Metadata.getSchemeIdentifiers
        match idsR with
        | Ok ids ->
          Expect.equal 
            (Map.find "isbn" ids) 
            "9780123456789" 
            "should contains uid in identifiers"
        | Error e -> failtest e.Message

      testCase "get unique identifier" <| fun _ ->
        let pkguid = 
          makeMetadata () |> Result.bind Package.Metadata.getUniqueIdentifier
        match pkguid with
        | Ok puid ->
          Expect.equal 
            puid "org.idpf.epub31" 
            <| "uid should be org.idpf.epub31, but got" + puid
        | Error e -> failtest e.Message

      testCase "get release identifier" <| fun _ ->
        let rid = 
          makeMetadata () |> Result.bind Package.Metadata.getReleaseIdentifier
        match rid with
        | Ok id ->
          Expect.equal
            id
            "org.idpf.epub31@2017-01-31T18:56:44Z"
            "should get release id"
        | Error e -> failtest e.Message

      testCase "get title" <| fun _ ->
        let title = makeMetadata () |> Result.bind Metadata.getTitle
        match title with
        | Ok t -> Expect.equal t "EPUB 3.1" "should get title"
        | Error e -> failtest e.Message

      testCase "get language" <| fun _ ->
        let lang = makeMetadata () |> Result.bind Metadata.getLanguage
        match lang with
        | Ok t -> Expect.equal t "en" "should get language"
        | Error e -> failtest e.Message

      testCase "get creators" <| fun _ ->
        let creators = makeMetadata () |> Result.bind Metadata.getCreators
        match creators with
        | Ok c -> 
          Expect.sequenceEqual 
            c 
            ["Markus Gylling"; "Tzviya Siegman"; "Matt Garrish"]
            "should get creators"
        | Error e -> failtest e.Message
    ]

    testList "Manifest" [
      testCase "get Manifetst Items" <| fun _ ->
        match getManifestItems () with
        | Ok items ->
          Expect.equal (Seq.length items) 16 "Manifest items should be 16"
        | Error e -> failtest e.Message

      testCase "get item attributes" <| fun _ ->
        let item = getManifestItems () |> Result.map Seq.head
        let id = item |> Result.bind Package.Manifest.Item.getId
        let href = item |> Result.bind Package.Manifest.Item.getHref
        let mt = item |> Result.bind Package.Manifest.Item.getMediaType
        let props = 
          item |> Result.bind (Package.Manifest.Item.getAttribute "properties")
        match id, href, mt, props with
        | Ok i, Ok h, Ok m, Ok p ->
          Expect.equal
            (i, h, m, p)
            ("nav", "nav.xhtml", "application/xhtml+xml", "nav")
            """item props should be ("nav", "nav.xhtml", "application/xhtml+xml", "nav")"""
        | _ -> failtest "Fail to get item id, href, media type, and properties"
        let fallback = 
          item |> Result.bind (Package.Manifest.Item.getAttribute "fallback")
        Expect.isError fallback "item fallback should be none"

      testCase "get item by id" <| fun _ ->
        let id = "res010"
        let idr =
          makePackage ()
          |> Result.bind Package.getManifest
          |> Result.bind (Package.Manifest.getItemById id)
          |> Result.bind Package.Manifest.Item.getId
        match idr with
        | Ok i -> 
          Expect.equal
            i id
            <| sprintf "get item by id %A, resutl item id should be %A" id id
        | _ -> failtest "Fail to get item by id"

      testCase "get epub 3 cover image href" <| fun _ ->
        let href = Package.withFile "assets/test.opf" "" |> getCoverHref
        match href with
        | Ok h -> Expect.equal h "cover.jpg" "should get cover image href"
        | Error e -> failtest e.Message

      testCase "get epub 2 cover image href" <| fun _ ->
        let href = Package.withFile "assets/epub2.opf" "" |> getCoverHref
        match href with
        | Ok h -> Expect.equal h "cover.png" "should get cover image href"
        | Error e -> failtest e.Message
    ]

    testList "Spine" [
      testCase "get spine page progression direction" <| fun _ ->
        let dir =
          makePackage ()
          |> Result.bind Package.getSpine
          |> Result.map Package.Spine.getPageProgressionDirection
        match dir with
        | Ok d ->
          Expect.equal
            d
            Package.Spine.PageProgressionDirection.Default
            "Spine page progression direction shoulb be default"
        | Error e -> failtest e.Message

      testCase "get spine itemrefs" <| fun _ ->
         let itemrefsR =
          makePackage ()
          |> Result.bind Package.getSpine
          |> Result.bind Package.Spine.getItemRefs
         match itemrefsR with
         | Ok refs ->
           Expect.equal (Seq.length refs) 10 "number of item refs should be 10"
         | Error e -> failtest e.Message
    ]

    testList "Nav" [
      testCase "init nav" <| fun _ ->
        Nav.withFile "assets/nav.xhtml" ""
        |> Flip.Expect.isOk "should init nav"

      testCase "get toc" <| fun _ ->
        Nav.withFile "assets/nav.xhtml" ""
        |> Result.bind Nav.getToc
        |> Flip.Expect.isOk "should get toc"

      testCase "get toc heading" <| fun _ ->
        let title =
          Nav.withFile "assets/nav.xhtml" ""
          |> Result.bind Nav.getToc
          |> Result.bind Nav.Toc.getHeadingTitle
        match title with
        | Ok t -> Expect.equal t "THE CONTENTS" "should get nav headings"
        | Error e -> failtest e.Message

      testCase "get all toc items" <| fun _ ->
        let items =
          Nav.withFile "assets/nav.xhtml" ""
          |> Result.bind Nav.getToc
          |> Result.map Nav.Toc.getItems

        match items with
        | Ok items ->
          Expect.equal 
            (Option.get items.[0].ResourcePath)
            "s04.xhtml#pgepubid00492" "should get first item"
          let item = items.[0].SubItems.[2]
          Expect.equal item.Title "Abram S. Isaacs" "should 0 2 0 item"
        | Error e -> failtest e.Message

      testCase "get toc item resource path" <| fun _ ->
        let container = makeValidContainer ()
        let items =
          container
          |> Result.bind Container.getNav
          |> Result.bind Nav.getToc
          |> Result.map Nav.Toc.getItems
        match items with
        | Ok items ->
          let item = items.[1]
          let path = Option.get item.ResourcePath
          Expect.equal 
            path "EPUB/31/spec/epub-overview.html" "should get resource path"
          let str =
            container
            |> Result.bind (Container.getResource path)
            |> Result.map (fun s ->
              use reader = new StreamReader (s, System.Text.Encoding.UTF8)
              reader.ReadToEnd ()
            )
          match str with
          | Ok s -> 
            Expect.stringStarts 
              s """<?xml version="1.0" encoding="UTF-8"?>"""
              "should read toc item content"
          | Error e -> failtest e.Message
        | Error e -> failtest e.Message

      testCase "landmarks" <| fun _ ->
        let lm =
          Nav.withFile "assets/nav.xhtml" ""
          |> Result.bind Nav.getLandmarks
        let hd = lm |> Result.bind Nav.Landmarks.getHeadingTitle
        match hd with
        | Ok h -> Expect.equal h "Guide" "should get landmark heading title"
        | Error e -> failtest e.Message
        let items = lm |> Result.map Nav.Landmarks.getItems
        match items with
        | Ok i ->
          Expect.equal (Array.length i) 2 "should get all items"
          Expect.equal i.[1].Title "Begin Reading" "should get 2nd item"
        | Error e -> failtest e.Message

      testCase "page list" <| fun _ ->
        let pl =
          Nav.withFile "assets/nav.xhtml" ""
          |> Result.bind Nav.getPageList
        let hd = pl |> Result.bind Nav.PageList.getHeadingTitle
        match hd with
        | Ok h -> Expect.equal h "Pages" "should get pagelist heading title"
        | Error e -> failtest e.Message
        let pages = pl |> Result.map Nav.PageList.getPages
        match pages with
        | Ok p ->
          Expect.equal 
            (Map.find "174" p) "s04.xhtml#Page_174" "should get page 174"
        | Error e -> failtest e.Message
    ]

    testList "Ncx" [
      testCase "toc" <| fun _ ->
        let items =
          Ncx.withFile "assets/epub2.ncx" ""
          |> Result.bind Ncx.getToc
          |> Result.map Ncx.Toc.getItems
        match items with
        | Ok items ->
          Expect.equal (Array.length items) 2 "should get all items"
          Expect.equal 
            items.[0].SubItems.[0].Title "Chapter 1.1" "should get 0 1 title"
          Expect.equal 
            (Option.get items.[0].SubItems.[0].ResourcePath)
            "content.html#ch_1_1" "should get 0 0 href"
        | Error e -> failtest e.Message

      testCase "page list" <| fun _ ->
        let pages =
          Ncx.withFile "assets/epub2.ncx" ""
          |> Result.bind Ncx.getPageList
          |> Result.map Ncx.PageList.getPages
        match pages with
        | Ok p ->
          Expect.equal (Map.find "2" p) "content.html#p2" "should get page 2"
        | Error e -> failtest e.Message
    ]
  ]
