using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using v8desk.domain.Entities;
using v8desk.domain.ValueObjects;

namespace v8desk.infrastructure.Persistence;

internal static class JsonPersistencia
{
    private static readonly JsonSerializerOptions Opcoes = CriarOpcoes();

    private static JsonSerializerOptions CriarOpcoes()
    {
        var opcoes = new JsonSerializerOptions();
        opcoes.Converters.Add(new CalendarioConverter());
        opcoes.Converters.Add(new CaminhoConverter());
        opcoes.Converters.Add(new ExcecaoConverter());
        opcoes.Converters.Add(new AnexoConverter());
        return opcoes;
    }

    public static string Escrever<T>(T valor) => JsonSerializer.Serialize(valor, Opcoes);
    public static T Ler<T>(string json) => JsonSerializer.Deserialize<T>(json, Opcoes)!;

    public static void Mapear<T>(PropertyBuilder<T> propriedade)
    {
        propriedade.HasConversion(new ValueConverter<T, string>(v => Escrever(v), v => Ler<T>(v)));
        propriedade.Metadata.SetValueComparer(new ValueComparer<T>(
            (a, b) => Escrever(a) == Escrever(b),
            v => Escrever(v).GetHashCode(),
            v => Ler<T>(Escrever(v))));
        propriedade.HasColumnType("jsonb");
    }

    private sealed record CalendarioDados(Guid Id, long Versao, string FusoHorarioId,
        Dictionary<DayOfWeek, IReadOnlyList<IntervaloExpediente>> Expediente,
        List<ExcecaoCalendario> Excecoes, bool Historico);

    private sealed class CalendarioConverter : JsonConverter<CalendarioEmpresa>
    {
        public override CalendarioEmpresa Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            var dados = JsonSerializer.Deserialize<CalendarioDados>(ref reader, options)!;
            return CalendarioEmpresa.Restaurar(dados.Id, dados.Versao, dados.FusoHorarioId,
                dados.Expediente, dados.Excecoes, dados.Historico);
        }

        public override void Write(Utf8JsonWriter writer, CalendarioEmpresa valor, JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, new CalendarioDados(valor.Id, valor.Versao, valor.FusoHorarioId,
                valor.Expediente.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value),
                valor.Excecoes.ToList(), valor.Historico), options);
    }

    private sealed class CaminhoConverter : JsonConverter<CaminhoCategoria>
    {
        public override CaminhoCategoria Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            => new(JsonSerializer.Deserialize<List<ReferenciaHistorica>>(ref reader, options)!);

        public override void Write(Utf8JsonWriter writer, CaminhoCategoria valor, JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, valor.Niveis, options);
    }

    private sealed record ExcecaoDados(DateOnly Data, string Motivo, List<IntervaloExpediente> Intervalos);
    private sealed class ExcecaoConverter : JsonConverter<ExcecaoCalendario>
    {
        public override ExcecaoCalendario Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            var d = JsonSerializer.Deserialize<ExcecaoDados>(ref reader, options)!;
            return new(d.Data, d.Motivo, d.Intervalos);
        }
        public override void Write(Utf8JsonWriter writer, ExcecaoCalendario v, JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, new ExcecaoDados(v.Data, v.Motivo, v.Intervalos.ToList()), options);
    }

    private sealed record AnexoDados(Guid Id, string NomeOriginal, string TipoConteudo, long TamanhoBytes,
        string ChaveArmazenamento, string HashIntegridade, Guid AutorId, DateTimeOffset CriadoEm);
    private sealed class AnexoConverter : JsonConverter<Anexo>
    {
        public override Anexo Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            var d = JsonSerializer.Deserialize<AnexoDados>(ref reader, options)!;
            return new Anexo(d.NomeOriginal, d.TipoConteudo, d.TamanhoBytes, d.ChaveArmazenamento,
                d.HashIntegridade, d.AutorId, d.CriadoEm).RestaurarIdentificador(d.Id);
        }
        public override void Write(Utf8JsonWriter writer, Anexo v, JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, new AnexoDados(v.Id, v.NomeOriginal, v.TipoConteudo, v.TamanhoBytes,
                v.ChaveArmazenamento, v.HashIntegridade, v.AutorId, v.CriadoEm), options);
    }
}
