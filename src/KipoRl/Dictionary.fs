module Dictionary

open System
open System.Collections.Generic

let inline tryFindV (key: 'K) (dict: Dictionary<'K, 'V>) : 'V voption =
  match dict.TryGetValue(key) with
  | true, v -> ValueSome v
  | false, _ -> ValueNone

let inline findV (key: 'K) (dict: Dictionary<'K, 'V>) : 'V =
  match dict.TryGetValue(key) with
  | true, v -> v
  | false, _ -> raise(KeyNotFoundException($"Key not found: {key}"))

let inline containsKey (key: 'K) (dict: Dictionary<'K, 'V>) : bool =
  dict.ContainsKey(key)

let inline removeWhere
  ([<InlineIfLambda>] predicate: 'K -> 'V -> bool)
  (dict: Dictionary<'K, 'V>)
  : int =
  let mutable removed = 0
  let toRemove = ResizeArray<'K>()

  for KeyValue(k, v) in dict do
    if predicate k v then
      toRemove.Add(k)

  for k in toRemove do
    if dict.Remove(k) then
      removed <- removed + 1

  removed

let inline iterKV
  ([<InlineIfLambda>] action: 'K -> 'V -> unit)
  (dict: Dictionary<'K, 'V>)
  : unit =
  for KeyValue(k, v) in dict do
    action k v

let inline foldKV
  ([<InlineIfLambda>] folder: 'S -> 'K -> 'V -> 'S)
  (state: 'S)
  (dict: Dictionary<'K, 'V>)
  : 'S =
  let mutable acc = state

  for KeyValue(k, v) in dict do
    acc <- folder acc k v

  acc

let inline toArray(dict: Dictionary<'K, 'V>) : struct ('K * 'V)[] =
  let result = Array.zeroCreate<struct ('K * 'V)>(dict.Count)
  let mutable i = 0

  for KeyValue(k, v) in dict do
    result.[i] <- struct (k, v)
    i <- i + 1

  result

let inline countWhere
  ([<InlineIfLambda>] predicate: 'K -> 'V -> bool)
  (dict: Dictionary<'K, 'V>)
  : int =
  let mutable count = 0

  for KeyValue(k, v) in dict do
    if predicate k v then
      count <- count + 1

  count

let inline tryFindOrDefault
  (key: 'K)
  (defaultValue: 'V)
  (dict: Dictionary<'K, 'V>)
  : 'V =
  match dict.TryGetValue(key) with
  | true, v -> v
  | false, _ -> defaultValue
