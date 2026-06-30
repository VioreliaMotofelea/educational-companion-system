# Minimal sample — database normalization

**Status:** prefer **`databases-normalization-notes.md`** on resource **066**.

This file intentionally stays brief to verify Markdown ingestion and summary generation on a small input.

## Topic

Functional dependencies and **BCNF**: every determinant must be a superkey. **Update anomalies** appear when course titles are duplicated on enrollment rows.

## One-line practice

Decompose `Enrollment(CourseTitle, …)` so `CourseCode → CourseTitle` lives in a **Course** table.

*Original content — not for primary defense narrative.*
