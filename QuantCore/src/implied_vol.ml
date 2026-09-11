open Pricing

type iv_result = {
  volatility: float;
  iterations: int;
  converged: bool;
  error: float;
}

let solve
  ~market_price ~spot ~strike ~maturity ~rate ~option_type
  ?initial_vol ?max_iterations ?tolerance () =
  let initial_vol = match initial_vol with Some v -> v | None -> 0.2 in
  let max_iterations = match max_iterations with Some v -> v | None -> 100 in
  let tolerance = match tolerance with Some v -> v | None -> 1e-6 in
  begin
    if market_price <= 0.0 then
      invalid_arg "Market price must be positive";
    if market_price > spot && option_type = Call then
      invalid_arg (Printf.sprintf "Call price %.2f exceeds spot %.2f (impossible)" market_price spot);

    let rec iterate sigma iteration =
      if iteration > max_iterations then
        { volatility = sigma; iterations = iteration; converged = false;
          error = Float.nan }
      else
        let input = {
          spot;
          strike;
          maturity;
          rate;
          volatility = sigma;
          option_type;
        } in

        try
          let result = price_option input in
          let model_price = result.price in
          let residual = model_price -. market_price in

          if Float.abs residual < tolerance then
            { volatility = sigma; iterations = iteration; converged = true;
              error = Float.abs residual }
          else
            let vega = result.vega in
            if Float.abs vega < 1e-10 then
              { volatility = sigma; iterations = iteration; converged = false;
                error = Float.abs residual }
            else
              let sigma_new = sigma -. residual /. vega in
              let sigma_bounded = Float.max 0.001 (Float.min 3.0 sigma_new) in
              iterate sigma_bounded (iteration + 1)
        with _ ->
          { volatility = sigma; iterations = iteration; converged = false;
            error = Float.nan }
    in
    iterate initial_vol 0
  end

let solve_bisection
  ~market_price ~spot ~strike ~maturity ~rate ~option_type
  ?vol_min ?vol_max ?tolerance () =
  let vol_min = match vol_min with Some v -> v | None -> 0.001 in
  let vol_max = match vol_max with Some v -> v | None -> 3.0 in
  let tolerance = match tolerance with Some v -> v | None -> 1e-6 in
  begin
    let objective vol =
      let input = {
        spot; strike; maturity; rate;
        volatility = vol; option_type;
      } in
      let result = price_option input in
      result.price -. market_price
    in

    let rec bisect left right iteration =
      if iteration > 100 then
        { volatility = (left +. right) /. 2.0; iterations = iteration;
          converged = false; error = Float.nan }
      else
        let mid = (left +. right) /. 2.0 in
        let f_mid = objective mid in
        let error = Float.abs f_mid in

        if error < tolerance then
          { volatility = mid; iterations = iteration; converged = true; error }
        else
          let f_left = objective left in
          if (f_left > 0.0) = (f_mid > 0.0) then
            bisect mid right (iteration + 1)
          else
            bisect left mid (iteration + 1)
    in

    try
      bisect vol_min vol_max 0
    with _ ->
      { volatility = (vol_min +. vol_max) /. 2.0; iterations = 0;
        converged = false; error = Float.nan }
  end
