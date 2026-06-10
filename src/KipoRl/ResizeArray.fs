module ResizeArray

open System
open System.Collections.Generic

let inline chooseV
  ([<InlineIfLambda>] chooser: 'T -> 'U voption)
  (source: ResizeArray<'T>)
  : ResizeArray<'U> =
  let result = ResizeArray<'U>()

  for i = 0 to source.Count - 1 do
    match chooser source[i] with
    | ValueSome v -> result.Add v
    | ValueNone -> ()

  result

let inline filterV
  ([<InlineIfLambda>] predicate: 'T -> bool)
  (source: ResizeArray<'T>)
  : ResizeArray<'T> =
  let result = ResizeArray<'T>()

  for i = 0 to source.Count - 1 do
    let item = source.[i]

    if predicate item then
      result.Add item

  result

let inline filterVInPlace
  ([<InlineIfLambda>] predicate: 'T -> bool)
  (source: ResizeArray<'T>)
  : unit =
  let mutable writeIdx = 0

  for readIdx = 0 to source.Count - 1 do
    let item = source.[readIdx]

    if predicate item then
      source.[writeIdx] <- item
      writeIdx <- writeIdx + 1

  if writeIdx < source.Count then
    source.RemoveRange(writeIdx, source.Count - writeIdx)

let inline mapV
  ([<InlineIfLambda>] mapper: 'T -> 'U)
  (source: ResizeArray<'T>)
  : ResizeArray<'U> =
  let result = ResizeArray<'U> source.Count

  for i = 0 to source.Count - 1 do
    result.Add(mapper source.[i])

  result

let inline mapVInPlace
  ([<InlineIfLambda>] mapper: 'T -> 'T)
  (source: ResizeArray<'T>)
  : unit =
  for i = 0 to source.Count - 1 do
    source.[i] <- mapper source.[i]

let inline tryFindV
  ([<InlineIfLambda>] predicate: 'T -> bool)
  (source: ResizeArray<'T>)
  : 'T voption =
  let mutable result = ValueNone
  let mutable i = 0

  while i < source.Count && result.IsNone do
    let item = source.[i]

    if predicate item then
      result <- ValueSome item

    i <- i + 1

  result

let inline existsV
  ([<InlineIfLambda>] predicate: 'T -> bool)
  (source: ResizeArray<'T>)
  : bool =
  let mutable found = false
  let mutable i = 0

  while i < source.Count && not found do
    if predicate source.[i] then
      found <- true

    i <- i + 1

  found

let inline iterV
  ([<InlineIfLambda>] action: 'T -> unit)
  (source: ResizeArray<'T>)
  : unit =
  for i = 0 to source.Count - 1 do
    action source.[i]

let inline foldV
  ([<InlineIfLambda>] folder: 'S -> 'T -> 'S)
  (state: 'S)
  (source: ResizeArray<'T>)
  : 'S =
  let mutable acc = state

  for i = 0 to source.Count - 1 do
    acc <- folder acc source.[i]

  acc

let inline collectV
  ([<InlineIfLambda>] mapper: 'T -> 'U[])
  (source: ResizeArray<'T>)
  : ResizeArray<'U> =
  let result = ResizeArray<'U>()

  for i = 0 to source.Count - 1 do
    let items = mapper source[i]

    for j = 0 to items.Length - 1 do
      result.Add items[j]

  result
