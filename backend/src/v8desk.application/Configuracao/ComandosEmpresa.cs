using v8desk.domain.ValueObjects;

namespace v8desk.application.Configuracao;

public abstract record ComandoEmpresa;
public sealed record RenomearEmpresa(string Nome) : ComandoEmpresa;
public sealed record CriarSetor(string Nome) : ComandoEmpresa;
public sealed record CriarUsuario(string Nome) : ComandoEmpresa;
public sealed record RenomearUsuario(Guid UsuarioId, string Nome) : ComandoEmpresa;
public sealed record DesativarUsuario(Guid UsuarioId) : ComandoEmpresa;
public sealed record DefinirDisponibilidadeUsuario(Guid UsuarioId, bool Disponivel) : ComandoEmpresa;
public sealed record ConfigurarLimiteSetoresAtendente(int? Limite) : ComandoEmpresa;
public sealed record ConfigurarCicloVidaEmpresa(PoliticaCicloVida Politica) : ComandoEmpresa;
public sealed record AlterarFusoHorario(string FusoHorarioId) : ComandoEmpresa;
public sealed record IntervaloExpedienteEntrada(TimeOnly Inicio, TimeOnly Fim);
public sealed record DefinirExpediente(DayOfWeek Dia, IReadOnlyList<IntervaloExpedienteEntrada> Intervalos) : ComandoEmpresa;
public sealed record RegistrarExcecaoCalendario(DateOnly Data, string Motivo, IReadOnlyList<IntervaloExpedienteEntrada> Intervalos) : ComandoEmpresa;
public sealed record RemoverExcecaoCalendario(DateOnly Data) : ComandoEmpresa;
public sealed record ResultadoEmpresa(Guid EmpresaId, Guid RegistroId);
