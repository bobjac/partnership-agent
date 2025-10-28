-- Azure SQL Chat Messages Table Setup
-- Run this script on your Azure SQL database to create the ChatMessages table

-- Drop table if exists (be careful in production!)
IF OBJECT_ID('dbo.ChatMessages', 'U') IS NOT NULL
    DROP TABLE dbo.ChatMessages;
GO

-- Create ChatMessages table
CREATE TABLE dbo.ChatMessages (
    Id NVARCHAR(450) PRIMARY KEY,
    ThreadId NVARCHAR(450) NOT NULL,
    Role NVARCHAR(50) NOT NULL,
    Content NVARCHAR(MAX),
    ModelId NVARCHAR(255),
    InnerContentJson NVARCHAR(MAX),
    MetadataJson NVARCHAR(MAX),
    DateInserted DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
GO

-- Create indexes for better query performance
CREATE NONCLUSTERED INDEX IX_ChatMessages_ThreadId_DateInserted
ON dbo.ChatMessages (ThreadId, DateInserted);
GO

CREATE NONCLUSTERED INDEX IX_ChatMessages_DateInserted
ON dbo.ChatMessages (DateInserted);
GO

-- Verify table creation
SELECT 'ChatMessages table created successfully' AS Status;
SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ChatMessages';
GO
