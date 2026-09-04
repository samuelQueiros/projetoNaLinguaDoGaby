using System.Security.Cryptography;
using System.Text;

namespace ErpFinanceiro.Infrastructure.Seguranca;

/// <summary>
/// Cifra/decifra campos sensíveis (dados bancários, chave PIX — CLAUDE.md
/// seção 3) com AES-256-GCM (autenticado: detecta adulteração do dado
/// cifrado, não só confidencialidade). A chave vem de configuração
/// (Criptografia:ChaveAes256Base64 — 32 bytes em base64), nunca hardcoded;
/// em produção deve vir de variável de ambiente/secret manager, nunca de
/// appsettings.json versionado.
/// </summary>
public sealed class CriptografiaAes256
{
    private readonly byte[] chave;

    public CriptografiaAes256(string chaveBase64)
    {
        if (string.IsNullOrWhiteSpace(chaveBase64))
        {
            throw new InvalidOperationException(
                "Configuração ausente: Criptografia:ChaveAes256Base64 (chave AES-256 em base64, 32 bytes). " +
                "Necessária para ler/gravar dados bancários de fornecedores.");
        }

        chave = Convert.FromBase64String(chaveBase64);
        if (chave.Length != 32)
        {
            throw new InvalidOperationException(
                "Criptografia:ChaveAes256Base64 deve decodificar para exatamente 32 bytes (AES-256).");
        }
    }

    public string Cifrar(string textoClaro)
    {
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
        RandomNumberGenerator.Fill(nonce);

        var textoClaroBytes = Encoding.UTF8.GetBytes(textoClaro);
        var textoCifradoBytes = new byte[textoClaroBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];

        using (var aes = new AesGcm(chave, tag.Length))
        {
            aes.Encrypt(nonce, textoClaroBytes, textoCifradoBytes, tag);
        }

        // Formato armazenado: nonce || tag || textoCifrado, tudo em base64.
        var resultado = new byte[nonce.Length + tag.Length + textoCifradoBytes.Length];
        Buffer.BlockCopy(nonce, 0, resultado, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, resultado, nonce.Length, tag.Length);
        Buffer.BlockCopy(textoCifradoBytes, 0, resultado, nonce.Length + tag.Length, textoCifradoBytes.Length);

        return Convert.ToBase64String(resultado);
    }

    public string Decifrar(string textoCifradoBase64)
    {
        var dados = Convert.FromBase64String(textoCifradoBase64);
        var nonceSize = AesGcm.NonceByteSizes.MaxSize;
        var tagSize = AesGcm.TagByteSizes.MaxSize;

        var nonce = dados.AsSpan(0, nonceSize).ToArray();
        var tag = dados.AsSpan(nonceSize, tagSize).ToArray();
        var textoCifrado = dados.AsSpan(nonceSize + tagSize).ToArray();
        var textoClaroBytes = new byte[textoCifrado.Length];

        using (var aes = new AesGcm(chave, tagSize))
        {
            aes.Decrypt(nonce, textoCifrado, tag, textoClaroBytes);
        }

        return Encoding.UTF8.GetString(textoClaroBytes);
    }
}
