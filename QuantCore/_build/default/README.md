# QuantCore: OCaml PDE Solver

This directory contains the quantitative mathematical core of the Trading Engine. It implements a Partial Differential Equation (PDE) solver to provide fair-value pricing and risk Greeks for financial instruments.

## Structure
- `src/`: Core PDE solver logic, tridiagonal matrix algorithms, and pricing models.
- `bin/`: Entry points and API wrappers (including the `pricing_api` used by the C# engine).
- `dune-project`: OCaml project configuration.

## Build Instructions
This project requires the OCaml build system `dune` and the `yojson` library.

### Prerequisites
1. Install OCaml and OPAM:
   ```bash
   # On Ubuntu/Debian
   sudo apt-get install opam
   opam init
   eval $(opam env)
   ```
2. Install dependencies:
   ```bash
   opam install yojson
   ```

### Compiling the API
To build the pricing API binary used by the C# engine:
```bash
cd QuantCore
dune build bin/pricing_api.exe
```
The resulting binary will be located in `_build/default/bin/pricing_api.exe`.

## Integration
The C# engine communicates with the `pricing_api` binary via standard I/O (stdin/stdout) using JSON.
