FROM alpine:latest

# Install SQLite
RUN apk add --no-cache sqlite

# Create data directory
RUN mkdir -p /data

# Set working directory
WORKDIR /data

# Create a simple script to initialize the database
COPY init.sql /init.sql
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

# Expose SQLite database file via volume
VOLUME ["/data"]

# Keep container running
ENTRYPOINT ["/entrypoint.sh"]