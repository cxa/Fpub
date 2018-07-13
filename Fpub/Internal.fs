namespace Fpub

module internal Internal =
  module Result =
    let attempt f =
      try Ok <| f ()
      with exn -> Error exn

    let attemptMap f r =
      r |> Result.bind (fun a ->
        try Ok <| f a
        with e -> Error e
      )

    let attemptBind f r =
      r |> Result.bind (fun a ->
        try f a
        with e -> Error e
      )

    let getOr ``default`` r =
      match r with
      | Ok o -> o
      | _ -> ``default``

    let mapOr f ``default`` r =
      match r with
      | Ok o -> f o
      | _ -> ``default``

    let mapOr2 f ``default`` r1 r2 =
      match r1, r2 with
      | Ok o1, Ok o2 -> f o1 o2
      | _ -> ``default``

  type DirContext<'a> =
    { context: 'a
    ; dir: string
    }

    static member Create context dir =
      { context = context
      ; dir = dir
      }

    static member Create' dir context =
      { context = context
      ; dir = dir
      }

  let flip f a b =
    f b a
