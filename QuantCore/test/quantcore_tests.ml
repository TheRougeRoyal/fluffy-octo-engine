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

let () =
  test_rannacher_gamma ();
  print_endline "QuantCore tests passed"
