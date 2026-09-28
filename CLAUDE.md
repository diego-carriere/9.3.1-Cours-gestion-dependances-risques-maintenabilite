# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository purpose

This is a training-course repository for "Gestion des dépendances, risques et maintenabilité" — a 3-day course (Jour 1: comprendre et maîtriser ses dépendances; Jour 2: découpler, packager et sécuriser ses choix; Jour 3: appliquer et évaluer). It is a git repository; CI lives in `.github/workflows/ci.yml`.

- `Support J1.md` / `Support J1.pdf` — Day 1 slide deck (source of truth is the PDF; the `.md` is a text extraction and may render diagrams as garbled inline text — treat picture/diagram callouts in the `.md` as lossy).
- `Support J2.pdf` — Day 2 slide deck (decoupling, packaging, licences; parts 8–9 "Packages et licences" / "Auditer les licences" back TP4).
- `TP-meteo/` — the practical exercises TP1–TP4 and their shared implementation: briefs in `TP-meteo/documentation/` (`TP1`–`TP4`, `.md`/`.pdf`), `prompt.md` (the user's own working prompt and rules for these TPs), and a .NET 10 solution (`Meteo.slnx`, `src/`, `tests/`). `TP-meteo/README.md` justifies every architectural choice against the Day 1 deck — keep it in sync with the code.

Commands (run from `TP-meteo/`): `dotnet build` (TreatWarningsAsErrors), `dotnet test` (offline, HTTP transport faked), `dotnet run --project src/Meteo.Api`, `./licenses/audit.sh` (licence gate, exactly as CI runs it; needs network) and `./licenses/audit-canary.sh` (proves the gate still rejects a GPL package).

Commit convention (from `prompt.md`): every commit message is prefixed `[TP1]`, `[TP2]`, `[TP3]` or `[TP4]` according to the TP it serves, e.g. `[TP2] fix(infra): ...`.

## TP1 — API Météo assignment

The brief in `TP-meteo/documentation/TP1.md` requires:

- An API that receives a postal address via GET and returns the weather forecast for that location, by chaining two external services:
  1. **Geocoding** — Nominatim (place name → lat/lon), e.g. `nominatim.openstreetmap.org/search?q=Alès&format=json`
  2. **Weather** — Open-Meteo (lat/lon → forecast), e.g. `api.open-meteo.com/v1/forecast?latitude=48.85&longitude=2.35&hourly=shortwave_radiation`
- Free choice of language, but the implementation must be version-controlled with Git.
- The implementation **must** demonstrate the architectural practices from the Day 1 course: low coupling, Inversion of Control (IoC), and Dependency Injection (DI).
- Code must be testable and actually tested, both with unit tests and end-to-end tests.

Per `TP-meteo/prompt.md`, architectural choices were settled before the plan and code, and each is justified against the constraints (low coupling, IoC/DI, unit + e2e testability) in `TP-meteo/README.md`. Keep that discipline for any further change: state and justify the decision, don't default to a stack out of habit.

## TP2 — changement d'API

`TP-meteo/documentation/TP2.md` tests the TP1 architecture: the cost of change is the measure. It requires a BAN geocoding adapter and a MET Norway weather adapter behind the existing ports (MET Norway rejects requests without an identifiable User-Agent — per `prompt.md` it carries the user's contact e-mail, set in `appsettings.json`), a provider choice made by configuration without recompiling (old providers stay available), a single contract test suite run against every implementation of a port (valid address, not found, empty response, accented characters, with stubbed HTTP), and proof that provider DTOs/field names never leave their adapter.

## TP3 — mode démo, cache et format

`TP-meteo/documentation/TP3.md` consolidates TP1/TP2, again measured by the cost of change (add code behind existing abstractions, barely modify existing code): `demo=true` returns simulated data and never calls the real geocoder or weather service; two successive requests for the same address make a single real call to the active geocoder (no `static`, the cache implementation must stay swappable); every provider and demo mode return exactly `{ address, latitude, longitude, hourly: [{ time, temperatureCelsius }] }` — switching provider or enabling demo changes the content, never the shape.

## TP4 — audit de conformité des licences

`TP-meteo/documentation/TP4.md` audits the existing project and adds no dependency: scan every NuGet dependency, direct and transitive, with `dotnet-project-licenses` and export the result (`licenses.json`); classify every licence as permissive, copyleft or proprietary, explicitly flagging any copyleft (GPL, AGPL, LGPL) or unidentified one; write a decision record for each copyleft case — or, when the project has none, for another case — giving the package, its position in the dependency tree (direct/transitive, depth), the chosen strategy (rewrite, substitute a permissive equivalent, isolate behind an existing interface, negotiate another licence) and its justification against the existing boundaries/adapters; make CI fail when a licence outside a whitelist appears. Success criterion: deliberately adding a GPL test package turns the build red, removing it turns it green again. Deliverable: a report holding the raw scan, the classification table, the decision record(s) and the CI configuration.

## Course concepts to keep consistent with

The Day 1 deck establishes vocabulary/framing that should stay consistent if you produce course materials, exercises, or example code referencing it:

- Dependency = anything a module needs to function (function, class, library, external service...); internal vs. external, direct vs. transitive, explicit vs. implicit.
- Goal is to minimize **coupling**, not eliminate dependencies; pair low coupling with high **cohesion**.
- Four places to look for dependencies: code (imports), network (HTTP/SDK calls), manifests (`package.json`, `.csproj`, `requirements.txt`, etc.), and resources (DB, storage, cache, secrets) — plus hidden dependencies (env vars, singletons/global state, shelled-out external tools, execution context like clock/timezone/locale).
- Layered architecture (Presentation → Application/Services → Domain → Infrastructure), each layer depending only downward, is the reference architecture used to motivate IoC/DI.
- IoC/DI framing: a module should receive its dependencies (constructor injection preferred, then property, then method injection) rather than instantiate them (`new`) itself, so implementations are swappable and fakeable in tests.
