-- First, check if the tables exist and their structure
SELECT OBJECT_NAME(object_id) as TableName, name as ColumnName, type_name(user_type_id) as DataType
FROM sys.columns
WHERE OBJECT_NAME(object_id) IN ('Users', 'Transactions')
ORDER BY TableName, column_id;

-- If needed, drop and recreate the Transactions table
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Transactions')
BEGIN
    DROP TABLE Transactions;
END

-- Recreate the Transactions table with the correct structure
CREATE TABLE Transactions (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    Type NVARCHAR(50) NOT NULL,
    Description NVARCHAR(MAX),
    BalanceAfterTransaction DECIMAL(18,2) NOT NULL,
    TransactionDate DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (UserId) REFERENCES Users(Id)
); 