using TM.Services;
using System.Security;
using jnUtil;
using TM.Models;

namespace TM.Desktop.Services;

public sealed class FileToolsService(FileUtilityService files, IFileToolDialogs dialogs, PasswordFileService passwordFiles,
    DocumentFileService documentFiles, DocumentHmacService hmacFiles, AccountFileService accountFiles,
    PublicKeyFileService publicKeyFiles, DocumentSignatureService signatures) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);

    // WorkspaceService holds its document/certificate lifetime gate before acquiring this service's gate.
    public async Task<string> SignAsync(ProjectDocument document, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            string? input = await dialogs.SelectInputAsync(cancellationToken);
            if (input is null) return "Operation canceled. No signature was created.";
            string? output = await dialogs.SelectOutputAsync(Path.GetFileName(input) + ".p7c", cancellationToken);
            if (output is null) return "Operation canceled. No signature was created.";
            await signatures.CreateAsync(document, input, output, cancellationToken);
            return "Detached CMS signature created. The source file was retained.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException or ArgumentException or InvalidOperationException or NotSupportedException)
        { return "Signature operation failed. Check the certificate, file limit and permissions; existing files are not overwritten."; }
        finally { gate.Release(); }
    }

    public async Task<string> VerifySignatureAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            string? data = await dialogs.SelectInputAsync(cancellationToken);
            if (data is null) return "Operation canceled. No files were changed.";
            string? signature = await dialogs.SelectInputAsync(cancellationToken);
            if (signature is null) return "Operation canceled. No files were changed.";
            return await signatures.VerifyAsync(data, signature, cancellationToken) switch
            {
                SignatureVerificationResult.ValidTrusted => "CMS signature is valid and the included certificate chain is trusted.",
                SignatureVerificationResult.ValidUntrusted => "CMS signature is valid, but the included certificate chain is not trusted by this system.",
                _ => "CMS signature is invalid for the selected file."
            };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return "Signature verification failed. Check the selected files, format, file limit and permissions."; }
        finally { gate.Release(); }
    }

    // WorkspaceService holds its document/key lifetime gate before acquiring this service's gate.
    public async Task<string> ExecutePublicKeyAsync(ProjectDocument document, PublicKeyFileOperation operation,
        ReadOnlyMemory<byte> peerPublicKey, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(operation)) return "Choose a supported public-key file operation.";
        await gate.WaitAsync(cancellationToken);
        try
        {
            string? input = await dialogs.SelectInputAsync(cancellationToken);
            if (input is null) return "Operation canceled. No output was created.";
            cancellationToken.ThrowIfCancellationRequested();
            string suffix = operation is PublicKeyFileOperation.Encrypt ? ".aes" : ".decrypted";
            string? output = await dialogs.SelectOutputAsync(Path.GetFileName(input) + suffix, cancellationToken);
            if (output is null) return "Operation canceled. No output was created.";
            await publicKeyFiles.ExecuteAsync(document, operation, peerPublicKey, input, output, cancellationToken);
            return "Public-key output file created. The source file was retained.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException
            or System.Security.Cryptography.CryptographicException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return "Public-key file operation failed. Check the peer public key, document keys, input format, file limit and permissions. Existing files are not overwritten.";
        }
        finally { gate.Release(); }
    }

    public async Task<string> ExecuteAccountAsync(AccountFileOperation operation, bool acknowledged,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(operation) || !acknowledged)
            return "Choose a supported EFS operation and acknowledge its Windows/account and plaintext limitations.";
        await gate.WaitAsync(cancellationToken);
        try
        {
            string? input = await dialogs.SelectInputAsync(cancellationToken);
            if (input is null) return "Operation canceled. No output was created.";
            cancellationToken.ThrowIfCancellationRequested();
            string suffix = operation is AccountFileOperation.Encrypt ? ".enc" : ".decrypted";
            string? output = await dialogs.SelectOutputAsync(Path.GetFileName(input) + suffix, cancellationToken);
            if (output is null) return "Operation canceled. No output was created.";
            await accountFiles.ExecuteAsync(operation, input, output, cancellationToken);
            return "Output file created with the requested Windows EFS state. The source file was retained.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or System.Security.Cryptography.CryptographicException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return "EFS operation failed. Check Windows/EFS support, filesystem, permissions and access to the encryption certificate. Choose a new output file; existing files are not overwritten.";
        }
        finally { gate.Release(); }
    }

    public async Task<string> ExecuteAsync(FileUtilityOperation operation, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(operation)) return "Choose a supported file operation.";
        await gate.WaitAsync(cancellationToken);
        try
        {
            string? input = await dialogs.SelectInputAsync(cancellationToken);
            if (input is null) return "Operation canceled. No output was created.";
            cancellationToken.ThrowIfCancellationRequested();
            string suffix = operation switch
            {
                FileUtilityOperation.Base64Encode => ".b64",
                FileUtilityOperation.Base64Decode => ".decoded",
                _ => "." + operation.ToString().ToLowerInvariant()
            };
            string? output = await dialogs.SelectOutputAsync(Path.GetFileName(input) + suffix, cancellationToken);
            if (output is null) return "Operation canceled. No output was created.";
            await files.ExecuteAsync(operation, input, output, cancellationToken);
            return "Output file created. The source file was not changed.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException
            or System.Security.Cryptography.CryptographicException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return "File operation failed. Check the input and permissions, and choose a new output file. Existing files are not overwritten.";
        }
        finally { gate.Release(); }
    }

    public async Task<string> ExecutePasswordAsync(PasswordFileOperation operation, string password, string confirmation,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(operation) || string.IsNullOrEmpty(password) || password.Length > 4096 ||
            confirmation.Length > 4096 || (operation is PasswordFileOperation.Encrypt &&
            !string.Equals(password, confirmation, StringComparison.Ordinal)))
            return "Enter a file password and matching confirmation for encryption (maximum 4096 characters).";
        await gate.WaitAsync(cancellationToken);
        try
        {
            string? input = await dialogs.SelectInputAsync(cancellationToken);
            if (input is null) return "Operation canceled. No output was created.";
            cancellationToken.ThrowIfCancellationRequested();
            string suffix = operation is PasswordFileOperation.Encrypt ? ".aes" : ".decrypted";
            string? output = await dialogs.SelectOutputAsync(Path.GetFileName(input) + suffix, cancellationToken);
            if (output is null) return "Operation canceled. No output was created.";
            using SecureString securePassword = password.ToCharArray().ToSecureStringAndClear();
            await passwordFiles.ExecuteAsync(operation, input, output, securePassword, cancellationToken);
            return "Output file created. The source file was retained; encryption does not delete the original plaintext.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException
            or System.Security.Cryptography.CryptographicException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return "File operation failed. Check the password, input format and permissions, and choose a new output file. Existing files are not overwritten.";
        }
        finally { gate.Release(); }
    }

    // Called only while WorkspaceService holds its document lifetime gate.
    public async Task<string> ExecuteDocumentAsync(ProjectDocument document, DocumentFileOperation operation,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(operation)) return "Choose a supported document-file operation.";
        await gate.WaitAsync(cancellationToken);
        try
        {
            string? input = await dialogs.SelectInputAsync(cancellationToken);
            if (input is null) return "Operation canceled. No output was created.";
            cancellationToken.ThrowIfCancellationRequested();
            string suffix = operation is DocumentFileOperation.Encrypt ? ".aes" : ".decrypted";
            string? output = await dialogs.SelectOutputAsync(Path.GetFileName(input) + suffix, cancellationToken);
            if (output is null) return "Operation canceled. No output was created.";
            await documentFiles.ExecuteAsync(document, operation, input, output, cancellationToken);
            return "Output file created using the document key. The source file was retained.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException
            or System.Security.Cryptography.CryptographicException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return "Document-key file operation failed. Check the document, input format and permissions, and choose a new output file.";
        }
        finally { gate.Release(); }
    }

    // WorkspaceService holds its lifetime gate before acquiring this service's gate.
    public async Task<string> ExecuteHmacAsync(ProjectDocument document, HmacOperation operation, HmacAlgorithm algorithm,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(operation) || !Enum.IsDefined(algorithm)) return "Choose a supported HMAC operation and algorithm.";
        await gate.WaitAsync(cancellationToken);
        try
        {
            string? input = await dialogs.SelectInputAsync(cancellationToken);
            if (input is null) return "Operation canceled. No files were changed.";
            cancellationToken.ThrowIfCancellationRequested();
            if (operation is HmacOperation.Verify)
            {
                string? sidecar = await dialogs.SelectInputAsync(cancellationToken);
                if (sidecar is null) return "Operation canceled. No files were changed.";
                return await hmacFiles.VerifyAsync(document, algorithm, input, sidecar, cancellationToken)
                    ? "HMAC verified using this document key."
                    : "HMAC did not match. Check the file, sidecar, algorithm and original document key.";
            }
            string? output = await dialogs.SelectOutputAsync(Path.GetFileName(input) + ".HMAC_" + algorithm.ToString().ToUpperInvariant(), cancellationToken);
            if (output is null) return "Operation canceled. No files were changed.";
            await hmacFiles.CreateAsync(document, algorithm, input, output, cancellationToken);
            return "HMAC sidecar created. The source file was not changed.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException
            or System.Security.Cryptography.CryptographicException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return "HMAC operation failed. Check the sidecar format, algorithm, document and file permissions. Existing files are not overwritten.";
        }
        finally { gate.Release(); }
    }

    public void Dispose() => gate.Dispose();
}
