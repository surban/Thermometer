CREATE TABLE [dbo].[temperatures]
(
	[Id] INT NOT NULL PRIMARY KEY IDENTITY(1,1), 
    [timestamp] DATETIME NOT NULL, 
    [temperature] FLOAT NOT NULL 
)
