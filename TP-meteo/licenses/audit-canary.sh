#!/usr/bin/env bash
# Contrôle négatif du gate de licences (critère de réussite du TP4) : un projet jetable,
# hors de la solution, référence un paquet GPL-3.0. audit.sh DOIT le refuser, et pour
# cette raison-là. Si ce script échoue, le gate ne protège plus rien : liste blanche
# élargie par erreur, outil mis à jour qui ne bloque plus, ou audit cassé pour une autre
# raison.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
canary="$(mktemp -d)"
trap 'rm -rf "$canary"' EXIT

# Hors de TP-meteo/ : ni Directory.Packages.props ni Directory.Build.props ne s'appliquent.
cat > "$canary/GplCanary.csproj" <<'XML'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <!-- GPL-3.0-only, sans dépendance : un canari minimal, qui ne restaure rien d'autre. -->
    <PackageReference Include="DamerauLevenshteinDistance-Gpl" Version="1.0.0" />
  </ItemGroup>
</Project>
XML

if output="$("$here/audit.sh" "$canary" "$canary/licenses.json" 2>&1)"; then
  echo "$output"
  echo "ÉCHEC : un paquet GPL-3.0-only a passé le gate de licences." >&2
  exit 1
fi

# Le gate doit échouer sur la licence, pas sur une panne réseau ou d'outil.
if ! grep -q 'License Type (GPL-3.0-only)' <<< "$output"; then
  echo "$output"
  echo "ÉCHEC : audit.sh a échoué, mais pas sur la licence GPL du canari." >&2
  exit 1
fi

echo "OK : le gate refuse un paquet GPL-3.0-only."
