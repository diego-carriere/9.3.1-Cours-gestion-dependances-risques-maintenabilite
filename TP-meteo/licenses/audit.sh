#!/usr/bin/env bash
# Audit des licences NuGet (TP4). La même commande sert en local et en CI :
#   ./licenses/audit.sh                      # la solution, écrit licenses/licenses.json
#   ./licenses/audit.sh <dossier> <sortie>   # un autre dossier (cf. audit-canary.sh)
# Échoue si une dépendance, directe ou transitive, porte une licence absente de
# allowed-licenses.json, ou une licence que l'outil ne sait pas identifier.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(dirname "$here")"
input="${1:-$root}"
output="${2:-$here/licenses.json}"

cd "$root"
dotnet tool restore > /dev/null
# --use-project-assets-json lit le graphe résolu par la restauration : chaque paquet
# transitif, à la version réellement retenue (Central Package Management, pinning).
dotnet restore "$input" --verbosity quiet > /dev/null

if ! dotnet tool run dotnet-project-licenses \
    --input "$input" \
    --include-transitive --use-project-assets-json --unique \
    --print false \
    --allowed-license-types "$here/allowed-licenses.json" \
    --licenseurl-to-license-mappings "$here/reviewed-license-urls.json" \
    --json --outfile "$output"; then
  echo "Licence hors liste blanche, ou non identifiée : voir licenses/RAPPORT.md." >&2
  exit 1
fi

# L'outil écrit tout sur une ligne : indenté, le diff d'une PR montre le paquet qui change.
jq --indent 2 . "$output" > "$output.tmp" && mv "$output.tmp" "$output"

echo "Licences des paquets NuGet, directs et transitifs :"
jq -r 'group_by(.LicenseType) | .[] | "  \(.[0].LicenseType)  \(length)"' "$output"
