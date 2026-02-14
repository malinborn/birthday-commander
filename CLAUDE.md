# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

BirthdayCommander is a .NET 8.0 bot for Mattermost that manages birthday subscriptions, notifications, and gift coordination. Corporate employees can track colleagues' birthdays and organize celebrations.

## Build & Development Commands

```bash
# Build solution
dotnet build

# Run tests (requires test database)
docker compose -f BirthdayCommander.Tests/docker-compose.test-db.yml up -d
dotnet test

# Run locally
dotnet run --project BirthdayCommander.API

# Docker deployment
docker compose up -d
```

## Local Development Setup

1. Start test database: `docker compose -f BirthdayCommander.Tests/docker-compose.test-db.yml up -d`
2. Initialize user secrets:
   ```bash
   dotnet user-secrets init
   dotnet user-secrets set "Mattermost:BotToken" "{TOKEN}"
   dotnet user-secrets set "Mattermost:ServerUrl" "https://{DOMAIN}"
   dotnet user-secrets set "Mattermost:WebHookSecret" "{WEBHOOK_SECRET}"
   ```

## Architecture

Clean Architecture with 4 projects:

| Project | Responsibility |
|---------|---------------|
| `BirthdayCommander.Core` | Domain entities (`Employee`, `Subscription`, `BirthdayChat`), interfaces, Mattermost WebSocket models |
| `BirthdayCommander.Infrastructure` | Dapper repositories, FluentMigrator migrations, services implementation, background WebSocket handler |
| `BirthdayCommander.API` | Minimal API entry point, DI setup, `DirectMessageHandler` for command orchestration |
| `BirthdayCommander.Tests` | xUnit tests with PostgreSQL test container |

### Key Dependencies

- **Dapper** - micro-ORM for data access
- **FluentMigrator** - database migrations
- **Websocket.Client** - Mattermost real-time communication
- **Npgsql** - PostgreSQL driver

## Database

PostgreSQL with migrations in `BirthdayCommander.Infrastructure/Migrations/`. Centralized SQL queries in `SqlScripts` static class.

## Bot Commands

Parsed via `MessageParser` with regex patterns. Supports Russian and English. Main commands:
- Subscribe/unsubscribe to birthdays (by email)
- Set own birthday (DD.MM.YYYY format)
- Set wishlist URL
- Toggle birthday visibility (invisibility flag)
- List upcoming birthdays

## Code Style

Modern C# 12 features required:
- File-scoped namespaces
- Primary constructors
- Record types for DTOs
- Switch expressions
- Arrow-style methods
- Required properties

## CI/CD

GitHub Actions workflow builds Docker image, pushes to ghcr.io, and deploys via SSH with docker compose on push to main.
