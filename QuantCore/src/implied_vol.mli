type iv_result = {
  volatility: float;      (** Implied volatility *)
  iterations: int;        (** Number of iterations *)
  converged: bool;        (** Did it converge? *)
  error: float;           (** |model_price - market_price| *)
}

(** Newton-Raphson implied volatility solver
    @param market_price Market-observed option price
    @param spot Current asset price
    @param strike Strike price
    @param maturity Time to maturity (years)
    @param rate Risk-free rate
    @param option_type Call or Put
    @param initial_vol Starting guess for vol (default 0.2 = 20%)
    @param max_iterations Max iterations (default 100)
    @param tolerance Convergence tolerance (default 1e-6)
    @return Implied volatility and convergence info
*)
val solve :
  market_price:float -> spot:float -> strike:float -> maturity:float ->
  rate:float -> option_type:Pricing.option_type ->
  ?initial_vol:float -> ?max_iterations:int -> ?tolerance:float ->
  unit -> iv_result

(** Bisection implied volatility solver (more robust but slower)
    @param market_price Market-observed option price
    @param vol_min Minimum vol search bound (default 0.1%)
    @param vol_max Maximum vol search bound (default 300%)
    @param tolerance Convergence tolerance (default 1e-6)
*)
val solve_bisection :
  market_price:float -> spot:float -> strike:float -> maturity:float ->
  rate:float -> option_type:Pricing.option_type ->
  ?vol_min:float -> ?vol_max:float -> ?tolerance:float ->
  unit -> iv_result
