using Microsoft.Extensions.Configuration;

namespace Meteo.Api.E2ETests;

/// <summary>
/// Source de configuration en mémoire qu'un test peut modifier APRÈS le démarrage de
/// l'hôte, en déclenchant explicitement le jeton de rechargement de configuration. C'est ce
/// qui permet à <c>ProviderSwitchingTests</c> de prouver "changer de fournisseur sans
/// redéploiement" (TP2) : le processus tourne déjà, seule la valeur change, et les
/// sélecteurs de fournisseur la relisent dans <c>IConfiguration</c> sans qu'on recrée l'hôte.
/// </summary>
public sealed class ReloadableMemoryConfigurationSource : IConfigurationSource
{
    private readonly ReloadableMemoryConfigurationProvider _provider = new();

    public IConfigurationProvider Build(IConfigurationBuilder builder) => _provider;

    /// <summary>Change une valeur et déclenche immédiatement le rechargement.</summary>
    public void Set(string key, string? value) => _provider.SetAndReload(key, value);

    private sealed class ReloadableMemoryConfigurationProvider : ConfigurationProvider
    {
        public void SetAndReload(string key, string? value)
        {
            Data[key] = value;
            OnReload();
        }
    }
}
