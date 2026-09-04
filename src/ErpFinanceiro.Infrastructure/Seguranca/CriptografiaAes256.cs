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
///
/// Cada valor cifrado exige um <c>contexto</c> (dado associado autenticado —
/// AAD) amarrando o texto cifrado a "onde ele pertence" (hoje: a coluna,
/// ex. "DadosBancariosFornecedor.Conta"). Isso impede que alguém com acesso
/// de escrita direto ao Postgres copie o ChavePix cifrado de um registro
/// para a coluna Conta de outro (ou de outra linha) e a decifragem
/// continue "funcionando" — com contexto errado, o GCM rejeita (revisão do
/// security-auditor, Passo 9).
///
/// Limitação conhecida: o contexto usado hoje é por coluna, não por linha
/// (não inclui o Id do registro/fornecedor) — um DBA malicioso ainda
/// poderia copiar o cifrado de uma linha para outra da MESMA coluna sem
/// detecção. Amarrar por linha exigiria acesso à entidade no momento da
/// conversão (fora do que um ValueConverter simples do EF Core permite) —
/// registrado como melhoria futura, não bloqueante para o MVP sem dados
/// reais ainda.
///
/// Formato armazenado: "&lt;versão&gt;:" + base64(nonce || tag || cifrado).
/// O prefixo de versão existe para permitir rotação de chave no futuro sem
/// quebrar dados já gravados (basta um construtor que aceite múltiplas
/// chaves por versão quando isso for necessário) — hoje só "v1" existe.
/// </summary>
public sealed class CriptografiaAes256
{
    private const string VersaoAtual = "v1";

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

    public string Cifrar(string textoClaro, string contexto)
    {
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
        RandomNumberGenerator.Fill(nonce);

        var associatedData = Encoding.UTF8.GetBytes(contexto);
        var textoClaroBytes = Encoding.UTF8.GetBytes(textoClaro);
        var textoCifradoBytes = new byte[textoClaroBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];

        using (var aes = new AesGcm(chave, tag.Length))
        {
            aes.Encrypt(nonce, textoClaroBytes, textoCifradoBytes, tag, associatedData);
        }

        // nonce || tag || textoCifrado, tudo em base64, prefixado com a versão da chave.
        var resultado = new byte[nonce.Length + tag.Length + textoCifradoBytes.Length];
        Buffer.BlockCopy(nonce, 0, resultado, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, resultado, nonce.Length, tag.Length);
        Buffer.BlockCopy(textoCifradoBytes, 0, resultado, nonce.Length + tag.Length, textoCifradoBytes.Length);

        return $"{VersaoAtual}:{Convert.ToBase64String(resultado)}";
    }

    public string Decifrar(string textoCifradoVersionado, string contexto)
    {
        var separador = textoCifradoVersionado.IndexOf(':');
        if (separador < 0)
        {
            throw new InvalidOperationException("Valor cifrado em formato inválido (sem prefixo de versão).");
        }

        var versao = textoCifradoVersionado[..separador];
        if (versao != VersaoAtual)
        {
            throw new InvalidOperationException(
                $"Valor cifrado com a versão de chave '{versao}', que esta instância não reconhece " +
                $"(só '{VersaoAtual}' está configurada). Isso indica rotação de chave sem migração dos dados antigos.");
        }

        var dados = Convert.FromBase64String(textoCifradoVersionado[(separador + 1)..]);
        var nonceSize = AesGcm.NonceByteSizes.MaxSize;
        var tagSize = AesGcm.TagByteSizes.MaxSize;

        var nonce = dados.AsSpan(0, nonceSize).ToArray();
        var tag = dados.AsSpan(nonceSize, tagSize).ToArray();
        var textoCifrado = dados.AsSpan(nonceSize + tagSize).ToArray();
        var textoClaroBytes = new byte[textoCifrado.Length];
        var associatedData = Encoding.UTF8.GetBytes(contexto);

        using (var aes = new AesGcm(chave, tagSize))
        {
            aes.Decrypt(nonce, textoCifrado, tag, textoClaroBytes, associatedData);
        }

        return Encoding.UTF8.GetString(textoClaroBytes);
    }
}
