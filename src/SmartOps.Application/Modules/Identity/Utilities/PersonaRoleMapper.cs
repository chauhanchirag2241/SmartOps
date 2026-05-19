using Microsoft.Extensions.Options;
using SmartOps.Application.Configuration;

namespace SmartOps.Application.Modules.Identity.Utilities;

public interface IPersonaRoleMapper
{
    string ResolveRoleName(string? personaLabel, string fallbackRoleName);
}

public sealed class PersonaRoleMapper : IPersonaRoleMapper
{
    private readonly IReadOnlyDictionary<string, string> _mappings;

    public PersonaRoleMapper(IOptions<PersonaRoleMappingOptions> options)
    {
        PersonaRoleMappingOptions config = options.Value;
        _mappings = config.Mappings.Count > 0
            ? config.Mappings
            : PersonaRoleMappingOptions.CreateDefaultMappings();
    }

    public string ResolveRoleName(string? personaLabel, string fallbackRoleName)
    {
        if (string.IsNullOrWhiteSpace(personaLabel))
        {
            return fallbackRoleName;
        }

        string key = personaLabel.Trim();
        return _mappings.TryGetValue(key, out string? roleName) ? roleName : fallbackRoleName;
    }
}
