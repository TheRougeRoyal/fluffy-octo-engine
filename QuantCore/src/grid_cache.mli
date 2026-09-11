type cache_stats = {
  cache_size: int;
  hits: int;
  misses: int;
  total: int;
  hit_rate_pct: float;
}

val get_or_create_grid : s_min:float -> s_max:float -> n_s:int -> n_t:int -> Grid.t
val cache_stats : unit -> cache_stats
val clear_cache : unit -> unit
