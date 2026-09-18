# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository purpose

This is a training-course repository (not a software project on its own) for "Gestion des dépendances, risques et maintenabilité" — a 3-day course (Jour 1: comprendre et maîtriser ses dépendances; Jour 2: découpler, packager et sécuriser ses choix; Jour 3: appliquer et évaluer). It is not a git repository yet.

- `Support J1.md` / `Support J1.pdf` — Day 1 slide deck (source of truth is the PDF; the `.md` is a text extraction and may render diagrams as garbled inline text — treat picture/diagram callouts in the `.md` as lossy).
- `TP1-meteo/` — the practical exercise ("TP1") tied to the Day 1 material, containing the assignment brief (`TP1 - API Météo.md`/`.pdf`) and `prompt.md` (the user's own working prompt describing what they want Claude's help with for this TP).

No application code exists yet anywhere in this repo — TP1 has not been implemented. There are no build, lint, or test commands to run because there is nothing to build yet.

## TP1 — API Météo assignment

When asked to help with TP1, the brief in `TP1-meteo/TP1 - API Météo.md` requires:

- An API that receives a postal address via GET and returns the weather forecast for that location, by chaining two external services:
  1. **Geocoding** — Nominatim (place name → lat/lon), e.g. `nominatim.openstreetmap.org/search?q=Alès&format=json`
  2. **Weather** — Open-Meteo (lat/lon → forecast), e.g. `api.open-meteo.com/v1/forecast?latitude=48.85&longitude=2.35&hourly=shortwave_radiation`
- Free choice of language, but the implementation must be version-controlled with Git.
- The implementation **must** demonstrate the architectural practices from the Day 1 course: low coupling, Inversion of Control (IoC), and Dependency Injection (DI).
- Code must be testable and actually tested, both with unit tests and end-to-end tests.

Per `TP1-meteo/prompt.md`, the user's intent is to first nail down architectural choices (language, libraries, design patterns, SOLID principles, coding conventions — e.g. treating the forecast endpoint's dependency wiring as a singleton lifetime) driven by the IoC/DI/testability constraints above, and only then produce an implementation plan. When picking up this work, follow that sequencing: settle and state the architectural decisions before generating a plan or code, and justify each choice against the constraints (low coupling, IoC/DI, unit + e2e testability) rather than defaulting to a stack out of habit.

## Course concepts to keep consistent with

The Day 1 deck establishes vocabulary/framing that should stay consistent if you produce course materials, exercises, or example code referencing it:

- Dependency = anything a module needs to function (function, class, library, external service...); internal vs. external, direct vs. transitive, explicit vs. implicit.
- Goal is to minimize **coupling**, not eliminate dependencies; pair low coupling with high **cohesion**.
- Four places to look for dependencies: code (imports), network (HTTP/SDK calls), manifests (`package.json`, `.csproj`, `requirements.txt`, etc.), and resources (DB, storage, cache, secrets) — plus hidden dependencies (env vars, singletons/global state, shelled-out external tools, execution context like clock/timezone/locale).
- Layered architecture (Presentation → Application/Services → Domain → Infrastructure), each layer depending only downward, is the reference architecture used to motivate IoC/DI.
- IoC/DI framing: a module should receive its dependencies (constructor injection preferred, then property, then method injection) rather than instantiate them (`new`) itself, so implementations are swappable and fakeable in tests.
