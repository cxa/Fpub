namespace Fpub

type TocItem =
  { title: string
  ; resourcePath: string option
  ; subitems: TocItem array
  }
