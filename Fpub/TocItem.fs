namespace Fpub

type TocItem =
  { Title: string
    ResourcePath: string option
    SubItems: TocItem array }
