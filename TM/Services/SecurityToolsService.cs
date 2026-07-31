using TM.Helpers;

namespace TM.Services;

public sealed class SecurityToolsService(UserInteractionService interactions, 
    ShellService shell,
    ProjectCryptoService crypto)
{
    public void EncodeFileToBase64()
    {
        if (!interactions.TryGetOpenFilePath("Select file to base64 encode", out string path))
            return;

        try
        {
            SecurityHelper.Base64EncodeFile(path, $"{path}.b64");
            interactions.ShowInfo($"{path} successfully encoded");
        }
        catch (Exception ex)
        {
            interactions.ShowError(ex);
        }
    }

    public void DecodeFileFromBase64()
    {
        if (!interactions.TryGetOpenFilePath("Select base64 file to decode", out string path, "Base 64", "b64"))
            return;

        try
        {
            string extension = Path.GetExtension(path);
            string newFilename = extension == ".b64"
                ? path[..^extension.Length]
                : $"{path}.decoded";

            SecurityHelper.Base64DeccodeFile(path, newFilename);
            interactions.ShowInfo($"{path} successfully decoded");
        }
        catch (Exception ex)
        {
            interactions.ShowWarning(ex.Message, "Error");
        }
    }

    public void EncryptFile(ProjectDocument model)
    {
        if (!interactions.TryGetOpenFilePath("Select file to encrypt", out string path))
            return;

        byte[]? key = null;
        try
        {
            byte[] salt = SecurityHelper.GetRandomKey(ProjectCryptoService.SaltLength);
            key = crypto.DeriveKey(model, "file encryption", salt);

            if (FileHelper.EncryptFile(path, ref key, salt))
                interactions.ShowInfo($"{path} successfully encrypted");
        }
        catch (CryptographicException)
        {
            interactions.ShowWarning($"Could not encrypt {path}", "Encryption error");
        }
        catch (Exception ex)
        {
            interactions.ShowError(ex);
        }
        finally
        {
            ProjectCryptoService.ClearArray(ref key);
        }
    }

    public void DecryptFile(ProjectDocument model)
    {
        if (!interactions.TryGetOpenFilePath("Select file to decrypt", out string path))
            return;

        byte[]? key = null;
        try
        {
            byte[] salt = FileHelper.ReadSaltFromFile(path);
            key = crypto.DeriveKey(model, "file encryption", salt);

            if (FileHelper.DecryptFile(path, ref key))
                interactions.ShowInfo($"{path} successfully decrypted");
        }
        catch (CryptographicException)
        {
            interactions.ShowWarning($"Could not decrypt {path}", "Encryption error");
        }
        catch (Exception ex)
        {
            interactions.ShowWarning(ex.Message, "Error");
        }
        finally
        {
            ProjectCryptoService.ClearArray(ref key);
        }
    }

    public void EncryptFileWithPassword()
    {
        if (!interactions.TryGetOpenFilePath("Select file to encrypt", out string path))
            return;

        ExecuteWithUiErrorHandling(
            () =>
            {
                if (!interactions.TryGetPassword("Encrypt file", "Enter password:", out SecureString pass))
                    return;

                using (pass)
                {
                    if (FileHelper.EncryptFile(path, pass))
                        interactions.ShowInfo($"{path} successfully encrypted");
                }
            },
            $"Could not encrypt {path}");
    }

    public void DecryptFileWithPassword()
    {
        if (!interactions.TryGetOpenFilePath("Select file to decrypt", out string path))
            return;

        ExecuteWithUiErrorHandling(
            () =>
            {
                if (!interactions.TryGetPassword("Decrypt file", "Enter password", out SecureString pass))
                    return;

                using (pass)
                {
                    if (FileHelper.DecryptFile(path, pass))
                        interactions.ShowInfo($"{path} successfully decrypted");
                }
            },
            $"Could not decrypt {path}");
    }

    public void EncryptFileForCurrentAccount()
    {
        if (!interactions.TryGetOpenFilePath("Select file to encrypt", out string path))
            return;

        ExecuteWithUiErrorHandling(
            () =>
            {
                if (FileHelper.EncryptFile(path))
                    interactions.ShowInfo($"{path} successfully encrypted");
            },
            $"Could not encrypt {path}");
    }

    public void DecryptFileForCurrentAccount()
    {
        if (!interactions.TryGetOpenFilePath("Select file to decrypt", out string path))
            return;

        ExecuteWithUiErrorHandling(
            () =>
            {
                if (FileHelper.DecryptFile(path))
                    interactions.ShowInfo($"{path} successfully decrypted");
            },
            $"Could not decrypt {path}");
    }

    public void GenerateCertificate(ProjectDocument model)
    {
        crypto.EnsureCaCertificate(model);
        interactions.ShowInfo("Signing certificate ready.", "Certificate");
    }

    public void ImportCertificate(ProjectDocument model)
    {
        const string header = "Import certificate";

        if (!interactions.TryGetOpenFilePath(header, out string path, "Pfx files", "pfx"))
            return;

        if (!interactions.TryGetPassword(header, "Password for pfx private key:", out SecureString pfxPass))
            return;

        using (pfxPass)
        {
            try
            {
                model.Security.CaCertificate = X509Helper.LoadPfxFromFile(path, pfxPass);
                interactions.ShowInfo($"Successfully imported {path}", header);
            }
            catch (Exception ex)
            {
                interactions.ShowWarning(ex.Message, "Error");
            }
        }
    }

    public void ExportCertificate(ProjectDocument model)
    {
        if (!interactions.TryGetSaveFilePath("Export certificate", out string path, "Certificate", "cer"))
            return;

        crypto.EnsureCaCertificate(model);
        X509Helper.SaveX509ToCerFile(model.Security.CaCertificate, path);
        interactions.ShowInfo($"Certificate saved to {path}", "Certificate");
    }

    public void CreateSignature(ProjectDocument model)
    {
        if (!interactions.TryGetOpenFilePath("Select file to sign", out string path))
            return;

        try
        {
            byte[] signature = crypto.SignFile(model, path);
            string signatureFileName = $"{path}.p7c";

            if (File.Exists(signatureFileName))
                File.Delete(signatureFileName);

            File.WriteAllBytes(signatureFileName, signature);
            interactions.ShowInfo($"{signatureFileName} successfully written");
        }
        catch (Exception ex)
        {
            interactions.ShowError($"{path} could not be signed: {ex.Message}");
        }
    }

    public void VerifySignature()
    {
        if (!interactions.TryGetOpenFilePath("Select signature to verify", out string path, "PKCS7 signature", "p7c"))
            return;

        string fileName = path[..^4];
        if (!path.EndsWith(".p7c", StringComparison.OrdinalIgnoreCase) || !File.Exists(fileName))
            interactions.TryGetOpenFilePath("Select signed file to verify", out fileName);

        try
        {
            byte[] dataToVerify = File.ReadAllBytes(fileName);
            byte[] signature = File.ReadAllBytes(path);

            bool ok = CMSHelper.Verify(dataToVerify, signature, false);
            if (ok)
            {
                interactions.ShowInfo($"{fileName} successfully verified!");
                return;
            }

            ok = CMSHelper.Verify(dataToVerify, signature, true);
            if (ok)
            {
                interactions.ShowWarning(
                    $"Warning! Couldn't verify certificate chain for {path}",
                    "Certificate validation failed");
                return;
            }

            interactions.ShowError($"{fileName} could not be verified!", "Failed validation");
        }
        catch (Exception ex)
        {
            interactions.ShowError($"{fileName} could not be verified!{ex.Message}", "Error in validation");
        }
    }

    public void GenerateKeys(ProjectDocument model)
    {
        crypto.GenerateKeys(model);
        interactions.ShowInfo("New ECDH keypair generated.", "ECDH");
    }

    public void CopyPublicKey(ProjectDocument model)
    {
        if (model.Security.PublicKey is not null)
            shell.CopyToClipboard(model.Security.PublicKey, 300);
    }

    public void EncryptWithPublicKey(ProjectDocument model)
    {
        if (!interactions.TryGetOpenFilePath("Select file to encrypt", out string path))
            return;

        try
        {
            if (!interactions.TryGetText("Encrypt file", "Enter recipient's public key", out string publicKey))
                return;

            string newFilename = $"{path}.aes";
            if (File.Exists(newFilename))
                File.Delete(newFilename);

            byte[]? privateKey = crypto.GetUnprotectedPrivateKey(model);
            byte[]? symmetricKey = SecurityHelper.DeriveSymmetricKey(privateKey, publicKey.FromBase64());
            ProjectCryptoService.ClearArray(ref privateKey);

            byte[] encrypted = SecurityHelper.GCMEncrypt(File.ReadAllBytes(path), symmetricKey);
            ProjectCryptoService.ClearArray(ref symmetricKey);

            File.WriteAllBytes(newFilename, encrypted);
            interactions.ShowInfo($"{newFilename} successfully encrypted");
        }
        catch (Exception ex)
        {
            interactions.ShowError($"{path} could not be encrypted: {ex.Message}");
        }
    }

    public void DecryptWithPrivateKey(ProjectDocument model)
    {
        if (!interactions.TryGetOpenFilePath("Select file to decrypt", out string path))
            return;

        try
        {
            if (!interactions.TryGetText("Decrypt file", "Enter sender's public key", out string publicKey))
                return;

            string extension = Path.GetExtension(path);
            string newFilename = extension == ".aes"
                ? path[..^extension.Length]
                : $"{path}.decrypted";

            while (File.Exists(newFilename))
                newFilename += ".new";

            byte[]? privateKey = crypto.GetUnprotectedPrivateKey(model);
            byte[]? symmetricKey = SecurityHelper.DeriveSymmetricKey(privateKey, publicKey.FromBase64());
            ProjectCryptoService.ClearArray(ref privateKey);

            byte[] decrypted = SecurityHelper.GCMDecrypt(File.ReadAllBytes(path), symmetricKey);
            ProjectCryptoService.ClearArray(ref symmetricKey);

            File.WriteAllBytes(newFilename, decrypted);
            interactions.ShowInfo($"{newFilename} successfully decrypted");
        }
        catch (Exception ex)
        {
            interactions.ShowError($"{path} could not be decrypted: {ex.Message}");
        }
    }

    public void PurgeFile()
    {
        if (!interactions.TryGetOpenFilePath("Select file to purge", out string path))
            return;

        try
        {
            SecurityHelper.WipeFile(path, 20);
            interactions.ShowInfo($"{path} successfully purged");
        }
        catch (Exception ex)
        {
            interactions.ShowError($"Could not purge {path}: {ex.Message}");
        }
    }

    public void ComputeHash(string algorithm)
    {
        string normalized = NormalizeAlgorithm(algorithm);

        if (!interactions.TryGetOpenFilePath($"Select file to {normalized} hash", out string path))
            return;

        string outputFile = normalized switch
        {
            "MD5" => HashHelper.MD5File(path),
            "SHA1" => HashHelper.Sha1File(path),
            "SHA256" => HashHelper.Sha256File(path),
            "SHA384" => HashHelper.Sha384File(path),
            "SHA512" => HashHelper.Sha512File(path),
            _ => throw new InvalidOperationException($"Unsupported algorithm: {algorithm}")
        };

        interactions.ShowInfo($"Successfully hashed {path} to {outputFile}", "Hash calculated");
    }

    public void CreateHmac(ProjectDocument model, string algorithm)
    {
        string normalized = NormalizeAlgorithm(algorithm);

        if (!interactions.TryGetOpenFilePath($"Select file to {normalized} HMAC", out string path))
            return;

        byte[] salt = SecurityHelper.GetRandomKey(HashHelper.SALT_LEN);
        byte[] key = crypto.DeriveKey(model, "HMAC", salt, 64);

        string outputFile = normalized switch
        {
            "MD5" => HashHelper.MD5SignFile(key, path),
            "SHA1" => HashHelper.Sha1SignFile(key, path),
            "SHA256" => HashHelper.Sha256SignFile(key, path),
            "SHA384" => HashHelper.Sha384SignFile(key, path),
            "SHA512" => HashHelper.Sha512SignFile(key, path),
            _ => throw new InvalidOperationException($"Unsupported algorithm: {algorithm}")
        };

        File.AppendAllText(outputFile, "\n" + salt.ToBase64(), Encoding.UTF8);
        interactions.ShowInfo($"Successfully signed {path} to {outputFile}", "HMAC calculated");
    }

    public void VerifyHmac(ProjectDocument model, string algorithm)
    {
        string normalized = NormalizeAlgorithm(algorithm);

        if (!interactions.TryGetOpenFilePath($"Select file to verify {normalized} HMAC", out string path))
            return;

        if (!interactions.TryGetOpenFilePath("Select stored HMAC file", out string macPath))
            return;

        string macFile = File.ReadAllText(macPath);
        string[] lines = macFile.SplitToLines();

        if (lines.Length < 2)
        {
            interactions.ShowWarning(
                $"Invalid HMAC file format in:\n{macPath}\n\nExpected 2 lines:\n1) HMAC\n2) Base64 salt",
                "HMAC verification error");
            return;
        }

        byte[] mac;
        byte[] salt;

        try
        {
            mac = lines[0].FromPrettyPrint();
        }
        catch (Exception ex)
        {
            interactions.ShowWarning(
                $"Invalid HMAC value in:\n{macPath}\n\n{ex.Message}",
                "HMAC verification error");
            return;
        }

        try
        {
            salt = lines[1].FromBase64();
        }
        catch (Exception ex)
        {
            interactions.ShowWarning(
                $"Invalid salt value in:\n{macPath}\n\n{ex.Message}",
                "HMAC verification error");
            return;
        }

        byte[] key = crypto.DeriveKey(model, "HMAC", salt, 64);

        bool verified = normalized switch
        {
            "MD5" => HashHelper.MD5VerifyFile(key, path, mac),
            "SHA1" => HashHelper.Sha1VerifyFile(key, path, mac),
            "SHA256" => HashHelper.Sha256VerifyFile(key, path, mac),
            "SHA384" => HashHelper.Sha384VerifyFile(key, path, mac),
            "SHA512" => HashHelper.Sha512VerifyFile(key, path, mac),
            _ => throw new InvalidOperationException($"Unsupported algorithm: {algorithm}")
        };

        if (verified)
            interactions.ShowInfo($"Successfully verified {path}", "HMAC verified");
        else
            interactions.ShowError($"Could not verify {path}", "HMAC verification error");
    }

    private void ExecuteWithUiErrorHandling(
        Action action,
        string cryptographicErrorMessage,
        string cryptographicTitle = "Encryption error")
    {
        try
        {
            action();
        }
        catch (CryptographicException)
        {
            interactions.ShowWarning(cryptographicErrorMessage, cryptographicTitle);
        }
        catch (Exception ex)
        {
            interactions.ShowError(ex);
        }
    }

    private static string NormalizeAlgorithm(string? algorithm) =>
        algorithm?.Trim().ToUpperInvariant() ?? string.Empty;
}
