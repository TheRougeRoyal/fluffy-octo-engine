open Pde_opt

let assert_close ~name ~expected ~actual ~tolerance =
  if Float.abs (actual -. expected) > tolerance then
    failwith
      (Printf.sprintf "%s expected %.12g, got %.12g (tolerance %.12g)"
         name expected actual tolerance)

let test_rannacher_gamma () =
  let spot = 100.0 in
  let strike = 100.0 in
  let maturity = 0.02 in
  let rate = 0.01 in
  let volatility = 0.2 in
  let params = Bs_params.make ~r:rate ~sigma:volatility ~k:strike ~t:maturity in
  let grid = Grid.make ~s_min:50.0 ~s_max:150.0 ~n_s:400 ~n_t:40 () in
  let solution =
    Pde1d.solve_european ~params ~grid ~payoff:`Call ~scheme:`CN
  in
  let eps = Grid.ds grid in
  let price_up = Pde1d.interpolate_at ~grid ~values:solution ~s:(spot +. eps) in
  let price = Pde1d.interpolate_at ~grid ~values:solution ~s:spot in
  let price_down = Pde1d.interpolate_at ~grid ~values:solution ~s:(spot -. eps) in
  let gamma = (price_up -. 2.0 *. price +. price_down) /. (eps *. eps) in
  let analytic_gamma =
    Payoff.analytic_black_scholes_gamma
      ~r:rate ~sigma:volatility ~t:maturity ~s0:spot ~k:strike
  in
  assert_close ~name:"Rannacher near-expiry gamma"
    ~expected:analytic_gamma ~actual:gamma ~tolerance:0.02

let test_price_option_regression () =
  let input : Pricing.pricing_input =
    {
      spot = 100.0;
      strike = 100.0;
      maturity = 1.0;
      rate = 0.05;
      volatility = 0.2;
      option_type = Pricing.Call;
    }
  in
  let output = Pricing.price_option input in
  let expected = [
    ("price", 10.450409776358937, output.price);
    ("analytic_price", 10.450575619322287, output.analytic_price);
    ("error", 0.00016584296334976045, output.error);
    ("delta", 0.63792185082570985, output.delta);
    ("gamma", 0.02015969370602555, output.gamma);
    ("theta", -6.4244287842194936, output.theta);
    ("vega", 37.573902448700025, output.vega);
    ("rho", 0.532286, output.rho);
  ] in
  List.iter
    (fun (name, expected_value, actual) ->
      let tol = if name = "rho" then 0.5 else 1e-8 in
      assert_close ~name ~expected:expected_value ~actual ~tolerance:tol)
    expected

let test_rho_validation () =
  let input_call : Pricing.pricing_input =
    {
      spot = 100.0;
      strike = 100.0;
      maturity = 1.0;
      rate = 0.05;
      volatility = 0.2;
      option_type = Pricing.Call;
    } in
  let res_call = Pricing.price_option input_call in
  let analytic_rho_call = Payoff.analytic_black_scholes_rho `Call
    ~r:input_call.rate ~sigma:input_call.volatility ~t:input_call.maturity
    ~s0:input_call.spot ~k:input_call.strike /. 100.0 in
  assert_close ~name:"Call rho"
    ~expected:analytic_rho_call ~actual:res_call.rho ~tolerance:0.1;

  let input_put : Pricing.pricing_input =
    {
      spot = input_call.spot;
      strike = input_call.strike;
      maturity = input_call.maturity;
      rate = input_call.rate;
      volatility = input_call.volatility;
      option_type = Pricing.Put;
    } in
  let res_put = Pricing.price_option input_put in
  let analytic_rho_put = Payoff.analytic_black_scholes_rho `Put
    ~r:input_put.rate ~sigma:input_put.volatility ~t:input_put.maturity
    ~s0:input_put.spot ~k:input_put.strike /. 100.0 in
  assert_close ~name:"Put rho"
    ~expected:analytic_rho_put ~actual:res_put.rho ~tolerance:0.1;

  assert (res_call.rho > 0.0);
  assert (res_put.rho < 0.0)

let test_rho_low_vol () =
  let input : Pricing.pricing_input =
    {
      spot = 100.0;
      strike = 100.0;
      maturity = 1.0;
      rate = 0.05;
      volatility = 0.05;
      option_type = Pricing.Call;
    } in
  let res = Pricing.price_option input in
  let analytic_rho = Payoff.analytic_black_scholes_rho `Call
    ~r:input.rate ~sigma:input.volatility ~t:input.maturity
    ~s0:input.spot ~k:input.strike /. 100.0 in
  assert_close ~name:"Low vol rho"
    ~expected:analytic_rho ~actual:res.rho ~tolerance:0.1

let test_implied_vol () =
  print_endline "Starting IV tests...";
  let spot = 100.0 in
  let strike = 100.0 in
  let maturity = 1.0 in
  let rate = 0.05 in
  let true_vol = 0.2 in
  let option_type = Pricing.Call in

  (* 1. Basic Convergence *)
  let market_price =
    let input : Pricing.pricing_input = {
      spot = spot; strike = strike; maturity = maturity; rate = rate;
      volatility = true_vol; option_type = option_type;
    } in
    (Pricing.price_option input).price
  in

  let res = Implied_vol.solve
    ~market_price ~spot ~strike ~maturity ~rate ~option_type
    ~initial_vol:0.3 () in

  assert (res.converged);
  assert_close ~name:"IV Convergence"
    ~expected:true_vol ~actual:res.volatility ~tolerance:1e-4;

  (* 2. Robustness: Newton vs Bisection *)
  let res_bisect = Implied_vol.solve_bisection
    ~market_price ~spot ~strike ~maturity ~rate ~option_type () in
  assert (res_bisect.converged);
  assert_close ~name:"IV Bisection Convergence"
    ~expected:true_vol ~actual:res_bisect.volatility ~tolerance:1e-4;

  (* 3. Extreme Case: Low Volatility *)
  let low_vol = 0.02 in
  let market_price_low =
    let input : Pricing.pricing_input = {
      spot = spot; strike = strike; maturity = maturity; rate = rate;
      volatility = low_vol; option_type = option_type;
    } in
    (Pricing.price_option input).price
  in
  let res_low = Implied_vol.solve
    ~market_price:market_price_low ~spot ~strike ~maturity ~rate ~option_type () in
  assert (res_low.converged);
  assert_close ~name:"IV Low Vol Convergence"
    ~expected:low_vol ~actual:res_low.volatility ~tolerance:0.01;

  (* 4. Extreme Case: High Volatility *)
  let high_vol = 0.8 in
  let market_price_high =
    let input : Pricing.pricing_input = {
      spot = spot; strike = strike; maturity = maturity; rate = rate;
      volatility = high_vol; option_type = option_type;
    } in
    (Pricing.price_option input).price
  in
  let res_high = Implied_vol.solve
    ~market_price:market_price_high ~spot ~strike ~maturity ~rate ~option_type () in
  assert (res_high.converged);
  assert_close ~name:"IV High Vol Convergence"
    ~expected:high_vol ~actual:res_high.volatility ~tolerance:1e-4;

  (* 5. Out of bounds: Impossible price *)
  let impossible_price = 200.0 in (* Call price cannot exceed spot *)
  try
    let _ = Implied_vol.solve
      ~market_price:impossible_price ~spot ~strike ~maturity ~rate ~option_type () in
    failwith "Should have failed for impossible price"
  with Invalid_argument _ -> ()

let () =
  print_endline "Running Rannacher gamma test...";
  test_rannacher_gamma ();
  print_endline "Running regression test...";
  test_price_option_regression ();
  print_endline "Running rho validation test...";
  test_rho_validation ();
  print_endline "Running low vol rho test...";
  test_rho_low_vol ();
  print_endline "Running IV tests...";
  test_implied_vol ();
  print_endline "QuantCore tests passed"
