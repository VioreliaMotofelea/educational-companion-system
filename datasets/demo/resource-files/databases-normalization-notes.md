# Week 3 Reading: Relational Database Normalization

**Course:** Databases (demo) — `databases-demo-course`  
**Catalog resource:** Week 3 Reading — Normalization (CourseOnly)  
**Audience:** Second-year students completing the relational design module

---

## Course context

This reading supports Week 3 of the Databases course. You have already modeled entities and drawn ER diagrams. Normalization turns those models into **relational schemas** that reduce redundancy and protect data integrity when rows are inserted, updated, or deleted. The material is **course-only**: only learners enrolled in `databases-demo-course` should see the uploaded file and its extracted summary in the Educational Companion System.

---

## Intended learning outcomes

After working through this reading and the practice tasks, you should be able to:

1. State functional dependencies (FDs) for a given schema and identify **candidate keys**.
2. Explain **1NF, 2NF, 3NF, and BCNF** using examples, not only definitions.
3. Recognize **update, insertion, and deletion anomalies** in denormalized tables.
4. Propose a normalized decomposition and discuss the **normalization vs performance** trade-off.
5. Reflect on why **access-aware** course materials must not leak summaries to learners outside the course scope.

**Keywords for study and semantic matching:** normalization, BCNF, functional dependencies, relational schema, database design, update anomalies, candidate key, decomposition, integrity.

---

## Why normalization matters

Poor schema design stores the same fact in many places. When that fact changes, every copy must be updated consistently—a common source of bugs in student projects and production systems. Normalization is a structured way to **eliminate redundancy** by splitting tables so each non-key attribute depends on the whole key of its relation (and only on keys, at BCNF).

Normalization is not “free.” More tables mean more joins at query time. Experienced designers normalize to a target normal form (often 3NF or BCNF) and then **denormalize selectively** for hot read paths, documenting the intentional redundancy.

---

## Relational schema example (before normalization)

Consider a single table capturing enrollments, courses, and instructors:

| EnrollmentId | StudentId | CourseCode | CourseTitle | Credits | InstructorName | InstructorOffice | Semester | Grade |
|--------------|-----------|------------|-------------|---------|----------------|------------------|----------|-------|

**Functional dependencies (informal):**

- `EnrollmentId` → all other attributes (enrollment is the grain of one row).
- `CourseCode` → `CourseTitle`, `Credits` (course facts repeat per enrollment).
- `InstructorName` → `InstructorOffice` (office is a property of instructor, not enrollment).

**Candidate keys:** `{EnrollmentId}` is a candidate key. `{StudentId, CourseCode, Semester}` may also identify an enrollment if the business rule forbids duplicate enrollments in the same term.

---

## Normal forms (working definitions)

### First normal form (1NF)

All attributes hold **atomic** values; no repeating groups or nested lists in columns. If you store multiple phone numbers in one cell separated by commas, you are not in 1NF.

### Second normal form (2NF)

Relation is in 1NF and every non-key attribute depends on the **entire** primary key, not on a proper subset. Problems appear when the key is composite and some attributes depend only on part of it (partial dependency).

### Third normal form (3NF)

Relation is in 2NF and no non-key attribute depends on another non-key attribute (**transitive dependency**). Course title depending on course code while course code is not the key of the enrollment row is a classic transitive chain through a non-key attribute.

### Boyce–Codd normal form (BCNF)

For every non-trivial FD `X → Y`, **X is a superkey**. BCNF is stricter than 3NF. When multiple overlapping candidate keys exist (e.g., scheduling rooms by course and by instructor), 3NF may still allow anomalies that BCNF removes.

---

## Anomalies in the enrollment table

**Update anomaly:** If course `DB201` changes its title, every enrollment row for that course must be updated. Missing one row leaves inconsistent titles.

**Insertion anomaly:** You cannot record a new course until at least one student enrolls, because `CourseCode` only appears with `StudentId` in the wide table.

**Deletion anomaly:** If the last enrollment for a course is removed, you may lose course metadata (title, credits) entirely.

Decomposition into `Student`, `Course`, `Instructor`, and `Enrollment` tables addresses these issues by storing each fact once in the appropriate relation.

---

## Mini case study: Enrollment / Course / Instructor

**Target decomposition:**

1. **Course**(`CourseCode`, `CourseTitle`, `Credits`)
2. **Instructor**(`InstructorId`, `InstructorName`, `InstructorOffice`)
3. **Enrollment**(`EnrollmentId`, `StudentId`, `CourseCode`, `Semester`, `Grade`, `InstructorId`)

**Enrollment** references `Course` and `Instructor` by foreign keys. Grades remain enrollment-specific. Join paths are predictable: enrollments ↔ courses, enrollments ↔ instructors.

**BCNF check:** In each table, every determinant of a non-key attribute is a superkey. If you mistakenly kept `InstructorOffice` in `Enrollment` while `InstructorName` was not part of the key, `InstructorName → InstructorOffice` would violate BCNF because `InstructorName` is not a superkey of the enrollment relation.

---

## Normalization vs performance

- **Pros of higher normal forms:** fewer anomalies, simpler integrity constraints, clearer semantics.
- **Cons:** more joins, more complex queries for dashboards, possible need for materialized views or cached aggregates.
- **Pragmatic rule:** normalize during design; measure read latency; denormalize only with documented invariants (triggers, batch jobs, or application checks).

---

## Guided practice

1. List all FDs you believe hold for the wide enrollment table. Mark which are **partial** or **transitive**.
2. Draw the decomposition into 3NF/BCNF tables and label primary and foreign keys.
3. Give one SQL `UPDATE` that would cause an update anomaly in the wide table but not in your decomposition.
4. Explain why `{StudentId, CourseCode}` might or might not be a candidate key without `Semester`.

**Deliverable:** One-page schema diagram plus a short paragraph on which normal form each resulting table satisfies.

---

## Reflection and access-aware learning

1. Why should an extracted text summary of this CourseOnly reading **not** appear in Bianca’s accessible catalog if she is not in `databases-demo-course`?
2. How does attaching this file to catalog resource 066 improve **semantic recommendations** for Alex without exposing the full document text in every API response?
3. When might a teacher attach a **private** supplement instead of a course-wide reading?

---

## Self-assessment

| I can… | Confident | Needs review |
|--------|-----------|--------------|
| Identify partial and transitive dependencies | ☐ | ☐ |
| Decompose a wide table to 3NF/BCNF | ☐ | ☐ |
| Name three anomaly types with examples | ☐ | ☐ |
| Discuss one performance trade-off | ☐ | ☐ |

---

## Connection to adaptive learning

The Educational Companion can use the **extracted summary** of this reading (visible only to learners with access to resource 066) to suggest related catalog items on **functional dependencies**, **SQL joins**, or **database design** when your progress signals show gaps in normalization. Enrichment does not bypass visibility rules: summaries inherit the same scope as the parent resource.

*Original thesis demo material — not copied from external textbooks.*
