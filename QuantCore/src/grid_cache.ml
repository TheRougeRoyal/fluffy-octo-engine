(* Remove unused open Grid *)

type cache_stats = {
  cache_size: int;
  hits: int;
  misses: int;
  total: int;
  hit_rate_pct: float;
}

(* Internal: thread-safe grid cache *)
let grid_cache = Hashtbl.create 1000
let cache_hits = ref 0
let cache_misses = ref 0
let cache_lock = Mutex.create ()

(* Key: use string representation to handle float precision (6 decimal places) *)
let grid_cache_key ~s_min ~s_max ~n_s ~n_t =
  Printf.sprintf "%.6f:%.6f:%d:%d" s_min s_max n_s n_t

let get_or_create_grid ~s_min ~s_max ~n_s ~n_t =
  let key = grid_cache_key ~s_min ~s_max ~n_s ~n_t in
  Mutex.lock cache_lock;
  try
    match Hashtbl.find_opt grid_cache key with
    | Some grid ->
      incr cache_hits;
      Mutex.unlock cache_lock;
      grid
    | None ->
      Mutex.unlock cache_lock;
      (* Build grid outside lock to avoid blocking other lookups during expensive construction *)
      let grid = Grid.make ~s_min ~s_max ~n_s ~n_t () in
      Mutex.lock cache_lock;
      Hashtbl.replace grid_cache key grid;
      incr cache_misses;
      Mutex.unlock cache_lock;
      grid
  with exn ->
    (try Mutex.unlock cache_lock with _ -> ());
    Grid.make ~s_min ~s_max ~n_s ~n_t ()

let cache_stats () =
  Mutex.lock cache_lock;
  try
    let hits = !cache_hits in
    let misses = !cache_misses in
    let total = hits + misses in
    let hit_rate = if total = 0 then 0.0 else float_of_int hits /. float_of_int total *. 100.0 in
    let stats = {
      cache_size = Hashtbl.length grid_cache;
      hits;
      misses;
      total;
      hit_rate_pct = hit_rate;
    } in
    Mutex.unlock cache_lock;
    stats
  with _ ->
    Mutex.unlock cache_lock;
    { cache_size=0; hits=0; misses=0; total=0; hit_rate_pct=0.0 }

let clear_cache () =
  Mutex.lock cache_lock;
  Hashtbl.clear grid_cache;
  cache_hits := 0;
  cache_misses := 0;
  Mutex.unlock cache_lock
