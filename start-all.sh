#!/bin/bash
set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"

# 1. Environment paths
export PATH="$HOME/.dotnet:$HOME/pg_root/usr/lib/postgresql/16/bin:$PATH"
export LD_LIBRARY_PATH="$HOME/pg_root/usr/lib/x86_64-linux-gnu:$LD_LIBRARY_PATH"
export NVM_DIR="$HOME/.nvm"
[ -s "$NVM_DIR/nvm.sh" ] && \. "$NVM_DIR/nvm.sh"

# 2. Start PostgreSQL if not already running
if ! pg_isready -h localhost -p 5432 >/dev/null 2>&1; then
    echo "Starting PostgreSQL..."
    pg_ctl -D "$HOME/pg_data" -l "$HOME/pg_data/server.log" -o "-p 5432 -k /tmp" start
fi

# 3. Export DB Configuration for Backend
export DATABASE_URL="postgresql://postgres@localhost:5432/trading_engine"
export DATABASE_SSL_MODE="Disable"
export PORT="5000"

echo "PostgreSQL is running."
echo "Starting Backend on http://localhost:5000 ..."
echo "Starting Frontend on http://localhost:3000 ..."
