# Fpub

Library for reading EPUB file format built on top of .NET Standard 2.0 and F#.

Safe, simple and extendable.

Available on NuGet: <https://www.nuget.org/packages/com.realazy.fpub/>

## Simple things should be simple

```fsharp
module Container = begin
  type T

  // Open EPUB
  val withStream : stream:System.IO.Stream -> Result<T,exn>
  val withFile : file:string -> Result<T,exn>

  // Get resource from EPUB with path
  val getResource : T -> path:string -> Result<System.IO.Stream,exn>

  // Get EPUB package document
  val getPackagePaths : container:T -> Result<seq<string>,exn>
  val getDefaultPackage : container:T -> Result<Package.T,exn>

  // Get EPUB navigation document
  val getNav : container:T -> Result<Nav.T,exn>

  // Get EPUB 2 NCX document (until navigation document is missing)
  val getNcx : container:T -> Result<Ncx.T,exn>
end

// Read Package Document
module Package = begin
  module Metadata = begin
    type T
    val getElement : T -> Element.T
    val getUniqueIdentifier : T -> Result<string,exn>
    val getReleaseIdentifier : metadata:T -> Result<string,exn>
    val getAllIdentifiers : T -> Result<seq<string>,exn>
    val getSchemeIdentifiers : T -> Result<Map<string,string>,exn>
    val getTitle : T -> Result<string,exn>
    val getLanguage : T -> Result<string,exn>
    val getCreators : T -> Result<seq<string>,exn>
    val getContributors : T -> Result<seq<string>,exn>
    val getDate : T -> Result<System.DateTime,exn>
  end

  module Manifest = begin
    type T
    val getElement : T -> Element.T
    module Item = begin
      type T
      val getElement : T -> Element.T
      val getAttribute : attribute:string -> T -> Result<string,exn>
      val getId : item:T -> Result<string,exn>
      val getHref : item:T -> Result<string,exn>
      val getMediaType : item:T -> Result<string,exn>
      val getResourcePath : item:T -> Result<string,exn>
    end
    val getItems : T -> Result<seq<Item.T>,exn>
    val getItemById : id:string -> T -> Result<Item.T,exn>
    val getNavItem : T -> Result<Item.T,exn>
    val getCoverImageItem : item:T -> Result<Item.T,exn>
  end
  module Spine = begin
    type T
    val getElement : T -> Element.T
    module ItemRef = begin
      type T
      val getElement : T -> Element.T
      val getId : T -> Result<string,exn>
      val getLinear : T -> Result<bool,exn>
      val getProperties : T -> Result<string,exn>
      val getIdref : T -> Result<string,exn>
      val getManifestItem :
        itemRef:T -> element:Manifest.T -> Result<Manifest.Item.T,exn>
    end
    module PageProgressionDirection = begin
      type T =
        | Ltr
        | Rtl
        | Default
    end
    val getPageProgressionDirection : T -> PageProgressionDirection.T
    val getItemRefs : T -> Result<seq<ItemRef.T>,exn>
  end
  type T
  val withFile : uri:string -> pkgDir:string -> Result<T,exn>
  val withStream : stream:System.IO.Stream -> pkgDir:string -> Result<T,exn>
  val getElement : T -> Element.T
  val getVersion : T -> Result<double,exn>
  val getUniqueIdentifier : T -> Result<string,exn>
  val getMetadata : T -> Result<Metadata.T,exn>
  val getManifest : T -> Result<Manifest.T,exn>
  val getSpine : T -> Result<Spine.T,exn>
  val getNavDocPath : T -> Result<string,exn>
  val getNcxPath : package:T -> Result<string,exn>
end

// Navigating

type TocItem = // Table of contents item
  {title: string;
    resourcePath: string option;
    subitems: TocItem array;}

// Use EPUB3 navigation document
module Nav = begin
  module Toc = begin
    type T
    val getHeadingTitle : T -> Result<string,exn>
    val getItems : T -> TocItem []
  end
  module Landmarks = begin
    type Item =
      {title: string;
        type: string;
        resourcePath: string;
        subitems: Item array;}
    type T
    val getHeadingTitle : T -> Result<string,exn>
    val getItems : T -> Item []
  end
  module PageList = begin
    type T
    val getHeadingTitle : T -> Result<string,exn>
    val getPages : T -> Map<string,string>
  end
  type T
  val withFile : uri:string -> docDir:string -> Result<T,exn>
  val withStream : stream:System.IO.Stream -> docDir:string -> Result<T,exn>
  val getElement : T -> Element.T
  val getToc : T -> Result<Toc.T,exn>
  val getLandmarks : T -> Result<Landmarks.T,exn>
  val getPageList : T -> Result<PageList.T,exn>
end

// Use EPUB2 Ncx when EPUB3 navigation documen is missing
module Ncx = begin
  module Toc = begin
    type T
    val getItems : T -> TocItem []
  end
  module PageList = begin
    type T
    val getPages : T -> Map<string,string>
  end
  type T
  val withFile : uri:string -> docDir:string -> Result<T,exn>
  val withStream : stream:System.IO.Stream -> docDir:string -> Result<T,exn>
  val getElement : T -> Element.T
  val getToc : T -> Result<Toc.T,exn>
  val getPageList : T -> Result<PageList.T,exn>
end
```

## Complex things should be possible

`Package`, `Nav`, `Ncx` and their submodules are abstracted in `Element.T`, which is an XML Element. Using `Element` to retrieve any information you needed.

```fsharp
module Element = begin
  type T
  val getValue : T -> string
  val getAttribute : name:string -> T -> Result<string,exn>
  val eval : xpath:string -> T -> Result<obj,exn>
  val evalToString : xpath:string -> element:T -> Result<string,exn>
  val evalToDouble : xpath:string -> element:T -> Result<double,exn>
  val evalToBoolean : xpath:string -> element:T -> Result<bool,exn>
  val evalToElements : xpath:string -> element:T -> Result<seq<T>,exn>
  val getFirstElement : xpath:string -> T -> Result<T,exn>
  val getAllElements : xpath:string -> T -> Result<seq<T>,exn>
end
```

## License

MIT

## Author

- Blog: [realazy.com](https://realazy.com) (Chinese)
- Github: [@cxa](https://github.com/cxa)
- Twitter: [@\_cxa](https://twitter.com/_cxa) (Chinese mainly)
