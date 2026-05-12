# Demo Dataset Schema

This folder defines a curated demo catalog separate from OULAD research data.

## Files

- `resources.json` (array)
  - `id` (UUID, required)
  - `title` (string, required)
  - `description` (string, optional)
  - `topic` (string, required)
  - `difficulty` (int 1..5, required)
  - `estimatedDurationMinutes` (int > 0, required)
  - `contentType` (`Article` | `Video` | `Quiz`, required)
  - `source` (string, optional)
  - `url` (string, optional)

- `users.json` (array)
  - `userId` (string, required)
  - `email` (string, optional)
  - `dailyAvailableMinutes` (int > 0, required)
  - `level` (int >= 1, required)
  - `xp` (int >= 0, required)
  - `preferences` (object, optional)
    - `preferredDifficulty` (int 1..5, optional)
    - `preferredContentTypesCsv` (string, optional)
    - `preferredTopicsCsv` (string, optional)

- `interactions.json` (array)
  - `userId` (string, required, must exist in `users.json`)
  - `learningResourceId` (UUID string, required, must exist in `resources.json`)
  - `interactionType` (`Viewed` | `Completed` | `Rated` | `Skipped`, required)
  - `rating` (int 1..5, optional)
  - `timeSpentMinutes` (int >= 0, optional)
  - `createdAtUtc` (ISO datetime string, optional)

- `tasks.json` (array)
  - `userId` (string, required, must exist in `users.json`)
  - `learningResourceId` (UUID string or `null`)
  - `title` (string, required)
  - `notes` (string, optional)
  - `deadlineUtc` (ISO datetime string, required)
  - `estimatedMinutes` (int > 0, required)
  - `priority` (int 1..5, required)
  - `status` (`Pending` | `Completed` | `Overdue`, required)

## Seeder

Use `scripts/datasets/seed_demo_catalog.py` to validate these files and generate/apply SQL.
