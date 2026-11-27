using GastosApp.Web.Services;

namespace GastosApp.Web.Tests;

/// <summary>
/// Práctica 12 · reintento con espera progresiva (exponential backoff) de la cola sin conexión: OfflineSync.RetryDelay.
/// 5 s, 10 s, 20 s… hasta un tope de 5 min, más hasta un 20 % al azar para no reintentar todos a la vez.
/// </summary>
public class RetryBackoffTests
{
    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 10)]
    [InlineData(2, 20)]
    [InlineData(3, 40)]
    [InlineData(5, 160)]
    public void SinAzar_LaEsperaSeDuplicaEnCadaFallo(int failures, double seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), OfflineSync.RetryDelay(failures, jitter: 0));

    [Theory]
    [InlineData(6)]
    [InlineData(20)]
    [InlineData(int.MaxValue)]
    public void LaEsperaNuncaPasaDeCincoMinutos(int failures) =>
        Assert.Equal(TimeSpan.FromMinutes(5), OfflineSync.RetryDelay(failures, jitter: 0));

    [Fact]
    public void ElAzarAgregaComoMaximoUnVeintePorCiento() =>
        Assert.Equal(TimeSpan.FromSeconds(12), OfflineSync.RetryDelay(1, jitter: 1));

    [Theory]
    [InlineData(-3, 5)]   // valores fuera de rango no producen esperas negativas ni absurdas
    [InlineData(0, 5)]
    public void ValoresFueraDeRango_SeAcotan(int failures, double seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), OfflineSync.RetryDelay(failures, jitter: -1));
}
