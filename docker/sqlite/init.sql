-- SQLite Chat Messages Table
DROP TABLE IF EXISTS ChatMessages;

CREATE TABLE ChatMessages (
    Id TEXT PRIMARY KEY,
    ThreadId TEXT NOT NULL,
    Role TEXT NOT NULL,
    Content TEXT,
    ModelId TEXT,
    InnerContentJson TEXT,
    MetadataJson TEXT,
    DateInserted TEXT NOT NULL
);

-- Create indexes for better query performance
CREATE INDEX IX_ChatMessages_ThreadId_DateInserted 
ON ChatMessages (ThreadId, DateInserted);

CREATE INDEX IX_ChatMessages_DateInserted 
ON ChatMessages (DateInserted);