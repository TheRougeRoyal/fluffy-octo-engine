open Pde_opt

(* JSON pricing binary - reads params from stdin JSON, outputs JSON result *)

let () =
  let input = Stdlib.input_line stdin in
  let json = Yojson.Basic.from_string input in
  let open Yojson.Basic.Util in

  (* Helper to handle both int and float JSON values *)
  let to_number j = match j with
    | `Int i -> float_of_int i
    | `Float f -> f
    | _ -> raise (Type_error ("Expected number", j))
  in

  let to_float_option j = match j with
    | `Int i -> Some (float_of_int i)
    | `Float f -> Some f
    | _ -> None
  in

  let request_type = json |> member "requestType" |> to_string_option in

  match request_type with
  | Some "impliedVol" ->
      let market_price = json |> member "marketPrice" |> to_number in
      let spot = json |> member "spot" |> to_number in
      let strike = json |> member "strike" |> to_number in
      let maturity = json |> member "maturity" |> to_number in
      let rate = json |> member "rate" |> to_number in
      let option_type_str = json |> member "optionType" |> to_string in
      let initial_vol = json |> member "initialVol" |> to_float_option in

      let option_type = match String.lowercase_ascii option_type_str with
        | "call" -> Pricing.Call
        | "put" -> Pricing.Put
        | _ -> Pricing.Call
      in

      begin
        try
          let result = Implied_vol.solve
            ~market_price ~spot ~strike ~maturity ~rate ~option_type
            ?initial_vol () in

          let model_price =
            let input = Pricing.{
              spot; strike; maturity; rate;
              volatility = result.volatility; option_type;
            } in
            (Pricing.price_option input).price
          in

          let output = `Assoc [
            ("success", `Bool result.converged);
            ("impliedVol", `Float result.volatility);
            ("iterations", `Int result.iterations);
            ("modelPrice", `Float model_price);
            ("marketPrice", `Float market_price);
            ("error", `Float result.error);
          ] in

          print_string (Yojson.Basic.to_string output);
          print_newline ()
        with Invalid_argument msg ->
          let error = `Assoc [
            ("success", `Bool false);
            ("error", `String msg);
          ] in
          print_string (Yojson.Basic.to_string error);
          print_newline ()
        | exn ->
          let error = `Assoc [
            ("success", `Bool false);
            ("error", `String (Printexc.to_string exn));
          ] in
          print_string (Yojson.Basic.to_string error);
          print_newline ()
      end

  | Some _ ->
      let spot = json |> member "spot" |> to_number in
      let strike = json |> member "strike" |> to_number in
      let maturity = json |> member "maturity" |> to_number in
      let rate = json |> member "rate" |> to_number in
      let volatility = json |> member "volatility" |> to_number in
      let option_type_str = json |> member "optionType" |> to_string in
      let scheme_str = json |> member "scheme" |> to_string_option |> Option.value ~default:"CN" in

      let option_type = match String.lowercase_ascii option_type_str with
        | "call" -> Pricing.Call
        | "put" -> Pricing.Put
        | _ -> Pricing.Call
      in

      let scheme = match String.uppercase_ascii scheme_str with
        | "BE" | "backward-euler" -> `BE
        | _ -> `CN
      in

      let cache_control = json |> member "cacheControl" |> to_string_option |> Option.value ~default:"normal" in
      let () = match cache_control with
        | "clear" -> Grid_cache.clear_cache ()
        | "stats" ->
            let stats = Grid_cache.cache_stats () in
            Printf.eprintf "Cache stats: %d grids, %.1f%% hit rate\n"
              stats.cache_size stats.hit_rate_pct
        | _ -> ()
      in

      let input_params = Pricing.{
        spot;
        strike;
        maturity;
        rate;
        volatility;
        option_type;
      } in

      begin
        try
          let result = Pricing.price_option ~scheme input_params in

          let output = `Assoc [
            ("success", `Bool true);
            ("pdePrice", `Float result.price);
            ("analyticPrice", `Float result.analytic_price);
            ("relativeError", `Float (result.error /. result.analytic_price *. 100.0));
            ("greeks", `Assoc [
              ("delta", `Float result.delta);
              ("gamma", `Float result.gamma);
              ("theta", `Float result.theta);
              ("vega", `Float result.vega);
              ("rho", `Float result.rho);
            ]);
          ] in

          print_string (Yojson.Basic.to_string output);
          print_newline ()
        with Invalid_argument msg ->
          let error = `Assoc [
            ("success", `Bool false);
            ("error", `String msg);
          ] in
          print_string (Yojson.Basic.to_string error);
          print_newline ()
        | exn ->
          let error = `Assoc [
            ("success", `Bool false);
            ("error", `String (Printexc.to_string exn));
          ] in
          print_string (Yojson.Basic.to_string error);
          print_newline ()
      end

  | None ->
      let error = `Assoc [
        ("success", `Bool false);
        ("error", `String "Missing requestType or invalid JSON");
      ] in
      print_string (Yojson.Basic.to_string error);
      print_newline ()
