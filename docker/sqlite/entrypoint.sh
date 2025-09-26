#!/bin/sh

# Initialize database if it doesn't exist
if [ ! -f /data/partnership-agent.db ]; then
    echo "Initializing SQLite database..."
    sqlite3 /data/partnership-agent.db < /init.sql
    echo "Database initialized successfully!"
else
    echo "Database already exists, skipping initialization."
fi

# Keep container running
tail -f /dev/null