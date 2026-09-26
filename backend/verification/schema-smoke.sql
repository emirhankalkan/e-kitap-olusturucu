USE [EKitapDb];
SET NOCOUNT ON;
SET XACT_ABORT OFF;

BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @BookId uniqueidentifier = NEWID();
    INSERT INTO Kitaplar (Id, Name, Status, CreatedAt)
    VALUES (@BookId, N'Geçici şema kontrolü', N'Pending', SYSUTCDATETIME());

    INSERT INTO Bildiriler (Id, BookId, OriginalFileName, StoredFilePath, SortOrder, UploadedAt)
    SELECT NEWID(), @BookId, CONCAT(n, N'.docx'), CONCAT(N'test/', n, N'.docx'), n, SYSUTCDATETIME()
    FROM (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10)) AS Numbers(n);

    IF (SELECT COUNT(*) FROM Bildiriler WHERE BookId = @BookId) <> 10
        THROW 51000, 'On bildiri kaydedilemedi.', 1;

    BEGIN TRY
        UPDATE Bildiriler SET SortOrder = 1 WHERE BookId = @BookId AND SortOrder = 2;
        THROW 51001, 'Tekrarlanan sira reddedilmedi.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() NOT IN (2601, 2627) THROW;
    END CATCH;

    BEGIN TRY
        UPDATE Bildiriler SET SortOrder = 11 WHERE BookId = @BookId AND SortOrder = 1;
        THROW 51002, 'Gecersiz sira reddedilmedi.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 547 THROW;
    END CATCH;

    BEGIN TRY
        UPDATE Bildiriler SET BookId = NEWID() WHERE BookId = @BookId AND SortOrder = 1;
        THROW 51003, 'Gecersiz kitap baglantisi reddedilmedi.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 547 THROW;
    END CATCH;

    BEGIN TRY
        UPDATE Bildiriler SET StartPage = 0 WHERE BookId = @BookId AND SortOrder = 1;
        THROW 51004, 'Gecersiz baslangic sayfasi reddedilmedi.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 547 THROW;
    END CATCH;

    DELETE FROM Kitaplar WHERE Id = @BookId;
    IF EXISTS (SELECT 1 FROM Bildiriler WHERE BookId = @BookId)
        THROW 51005, 'Cascade iliskisi calismadi.', 1;

    ROLLBACK TRANSACTION;
    SELECT N'PASS: 10 bildiri, benzersiz sıra, sıra aralığı, foreign key, sayfa kısıtı, cascade.' AS Result;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
